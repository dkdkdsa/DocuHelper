using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace DocuHelper.Importers.Hwp
{
    internal sealed class HwpxRunBuilder
    {
        private bool _locked;

        public List<HwpRun> Runs { get; } = new();

        public void Append(string text, HwpxCharShape shape)
        {
            var value = Clean(text);
            if (value.Length == 0)
                return;

            var last = Runs.Count == 0 ? null : Runs[^1];
            if (last != null && !_locked && shape.Matches(last))
            {
                last.Text += value;
                return;
            }

            Runs.Add(shape.ToRun(value));
            _locked = false;
        }

        public void AppendMark(string text, HwpxCharShape shape)
        {
            var run = shape.ToRun(text);
            run.Superscript = true;
            run.Subscript = false;
            Runs.Add(run);
            _locked = true;
        }

        public static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var value = text.Replace("\r\n", "\n").Replace('\r', '\n').Replace((char)0x2028, '\n').Replace((char)0x2029, '\n');
            var result = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                {
                    result.Append(c).Append(value[i + 1]);
                    i++;
                    continue;
                }

                if (c == '\n' || c == '\t' || (!char.IsControl(c) && XmlConvert.IsXmlChar(c)))
                    result.Append(c);
            }

            return result.ToString();
        }
    }
}
