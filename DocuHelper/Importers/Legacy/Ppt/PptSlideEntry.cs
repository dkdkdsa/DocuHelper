using System.Collections.Generic;

namespace DocuHelper.Importers.Legacy.Ppt
{
    internal sealed class PptSlideEntry
    {
        public uint PersistId { get; set; }

        public uint SlideId { get; set; }

        public List<PptOutlineText> Texts { get; } = new();

        public static List<PptSlideEntry> Read(PptRecord list)
        {
            var entries = new List<PptSlideEntry>();
            PptSlideEntry current = null;
            PptOutlineText text = null;
            foreach (var child in list.Children)
            {
                switch (child.Type)
                {
                    case PptTypes.SlidePersistAtom:
                        current = new PptSlideEntry { PersistId = child.UInt32(0), SlideId = child.UInt32(12) };
                        entries.Add(current);
                        text = null;
                        break;
                    case PptTypes.TextHeaderAtom:
                        text = new PptOutlineText { TextType = (int)child.UInt32(0) };
                        current?.Texts.Add(text);
                        break;
                    case PptTypes.TextCharsAtom:
                    case PptTypes.TextBytesAtom:
                        if (text != null)
                            text.Text = PptTextBuilder.ReadText(child);

                        break;
                    case PptTypes.StyleTextPropAtom:
                        if (text != null)
                            text.Style = child;

                        break;
                }
            }

            return entries;
        }
    }
}
