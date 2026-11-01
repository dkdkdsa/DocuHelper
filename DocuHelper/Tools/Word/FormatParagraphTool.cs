using System.Collections.Generic;
using System.Linq;
using DocuHelper.Docus;
using DocuHelper.Importers.Hwp;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class FormatParagraphTool : WordTool
    {
        private const int DefaultTab = 720;

        public override string Name => "format_paragraph";

        public override string Description =>
            "Change how paragraphs look: style (Normal, Title, Heading 1-9, No Spacing, Quote, or any style in the document), alignment, indents, spacing, "
            + "line spacing, keep-with-next, outline level, borders and shading. bold, italic and underline still switch the look of all text in the paragraph. "
            + "Pass id or ids and only what should change. For a table id only bold, italic and underline work and they apply to every cell (use edit_table for cell formatting).";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "id": {"type": "string", "description": "Id of the paragraph or table, from read_paragraphs."},
                "ids": {"type": "array", "items": {"type": "string"}, "description": "Several paragraphs at once (use instead of id)."},
                "style": {"type": "string", "description": "body, heading1, heading2, bullet, number (as before) or any style name/id: Normal, Title, Subtitle, Heading 3, No Spacing, Quote, List Paragraph, 표준, 제목 1 ..."},
                "bold": {"type": "boolean"},
                "italic": {"type": "boolean"},
                "underline": {"type": "boolean"},
                "reset": {"type": "boolean", "description": "Remove direct paragraph formatting first (keeps list numbering)."},
                "align": {"type": "string", "enum": ["left", "center", "right", "justify", "distribute", "divide"]},
                "indent_left_cm": {"type": "number", "minimum": -10, "maximum": 30},
                "indent_right_cm": {"type": "number", "minimum": -10, "maximum": 30},
                "first_line_cm": {"type": "number", "minimum": 0, "maximum": 30, "description": "Extra indent of the first line."},
                "hanging_cm": {"type": "number", "minimum": 0, "maximum": 30, "description": "Indent of every line except the first."},
                "indent_step": {"type": "integer", "enum": [-1, 1], "description": "Move the left indent one default tab stop left or right."},
                "space_before_pt": {"type": "number", "minimum": 0, "maximum": 1584},
                "space_after_pt": {"type": "number", "minimum": 0, "maximum": 1584},
                "line_spacing": {
                  "type": "object",
                  "properties": {
                    "mode": {"type": "string", "enum": ["multiple", "exact", "at_least"]},
                    "value": {"type": "number", "description": "multiple: 1.0, 1.15, 1.5, 2.0 ...; exact and at_least: points."}
                  },
                  "required": ["mode", "value"]
                },
                "keep_with_next": {"type": "boolean"},
                "keep_lines": {"type": "boolean"},
                "page_break_before": {"type": "boolean"},
                "widow_control": {"type": "boolean"},
                "outline_level": {"type": "integer", "minimum": 0, "maximum": 9, "description": "0 = body text (not in the table of contents), 1-9 = heading level."},
                "shading": {"type": "string", "description": "Paragraph background #RRGGBB or \"none\"."},
                "borders": {
                  "type": "object",
                  "properties": {
                    "sides": {"type": "array", "items": {"type": "string", "enum": ["top", "bottom", "left", "right", "box", "none"]}},
                    "style": {"type": "string", "enum": ["single", "double", "dotted", "dashed", "thick"]},
                    "width_pt": {"type": "number", "minimum": 0.25, "maximum": 6},
                    "color": {"type": "string"},
                    "space_pt": {"type": "number", "minimum": 0, "maximum": 31}
                  }
                },
                "suppress_line_numbers": {"type": "boolean"}
              }
            }
            """;

        protected override WordHwpSupport Hwp => WordHwpSupport.Partial;

        protected override ToolResult Run(WordDocu docu, ToolInput input)
        {
            var bold = input.GetOptionalBool("bold");
            var italic = input.GetOptionalBool("italic");
            var underline = input.GetOptionalBool("underline");
            var spec = WordParagraphFormat.Parse(input);
            var styleText = input.Has("style") ? input.GetOptionalString("style") : null;
            if (bold == null && italic == null && underline == null && styleText == null && !spec.HasAny)
                throw new ToolInputException("바꿀 모양을 하나 이상 넣어 주세요. style, align, 들여쓰기, 간격, 줄 간격, bold, italic, underline 같은 값 중에서 골라 주세요.");

            CheckHwp(docu, spec);

            var scan = WordScan.Read(docu.Document);
            var targets = WordParagraphTargets.Resolve(scan, input);
            if ((styleText != null || spec.HasAny) && targets.Any(t => t.IsTable))
                throw new ToolInputException("표에는 굵게, 기울임, 밑줄만 바꿀 수 있어요. 표 안의 글은 문단 id로 고쳐 주세요.");

            var choice = styleText == null ? null : ResolveChoice(scan, styleText);
            var tab = docu.Document.MainDocumentPart.DocumentSettingsPart?.Settings?.GetFirstChild<DefaultTabStop>()?.Val?.Value ?? DefaultTab;

            foreach (var target in targets.Where(t => !t.IsTable))
            {
                var paragraph = target.Paragraph;
                var properties = paragraph.ParagraphProperties ??= new ParagraphProperties();
                if (spec.Reset)
                    WordParagraphFormat.Reset(properties);

                if (choice != null)
                    Apply(scan, target, choice);

                if (spec.HasAny)
                    WordParagraphFormat.Apply(paragraph.ParagraphProperties ??= new ParagraphProperties(), spec, WordInheritedParagraph.ForParagraph(scan, paragraph), true, tab);

                if (paragraph.ParagraphProperties != null && !paragraph.ParagraphProperties.HasChildren)
                    paragraph.ParagraphProperties = null;
            }

            var runs = targets.SelectMany(t => t.Paragraphs).Distinct().ToList();
            if (bold != null)
                WordStyler.SetFlag<Bold, BoldComplexScript>(scan, runs, bold.Value);

            if (italic != null)
                WordStyler.SetFlag<Italic, ItalicComplexScript>(scan, runs, italic.Value);

            if (underline != null)
                WordStyler.SetUnderline(scan, runs, underline.Value);

            var after = WordScan.Read(docu.Document);
            var location = WordScan.PageLabel(targets.Select(t => after.Find(t.Block.Id) ?? t.Block));
            var parts = Describe(bold, italic, underline, spec, choice);
            var anchors = targets.Select(t => t.Anchor).Distinct().ToList();
            var subject = targets.Count == 1 ? targets[0].Id : $"{targets.Count} paragraphs";
            return ToolResult.Changed(
                $"Formatted {subject}: {string.Join(", ", parts.Select(p => p.English))}.",
                Change(location, Icon(bold, italic, underline, spec, choice), anchors),
                string.Join(", ", parts.Select(p => p.Korean)));
        }

        private static void CheckHwp(WordDocu docu, WordParagraphSpec spec)
        {
            if (docu is not HwpDocu)
                return;

            if (spec.Right != null)
                HwpGuard.Require(docu, HwpFeature.ParaRightIndent);

            if (spec.KeepNext != null || spec.KeepLines != null)
                HwpGuard.Require(docu, HwpFeature.ParaKeep);

            if (spec.Align == JustificationValues.ThaiDistribute)
                HwpGuard.Require(docu, HwpFeature.ParaDivide);

            if (spec.Line != null && spec.LineRule != LineSpacingRuleValues.Auto)
                HwpGuard.Require(docu, HwpFeature.ParaFixedLine);

            if (spec.Shading != null || spec.Borders != null)
                HwpGuard.Require(docu, HwpFeature.ParaBorder);

            RefuseInHwp(docu, "문단 앞 쪽 나눔", spec.PageBreakBefore != null);
            RefuseInHwp(docu, "줄 번호 제외", spec.SuppressLineNumbers != null);
            RefuseInHwp(docu, "개요 수준", spec.OutlineLevel != null);
        }

        private static StyleChoice ResolveChoice(WordScan scan, string value)
        {
            if (WordStyles.Normalize(value) != "normal" && TryParseStyle(value, out var kind))
                return new StyleChoice(kind, null, null);

            var builtin = WordBuiltinStyles.Find(value);
            var existing = scan.Styles.Find(value.Trim()) ?? scan.Styles.FindByName(value);
            if (existing == null && builtin != null && builtin.Type != StyleValues.Paragraph)
                throw new ToolInputException($"‘{value}’은(는) 글자 스타일이라 문단에 쓸 수 없어요. 글자 모양은 format_text로 바꿔 주세요.");

            var id = WordBuiltinStyles.Ensure(scan.Styles, value);
            var style = scan.Styles.Find(id);
            if (style.Type?.Value != StyleValues.Paragraph)
                throw new ToolInputException($"‘{value}’은(는) 문단 스타일이 아니에요. 문단 스타일 이름을 넣어 주세요.");

            var name = WordStyles.NameOf(style);
            var display = WordBuiltinStyles.Find(name)?.Korean ?? style.StyleName?.Val?.Value ?? id;
            if (id == scan.Styles.NormalId)
                return new StyleChoice(WordKind.Body, id, display);

            var mapped = name switch
            {
                "title" or "제목" => WordKind.Title,
                "heading 1" or "제목 1" => WordKind.Heading1,
                "heading 2" or "제목 2" => WordKind.Heading2,
                _ => (WordKind?)null,
            };

            return new StyleChoice(mapped, id, display);
        }

        private static void Apply(WordScan scan, WordParagraphTarget target, StyleChoice choice)
        {
            var paragraph = target.Paragraph;
            if (choice.Kind != null)
            {
                var before = target.InCell ? null : scan.Before(target.Block);
                var after = target.InCell ? null : scan.After(target.Block);
                WordStyler.ApplyKind(scan, paragraph, before, after, choice.Kind.Value);
                paragraph.ParagraphProperties ??= new ParagraphProperties();
                if (choice.StyleId == scan.Styles.NormalId)
                    paragraph.ParagraphProperties.ParagraphStyleId = null;

                return;
            }

            var properties = paragraph.ParagraphProperties ??= new ParagraphProperties();
            if (scan.Styles.IsHeadingStyle(choice.StyleId))
            {
                properties.NumberingProperties = null;
                properties.RemoveAllChildren<OutlineLevel>();
            }

            properties.ParagraphStyleId = new ParagraphStyleId { Val = choice.StyleId };
        }

        private static string Icon(bool? bold, bool? italic, bool? underline, WordParagraphSpec spec, StyleChoice choice)
        {
            if (choice != null)
            {
                return choice.Kind switch
                {
                    WordKind.Bullet => "list",
                    WordKind.Number => "list-ordered",
                    _ => "pencil",
                };
            }

            if (spec.Align != null || spec.HasIndent)
                return WordParagraphFormat.Icon(spec);

            if (bold != null)
                return "bold";

            if (italic != null)
                return "italic";

            return underline != null ? "underline" : "pencil";
        }

        private static List<(string English, string Korean)> Describe(bool? bold, bool? italic, bool? underline, WordParagraphSpec spec, StyleChoice choice)
        {
            var parts = new List<(string English, string Korean)>();
            if (choice != null)
            {
                if (choice.Kind != null)
                {
                    parts.Add(($"style {KindName(choice.Kind.Value)}", choice.Kind.Value switch
                    {
                        WordKind.Title => "제목",
                        WordKind.Heading1 => "제목 1",
                        WordKind.Heading2 => "제목 2",
                        WordKind.Bullet => "글머리 기호",
                        WordKind.Number => "번호 매기기",
                        _ => "본문",
                    }));
                }
                else
                {
                    parts.Add(($"style {choice.Display}", choice.Display));
                }
            }

            parts.AddRange(WordParagraphFormat.Describe(spec));

            if (bold != null)
                parts.Add(bold.Value ? ("bold on", "굵게") : ("bold off", "굵게 끔"));

            if (italic != null)
                parts.Add(italic.Value ? ("italic on", "기울임") : ("italic off", "기울임 끔"));

            if (underline != null)
                parts.Add(underline.Value ? ("underline on", "밑줄") : ("underline off", "밑줄 끔"));

            return parts;
        }

        private sealed record StyleChoice(WordKind? Kind, string StyleId, string Display);
    }
}
