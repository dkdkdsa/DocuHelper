using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DocuHelper.Importers.Hwp
{
    internal sealed class HwpxNumbering
    {
        private const int Unset = int.MinValue;

        private readonly HwpxHeader _header;
        private readonly Dictionary<int, int[]> _counters = new();

        public HwpxNumbering(HwpxHeader header)
        {
            _header = header;
        }

        public string Next(HwpxParaShape shape, int outline) => shape.HeadingType switch
        {
            "OUTLINE" => Number(outline, shape.HeadingLevel),
            "NUMBER" => Number(shape.HeadingId, shape.HeadingLevel),
            "BULLET" => _header.Bullet(shape.HeadingId, shape.Checked),
            _ => null,
        };

        private string Number(int id, int level)
        {
            var levels = _header.Numbering(id);
            if (levels == null || levels.Length == 0)
                return null;

            if (!_counters.TryGetValue(id, out var counters))
            {
                counters = Enumerable.Repeat(Unset, levels.Length).ToArray();
                _counters[id] = counters;
            }

            level = Math.Clamp(level, 0, levels.Length - 1);
            for (var i = 0; i < level; i++)
            {
                if (counters[i] == Unset)
                    counters[i] = levels[i].Start;
            }

            counters[level] = counters[level] == Unset ? levels[level].Start : counters[level] + 1;
            for (var i = level + 1; i < counters.Length; i++)
                counters[i] = Unset;

            return Render(levels, counters, levels[level].Text);
        }

        private static string Render(HwpxNumberingLevel[] levels, int[] counters, string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            var result = new StringBuilder();
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c != '^' || i + 1 >= text.Length || text[i + 1] < '1' || text[i + 1] > '9')
                {
                    result.Append(c);
                    continue;
                }

                var index = text[i + 1] - '1';
                i++;
                if (index == 0 && levels.Length >= 10 && i + 1 < text.Length && text[i + 1] == '0')
                {
                    index = 9;
                    i++;
                }

                if (index >= levels.Length)
                    continue;

                var value = counters[index] == Unset ? levels[index].Start : counters[index];
                result.Append(HwpxNumberFormat.Format(value, levels[index].Format));
            }

            var label = HwpxRunBuilder.Clean(result.ToString()).Trim();
            return label.Length == 0 ? null : label;
        }
    }
}
