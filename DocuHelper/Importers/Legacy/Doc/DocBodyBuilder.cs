using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal sealed class DocBodyBuilder
    {
        private const int MaxListLevel = 8;

        private readonly DocDocument _doc;
        private readonly DocStyleMap _styles;
        private readonly HashSet<int> _lists;
        private readonly DocFormatLookup _lookup;
        private readonly DocParagraphWriter _writer;
        private readonly List<OpenXmlElement> _body = new();
        private readonly List<DocTableState> _tables = new();

        private DocSection _pendingSection;

        public DocBodyBuilder(DocDocument doc, DocStyleMap styles, HashSet<int> lists, DocWordMedia media)
        {
            _doc = doc;
            _styles = styles;
            _lists = lists;
            _lookup = new DocFormatLookup(doc);
            _writer = new DocParagraphWriter(doc, _lookup, styles, media);
        }

        public List<OpenXmlElement> Build()
        {
            var text = _doc.Text;
            var count = text.Length;
            var sections = _doc.Sections;
            var section = 0;
            var position = 0;
            while (position < count)
            {
                while (section < sections.Count - 1 && position >= sections[section].CpEnd)
                    section++;

                var last = section == sections.Count - 1;
                var sectionEnd = sections[section].CpEnd;
                var end = FindEnd(text, position, sectionEnd, last);
                var terminator = end < count ? text[end] : '\r';
                var pap = _lookup.Paragraph(Math.Min(end, count - 1));
                var depth = pap.Depth;
                if (depth > 0 && (pap.RowEnd || pap.InnerRowEnd))
                {
                    CloseTables(depth);
                    Table(depth).EndRow(pap.Row);
                }
                else
                {
                    var paragraph = BuildParagraph(position, end, pap);
                    if (!last && end >= sectionEnd - 1)
                        _pendingSection = sections[section];

                    if (depth == 0)
                    {
                        CloseTables(0);
                        if (_pendingSection != null)
                        {
                            Attach(paragraph, _pendingSection);
                            _pendingSection = null;
                        }

                        _body.Add(paragraph);
                    }
                    else
                    {
                        CloseTables(depth);
                        var state = Table(depth);
                        state.Current.Add(paragraph);
                        if (terminator == '\a' || pap.InnerCellEnd)
                            state.EndCell();
                    }
                }

                position = end + 1;
            }

            CloseTables(0);
            if (_pendingSection != null)
            {
                var paragraph = new Paragraph();
                Attach(paragraph, _pendingSection);
                _body.Add(paragraph);
            }

            if (_body.Count == 0 || _body[^1] is Table)
                _body.Add(new Paragraph());

            return _body;
        }

        private static int FindEnd(string text, int start, int sectionEnd, bool last)
        {
            for (var i = start; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '\r' || c == '\a' || (!last && i >= sectionEnd - 1))
                    return i;
            }

            return text.Length;
        }

        private static void Attach(Paragraph paragraph, DocSection section)
        {
            var properties = paragraph.ParagraphProperties ??= new ParagraphProperties();
            var element = new SectionProperties();
            DocWordPage.Fill(element, section);
            properties.AppendChild(element);
        }

        private Paragraph BuildParagraph(int start, int end, DocPap pap)
        {
            var istd = pap.Istd ?? 0;
            var paragraph = _writer.Build(start, end, istd);
            var properties = new ParagraphProperties();
            var style = _styles.IdOf(istd, false);
            if (style != null)
                properties.AppendChild(new ParagraphStyleId { Val = style });

            var inherited = _doc.Styles.ResolveParagraph(istd);
            var number = pap.ListOverride ?? inherited.ListOverride;
            var level = pap.ListLevel ?? inherited.ListLevel ?? 0;
            int? list = null;
            if (number.HasValue && _lists.Contains(Math.Abs(number.Value)))
                list = Math.Abs(number.Value);

            DocWordParagraphs.Fill(properties, pap, list, Math.Clamp(level, 0, MaxListLevel));
            if (properties.HasChildren)
                paragraph.PrependChild(properties);

            return paragraph;
        }

        private DocTableState Table(int depth)
        {
            while (_tables.Count < depth)
                _tables.Add(new DocTableState());

            return _tables[depth - 1];
        }

        private void CloseTables(int depth)
        {
            while (_tables.Count > depth)
            {
                var state = _tables[^1];
                _tables.RemoveAt(_tables.Count - 1);
                state.EndRow(null);
                var width = DocWordPage.ContentWidth(_doc.Sections[0]);
                var table = DocWordTables.Build(state.Rows, width);
                if (table == null)
                    continue;

                if (_tables.Count > 0)
                {
                    _tables[^1].Current.Add(table);
                }
                else
                {
                    if (_body.Count > 0 && _body[^1] is Table)
                        _body.Add(new Paragraph());

                    _body.Add(table);
                }
            }
        }
    }
}
