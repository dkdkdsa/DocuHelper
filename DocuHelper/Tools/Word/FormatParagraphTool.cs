using System.Collections.Generic;
using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class FormatParagraphTool : WordTool
    {
        public override string Name => "format_paragraph";

        public override string Description =>
            "Change how one paragraph looks. bold, italic and underline switch that look on or off for all of its text; "
            + "align is left or center; style turns it into body text, heading1, heading2, a bullet item or a numbered item. "
            + "Pass only what should change. For a table id only bold, italic and underline work, and they apply to every cell.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "id": {"type": "string", "description": "Id of the paragraph or table, from read_paragraphs."},
                "bold": {"type": "boolean"},
                "italic": {"type": "boolean"},
                "underline": {"type": "boolean"},
                "align": {"type": "string", "enum": ["left", "center"]},
                "style": {"type": "string", "enum": ["body", "heading1", "heading2", "bullet", "number"]}
              },
              "required": ["id"]
            }
            """;

        protected override ToolResult Run(WordDocu docu, ToolInput input)
        {
            var id = input.GetString("id");
            var bold = input.GetOptionalBool("bold");
            var italic = input.GetOptionalBool("italic");
            var underline = input.GetOptionalBool("underline");
            var align = ParseAlign(input.GetOptionalString("align"));
            WordKind? style = input.Has("style") ? ParseStyle(input.GetOptionalString("style"), "style") : null;
            if (bold == null && italic == null && underline == null && align == null && style == null)
                throw new ToolInputException("바꿀 모양을 하나 이상 넣어 주세요. bold, italic, underline, align, style 중에서 골라 주세요.");

            var scan = WordScan.Read(docu.Document);
            var block = scan.Find(id) ?? throw NotFound(id);
            if (block.IsTable && (align != null || style != null))
                throw new ToolInputException("표에는 굵게, 기울임, 밑줄만 바꿀 수 있어요.");

            if (style != null && style.Value != block.Kind)
                WordStyler.ApplyKind(scan, block, style.Value);

            if (align != null)
                WordStyler.SetAlignment(scan, block.Paragraph, align.Value);

            var paragraphs = block.IsTable ? block.Table.Descendants<Paragraph>().ToList() : new List<Paragraph> { block.Paragraph };
            if (bold != null)
                WordStyler.SetFlag<Bold, BoldComplexScript>(scan, paragraphs, bold.Value);

            if (italic != null)
                WordStyler.SetFlag<Italic, ItalicComplexScript>(scan, paragraphs, italic.Value);

            if (underline != null)
                WordStyler.SetUnderline(scan, paragraphs, underline.Value);

            var updated = WordScan.Read(docu.Document).Find(block.Id) ?? block;
            var location = WordScan.PageLabel(new[] { updated });
            var parts = Describe(bold, italic, underline, align, style);
            return ToolResult.Changed(
                $"Formatted {block.Id}: {string.Join(", ", parts.Select(p => p.English))}.",
                Change(location, Icon(bold, italic, underline, align, style), new[] { block.Anchor }),
                string.Join(", ", parts.Select(p => p.Korean)));
        }

        private static bool? ParseAlign(string value)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "":
                    return null;
                case "left":
                case "start":
                    return false;
                case "center":
                case "centre":
                case "middle":
                    return true;
                default:
                    throw new ToolInputException("'align' 값은 left 또는 center여야 해요.");
            }
        }

        private static string Icon(bool? bold, bool? italic, bool? underline, bool? align, WordKind? style)
        {
            if (style != null)
            {
                return style.Value switch
                {
                    WordKind.Bullet => "list",
                    WordKind.Number => "list-ordered",
                    _ => "pencil",
                };
            }

            if (align != null)
                return align.Value ? "align-center" : "align-left";

            if (bold != null)
                return "bold";

            return italic != null ? "italic" : underline != null ? "underline" : "pencil";
        }

        private static List<(string English, string Korean)> Describe(bool? bold, bool? italic, bool? underline, bool? align, WordKind? style)
        {
            var parts = new List<(string English, string Korean)>();
            if (style != null)
            {
                parts.Add(($"style {KindName(style.Value)}", style.Value switch
                {
                    WordKind.Title => "제목",
                    WordKind.Heading1 => "제목 1",
                    WordKind.Heading2 => "제목 2",
                    WordKind.Bullet => "글머리 기호",
                    WordKind.Number => "번호 매기기",
                    _ => "본문",
                }));
            }

            if (align != null)
                parts.Add(align.Value ? ("centered", "가운데") : ("left aligned", "왼쪽"));

            if (bold != null)
                parts.Add(bold.Value ? ("bold on", "굵게") : ("bold off", "굵게 끔"));

            if (italic != null)
                parts.Add(italic.Value ? ("italic on", "기울임") : ("italic off", "기울임 끔"));

            if (underline != null)
                parts.Add(underline.Value ? ("underline on", "밑줄") : ("underline off", "밑줄 끔"));

            return parts;
        }
    }
}
