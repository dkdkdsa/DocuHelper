using System;
using System.Collections.Generic;
using System.Linq;

namespace DocuHelper.Tools.Excel
{
    internal static class TextTable
    {
        public static string Delimiter(string text)
        {
            var value = text ?? string.Empty;
            var name = value.Trim().ToLowerInvariant();
            if (name is "tab" or "탭" || value == "\t")
                return "\t";

            if (name is "space" or "공백" || value == " ")
                return " ";

            if (value.Length == 0)
                throw new ToolInputException("구분 기호를 알려 주세요. 예: \",\", \";\", \"|\", \"tab\", \"space\"");

            return value;
        }

        public static List<string> Delimited(string text, string delimiter, bool consecutiveAsOne) =>
            consecutiveAsOne
                ? text.Split(delimiter, StringSplitOptions.RemoveEmptyEntries).ToList()
                : text.Split(delimiter, StringSplitOptions.None).ToList();

        public static List<string> Fixed(string text, IReadOnlyList<int> widths)
        {
            var parts = new List<string>();
            var position = 0;
            foreach (var width in widths)
            {
                var take = Math.Min(width, text.Length - position);
                parts.Add(text.Substring(position, take));
                position += take;
            }

            if (position < text.Length)
                parts.Add(text.Substring(position));

            return parts;
        }
    }
}
