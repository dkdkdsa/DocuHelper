using System.Collections.Generic;
using DocuHelper.Importers.Hwp;

namespace DocuHelper.Importers.Legacy.Ppt
{
    internal static class PptDocumentReader
    {
        private const string DocumentStream = "PowerPoint Document";
        private const string CurrentUserStream = "Current User";
        private const string PicturesStream = "Pictures";
        private const string EncryptedStream = "EncryptedSummary";
        private const int MaxSlides = 5000;
        private const int MaxTopLevel = 200000;

        public static PptDocument Read(HwpCompoundFile file)
        {
            if (file.ReadStream(EncryptedStream) != null)
                throw new LegacyOfficeException(PptMessages.Encrypted);

            var stream = file.ReadStream(DocumentStream);
            if (stream == null || stream.Length < PptRecord.HeaderSize)
                throw new LegacyOfficeException(PptMessages.Old);

            var source = new PptSource
            {
                Stream = stream,
                Persist = PptPersistReader.Read(stream, file.ReadStream(CurrentUserStream)),
            };

            if (source.Persist.Encrypted)
                throw new LegacyOfficeException(PptMessages.Encrypted);

            ScanTopLevel(source);
            foreach (var record in source.TopLevel)
            {
                if (record.Type == PptTypes.CryptSession10Container)
                    throw new LegacyOfficeException(PptMessages.Encrypted);
            }

            source.Document = source.Persist.Record(stream, source.Persist.DocumentId, PptTypes.Document)
                ?? source.TopLevel.Find(r => r.Type == PptTypes.Document);
            if (source.Document == null)
                throw new LegacyOfficeException(PptMessages.Old);

            ReadEnvironment(source);
            ReadDrawingGroup(source, file.ReadStream(PicturesStream));
            return Assemble(source);
        }

        private static PptDocument Assemble(PptSource source)
        {
            var document = new PptDocument();
            foreach (var font in source.Fonts)
                document.Fonts.Add(font);

            var atom = source.Document.Find(PptTypes.DocumentAtom);
            if (atom != null)
            {
                var width = atom.Int32(0);
                var height = atom.Int32(4);
                if (width > 0 && height > 0)
                {
                    document.Width = width;
                    document.Height = height;
                }
            }

            var lists = new Dictionary<int, List<PptSlideEntry>>();
            foreach (var child in source.Document.Children)
            {
                if (child.Type == PptTypes.SlideListWithText && !lists.ContainsKey(child.Instance))
                    lists[child.Instance] = PptSlideEntry.Read(child);
            }

            var slides = lists.GetValueOrDefault(0) ?? new List<PptSlideEntry>();
            var masters = lists.GetValueOrDefault(1) ?? new List<PptSlideEntry>();
            var notes = lists.GetValueOrDefault(2) ?? new List<PptSlideEntry>();

            var masterRecords = new Dictionary<uint, PptRecord>();
            PptRecord mainMaster = null;
            foreach (var entry in masters)
            {
                var record = source.Persist.Record(source.Stream, entry.PersistId, PptTypes.MainMaster);
                if (record == null)
                    continue;

                masterRecords[entry.SlideId] = record;
                mainMaster ??= record;
            }

            mainMaster ??= source.TopLevel.Find(r => r.Type == PptTypes.MainMaster);
            var reader = new PptSlideReader(source, new PptMasterReader(source, mainMaster));
            var any = false;
            foreach (var entry in slides)
            {
                if (document.Slides.Count >= MaxSlides)
                    break;

                var record = source.Persist.Record(source.Stream, entry.PersistId, PptTypes.Slide);
                if (record == null)
                    continue;

                any = true;
                document.Slides.Add(reader.Read(record, entry.Texts, FindMaster(record, masterRecords, mainMaster), FindNotes(source, entry, notes)));
            }

            if (!any)
                ScanSlides(source, document, reader, masterRecords, mainMaster, slides);

            return document;
        }

        private static void ScanSlides(
            PptSource source,
            PptDocument document,
            PptSlideReader reader,
            Dictionary<uint, PptRecord> masterRecords,
            PptRecord mainMaster,
            List<PptSlideEntry> slides)
        {
            var index = 0;
            foreach (var record in source.TopLevel)
            {
                if (record.Type != PptTypes.Slide || document.Slides.Count >= MaxSlides)
                    continue;

                var entry = index < slides.Count ? slides[index] : null;
                index++;
                document.Slides.Add(reader.Read(record, entry?.Texts, FindMaster(record, masterRecords, mainMaster), null));
            }
        }

        private static PptRecord FindMaster(PptRecord slide, Dictionary<uint, PptRecord> masters, PptRecord main)
        {
            var atom = slide.Find(PptTypes.SlideAtom);
            if (atom != null && masters.TryGetValue(atom.UInt32(12), out var master))
                return master;

            return main;
        }

        private static PptRecord FindNotes(PptSource source, PptSlideEntry slide, List<PptSlideEntry> notes)
        {
            foreach (var entry in notes)
            {
                var record = source.Persist.Record(source.Stream, entry.PersistId, PptTypes.Notes);
                var atom = record?.Find(PptTypes.NotesAtom);
                if (atom != null && atom.UInt32(0) == slide.SlideId)
                    return record;
            }

            return null;
        }

        private static void ScanTopLevel(PptSource source)
        {
            var position = 0;
            while (source.TopLevel.Count < MaxTopLevel)
            {
                var record = PptRecord.Parse(source.Stream, position, source.Stream.Length);
                if (record == null)
                    break;

                source.TopLevel.Add(record);
                position = record.End;
            }
        }

        private static void ReadEnvironment(PptSource source)
        {
            var environment = source.Document.Find(PptTypes.Environment);
            if (environment != null)
                source.Styles.Read(environment);

            var collection = environment?.Find(PptTypes.FontCollection);
            if (collection == null)
                return;

            foreach (var entry in collection.Children)
            {
                if (entry.Type != PptTypes.FontEntityAtom)
                    continue;

                var name = entry.Utf16(0, 32);
                var end = name.IndexOf('\0');
                source.Fonts.Add(end >= 0 ? name.Substring(0, end) : name);
            }
        }

        private static void ReadDrawingGroup(PptSource source, byte[] pictures)
        {
            var group = source.Document.Find(PptTypes.DrawingGroup);
            source.Pictures = new PptPictureStore(group, pictures);
            var dgg = group?.Find(PptTypes.DggContainer);
            var options = dgg?.Find(PptTypes.Fopt);
            if (options != null)
                source.Defaults.Read(options);
        }
    }
}
