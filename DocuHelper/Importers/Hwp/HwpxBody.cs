using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace DocuHelper.Importers.Hwp
{
    internal sealed partial class HwpxBody
    {
        private const int MaxDepth = 64;

        private readonly HwpxHeader _header;
        private readonly HwpDocument _document;
        private readonly HwpxNumbering _numbering;
        private readonly List<string> _masterPages = new();
        private int _outline = 1;
        private int _deleting;
        private int _deleted;
        private int _depth;
        private bool _pageBreak;

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
    }
}
