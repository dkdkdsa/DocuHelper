using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal sealed class DocParagraphWriter
    {
        private readonly DocDocument _doc;
        private readonly DocFormatLookup _lookup;
        private readonly DocStyleMap _styles;
        private readonly DocWordMedia _media;
        private readonly Stack<DocField> _fields = new();
        private readonly Dictionary<int, DocPicture> _pictures = new();
        private readonly StringBuilder _text = new();

        private OpenXmlCompositeElement _container;
        private Paragraph _paragraph;
        private Run _run;
        private DocChp _runChp;
        private string _linkUrl;
        private Hyperlink _link;
        private int _hidden;

        public DocParagraphWriter(DocDocument doc, DocFormatLookup lookup, DocStyleMap styles, DocWordMedia media)
        {
            _doc = doc;
            _lookup = lookup;
            _styles = styles;
            _media = media;
        }

        public Paragraph Build(int start, int end, int style)
        {
            _paragraph = new Paragraph();
            _container = _paragraph;
            _run = null;
            _runChp = null;
            _link = null;
            for (var cp = start; cp < end; cp++)
                cp += Append(cp, end, style);

            FlushText();
            return _paragraph;
        }

        private int Append(int cp, int end, int style)
        {
            var c = _doc.Text[cp];
            if (c == 0x13)
            {
                FlushText();
                _fields.Push(new DocField());
                _hidden++;
                return 0;
            }

            if (c == 0x14)
            {
                if (_fields.Count > 0 && !_fields.Peek().InResult)
                {
                    var field = _fields.Peek();
                    field.InResult = true;
                    _hidden--;
                    FlushText();
                    field.Url = DocFieldParser.HyperlinkTarget(field.Instruction);
                    if (field.Url != null && _hidden == 0)
                        OpenLink(field.Url);
                }

                return 0;
            }

            if (c == 0x15)
            {
                if (_fields.Count > 0)
                {
                    var field = _fields.Pop();
                    if (!field.InResult)
                        _hidden--;

                    if (field.Url != null)
                        CloseLink();
                }

                return 0;
            }

            if (_hidden > 0)
            {
                if (_fields.Peek().InResult == false && c >= ' ')
                    _fields.Peek().Append(c);

                return 0;
            }

            var chp = _lookup.Characters(cp, style);
            if (chp.Deleted)
                return 0;

            switch (c)
            {
                case '\u0001':
                    AppendPicture(chp);
                    return 0;
                case '\t':
                    AppendElement(chp, new TabChar());
                    return 0;
                case '\u000B':
                    AppendElement(chp, new Break());
                    return 0;
                case '\u000C':
                    AppendElement(chp, new Break { Type = BreakValues.Page });
                    return 0;
                case '\u000E':
                    AppendElement(chp, new Break { Type = BreakValues.Column });
                    return 0;
                case '\u001E':
                    AppendElement(chp, new NoBreakHyphen());
                    return 0;
                case '\u001F':
                    AppendElement(chp, new SoftHyphen());
                    return 0;
                case '(' when chp.Special && chp.SymbolFont >= 0:
                    AppendElement(chp, new SymbolChar
                    {
                        Font = _doc.Fonts.Name(chp.SymbolFont) ?? "Symbol",
                        Char = (chp.SymbolChar & 0xFFFF).ToString("X4", CultureInfo.InvariantCulture),
                    });
                    return 0;
            }

            if (c < ' ' || c == '￾' || c == '￿')
                return 0;

            if (char.IsHighSurrogate(c))
            {
                if (cp + 1 >= end || !char.IsLowSurrogate(_doc.Text[cp + 1]))
                    return 0;

                AppendText(chp, c, _doc.Text[cp + 1]);
                return 1;
            }

            if (char.IsLowSurrogate(c))
                return 0;

            AppendText(chp, c, '\0');
            return 0;
        }

        private void OpenLink(string url)
        {
            FlushText();
            _linkUrl = url;
            _link = null;
            _run = null;
            _container = _paragraph;
        }

        private void CloseLink()
        {
            FlushText();
            _linkUrl = null;
            _link = null;
            _run = null;
            _container = _paragraph;
        }

        private void EnsureRun(DocChp chp)
        {
            if (_run != null && (ReferenceEquals(_runChp, chp) || chp.SameFormat(_runChp)))
                return;

            FlushText();
            if (_linkUrl != null && _link == null)
            {
                _link = _media.Link(_linkUrl);
                if (_link == null)
                    _linkUrl = null;
                else
                    _paragraph.AppendChild(_link);
            }

            _container = _link != null ? _link : _paragraph;
            _run = new Run();
            var properties = DocWordRuns.Properties(chp, _doc.Fonts, _styles);
            if (properties != null)
                _run.AppendChild(properties);

            _runChp = chp;
            _container.AppendChild(_run);
        }

        private void AppendText(DocChp chp, char first, char second)
        {
            EnsureRun(chp);
            _text.Append(first);
            if (second != '\0')
                _text.Append(second);
        }

        private void AppendElement(DocChp chp, OpenXmlElement element)
        {
            EnsureRun(chp);
            FlushText();
            _run.AppendChild(element);
        }

        private void FlushText()
        {
            if (_text.Length == 0 || _run == null)
            {
                _text.Clear();
                return;
            }

            _run.AppendChild(new Text(_text.ToString()) { Space = SpaceProcessingModeValues.Preserve });
            _text.Clear();
        }

        private void AppendPicture(DocChp chp)
        {
            if (!chp.Special || chp.Embedded || chp.PictureLocation < 0 || _doc.Data == null)
                return;

            if (!_pictures.TryGetValue(chp.PictureLocation, out var picture))
            {
                picture = DocPictureReader.Read(_doc.Data, chp.PictureLocation);
                _pictures[chp.PictureLocation] = picture;
            }

            if (picture == null)
                return;

            var plain = new DocChp { StyleId = chp.StyleId };
            AppendElement(plain, _media.Picture(picture, chp.PictureLocation));
            _run = null;
            _runChp = null;
        }
    }
}
