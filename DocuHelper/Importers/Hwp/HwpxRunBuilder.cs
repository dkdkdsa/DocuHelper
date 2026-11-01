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
            if (last != null && !_locked && shape.Matches(last) && last.Link == Link && ReferenceEquals(last.ClickHere, ClickHere))
            {
                last.Text += value;
                return;
            }

            var run = shape.ToRun(value);
            run.Link = Link;
            run.ClickHere = ClickHere;
            Runs.Add(run);
            _locked = false;
        }

        public void AppendNote(HwpNote note)
        {
            Runs.Add(new HwpRun { Note = note });
            _locked = true;
        }

        public void OpenKeep(string key)
        {
            Runs.Add(new HwpRun { KeepStart = key });
            _locked = true;
        }

        public void CloseKeep(string key)
        {
            Runs.Add(new HwpRun { KeepEnd = key });
            _locked = true;
        }

        public void AppendPicture(HwpPicture picture)
        {
            Runs.Add(new HwpRun { Picture = picture });
            _locked = true;
        }

        public void AppendField(string field, string text, HwpxCharShape shape)
        {
            var run = shape.ToRun(text);
            run.Field = field;
            Runs.Add(run);
            _locked = true;
        }

        public string Link { get; private set; }

        public HwpClickHere ClickHere { get; private set; }

        private string _linkId;

        private string _controlId;

        private int _controlStart;

        public void OpenLink(string id, string target)
        {
            _linkId = id;
            Link = target;
        }

        public void OpenClickHere(string id, HwpClickHere control)
        {
            _controlId = id;
            ClickHere = control;
            _controlStart = Runs.Count;
        }

        public void CloseField(string id)
        {
            if (id != null && id == _linkId)
            {
                Link = null;
                _linkId = null;
                return;
            }

            if (id == null || id != _controlId)
                return;

            if (Runs.Count == _controlStart && ClickHere != null)
                Runs.Add(new HwpRun { Text = ClickHere.Guide ?? string.Empty, ClickHere = new HwpClickHere { Name = ClickHere.Name, Guide = ClickHere.Guide, Placeholder = true } });

            ClickHere = null;
            _controlId = null;
        }

        public void AppendBookmark(string name)
        {
            if (string.IsNullOrEmpty(name))
                return;

            Runs.Add(new HwpRun { Bookmark = name });
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
