using System.Collections.Generic;
using System.Text;

namespace DocuHelper.Importers.Hwp
{
    internal sealed class HwpLabeler
    {
        private const int NotStarted = int.MinValue;

        private readonly HwpDocInfo _info;
        private readonly Dictionary<int, int[]> _counters = new();

        public HwpLabeler(HwpDocInfo info)
        {
            _info = info;
        }

        public string Label(HwpParaShape shape, int outlineId) => shape.HeadingType switch
        {
            HwpParaShape.HeadingBullet => _info.Bullet(shape.HeadingId),
            HwpParaShape.HeadingNumber => Number(shape.HeadingId, shape.Level),
            HwpParaShape.HeadingOutline => Number(outlineId, shape.Level),
            _ => null,
        };

        private string Number(int id, int level)
        {
            var numbering = _info.Numbering(id);
            if (numbering == null || level < 0 || level >= HwpNumbering.LevelCount)
                return null;

            if (!_counters.TryGetValue(id, out var counters))
            {
                counters = new int[HwpNumbering.LevelCount];
                for (var i = 0; i < counters.Length; i++)
                    counters[i] = NotStarted;

                _counters[id] = counters;
            }

            counters[level] = counters[level] == NotStarted ? numbering.Starts[level] : counters[level] + 1;
            for (var deeper = level + 1; deeper < counters.Length; deeper++)
                counters[deeper] = NotStarted;

            var format = numbering.Formats[level];
            if (string.IsNullOrEmpty(format))
                return null;

            var builder = new StringBuilder();
            for (var i = 0; i < format.Length; i++)
            {
                var target = i + 1 < format.Length && format[i] == '^' ? LevelOf(format[i + 1], level) : -1;
                if (target < 0)
                {
                    builder.Append(format[i]);
                    continue;
                }

                var value = counters[target] == NotStarted ? numbering.Starts[target] : counters[target];
                builder.Append(HwpNumberFormat.Format(value, numbering.Shapes[target]));
                i++;
            }

            var label = builder.ToString().TrimEnd();
            return label.Length > 0 ? label : null;
        }

        private static int LevelOf(char marker, int current)
        {
            if (marker >= '1' && marker <= '9')
                return marker - '1';

            return marker == 'N' || marker == 'n' ? current : -1;
        }
    }
}
