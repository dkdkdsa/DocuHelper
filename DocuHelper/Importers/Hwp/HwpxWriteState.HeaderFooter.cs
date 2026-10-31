using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using DocuHelper.Tools.Word;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using static DocuHelper.Importers.Hwp.HwpxNames;

namespace DocuHelper.Importers.Hwp
{
    internal sealed partial class HwpxWriteState
    {
        private const string HidePrefix = "_hwpHide_";
        private const string NewNumberPrefix = "_hwpNewNum_";
        private const string DashStart = "- ";
        private const string DashEnd = " -";

        private string _pageFormat = "DIGIT";

        internal sealed class PageFields
        {
            public HashSet<Run> Skip { get; } = new();

            public HashSet<Run> Anchors { get; } = new();
        }

        private sealed class FieldState
        {
            public string Instruction { get; set; } = string.Empty;

            public bool InResult { get; set; }

            public bool IsPage { get; set; }

            public bool Anchored { get; set; }
        }

        private void SetPageNumbering(Body body)
        {
            var numbers = body.Elements<SectionProperties>().LastOrDefault()?.GetFirstChild<PageNumberType>();
            _pageFormat = FormatType(numbers?.Format?.InnerText);
        }

        private List<XElement> HeaderFooterRuns(Body body)
        {
            var runs = new List<XElement>();
            var section = body.Elements<SectionProperties>().LastOrDefault();
            var main = _document?.MainDocumentPart;
            if (section == null || main == null)
                return runs;

            var width = TextWidth(section);
            var even = section.Elements<HeaderReference>().Any(r => IsKind(r.Type, HeaderFooterValues.Even))
                || section.Elements<FooterReference>().Any(r => IsKind(r.Type, HeaderFooterValues.Even));
            var pageNumberPlaced = false;
            foreach (var footer in new[] { true, false })
            {
                var defaultRoot = Root(section, footer, HeaderFooterValues.Default);
                if (defaultRoot != null)
                {
                    if (!pageNumberPlaced && PageNumberOf(defaultRoot, footer, out var position, out var side))
                    {
                        runs.Add(PageNumberRun(position, side));
                        pageNumberPlaced = true;
                    }
                    else
                    {
                        runs.Add(HeaderRun(footer, even ? "ODD" : "BOTH", defaultRoot, width));
                    }
                }

                var evenRoot = Root(section, footer, HeaderFooterValues.Even);
                if (evenRoot != null)
                    runs.Add(HeaderRun(footer, "EVEN", evenRoot, width));
            }

            return runs;
        }

        private static bool IsKind(EnumValue<HeaderFooterValues> type, HeaderFooterValues kind) =>
            (type?.Value ?? HeaderFooterValues.Default) == kind;

        private OpenXmlElement Root(SectionProperties section, bool footer, HeaderFooterValues kind)
        {
            var main = _document.MainDocumentPart;
            if (footer)
            {
                var reference = section.Elements<FooterReference>().FirstOrDefault(r => IsKind(r.Type, kind));
                return reference?.Id?.Value != null && main.TryGetPartById(reference.Id.Value, out var part) && part is FooterPart footerPart ? footerPart.Footer : null;
            }

            var header = section.Elements<HeaderReference>().FirstOrDefault(r => IsKind(r.Type, kind));
            return header?.Id?.Value != null && main.TryGetPartById(header.Id.Value, out var headerPart) && headerPart is HeaderPart headerRoot ? headerRoot.Header : null;
        }

        private XElement HeaderRun(bool footer, string apply, OpenXmlElement root, int width)
        {
            var paragraphs = new List<XElement>();
            Blocks(root.ChildElements, paragraphs);
            if (paragraphs.Count == 0)
                paragraphs.Add(Paragraph(0, 0, false, new List<XElement> { TextRun(0, string.Empty) }));

            var list = new XElement(Hp + "subList",
                new XAttribute("id", string.Empty),
                new XAttribute("textDirection", "HORIZONTAL"),
                new XAttribute("lineWrap", "BREAK"),
                new XAttribute("vertAlign", "TOP"),
                new XAttribute("linkListIDRef", "0"),
                new XAttribute("linkListNextIDRef", "0"),
                new XAttribute("textWidth", Number(width)),
                new XAttribute("textHeight", "0"),
                new XAttribute("hasTextRef", "0"),
                new XAttribute("hasNumRef", "0"),
                paragraphs);
            return new XElement(Hp + "run", new XAttribute("charPrIDRef", "0"),
                new XElement(Hp + "ctrl",
                    new XElement(Hp + (footer ? "footer" : "header"), new XAttribute("applyPageType", apply), new XAttribute("id", "0"), list)));
        }

        private XElement PageNumberRun(string position, string side) => new(Hp + "run", new XAttribute("charPrIDRef", "0"),
            new XElement(Hp + "ctrl",
                new XElement(Hp + "pageNum", new XAttribute("pos", position), new XAttribute("formatType", _pageFormat), new XAttribute("sideChar", side))));

        private XElement PageFieldRun(Run run, Paragraph paragraph) => new(Hp + "run",
            new XAttribute("charPrIDRef", Number(CharId(CharKeyOf(run, paragraph)))),
            new XElement(Hp + "ctrl",
                new XElement(Hp + "autoNum", new XAttribute("num", "1"), new XAttribute("numType", "PAGE"),
                    new XElement(Hp + "autoNumFormat", new XAttribute("type", _pageFormat), new XAttribute("userChar", string.Empty),
                        new XAttribute("prefixChar", string.Empty), new XAttribute("suffixChar", string.Empty), new XAttribute("supscript", "0")))));

