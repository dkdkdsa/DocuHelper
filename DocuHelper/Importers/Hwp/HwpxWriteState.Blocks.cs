using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using DocuHelper.Tools.Word;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using static DocuHelper.Importers.Hwp.HwpxNames;

namespace DocuHelper.Importers.Hwp
{
    internal sealed partial class HwpxWriteState
    {
        private void Blocks(IEnumerable<OpenXmlElement> elements, List<XElement> output)
        {
            if (_depth >= MaxDepth)
                return;

            _depth++;
            foreach (var element in elements)
            {
                switch (element)
                {
                    case DocumentFormat.OpenXml.Wordprocessing.Paragraph paragraph:
                        output.Add(Paragraph(paragraph));
                        break;
                    case Table table:
                        output.Add(TableParagraph(table));
                        break;
                    case SdtBlock block:
                        Blocks(block.SdtContentBlock?.ChildElements ?? Enumerable.Empty<OpenXmlElement>(), output);
                        break;
                    case CustomXmlBlock custom:
                        Blocks(custom.ChildElements, output);
                        break;
                }
            }

            _depth--;
        }

        private XElement Paragraph(DocumentFormat.OpenXml.Wordprocessing.Paragraph paragraph)
        {
            var label = Label(paragraph, out var listIndent);
            var para = ParaId(ParaKeyOf(paragraph, listIndent));
            var pageBreak = WordStyles.IsOn(_styles.ParagraphValue<PageBreakBefore>(paragraph));
            var columnBreak = false;
            var runs = new List<XElement>();
            runs.AddRange(ParagraphControls(paragraph));
            var fields = ScanFields(paragraph);
            if (label != null)
            {
                var first = paragraph.Descendants<Run>().FirstOrDefault(r => Owns(paragraph, r));
                runs.Add(TextRun(CharId(CharKeyOf(first, paragraph)), label));
            }

            var scope = new HwpxFieldScope();
            foreach (var element in paragraph.Descendants().Where(e => e is Run || e is BookmarkStart).ToList())
            {
                if (element is BookmarkStart bookmark)
                {
                    if (Owns(paragraph, bookmark) && bookmark.Name?.Value is string name && !name.StartsWith("_", System.StringComparison.Ordinal))
                        runs.Add(BookmarkRun(name));

                    continue;
                }

                var run = (Run)element;
                if (!Owns(paragraph, run))
                    continue;

                if (fields.Anchors.Contains(run))
                {
                    runs.Add(PageFieldRun(run, paragraph));
                    continue;
                }

                if (fields.Skip.Contains(run))
                    continue;

                MoveScope(scope, runs, LinkTargetOf(run), ControlOf(run));
                if (scope.Placeholder)
                    continue;

                foreach (var reference in run.Descendants<FootnoteReference>())
                {
                    var note = NoteRunOf(reference, false);
                    if (note != null)
                        runs.Add(note);
                }

                foreach (var reference in run.Descendants<EndnoteReference>())
                {
                    var note = NoteRunOf(reference, true);
                    if (note != null)
                        runs.Add(note);
                }

                foreach (var drawing in run.Descendants<Drawing>())
                {
                    var picture = PictureOf(drawing);
                    if (picture != null)
                        runs.Add(new XElement(Hp + "run", new XAttribute("charPrIDRef", Number(CharId(CharKeyOf(run, paragraph)))), picture));
                }

                var content = RunContent(run, ref pageBreak, ref columnBreak, runs.Count == 0);
                if (content.Count == 0)
                    continue;

                var element2 = new XElement(Hp + "run", new XAttribute("charPrIDRef", Number(CharId(CharKeyOf(run, paragraph)))));
                element2.Add(new XElement(Hp + "t", content));
                runs.Add(element2);
            }

            CloseScope(scope, runs);

            if (runs.Count == 0)
                runs.Add(TextRun(CharId(CharKeyOf(null, paragraph)), string.Empty));

            return Paragraph(para, StyleIdOf(paragraph, para, CharId(CharKeyOf(null, paragraph))), pageBreak, runs, columnBreak);
        }

        private static bool Owns(DocumentFormat.OpenXml.Wordprocessing.Paragraph paragraph, OpenXmlElement element) =>
            ReferenceEquals(element.Ancestors<DocumentFormat.OpenXml.Wordprocessing.Paragraph>().FirstOrDefault(), paragraph);

        private static List<object> RunContent(Run run, ref bool pageBreak, ref bool columnBreak, bool atStart)
        {
            var content = new List<object>();
            var text = new StringBuilder();
            void Flush()
            {
                if (text.Length == 0)
                    return;

                content.Add(new XText(text.ToString()));
                text.Clear();
            }

            foreach (var child in run.ChildElements)
            {
                switch (child)
                {
                    case Text value:
                        text.Append(Clean(value.Text));
                        break;
                    case TabChar:
                        Flush();
                        content.Add(new XElement(Hp + "tab", new XAttribute("width", "4000"), new XAttribute("leader", "NONE"), new XAttribute("type", "LEFT")));
                        break;
                    case Break column when column.Type?.Value == BreakValues.Column:
                        if (atStart && content.Count == 0 && text.Length == 0)
                            columnBreak = true;
                        else
                        {
                            Flush();
                            content.Add(new XElement(Hp + "lineBreak"));
                        }

                        break;
                    case Break line when line.Type?.Value == BreakValues.Page:
                        if (atStart && content.Count == 0 && text.Length == 0)
                            pageBreak = true;
                        else
                        {
                            Flush();
                            content.Add(new XElement(Hp + "lineBreak"));
                        }

                        break;
                    case Break:
                    case CarriageReturn:
                        Flush();
                        content.Add(new XElement(Hp + "lineBreak"));
                        break;
                    case NoBreakHyphen:
                        text.Append('-');
                        break;
                    case SymbolChar symbol when symbol.Char?.Value != null:
                        if (int.TryParse(symbol.Char.Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code) && code >= 0x20 && code < 0xD800)
                            text.Append(HwpBullet.Text((char)code));
                        break;
                }
            }

            Flush();
            return content;
        }

        private static string Clean(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var builder = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                if (c >= ' ' && c != '￾' && c != '￿')
                    builder.Append(c);
            }

            return builder.ToString();
        }

        private static XElement Paragraph(int para, int style, bool pageBreak, IEnumerable<XElement> runs, bool columnBreak = false) => new(Hp + "p",
            new XAttribute("id", "0"),
            new XAttribute("paraPrIDRef", Number(para)),
            new XAttribute("styleIDRef", Number(style)),
            new XAttribute("pageBreak", pageBreak ? "1" : "0"),
            new XAttribute("columnBreak", columnBreak ? "1" : "0"),
            new XAttribute("merged", "0"),
            runs);

        private static XElement TextRun(int charId, string text) =>
            new(Hp + "run", new XAttribute("charPrIDRef", Number(charId)), new XElement(Hp + "t", text));
    }
}
