using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class FormatTextTool : WordTool
    {
        private const int MaxTargets = 500;

        private static readonly string s_schema = """
            {
              "type": "object",
              "properties": {
                "targets": {
                  "type": "array",
                  "minItems": 1,
                  "description": "Where to apply: paragraph or table ids from read_paragraphs, optionally narrowed by start/end (0-based character offsets as read_paragraphs counts them, end exclusive) or match (text to find; occurrence N = Nth hit, 0 = all). Without them the whole paragraph is used. For a table id, row and column (1-based) pick one cell.",
                  "items": {
                    "type": "object",
                    "properties": {
                      "id": {"type": "string"},
                      "start": {"type": "integer", "minimum": 0},
                      "end": {"type": "integer", "minimum": 0},
                      "match": {"type": "string"},
                      "occurrence": {"type": "integer", "minimum": 0},
                      "row": {"type": "integer", "minimum": 1},
                      "column": {"type": "integer", "minimum": 1}
                    },
                    "required": ["id"]
                  }
                },
                "size_step": {"type": "string", "enum": ["grow", "shrink"], "description": "Next larger/smaller standard size (8 9 10 10.5 11 12 14 16 18 20 22 24 26 28 36 48 72)."},
                "case": {"type": "string", "enum": ["upper", "lower", "sentence", "title", "toggle"], "description": "Changes the letters themselves (Latin letters only)."},
                "clear_formatting": {"type": "boolean", "description": "First remove all direct character formatting from the targets."},
                @FORMAT@
              },
              "required": ["targets"]
            }
            """.Replace("@FORMAT@", WordRunFormat.PropertiesSchema);

        public override string Name => "format_text";

        public override string Description =>
            "Change the look of characters inside paragraphs or table cells: font, size, bold, italic, underline, strikethrough, text color, highlight, "
            + "superscript/subscript, small caps/all caps, letter case and character spacing. targets says where; pass only what should change. "
            + "Offsets count the characters read_paragraphs shows. To restyle a whole paragraph block (alignment, spacing, headings, lists) use format_paragraph instead.";

        public override string InputSchema => s_schema;

        protected override WordHwpSupport Hwp => WordHwpSupport.Partial;

        protected override ToolResult Run(WordDocu docu, ToolInput input)
        {
            var format = WordRunFormat.Parse(input.Root, null, true);
            if (format.IsEmpty)
                throw new ToolInputException("바꿀 모양을 하나 이상 넣어 주세요. font, size, bold, italic, underline, strike, color, highlight, vertical_align, caps, spacing_pt, size_step, case, clear_formatting 중에서 골라 주세요.");

            RefuseFormatInHwp(docu, format);

            var targets = input.Get("targets");
            if (targets.ValueKind == JsonValueKind.Array && targets.GetArrayLength() > MaxTargets)
                throw new ToolInputException($"한 번에 {MaxTargets}곳까지만 고를 수 있어요. 나누어서 요청해 주세요.");

            var scan = WordScan.Read(docu.Document);
            var ranges = Targets(scan, input);
            if (ranges.Any(r => r.IsEmpty && r.End > 0))
                throw new ToolInputException("서식을 바꿀 글자를 골라 주세요. start와 end가 같으면 바꿀 글자가 없어요.");

            var transaction = WordTransaction.Begin(new OpenXmlElement[] { scan.Body });
            var count = 0;
            try
            {
                var order = new List<Paragraph>();
                var groups = new Dictionary<Paragraph, List<WordRange>>();
                foreach (var range in ranges)
                {
                    if (!groups.TryGetValue(range.Paragraph, out var list))
                    {
                        groups[range.Paragraph] = list = new List<WordRange>();
                        order.Add(range.Paragraph);
                    }

                    list.Add(range);
                }

                foreach (var paragraph in order)
                    count += Format(scan, paragraph, groups[paragraph], format);
            }
            catch (Exception)
            {
                transaction.Rollback();
                throw;
            }

            var after = WordScan.Read(docu.Document);
            var blocks = ranges.Select(r => after.Find(r.Block.Id)).Where(b => b != null).Distinct().ToList();
            var paragraphs = ranges.Select(r => r.Paragraph).Distinct().Count();
            var parts = format.Describe();
            var content = $"Formatted {count} range(s) in {paragraphs} paragraph(s): {string.Join(", ", parts.Select(p => p.English))}.";
            if (WordRevisions.Active(docu) != null)
                content += " Formatting is applied directly and is not recorded as a tracked change.";

            return ToolResult.Changed(
                content,
                Change(WordScan.PageLabel(blocks), format.Icon(), blocks.Select(b => b.Anchor)),
                string.Join(", ", parts.Select(p => p.Korean)));
        }

        private static int Format(WordScan scan, Paragraph paragraph, List<WordRange> ranges, WordRunFormat format)
        {
            var map = WordTextMap.Build(paragraph);
            var count = 0;
            var whole = false;
            foreach (var range in ranges)
            {
                if (range.End > range.Start)
                {
                    if (format.ChangesProperties)
                    {
                        var runs = map.Isolate(range.Start, range.End).Concat(WordTextEditor.CoveredFieldRuns(map, range.Start, range.End)).Distinct().ToList();
                        foreach (var run in runs)
                        {
                            var props = run.RunProperties ?? (run.RunProperties = new RunProperties());
                            format.Apply(props, scan.Styles, paragraph);
                            if (!props.HasChildren)
                                run.RunProperties = null;
                        }
                    }

                    if (format.Case != null)
                        ApplyCase(map, range.Start, range.End, format.Case.Value);

                    count++;
                }
                else if (map.Length == 0)
                {
                    count++;
                }

                if (range.Start == 0 && range.End == map.Length)
                    whole = true;
            }

            if (whole && format.ChangesProperties)
            {
                var properties = paragraph.ParagraphProperties ?? (paragraph.ParagraphProperties = new ParagraphProperties());
                properties.ParagraphMarkRunProperties = WordRunFormat.ApplyToMark(properties.ParagraphMarkRunProperties, format, scan.Styles, paragraph);
                if (!properties.HasChildren)
                    paragraph.ParagraphProperties = null;
            }

            map.Normalize();
            WordIds.Renew(paragraph);
            return count;
        }

        private static void ApplyCase(WordTextMap map, int start, int end, WordCaseMode mode)
        {
            var original = map.Text.Substring(start, end - start);
            var changed = WordCase.Apply(original, mode);
            if (string.Equals(original, changed, StringComparison.Ordinal))
                return;

            foreach (var segment in map.Segments.Where(s => s.Text != null && s.End > start && s.Start < end))
            {
                var from = Math.Max(start, segment.Start);
                var to = Math.Min(end, segment.End);
                var value = segment.Text.Text;
                segment.Text.Text = value.Substring(0, from - segment.Start) + changed.Substring(from - start, to - from) + value.Substring(to - segment.Start);
            }
        }
    }
}
