using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    internal sealed class RangeRules
    {
        private readonly List<DataValidation> _validations = new();
        private readonly List<ConditionalFormatting> _formats = new();
        private readonly List<(Hyperlink Link, Uri Uri)> _links = new();

        public int Skipped { get; private set; }

        public int ValidationCount => _validations.Count;

        public int FormatCount => _formats.Count;

        public int LinkCount => _links.Count;

        public static RangeRules Capture(ExcelSheet source, CellRange range, int columnDelta, int rowDelta, bool validation, bool conditional, bool links)
        {
            var rules = new RangeRules();
            var worksheet = source.Worksheet;
            if (validation)
            {
                foreach (var item in worksheet.GetFirstChild<DataValidations>()?.Elements<DataValidation>() ?? Enumerable.Empty<DataValidation>())
                {
                    if (!Inside(item.SequenceOfReferences?.InnerText, range, columnDelta, rowDelta, out var moved, out var rowShift, out var columnShift))
                        continue;

                    var copy = (DataValidation)item.CloneNode(true);
                    copy.SequenceOfReferences = new ListValue<StringValue> { InnerText = moved };
                    Rebase(new OpenXmlElement[] { copy.Formula1, copy.Formula2 }, rowShift, columnShift);
                    rules._validations.Add(copy);
                }
            }

            if (conditional)
            {
                foreach (var item in worksheet.Elements<ConditionalFormatting>())
                {
                    if (!Inside(item.SequenceOfReferences?.InnerText, range, columnDelta, rowDelta, out var moved, out var rowShift, out var columnShift))
                        continue;

                    var copy = (ConditionalFormatting)item.CloneNode(true);
                    copy.SequenceOfReferences = new ListValue<StringValue> { InnerText = moved };
                    foreach (var rule in copy.Elements<ConditionalFormattingRule>().ToList())
                    {
                        if (rule.GetFirstChild<ExtensionList>() != null || ConditionalExtensions.HasLink(rule))
                        {
                            rule.Remove();
                            rules.Skipped++;
                            continue;
                        }

                        Rebase(rule.Elements<Formula>(), rowShift, columnShift);
                    }

                    if (copy.Elements<ConditionalFormattingRule>().Any())
                        rules._formats.Add(copy);
                }
            }

            if (links)
            {
                foreach (var link in worksheet.GetFirstChild<Hyperlinks>()?.Elements<Hyperlink>() ?? Enumerable.Empty<Hyperlink>())
                {
                    if (!CellRange.TryParse(link.Reference?.Value, out var at) || !range.Contains(at))
                        continue;

                    Uri uri = null;
                    if (link.Id?.Value != null)
                    {
                        var relationship = source.Part.HyperlinkRelationships.FirstOrDefault(r => r.Id == link.Id.Value);
                        if (relationship == null)
                            continue;

                        uri = relationship.Uri;
                    }

                    var copy = (Hyperlink)link.CloneNode(true);
                    copy.Id = null;
                    copy.Reference = new CellRange(at.Left + columnDelta, at.Top + rowDelta, at.Right + columnDelta, at.Bottom + rowDelta).ToString();
                    rules._links.Add((copy, uri));
                }
            }

            return rules;
        }

        public static void Remove(ExcelSheet sheet, CellRange area, bool validation, bool conditional, bool links)
        {
            var worksheet = sheet.Worksheet;
            if (validation)
            {
                var container = worksheet.GetFirstChild<DataValidations>();
                foreach (var item in container?.Elements<DataValidation>().ToList() ?? new List<DataValidation>())
                {
                    var remaining = Subtract(item.SequenceOfReferences?.InnerText, area, out var rowShift, out var columnShift);
                    if (remaining == null)
                        continue;

                    if (remaining.Length == 0)
                    {
                        item.Remove();
                        continue;
                    }

                    item.SequenceOfReferences = new ListValue<StringValue> { InnerText = remaining };
                    Rebase(new OpenXmlElement[] { item.Formula1, item.Formula2 }, rowShift, columnShift);
                }

                if (container != null)
                {
                    var count = container.Elements<DataValidation>().Count();
                    if (count == 0)
                        container.Remove();
                    else
                        container.Count = (uint)count;
                }
            }

            if (conditional)
            {
                foreach (var item in worksheet.Elements<ConditionalFormatting>().ToList())
                {
                    var remaining = Subtract(item.SequenceOfReferences?.InnerText, area, out var rowShift, out var columnShift);
                    if (remaining == null)
                        continue;

                    var linked = item.Elements<ConditionalFormattingRule>().Select(ConditionalExtensions.LinkId).Where(id => id != null).ToList();
                    if (remaining.Length == 0)
                    {
                        item.Remove();
                        ConditionalExtensions.RemoveLinked(worksheet, linked);
                        continue;
                    }

                    item.SequenceOfReferences = new ListValue<StringValue> { InnerText = remaining };
                    foreach (var rule in item.Elements<ConditionalFormattingRule>())
                        Rebase(rule.Elements<Formula>(), rowShift, columnShift);

                    foreach (var id in linked)
                    {
                        var block = ConditionalExtensions.BlockOfLink(worksheet, id);
                        if (block != null)
                            ConditionalExtensions.SetSqref(block, remaining);
                    }
                }
            }

            if (links)
                RemoveLinks(sheet, area);
        }

        public void Place(ExcelSheet target)
        {
            var worksheet = target.Worksheet;
            if (_validations.Count > 0)
            {
                var container = worksheet.GetFirstChild<DataValidations>() ?? WorksheetOrder.Insert(worksheet, new DataValidations());
                foreach (var item in _validations)
                    container.Append(item.CloneNode(true));

                container.Count = (uint)container.Elements<DataValidation>().Count();
            }

            if (_formats.Count > 0)
            {
                var priority = Math.Max(
                    worksheet.Elements<ConditionalFormatting>().SelectMany(f => f.Elements<ConditionalFormattingRule>()).Select(r => r.Priority?.Value ?? 0).DefaultIfEmpty(0).Max(),
                    ConditionalExtensions.Rules(worksheet).Select(ConditionalExtensions.PriorityOf).DefaultIfEmpty(0).Max());
                foreach (var item in _formats)
                {
                    var copy = (ConditionalFormatting)item.CloneNode(true);
                    foreach (var rule in copy.Elements<ConditionalFormattingRule>())
                        rule.Priority = ++priority;

                    WorksheetOrder.Insert(worksheet, copy);
                }
            }

            if (_links.Count > 0)
            {
                var container = worksheet.GetFirstChild<Hyperlinks>() ?? WorksheetOrder.Insert(worksheet, new Hyperlinks());
                foreach (var (link, uri) in _links)
                {
                    var copy = (Hyperlink)link.CloneNode(true);
                    if (uri != null)
                        copy.Id = target.Part.AddHyperlinkRelationship(uri, true).Id;

                    container.Append(copy);
                }
            }
        }

        private static bool Inside(string sqref, CellRange range, int columnDelta, int rowDelta, out string moved, out int rowShift, out int columnShift)
        {
            moved = null;
            rowShift = 0;
            columnShift = 0;
            if (!SqrefMath.TryParse(sqref, out var ranges))
                return false;

            var inside = SqrefMath.Intersect(ranges, range);
            if (inside.Count == 0)
                return false;

            var translated = SqrefMath.Translate(inside, columnDelta, rowDelta);
            var before = SqrefMath.Bounds(ranges);
            var after = SqrefMath.Bounds(translated);
            rowShift = after.Top - before.Top;
            columnShift = after.Left - before.Left;
            moved = SqrefMath.Format(translated);
            return moved != null;
        }

        private static string Subtract(string sqref, CellRange area, out int rowShift, out int columnShift)
        {
            rowShift = 0;
            columnShift = 0;
            if (!SqrefMath.TryParse(sqref, out var ranges) || !SqrefMath.Intersects(ranges, area))
                return null;

            var remaining = SqrefMath.Subtract(ranges, area);
            if (remaining.Count == 0)
                return string.Empty;

            var before = SqrefMath.Bounds(ranges);
            var after = SqrefMath.Bounds(remaining);
            rowShift = after.Top - before.Top;
            columnShift = after.Left - before.Left;
            return SqrefMath.Format(remaining);
        }

        private static void Rebase(IEnumerable<OpenXmlElement> formulas, int rowShift, int columnShift)
        {
            if (rowShift == 0 && columnShift == 0)
                return;

            foreach (var element in formulas)
            {
                if (element is OpenXmlLeafTextElement leaf && !string.IsNullOrEmpty(leaf.Text))
                    leaf.Text = FormulaText.Shift(leaf.Text, rowShift, columnShift);
            }
        }

        private static void RemoveLinks(ExcelSheet sheet, CellRange area)
        {
            var container = sheet.Worksheet.GetFirstChild<Hyperlinks>();
            if (container == null)
                return;

            foreach (var link in container.Elements<Hyperlink>().ToList())
            {
                if (!CellRange.TryParse(link.Reference?.Value, out var at) || !area.Contains(at))
                    continue;

                var id = link.Id?.Value;
                link.Remove();
                if (id == null || container.Elements<Hyperlink>().Any(l => l.Id?.Value == id))
                    continue;

                var relationship = sheet.Part.HyperlinkRelationships.FirstOrDefault(r => r.Id == id);
                if (relationship != null)
                    sheet.Part.DeleteReferenceRelationship(relationship);
            }

            if (!container.HasChildren)
                container.Remove();
        }
    }
}
