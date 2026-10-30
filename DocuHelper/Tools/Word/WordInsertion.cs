using System;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class WordInsertion
    {
        private OpenXmlElement _after;
        private readonly OpenXmlElement _before;
        private readonly OpenXmlElement _parent;

        private WordInsertion(OpenXmlElement after, OpenXmlElement before, OpenXmlElement parent, WordBlock previous, WordBlock next)
        {
            _after = after;
            _before = before;
            _parent = parent;
            Previous = previous;
            Next = next;
        }

        public WordBlock Previous { get; }

        public WordBlock Next { get; }

        public string Describe => Previous == null ? "at the start" : $"after {Previous.Id}";

        public static WordInsertion Resolve(WordScan scan, string after)
        {
            var key = (after ?? string.Empty).Trim();
            if (key.Length == 0)
                throw new ToolInputException("'after' 값이 필요해요. 문단 id나 \"start\", \"end\"를 넣어 주세요.");

            var blocks = scan.Blocks;
            if (string.Equals(key, "start", StringComparison.OrdinalIgnoreCase))
            {
                var first = scan.Body.ChildElements.FirstOrDefault(e => e is Paragraph || e is Table || e is SdtBlock || e is CustomXmlBlock);
                if (first != null)
                    return new WordInsertion(null, first, null, null, blocks.FirstOrDefault());

                return AtEnd(scan, null);
            }

            if (string.Equals(key, "end", StringComparison.OrdinalIgnoreCase))
            {
                var last = blocks.LastOrDefault();
                if (last != null && last.IsEmpty && !last.HasSectionBreak && last.Paragraph.Parent is Body)
                    return new WordInsertion(null, last.Paragraph, null, scan.Before(last), last);

                return AtEnd(scan, last);
            }

            var block = scan.Find(key);
            if (block == null)
                throw new ToolInputException($"‘{key}’ 문단을 찾지 못했어요. read_paragraphs로 id를 다시 확인해 주세요.");

            return new WordInsertion(block.Element, null, null, block, scan.After(block));
        }

        public void Insert(OpenXmlElement element)
        {
            if (_after != null)
            {
                _after.InsertAfterSelf(element);
                _after = element;
                return;
            }

            if (_before != null)
            {
                _before.InsertBeforeSelf(element);
                return;
            }

            _parent.AppendChild(element);
        }

        private static WordInsertion AtEnd(WordScan scan, WordBlock last)
        {
            var section = scan.Body.Elements<SectionProperties>().LastOrDefault();
            if (section != null)
                return new WordInsertion(null, section, null, last, null);

            return new WordInsertion(null, null, scan.Body, last, null);
        }
    }
}
