using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace DocuHelper.Importers.Hwp
{
    internal sealed class HwpxBody
    {
        private const int MaxDepth = 64;

        private static readonly HashSet<string> s_shapes = new(StringComparer.Ordinal)
        {
            "rect", "ellipse", "arc", "polygon", "curve", "line", "connectLine",
        };

        private static readonly HashSet<string> s_controls = new(StringComparer.Ordinal)
        {
            "btn", "radioBtn", "checkBtn", "comboBox", "edit", "listBox", "scrollBar",
        };

        private static readonly string s_artBreak = new([(char)0x240D, (char)0x240A]);

        private readonly HwpxHeader _header;
        private readonly HwpDocument _document;
        private readonly HwpxNumbering _numbering;
        private readonly List<string> _masterPages = new();
        private int _outline = 1;
        private int _footnotes;
        private int _endnotes;
        private int _deleting;
        private int _deleted;
        private int _depth;
        private bool _pageBreak;
        private bool _dropNumber;
        private bool _droppedNumber;

        public HwpxBody(HwpxHeader header, HwpDocument document)
        {
            _header = header;
            _document = document;
            _numbering = new HwpxNumbering(header);
        }

        public IReadOnlyList<string> MasterPages => _masterPages;

        public void ReadSection(XElement section, bool first)
        {
            var properties = section.Descendants().FirstOrDefault(e => e.Name.LocalName == "secPr");
            if (properties != null)
            {
                _outline = HwpxXml.Int(properties, "outlineShapeIDRef", _outline);
                _masterPages.AddRange(properties.Descendants()
                    .Where(e => e.Name.LocalName == "masterPage")
                    .Select(e => HwpxXml.Attribute(e, "idRef"))
                    .Where(id => !string.IsNullOrEmpty(id)));
            }

            if (first)
                _document.Page = Page(properties);

            _pageBreak = !first;
            _deleting = 0;
            foreach (var paragraph in HwpxXml.Children(section, "p"))
                ReadParagraph(paragraph, _document.Blocks);

            _pageBreak = false;
        }

        private static HwpPage Page(XElement properties)
        {
            var page = HwpxXml.Child(properties, "pagePr");
            var margin = HwpxXml.Child(page, "margin");
            return new HwpPage
            {
                Width = HwpxXml.Int(page, "width", 59528),
                Height = HwpxXml.Int(page, "height", 84188),
                Landscape = HwpxXml.Attribute(page, "landscape") == "NARROWLY",
                MarginLeft = HwpxXml.Int(margin, "left", 8504),
                MarginRight = HwpxXml.Int(margin, "right", 8504),
                MarginTop = HwpxXml.Int(margin, "top", 5668),
                MarginBottom = HwpxXml.Int(margin, "bottom", 4252),
                MarginHeader = HwpxXml.Int(margin, "header", 4252),
                MarginFooter = HwpxXml.Int(margin, "footer", 4252),
            };
        }

        private void ReadList(XElement list, List<HwpBlock> output)
        {
            if (list == null || _depth >= MaxDepth)
                return;

            var deleting = _deleting;
            _deleting = 0;
            _depth++;
            foreach (var paragraph in HwpxXml.Children(list, "p"))
                ReadParagraph(paragraph, output);

            _depth--;
            _deleting = deleting;
        }

        private void ReadParagraph(XElement element, List<HwpBlock> output)
        {
            var shape = _header.ParaShape(HwpxXml.Int(element, "paraPrIDRef", -1));
            var paragraph = new HwpParagraph
            {
                Alignment = shape.Alignment,
                LeftIndent = shape.LeftIndent,
                FirstLineIndent = shape.FirstLineIndent,
                SpaceBefore = shape.SpaceBefore,
                SpaceAfter = shape.SpaceAfter,
                LineSpacing = shape.LineSpacing,
                PageBreakBefore = _pageBreak || shape.PageBreakBefore || HwpxXml.Flag(element, "pageBreak"),
                Label = _numbering.Next(shape, _outline),
            };
            _pageBreak = false;

            var runs = new HwpxRunBuilder();
            var anchored = new List<HwpBlock>();
            var deleted = _deleted;
            foreach (var run in HwpxXml.Children(element, "run"))
            {
                var charShape = _header.CharShape(HwpxXml.Int(run, "charPrIDRef", -1));
                foreach (var child in HwpxXml.Children(run))
                    ReadInline(child, charShape, runs, anchored);
            }

            paragraph.Runs = runs.Runs;
            var empty = paragraph.Runs.Count == 0 && paragraph.Label == null && !paragraph.PageBreakBefore;
            if (!empty || (anchored.Count == 0 && _deleted == deleted))
                output.Add(paragraph);

            output.AddRange(anchored);
        }

        private void ReadInline(XElement element, HwpxCharShape shape, HwpxRunBuilder runs, List<HwpBlock> anchored)
        {
            switch (element.Name.LocalName)
            {
                case "t":
                    var own = HwpxXml.Int(element, "charPrIDRef", -1);
                    ReadText(element.Nodes(), own < 0 ? shape : _header.CharShape(own), runs, 0);
                    break;
                case "ctrl":
                    foreach (var control in HwpxXml.Children(element))
                        ReadControl(control, shape, runs);
                    break;
                case "compose":
                    Emit(HwpxXml.Attribute(element, "composeText"), shape, runs);
                    break;
                case "dutmal":
                    Emit(Dutmal(element), shape, runs);
                    break;
                default:
                    ReadObject(element, shape, anchored);
                    break;
            }
        }

        private void ReadText(IEnumerable<XNode> nodes, HwpxCharShape shape, HwpxRunBuilder runs, int depth)
        {
            foreach (var node in nodes)
            {
                if (node is XText text)
                {
                    Emit(text.Value, shape, runs);
                    continue;
                }

                if (node is not XElement element)
                    continue;

                switch (element.Name.LocalName)
                {
                    case "tab":
                        Emit("\t", shape, runs);
                        break;
                    case "lineBreak":
                        Emit("\n", shape, runs);
                        break;
                    case "hyphen":
                    case "hypen":
                        Emit("-", shape, runs);
                        break;
                    case "nbSpace":
                    case "fwSpace":
                        Emit(" ", shape, runs);
                        break;
                    case "deleteBegin":
                        _deleting++;
                        break;
                    case "deleteEnd":
                        _deleting = Math.Max(0, _deleting - 1);
                        break;
                    case "switch" when depth < HwpxXml.MaxSwitchDepth:
                        ReadText(HwpxXml.Branch(element)?.Nodes() ?? Enumerable.Empty<XNode>(), shape, runs, depth + 1);
                        break;
                }
            }
        }

        private void Emit(string text, HwpxCharShape shape, HwpxRunBuilder runs)
        {
            if (string.IsNullOrEmpty(text))
                return;

            if (_deleting > 0)
            {
                _deleted++;
                return;
            }

            runs.Append(text, shape);
        }

        private void ReadControl(XElement element, HwpxCharShape shape, HwpxRunBuilder runs)
        {
            switch (element.Name.LocalName)
            {
                case "header":
                case "footer":
                    if (HwpxXml.HasContent(element))
                        _document.Skipped.Add(HwpSkipped.HeaderFooter);
                    break;
                case "pageNum":
                    if (HwpxXml.Attribute(element, "pos") != "NONE")
                        _document.Skipped.Add(HwpSkipped.HeaderFooter);
                    break;
                case "footNote":
                    ReadNote(element, false, shape, runs);
                    break;
                case "endNote":
                    ReadNote(element, true, shape, runs);
                    break;
                case "autoNum":
                    ReadNumber(element, shape, runs);
                    break;
            }
        }

        private void ReadNote(XElement element, bool endnote, HwpxCharShape shape, HwpxRunBuilder runs)
        {
            if (_deleting > 0)
            {
                _deleted++;
                return;
            }

            var mark = endnote
                ? "미주" + (++_endnotes).ToString(CultureInfo.InvariantCulture) + ")"
                : (++_footnotes).ToString(CultureInfo.InvariantCulture) + ")";
            runs.AppendMark(mark, shape);
            var note = new HwpNote { Mark = mark };
            _document.Notes.Add(note);

            var drop = _dropNumber;
            var dropped = _droppedNumber;
            _dropNumber = true;
            _droppedNumber = false;
            ReadList(HwpxXml.Child(element, "subList"), note.Blocks);
            if (_droppedNumber)
                TrimStart(note.Blocks);

            _dropNumber = drop;
            _droppedNumber = dropped;
        }

        private void ReadNumber(XElement element, HwpxCharShape shape, HwpxRunBuilder runs)
        {
            var type = HwpxXml.Attribute(element, "numType");
            if (_dropNumber && (type == "FOOTNOTE" || type == "ENDNOTE"))
            {
                _dropNumber = false;
                _droppedNumber = true;
                return;
            }

            var format = HwpxXml.Child(element, "autoNumFormat");
            var number = HwpxNumberFormat.Format(HwpxXml.Int(element, "num", 1), HwpxXml.Attribute(format, "type") ?? "DIGIT");
            Emit(HwpxXml.Attribute(format, "prefixChar") + number + HwpxXml.Attribute(format, "suffixChar"), shape, runs);
        }

        private void ReadObject(XElement element, HwpxCharShape shape, List<HwpBlock> anchored)
        {
            if (_deleting > 0 || _depth >= MaxDepth)
                return;

            var name = element.Name.LocalName;
            switch (name)
            {
                case "tbl":
                    ReadCaption(element, true, anchored);
                    anchored.Add(ReadTable(element));
                    ReadCaption(element, false, anchored);
                    break;
                case "pic":
                    Skip(HwpSkipped.Picture, element, anchored);
                    break;
                case "equation":
                    Skip(HwpSkipped.Equation, element, anchored);
                    break;
                case "chart":
                    Skip(HwpSkipped.Chart, element, anchored);
                    break;
                case "ole":
                case "video":
                    Skip(HwpSkipped.Object, element, anchored);
                    break;
                case "container":
                    ReadCaption(element, true, anchored);
                    _depth++;
                    foreach (var child in HwpxXml.Children(element))
                        ReadObject(child, shape, anchored);

                    _depth--;
                    ReadCaption(element, false, anchored);
                    break;
                case "textart":
                    ReadTextArt(element, shape, anchored);
                    break;
                default:
                    if (s_controls.Contains(name))
                    {
                        _document.Skipped.Add(HwpSkipped.Object);
                    }
                    else if (s_shapes.Contains(name))
                    {
                        ReadCaption(element, true, anchored);
                        ReadList(HwpxXml.Child(HwpxXml.Child(element, "drawText"), "subList"), anchored);
                        ReadCaption(element, false, anchored);
                    }

                    break;
            }
        }

        private void Skip(HwpSkipped kind, XElement element, List<HwpBlock> anchored)
        {
            _document.Skipped.Add(kind);
            ReadCaption(element, true, anchored);
            ReadCaption(element, false, anchored);
        }

        private void ReadCaption(XElement element, bool before, List<HwpBlock> anchored)
        {
            var caption = HwpxXml.Child(element, "caption");
            if (caption == null)
                return;

            var side = HwpxXml.Attribute(caption, "side");
            if ((side == "TOP" || side == "LEFT") == before)
                ReadList(HwpxXml.Child(caption, "subList"), anchored);
        }

        private HwpTable ReadTable(XElement element)
        {
            var table = new HwpTable
            {
                RowCount = Math.Max(0, HwpxXml.Int(element, "rowCnt", 0)),
                ColumnCount = Math.Max(0, HwpxXml.Int(element, "colCnt", 0)),
            };
            var row = 0;
            foreach (var line in HwpxXml.Children(element, "tr"))
            {
                var column = 0;
                foreach (var item in HwpxXml.Children(line, "tc"))
                {
                    var address = HwpxXml.Child(item, "cellAddr");
                    var span = HwpxXml.Child(item, "cellSpan");
                    var cell = new HwpCell
                    {
                        Row = Math.Max(0, HwpxXml.Int(address, "rowAddr", row)),
                        Column = Math.Max(0, HwpxXml.Int(address, "colAddr", column)),
                        RowSpan = Math.Max(1, HwpxXml.Int(span, "rowSpan", 1)),
                        ColumnSpan = Math.Max(1, HwpxXml.Int(span, "colSpan", 1)),
                        Width = Math.Max(0, HwpxXml.Int(HwpxXml.Child(item, "cellSz"), "width", 0)),
                    };
                    ReadList(HwpxXml.Child(item, "subList"), cell.Blocks);
                    table.Cells.Add(cell);
                    table.RowCount = Math.Max(table.RowCount, cell.Row + cell.RowSpan);
                    table.ColumnCount = Math.Max(table.ColumnCount, cell.Column + cell.ColumnSpan);
                    column = cell.Column + cell.ColumnSpan;
                }

                row++;
            }

            return table;
        }

        private void ReadTextArt(XElement element, HwpxCharShape shape, List<HwpBlock> anchored)
        {
            var text = (HwpxXml.Attribute(element, "text") ?? string.Empty)
                .Replace(s_artBreak, "\n")
                .Replace((char)0x240D, '\n')
                .Replace((char)0x240A, '\n')
                .Trim();
            var runs = new HwpxRunBuilder();
            runs.Append(text, shape);
            if (runs.Runs.Count > 0)
                anchored.Add(new HwpParagraph { Runs = runs.Runs });
        }

        private static string Dutmal(XElement element)
        {
            var main = HwpxXml.Child(element, "mainText")?.Value ?? string.Empty;
            var sub = HwpxXml.Child(element, "subText")?.Value;
            return string.IsNullOrWhiteSpace(sub) ? main : main + "(" + sub.Trim() + ")";
        }

        private static void TrimStart(List<HwpBlock> blocks)
        {
            if (blocks.Count == 0 || blocks[0] is not HwpParagraph paragraph)
                return;

            while (paragraph.Runs.Count > 0)
            {
                var run = paragraph.Runs[0];
                run.Text = run.Text.TrimStart(' ');
                if (run.Text.Length > 0)
                    return;

                paragraph.Runs.RemoveAt(0);
            }
        }
    }
}
