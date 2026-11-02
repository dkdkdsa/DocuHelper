using System.Collections.Generic;
using System.Text;

namespace DocuHelper.Importers.Legacy.Ppt
{
    internal sealed class PptSlideReader
    {
        private const int NotesBodyPlaceholder = 0x0C;

        private readonly PptSource _source;
        private readonly PptMasterReader _masters;

        public PptSlideReader(PptSource source, PptMasterReader masters)
        {
            _source = source;
            _masters = masters;
        }

        public PptSlide Read(PptRecord slideRecord, List<PptOutlineText> outline, PptRecord master, PptRecord notes)
        {
            var atom = slideRecord.Find(PptTypes.SlideAtom);
            var info = _masters.Get(master);
            var scheme = PptColors.FindScheme(slideRecord) ?? info.Scheme;

            var context = new PptSlideContext
            {
                Pictures = _source.Pictures,
                Defaults = _source.Defaults,
                Scheme = scheme,
                Styles = info.Styles,
                Outline = outline ?? new List<PptOutlineText>(),
                MasterAnchors = info.Anchors,
            };

            var parser = new PptDrawingParser(context);
            parser.Parse(slideRecord.Find(PptTypes.PpDrawing));
            var slide = new PptSlide { Background = parser.Background ?? info.Background, Scheme = scheme, Geometry = atom == null ? -1 : (int)atom.UInt32(0) };
            slide.Shapes.AddRange(parser.Shapes);
            if (notes != null)
                ReadNotes(notes, slide, context);

            return slide;
        }

        private static void ReadNotes(PptRecord notes, PptSlide slide, PptSlideContext context)
        {
            var parser = new PptDrawingParser(context);
            parser.Parse(notes.Find(PptTypes.PpDrawing));
            foreach (var shape in parser.Shapes)
            {
                if (shape.PlaceholderId != NotesBodyPlaceholder || shape.Text == null)
                    continue;

                foreach (var paragraph in shape.Text.Paragraphs)
                {
                    var line = new StringBuilder();
                    foreach (var run in paragraph.Runs)
                        line.Append(run.IsBreak ? " " : run.Text);

                    slide.Notes.Add(line.ToString());
                }

                break;
            }
        }
    }
}
