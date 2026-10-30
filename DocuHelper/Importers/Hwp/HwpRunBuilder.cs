using System.Collections.Generic;
using System.Text;

namespace DocuHelper.Importers.Hwp
{
    internal sealed class HwpRunBuilder
    {
        private readonly List<HwpRun> _runs = new();
        private readonly StringBuilder _text = new();
        private HwpRun _format;

        public void Append(char value, HwpRun format)
        {
            Switch(format);
            _text.Append(value);
        }

        public void Append(string value, HwpRun format)
        {
            if (string.IsNullOrEmpty(value))
                return;

            Switch(format);
            _text.Append(value);
        }

        public List<HwpRun> Build()
        {
            Flush();
            return _runs;
        }

        public static HwpRun Copy(HwpRun format, string text) => new()
        {
            Text = text,
            Bold = format.Bold,
            Italic = format.Italic,
            Underline = format.Underline,
            Strike = format.Strike,
            Superscript = format.Superscript,
            Subscript = format.Subscript,
            Size = format.Size,
            Color = format.Color,
            Font = format.Font,
        };

        private static bool SameFormat(HwpRun a, HwpRun b) =>
            a.Bold == b.Bold && a.Italic == b.Italic && a.Underline == b.Underline && a.Strike == b.Strike
            && a.Superscript == b.Superscript && a.Subscript == b.Subscript && a.Size == b.Size
            && a.Color == b.Color && a.Font == b.Font;

        private void Switch(HwpRun format)
        {
            if (_format != null && SameFormat(_format, format))
                return;

            Flush();
            _format = format;
        }

        private void Flush()
        {
            if (_format != null && _text.Length > 0)
            {
                var text = _text.ToString();
                var last = _runs.Count > 0 ? _runs[^1] : null;
                if (last != null && SameFormat(last, _format))
                    last.Text += text;
                else
                    _runs.Add(Copy(_format, text));
            }

            _text.Clear();
        }
    }
}
