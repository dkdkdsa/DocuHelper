using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace DocuHelper.Importers.Hwp
{
    internal sealed partial class HwpxBody
    {
        private static readonly HashSet<string> s_shapes = new(StringComparer.Ordinal)
        {
            "rect", "ellipse", "arc", "polygon", "curve", "line", "connectLine",
        };

        private static readonly HashSet<string> s_controls = new(StringComparer.Ordinal)
        {
            "btn", "radioBtn", "checkBtn", "comboBox", "edit", "listBox", "scrollBar",
        };

        private static readonly string s_artBreak = new([(char)0x240D, (char)0x240A]);

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
    }
}