        private bool PageNumberOf(OpenXmlElement root, bool footer, out string position, out string side)
        {
            position = null;
            side = null;
            var paragraphs = root.Elements<Paragraph>().ToList();
            if (paragraphs.Count != 1 || root.Elements<Table>().Any())
                return false;

            var paragraph = paragraphs[0];
            var fields = ScanFields(paragraph);
            var text = new System.Text.StringBuilder();
            var before = new System.Text.StringBuilder();
            var after = new System.Text.StringBuilder();
            var seenField = false;
            var fieldCount = 0;
            foreach (var run in paragraph.Descendants<Run>())
            {
                if (fields.Anchors.Contains(run))
                {
                    fieldCount++;
                    seenField = true;
                    continue;
                }

                if (fields.Skip.Contains(run))
                    continue;

                var value = string.Concat(run.Elements<Text>().Select(t => t.Text));
                (seenField ? after : before).Append(value);
            }

            if (fieldCount != 1 || paragraph.Descendants<SimpleField>().Any(f => !IsPage(f.Instruction?.Value)))
                return false;

            var literal = before.ToString() + "|" + after.ToString();
            if (literal == "|")
                side = string.Empty;
            else if (literal == DashStart + "|" + DashEnd)
                side = "-";
            else
                return false;

            var justification = paragraph.ParagraphProperties?.Justification?.Val?.Value;
            var align = justification == JustificationValues.Center ? "CENTER"
                : justification == JustificationValues.Right || justification == JustificationValues.End ? "RIGHT" : "LEFT";
            position = (footer ? "BOTTOM_" : "TOP_") + align;
            return true;
        }

        private PageFields ScanFields(Paragraph paragraph)
        {
            var result = new PageFields();
            var stack = new Stack<FieldState>();
            var simpleAnchored = new HashSet<SimpleField>();
            foreach (var run in paragraph.Descendants<Run>())
            {
                var simple = run.Ancestors<SimpleField>().FirstOrDefault();
                if (simple != null)
                {
                    if (IsPage(simple.Instruction?.Value))
                    {
                        if (simpleAnchored.Add(simple))
                            result.Anchors.Add(run);
                        else
                            result.Skip.Add(run);
                    }

                    continue;
                }

                foreach (var mark in run.Elements<FieldChar>())
                {
                    var kind = mark.FieldCharType?.Value;
                    if (kind == FieldCharValues.Begin)
                    {
                        stack.Push(new FieldState());
                    }
                    else if (kind == FieldCharValues.Separate && stack.Count > 0)
                    {
                        stack.Peek().InResult = true;
                        stack.Peek().IsPage = IsPage(stack.Peek().Instruction);
                    }
                    else if (kind == FieldCharValues.End && stack.Count > 0)
                    {
                        stack.Pop();
                    }
                }

                foreach (var instruction in run.Elements<FieldCode>())
                {
                    if (stack.Count > 0)
                        stack.Peek().Instruction += instruction.Text;
                }

                if (stack.Count > 0 && stack.Peek().InResult && stack.Peek().IsPage && run.Elements<Text>().Any())
                {
                    if (!stack.Peek().Anchored)
                    {
                        stack.Peek().Anchored = true;
                        result.Anchors.Add(run);
                    }
                    else
                    {
                        result.Skip.Add(run);
                    }
                }
            }

            return result;
        }

        private static bool IsPage(string instruction)
        {
            var value = (instruction ?? string.Empty).Trim().ToUpperInvariant();
            return value == "PAGE" || value.StartsWith("PAGE ", StringComparison.Ordinal) || value.StartsWith("PAGE\\", StringComparison.Ordinal);
        }

        private IEnumerable<XElement> ParagraphControls(Paragraph paragraph)
        {
            foreach (var start in paragraph.Elements<BookmarkStart>())
            {
                var name = start.Name?.Value ?? string.Empty;
                if (name.StartsWith(HidePrefix, StringComparison.Ordinal))
                {
                    var flags = name.Substring(HidePrefix.Length);
                    yield return new XElement(Hp + "run", new XAttribute("charPrIDRef", "0"),
                        new XElement(Hp + "ctrl",
                            new XElement(Hp + "pageHiding",
                                new XAttribute("hideHeader", Flag(flags.Contains('h'))),
                                new XAttribute("hideFooter", Flag(flags.Contains('f'))),
                                new XAttribute("hideMasterPage", "0"),
                                new XAttribute("hideBorder", Flag(flags.Contains('b'))),
                                new XAttribute("hideFill", Flag(flags.Contains('g'))),
                                new XAttribute("hidePageNum", Flag(flags.Contains('p'))))));
                }
                else if (name.StartsWith(NewNumberPrefix, StringComparison.Ordinal) && int.TryParse(name.Substring(NewNumberPrefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                {
                    yield return new XElement(Hp + "run", new XAttribute("charPrIDRef", "0"),
                        new XElement(Hp + "ctrl", new XElement(Hp + "newNum", new XAttribute("num", Number(number)), new XAttribute("numType", "PAGE"))));
                }
            }
        }

        private static string Flag(bool value) => value ? "1" : "0";

        private static string FormatType(string value) => value switch
        {
            "upperRoman" => "ROMAN_CAPITAL",
            "lowerRoman" => "ROMAN_SMALL",
            "upperLetter" => "LATIN_CAPITAL",
            "lowerLetter" => "LATIN_SMALL",
            _ => "DIGIT",
        };

        private int TextWidth(SectionProperties section)
        {
            var size = section.GetFirstChild<PageSize>();
            var margin = section.GetFirstChild<PageMargin>();
            var width = size?.Width?.Value is uint w && w > 0 ? Units((int)w) : 59528;
            var left = margin?.Left?.Value is uint l ? Units((int)l) : 8504;
            var right = margin?.Right?.Value is uint r ? Units((int)r) : 8504;
            return Math.Max(1, width - left - right);
        }
    }
}
