using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

        public static List<string> Delimited(string text, string delimiter, bool consecutiveAsOne)
        {
            var parts = new List<string>();
            var field = new StringBuilder();
            var quoted = false;
            var index = 0;
            while (index < text.Length)
            {
                var current = text[index];
                if (quoted)
                {
                    if (current == '"' && index + 1 < text.Length && text[index + 1] == '"')
                    {
                        field.Append('"');
                        index += 2;
                        continue;
                    }

                    if (current == '"')
                        quoted = false;
                    else
                        field.Append(current);

                    index++;
                    continue;
                }

                if (current == '"' && field.ToString().Trim().Length == 0)
                {
                    field.Clear();
                    quoted = true;
                    index++;
                    continue;
                }

                if (index + delimiter.Length <= text.Length && string.CompareOrdinal(text, index, delimiter, 0, delimiter.Length) == 0)
                {
                    parts.Add(field.ToString());
                    field.Clear();
                    index += delimiter.Length;
                    continue;
                }

                field.Append(current);
                index++;
            }

            parts.Add(field.ToString());
            if (consecutiveAsOne)
                parts.RemoveAll(part => part.Length == 0);

            return parts;
        }

        public static List<string> Fixed(string text, IReadOnlyList<int> widths)
        {
            var parts = new List<string>();
            var position = 0;
            foreach (var width in widths)
            {
                var end = position;
                for (var count = 0; count < width && end < text.Length; count++)
                    end += char.IsSurrogatePair(text, end) ? 2 : 1;

                parts.Add(text.Substring(position, end - position));
                position = end;
            }

            if (position < text.Length)
                parts.Add(text.Substring(position));

            return parts;
        }
    }
}
