using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml;
using A = DocumentFormat.OpenXml.Drawing;

namespace DocuHelper.Tools.Ppt
{
    public static class PptRunSplitter
    {
        public static void Check(A.Paragraph paragraph, PptTextRange range)
        {
            var total = PptTextRange.LengthOf(paragraph);
            if (range.End > total)
            {
                throw new ToolInputException(
                    $"글자 범위 {range.Start.ToString(CultureInfo.InvariantCulture)}~{range.End.ToString(CultureInfo.InvariantCulture)}가 {range.Paragraph.ToString(CultureInfo.InvariantCulture)}번 단락 길이({total.ToString(CultureInfo.InvariantCulture)}글자)를 벗어났어요.");
            }
        }

        public static List<OpenXmlElement> Select(A.Paragraph paragraph, PptTextRange range)
        {
            Check(paragraph, range);
            return PptTextRange.Split(paragraph, range.Start, range.End);
        }

        public static List<OpenXmlElement> SelectAll(A.Paragraph paragraph) =>
            PptTextRange.SplitAll(paragraph);

        public static int Merge(A.Paragraph paragraph)
        {
            var merged = 0;
            var children = paragraph.ChildElements.ToList();
            for (var i = 1; i < children.Count; i++)
            {
                if (children[i] is not A.Run current || children[i - 1] is not A.Run previous)
                    continue;

                if (!SameProperties(previous, current))
                    continue;

                previous.Text ??= new A.Text(string.Empty);
                previous.Text.Text = (previous.Text.Text ?? string.Empty) + (current.Text?.Text ?? string.Empty);
                current.Remove();
                children[i] = previous;
                merged++;
            }

            return merged;
        }

        private static bool SameProperties(A.Run left, A.Run right) =>
            string.Equals(left.RunProperties?.OuterXml, right.RunProperties?.OuterXml, System.StringComparison.Ordinal);
    }
}
