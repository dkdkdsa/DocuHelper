using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using A = DocumentFormat.OpenXml.Drawing;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    internal static class ExcelFeatures
    {
        private const int MaxNoteLength = 30;

        public static readonly string[] Kinds =
        {
            "표", "필터", "조건부 서식", "검증", "차트", "그림", "도형", "텍스트 상자", "메모", "링크", "이름",
            "시트 보호", "통합 문서 보호", "개요", "인쇄",
        };

        public static List<Feature> Describe(ExcelBook book, ExcelSheet sheet)
        {
            var list = new List<Feature>();
            var worksheet = sheet.Worksheet;
            if (worksheet == null)
                return list;

            foreach (var table in sheet.Part.TableDefinitionParts.Select(p => p.Table).Where(t => t != null))
            {
                var name = table.DisplayName?.Value ?? table.Name?.Value ?? "표";
                var style = table.TableStyleInfo?.Name?.Value;
                list.Add(new Feature("표", $"{name} ‘{sheet.Name}’!{table.Reference?.Value}" + (style == null ? string.Empty : " " + style)));
            }

            var filter = worksheet.GetFirstChild<AutoFilter>();
            if (filter?.Reference?.Value != null)
            {
                var range = CellRange.TryParse(filter.Reference.Value, out var parsed) ? parsed : null;
                var criteria = new List<string>();
                foreach (var column in filter.Elements<FilterColumn>())
                {
                    var letter = range == null ? "?" : CellAddress.ColumnName(range.Left + (int)(column.ColumnId?.Value ?? 0));
                    var values = column.GetFirstChild<Filters>()?.Elements<Filter>().Select(f => f.Val?.Value).Where(v => v != null).ToList() ?? new List<string>();
                    criteria.Add($"열 {letter}" + (values.Count > 0 ? ": " + string.Join(", ", values.Take(5)) : string.Empty));
                }

                var hidden = range == null ? 0 : Enumerable.Range(range.Top + 1, Math.Max(0, range.RowCount - 1)).Count(sheet.IsRowHidden);
                var text = $"필터 {filter.Reference.Value}" + (criteria.Count > 0 ? " (" + string.Join("; ", criteria) + ")" : string.Empty)
                    + (hidden > 0 ? $", 숨긴 행 {hidden}개" : string.Empty);
                list.Add(new Feature("필터", text));
            }

            foreach (var rule in worksheet.Elements<ConditionalFormatting>())
            {
                var types = rule.Elements<ConditionalFormattingRule>().Select(r => r.Type?.InnerText ?? "규칙").Distinct().ToList();
                list.Add(new Feature("조건부 서식", $"{rule.SequenceOfReferences?.InnerText} {string.Join(", ", types)}"));
            }

            foreach (var validation in worksheet.GetFirstChild<DataValidations>()?.Elements<DataValidation>() ?? Enumerable.Empty<DataValidation>())
            {
                var type = validation.Type?.InnerText ?? "any";
                var source = validation.Formula1?.Text;
                list.Add(new Feature("검증", $"{validation.SequenceOfReferences?.InnerText} {type}" + (source == null ? string.Empty : $"({source})")));
            }

            var drawing = sheet.Part.DrawingsPart;
            foreach (var anchor in ExcelDrawings.Anchors(drawing))
                list.Add(DescribeAnchor(book, drawing, anchor));

            foreach (var link in worksheet.GetFirstChild<Hyperlinks>()?.Elements<Hyperlink>() ?? Enumerable.Empty<Hyperlink>())
            {
                var target = link.Location?.Value ?? sheet.Part.HyperlinkRelationships.FirstOrDefault(r => r.Id == link.Id?.Value)?.Uri.ToString() ?? "링크";
                list.Add(new Feature("링크", $"{link.Reference?.Value} {Shorten(target)}"));
            }

            foreach (var comment in sheet.Part.WorksheetCommentsPart?.Comments?.CommentList?.Elements<Comment>() ?? Enumerable.Empty<Comment>())
                list.Add(new Feature("메모", $"{comment.Reference?.Value} {Shorten(comment.CommentText?.InnerText ?? string.Empty)}"));

            var threaded = sheet.Part.Parts.Count(p => p.OpenXmlPart.ContentType.Contains("threadedComment", StringComparison.OrdinalIgnoreCase));
            if (threaded > 0)
                list.Add(new Feature("메모", $"댓글 파트 {threaded}개"));

            foreach (var name in book.WorkbookPart?.Workbook?.DefinedNames?.Elements<DefinedName>() ?? Enumerable.Empty<DefinedName>())
            {
                var text = name.Text ?? string.Empty;
                var local = name.LocalSheetId?.Value == (uint)sheet.Position;
                var refers = text.Contains($"'{sheet.Name}'!", StringComparison.Ordinal) || text.Contains(sheet.Name + "!", StringComparison.Ordinal);
                if (name.Name?.Value == "_xlnm.Print_Area" && local)
                    list.Add(new Feature("인쇄", $"인쇄 영역 {text}"));
                else if (name.Name?.Value != null && !name.Name.Value.StartsWith("_xlnm", StringComparison.Ordinal) && (local || refers))
                    list.Add(new Feature("이름", $"{name.Name.Value} = {text}"));
            }

            if (ProtectionGuard.ProtectionOf(sheet) != null)
                list.Add(new Feature("시트 보호", sheet.Name));

            if (ProtectionGuard.StructureLocked(book))
                list.Add(new Feature("통합 문서 보호", "구조 보호"));

            var outlineRows = sheet.Data?.Elements<Row>().Where(r => (r.OutlineLevel?.Value ?? 0) > 0).Select(r => (int)r.RowIndex.Value).ToList() ?? new List<int>();
            var outlineColumns = worksheet.GetFirstChild<Columns>()?.Elements<Column>().Count(c => (c.OutlineLevel?.Value ?? 0) > 0) ?? 0;
            if (outlineRows.Count > 0 || outlineColumns > 0)
            {
                var parts = new List<string>();
                if (outlineRows.Count > 0)
                    parts.Add($"행 {outlineRows.Min()}-{outlineRows.Max()}");

                if (outlineColumns > 0)
                    parts.Add($"열 {outlineColumns}개");

                list.Add(new Feature("개요", string.Join(", ", parts)));
            }

            if (worksheet.GetFirstChild<PageSetup>() != null && !list.Any(f => f.Kind == "인쇄"))
                list.Add(new Feature("인쇄", "페이지 설정"));

            return list;
        }

        public static string Headline(IReadOnlyList<Feature> features)
        {
            var brief = Brief(features);
            return brief == null ? null : "Features: " + brief + " (show=features for places).";
        }

        public static string Brief(IReadOnlyList<Feature> features)
        {
            if (features.Count == 0)
                return null;

            var parts = new List<string>();
            foreach (var kind in Kinds)
            {
                var items = features.Where(f => f.Kind == kind).ToList();
                if (items.Count == 0)
                    continue;

                parts.Add(items.Count == 1 && kind is "필터" or "시트 보호" or "통합 문서 보호" or "개요" or "인쇄" ? kind : $"{kind} {items.Count}");
            }

            return string.Join(", ", parts);
        }

        public static string Listing(string sheetName, IReadOnlyList<Feature> features)
        {
            if (features.Count == 0)
                return $"Features of ‘{sheetName}’: none.";

            var text = new System.Text.StringBuilder();
            text.AppendLine($"Features of ‘{sheetName}’:");
            foreach (var feature in features)
                text.AppendLine($"- {feature.Kind}: {feature.Text}");

            return text.ToString().TrimEnd();
        }

        private static Feature DescribeAnchor(ExcelBook book, DrawingsPart drawing, OpenXmlElement anchor)
        {
            var properties = anchor.Descendants<Xdr.NonVisualDrawingProperties>().FirstOrDefault();
            var name = properties?.Name?.Value ?? "개체";
            var from = anchor.ChildElements.FirstOrDefault(e => e.LocalName == "from");
            var to = anchor.ChildElements.FirstOrDefault(e => e.LocalName == "to");
            var start = from == null ? "?" : CellAddress.ColumnName(ExcelDrawings.CellOf(from).Column) + (ExcelDrawings.CellOf(from).Row + 1).ToString(CultureInfo.InvariantCulture);
            var place = to == null ? start : start + ":" + CellAddress.ColumnName(ExcelDrawings.CellOf(to).Column) + (ExcelDrawings.CellOf(to).Row + 1).ToString(CultureInfo.InvariantCulture);
            var relation = anchor.Descendants().FirstOrDefault(d => d.LocalName == "chart")?.GetAttributes().FirstOrDefault(a => a.LocalName == "id").Value;
            if (relation != null && drawing.GetPartById(relation) is ChartPart chart)
                return new Feature("차트", $"{name} {ChartKind(chart)} {place}");

            if (anchor.Descendants<Xdr.Picture>().Any())
                return new Feature("그림", $"{name} {place}");

            var textBox = anchor.Descendants().Any(d => d.LocalName == "cNvSpPr" && d.GetAttributes().Any(a => a.LocalName == "txBox" && a.Value == "1"));
            return new Feature(textBox ? "텍스트 상자" : "도형", $"{name} {place}");
        }

        private static string ChartKind(ChartPart chart)
        {
            var group = chart.ChartSpace?.Descendants().FirstOrDefault(d => d.LocalName is "barChart" or "lineChart" or "pieChart" or "doughnutChart" or "areaChart" or "scatterChart");
            return group?.LocalName switch
            {
                "barChart" => group.Descendants().FirstOrDefault(d => d.LocalName == "barDir")?.GetAttributes().FirstOrDefault(a => a.LocalName == "val").Value == "bar" ? "bar" : "column",
                "lineChart" => "line",
                "pieChart" => "pie",
                "doughnutChart" => "doughnut",
                "areaChart" => "area",
                "scatterChart" => "scatter",
                _ => "chart",
            };
        }

        private static string Shorten(string text)
        {
            var value = (text ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            return value.Length > MaxNoteLength ? value.Substring(0, MaxNoteLength) + "…" : value;
        }

        public sealed record Feature(string Kind, string Text);
    }
}
