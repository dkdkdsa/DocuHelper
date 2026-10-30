using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Ppt
{
    public class ReadSlidesTool : Tool
    {
        private const int DefaultCount = 10;
        private const int MaxCount = 30;
        private const int MaxLength = 8000;
        private const int MaxPoints = 24;
        private const int MaxSeries = 4;

        public override string Name => "read_slides";

        public override string Description =>
            "Read the slides of a PowerPoint file. For each slide: its number, layout, the text shapes with their shape id in brackets, "
            + "role (title, subtitle, body, text box), font size and paragraphs (indentation shows the bullet level), the speaker notes, "
            + "and chart data. Pass the shape id or title/subtitle/body as `shape` to the editing tools. "
            + "Returns up to 10 slides per call; use start and count to read more.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "start": {"type": "integer", "minimum": 1, "description": "First slide number to read (1-based). Default 1."},
                "count": {"type": "integer", "minimum": 1, "maximum": 30, "description": "How many slides to read. Default 10."}
              }
            }
            """;

        public override bool IsReadOnly => true;

        public override ToolResult Execute(Docu docu, ToolInput input)
        {
            var deck = PptReader.Read(PptSlides.DocumentOf(docu));
            var total = deck.Slides.Count;
            if (total == 0)
                return ToolResult.Ok($"\"{docu.Name}\" has no slides yet. Use add_slide to add one.", "슬라이드 0장");

            var start = input.GetOptionalInt("start") ?? 1;
            var count = input.GetOptionalInt("count") ?? DefaultCount;
            if (start < 1 || start > total)
                throw new ToolInputException($"'start' 값은 1부터 {total} 사이여야 해요.");

            if (count < 1)
                throw new ToolInputException("'count' 값은 1 이상이어야 해요.");

            var end = Math.Min(total, start + Math.Min(count, MaxCount) - 1);
            var blocks = new List<string>();
            var length = 0;
            for (var number = start; number <= end; number++)
            {
                var block = Describe(deck.Slides[number - 1]);
                if (blocks.Count > 0 && length + block.Length > MaxLength)
                    break;

                if (block.Length > MaxLength)
                    block = block.Substring(0, MaxLength) + Environment.NewLine + "… (the rest of this slide was cut)" + Environment.NewLine;

                blocks.Add(block);
                length += block.Length;
            }

            var last = start + blocks.Count - 1;
            var text = new StringBuilder();
            text.Append(CultureInfo.InvariantCulture, $"\"{docu.Name}\": {total} slide{PptOutput.Plural(total)}, {deck.AspectText}. ");
            text.AppendLine(start == last ? $"Slide {start} below." : $"Slides {start}-{last} below.");
            foreach (var block in blocks)
                text.Append(block);

            if (last < total)
                text.AppendLine($"More slides follow: call read_slides with start={last + 1}.");

            return ToolResult.Ok(text.ToString().TrimEnd(), $"슬라이드 {blocks.Count}장");
        }

        private static string Describe(PptSlideInfo slide)
        {
            var text = new StringBuilder();
            text.Append($"## Slide {slide.Number}");
            if (slide.IsHidden)
                text.Append(" (hidden)");

            if (!string.IsNullOrWhiteSpace(slide.LayoutName))
                text.Append($" · layout \"{slide.LayoutName}\"");

            text.AppendLine();
            foreach (var shape in slide.Shapes)
                AppendShape(text, shape);

            AppendNotes(text, slide);
            return text.ToString();
        }

        private static void AppendShape(StringBuilder text, PptShapeInfo shape)
        {
            var id = shape.ShapeId.ToString(CultureInfo.InvariantCulture);
            switch (shape.Role)
            {
                case PptShapeRole.Other:
                    return;
                case PptShapeRole.Picture:
                    text.AppendLine(string.IsNullOrWhiteSpace(shape.Detail) ? $"[{id}] picture" : $"[{id}] picture \"{PptOutput.Shorten(shape.Detail, 80)}\"");
                    return;
                case PptShapeRole.Table:
                    text.AppendLine($"[{id}] table {shape.Detail} (read only)");
                    return;
                case PptShapeRole.Chart:
                    AppendChart(text, id, shape.Chart);
                    return;
                case PptShapeRole.Footer when !shape.HasText:
                    return;
            }

            var label = PptOutput.ShapeLabel(shape);
            var first = shape.First;
            var style = first == null ? string.Empty : ", " + Style(first);
            var paragraphs = shape.Paragraphs.ToList();
            while (paragraphs.Count > 0 && paragraphs[^1].IsEmpty)
                paragraphs.RemoveAt(paragraphs.Count - 1);

            if (paragraphs.Count == 0)
            {
                text.AppendLine($"{label}{style}: (empty)");
                return;
            }

            if (paragraphs.Count == 1 && paragraphs[0].Level == 0)
            {
                text.AppendLine($"{label}{style}: {PptOutput.Inline(paragraphs[0].Text)}{SizeNote(paragraphs[0], first)}");
                return;
            }

            text.AppendLine($"{label}{style}:");
            foreach (var paragraph in paragraphs)
            {
                var line = paragraph.IsEmpty ? "(blank line)" : PptOutput.Inline(paragraph.Text);
                text.AppendLine($"{new string(' ', 2 + paragraph.Level * 2)}- {line}{SizeNote(paragraph, first)}");
            }
        }

        private static string Style(PptParagraphInfo paragraph)
        {
            var style = PptOutput.Points(paragraph.FontSize);
            if (paragraph.Bold)
                style += " bold";

            if (paragraph.Italic)
                style += " italic";

            if (paragraph.Underline)
                style += " underline";

            if (paragraph.Align == "ctr")
                style += " centered";
            else if (paragraph.Align == "r")
                style += " right-aligned";

            return style;
        }

        private static string SizeNote(PptParagraphInfo paragraph, PptParagraphInfo first) =>
            first != null && !paragraph.IsEmpty && Math.Abs(paragraph.FontSize - first.FontSize) >= 0.5
                ? $" ({PptOutput.Points(paragraph.FontSize)})"
                : string.Empty;

        private static void AppendChart(StringBuilder text, string id, PptChartInfo chart)
        {
            if (chart == null || chart.Series.Count == 0)
            {
                text.AppendLine($"[{id}] chart (data could not be read, read only)");
                return;
            }

            var title = string.IsNullOrWhiteSpace(chart.Title) ? string.Empty : $" \"{PptOutput.Shorten(chart.Title, 60)}\"";
            var header = $"[{id}] chart ({chart.Type}){title}, read only.";
            var categories = chart.Categories.Take(MaxPoints).Select(c => PptOutput.Shorten(c, 20)).ToList();
            var more = chart.Categories.Count > MaxPoints ? " | …" : string.Empty;

            if (chart.Series.Count == 1)
            {
                var series = chart.Series[0];
                var pairs = Enumerable.Range(0, Math.Min(MaxPoints, Math.Max(categories.Count, series.Values.Count)))
                    .Select(i => $"{(i < categories.Count ? categories[i] : (i + 1).ToString(CultureInfo.InvariantCulture))} {Value(series, i)}");
                var name = string.IsNullOrWhiteSpace(series.Name) ? "Values" : $"Series \"{PptOutput.Shorten(series.Name, 40)}\"";
                text.AppendLine($"{header} {name}: {string.Join(" | ", pairs)}{more}");
                return;
            }

            text.AppendLine($"{header} Categories: {string.Join(" | ", categories)}{more}");
            foreach (var series in chart.Series.Take(MaxSeries))
            {
                var values = Enumerable.Range(0, Math.Min(MaxPoints, series.Values.Count)).Select(i => Value(series, i));
                text.AppendLine($"  series \"{PptOutput.Shorten(series.Name ?? "?", 40)}\": {string.Join(" | ", values)}");
            }

            if (chart.Series.Count > MaxSeries)
                text.AppendLine($"  … {chart.Series.Count - MaxSeries} more series");
        }

        private static string Value(PptSeriesInfo series, int index)
        {
            var value = index < series.Values.Count ? series.Values[index] : null;
            return value == null ? "-" : PptOutput.Number(value.Value);
        }

        private static void AppendNotes(StringBuilder text, PptSlideInfo slide)
        {
            if (!slide.HasNotes)
            {
                text.AppendLine("notes: (none)");
                return;
            }

            if (slide.Notes.Count == 1)
            {
                text.AppendLine($"notes: {PptOutput.Inline(slide.Notes[0])}");
                return;
            }

            text.AppendLine("notes:");
            foreach (var line in slide.Notes)
                text.AppendLine("  " + PptOutput.Inline(line));
        }
    }
}
