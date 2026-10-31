using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml;
using A = DocumentFormat.OpenXml.Drawing;

namespace DocuHelper.Tools.Ppt
{
    public sealed class PptTextRange
    {
        public PptTextRange(int paragraph, int start, int end)
        {
            Paragraph = paragraph;
            Start = start;
            End = end;
        }

        public int Paragraph { get; }

        public int Start { get; }

        public int End { get; }

        public static PptTextRange Parse(ToolInput input, string name = "range")
        {
            var nested = PptInputs.Object(input, name);
            if (nested == null)
                return null;

            var paragraph = PptInputs.RequiredInteger(nested, "paragraph", 1, int.MaxValue);
            var start = PptInputs.RequiredInteger(nested, "start", 0, int.MaxValue);
            var end = PptInputs.RequiredInteger(nested, "end", 1, int.MaxValue);
            if (end <= start)
                throw new ToolInputException($"'{name}'의 end는 start보다 커야 해요. 글자 번호는 0부터 세고 end 글자는 포함하지 않아요.");

            return new PptTextRange(paragraph, start, end);
        }

        public static int LengthOf(A.Paragraph paragraph) =>
            paragraph.ChildElements.Sum(LengthOfElement);

        public static List<OpenXmlElement> Elements(A.Paragraph paragraph, int start, int end)
        {
            var result = new List<OpenXmlElement>();
            var offset = 0;
            foreach (var child in paragraph.ChildElements)
            {
                var length = LengthOfElement(child);
                if (!IsText(child))
                    continue;

                if (offset < end && offset + length > start)
                    result.Add(child);

                offset += length;
            }

            return result;
        }

        public static List<OpenXmlElement> Split(A.Paragraph paragraph, int start, int end)
        {
            var total = LengthOf(paragraph);
            if (start < 0 || end <= start || end > total)
            {
                throw new ToolInputException(
                    $"글자 범위 {start.ToString(CultureInfo.InvariantCulture)}~{end.ToString(CultureInfo.InvariantCulture)}가 단락 길이({total.ToString(CultureInfo.InvariantCulture)}글자)를 벗어났어요.");
            }

            var from = Boundary(paragraph, start, false);
            var to = Boundary(paragraph, end, true);
            return Elements(paragraph, from, to);
        }

        public static List<OpenXmlElement> SplitAll(A.Paragraph paragraph) =>
            Elements(paragraph, 0, int.MaxValue);

        private static int Boundary(A.Paragraph paragraph, int offset, bool forward)
        {
            var position = 0;
            foreach (var child in paragraph.ChildElements)
            {
                if (child is not A.Run run)
                {
                    position += LengthOfElement(child);
                    continue;
                }

                var text = run.Text?.Text ?? string.Empty;
                if (offset > position && offset < position + text.Length)
                {
                    var local = offset - position;
                    if (char.IsHighSurrogate(text[local - 1]) && char.IsLowSurrogate(text[local]))
                    {
                        local += forward ? 1 : -1;
                        if (local <= 0)
                            return position;

                        if (local >= text.Length)
                            return position + text.Length;
                    }

                    var tail = (A.Run)run.CloneNode(true);
                    run.Text.Text = text.Substring(0, local);
                    tail.Text.Text = text.Substring(local);
                    run.InsertAfterSelf(tail);
                    return position + local;
                }

                position += text.Length;
            }

            return offset;
        }

        private static bool IsText(OpenXmlElement element) => element is A.Run or A.Field or A.Break;

        private static int LengthOfElement(OpenXmlElement element) => element switch
        {
            A.Run run => run.Text?.Text?.Length ?? 0,
            A.Field field => field.Text?.Text?.Length ?? 0,
            A.Break => 1,
            _ => 0,
        };
    }
}
