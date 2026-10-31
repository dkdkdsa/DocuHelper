using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DocuHelper.Docus;
using DocuHelper.Importers.Hwp;
using DocuHelper.Tools.Word;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Hwp
{
    public class SetPageControlTool : WordTool
    {
        private static readonly string[] s_actions = { "hide_page_items", "show_page_items", "restart_page_number", "clear_page_number" };
        private static readonly string[] s_defaultItems = { "header", "footer", "page_number" };

        public override string Name => "set_page_control";

        public override string Description =>
            "Hancom 한글 files only: hide the header, footer, page number, border or background of one page, or restart page numbering at a paragraph. "
            + "The page is the one where the paragraph starts. Use set_header_footer for the header, footer and page number themselves.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "action": {"type": "string", "enum": ["hide_page_items", "show_page_items", "restart_page_number", "clear_page_number"]},
                "at": {"description": "Paragraph id, or {id}. The page where this paragraph starts.", "oneOf": [{"type": "string"}, {"type": "object", "properties": {"id": {"type": "string"}}, "required": ["id"]}]},
                "items": {"type": "array", "items": {"type": "string", "enum": ["header", "footer", "page_number", "border", "background"]}, "description": "hide_page_items / show_page_items. Default header, footer, page_number."},
                "start_at": {"type": "integer", "minimum": 0, "maximum": 99999, "description": "restart_page_number: the number that page gets. Default 1."}
              },
              "required": ["action", "at"]
            }
            """;

        protected override ToolResult Run(WordDocu docu, ToolInput input)
        {
            if (docu is not HwpDocu)
                throw new ToolInputException("쪽 제어는 한글 파일에서만 쓸 수 있어요.");

            HwpGuard.Require(docu, HwpFeature.PageControl, "쪽 감추기·새 번호");
            var action = input.GetOptionalString("action") ?? string.Empty;
            if (!s_actions.Contains(action))
                throw new ToolInputException($"'action' 값은 {string.Join(", ", s_actions)} 중 하나여야 해요.");

            if (!input.Has("at"))
                throw new ToolInputException("'at'에 쪽을 정할 문단 id를 넣어 주세요.");

            var snapshot = docu.Capture();
            try
            {
                var scan = WordScan.Read(docu.Document);
                var block = Target(scan, input.Get("at"));
                return action switch
                {
                    "hide_page_items" => Hide(docu, scan, block, Items(input)),
                    "show_page_items" => Show(scan, block, Items(input)),
                    "restart_page_number" => Restart(docu, scan, block, input.GetOptionalInt("start_at") ?? 1),
                    _ => Clear(scan, block),
                };
            }
            catch
            {
                docu.Restore(snapshot);
                throw;
            }
        }

        private static WordBlock Target(WordScan scan, JsonElement at)
        {
            var id = at.ValueKind == JsonValueKind.String ? at.GetString() : at.ValueKind == JsonValueKind.Object && at.TryGetProperty("id", out var value) ? value.GetString() : null;
            var block = id == null ? null : scan.Find(id);
            if (block == null)
                throw NotFound(id ?? string.Empty);

            if (block.IsTable || block.Paragraph == null)
                throw new ToolInputException("표 앞이나 뒤의 문단 id를 넣어 주세요.");

            return block;
        }

        private static IReadOnlyList<char> Items(ToolInput input)
        {
            var names = input.Has("items") ? input.GetStringList("items", false) : s_defaultItems;
            return names.Select(n => HwpPageControls.Letters[n]).Distinct().ToList();
        }

        private static ToolResult Hide(WordDocu docu, WordScan scan, WordBlock block, IReadOnlyList<char> letters)
        {
            var page = block.Page;
            var existing = HwpPageControls.Marks(scan, page, HwpPageControls.HidePrefix).ToList();
            var flags = HwpPageControls.Flags(existing.SelectMany(e => HwpPageControls.FlagsOf(e.Start)).Concat(letters));
            if (existing.Count > 0)
            {
                SetHide(existing, flags);
            }
            else
            {
                HwpPageControls.Add(block.Paragraph, HwpPageControls.HidePrefix + flags, WordParts.NextBookmarkId(docu));
            }

            var words = HwpPageControls.Words(HwpPageControls.Flags(letters));
            return Done(block, "eye-off", $"Hid {words} on page {page}.", $"{page}쪽 {words} 감춤");
        }

        private static ToolResult Show(WordScan scan, WordBlock block, IReadOnlyList<char> letters)
        {
            var page = block.Page;
            var existing = HwpPageControls.Marks(scan, page, HwpPageControls.HidePrefix).ToList();
            if (existing.Count == 0)
                throw new ToolInputException($"{page}쪽은 감춘 항목이 없어요.");

            var remaining = HwpPageControls.Flags(existing.SelectMany(e => HwpPageControls.FlagsOf(e.Start)).Where(c => !letters.Contains(c)));
            SetHide(existing, remaining);
            var words = HwpPageControls.Words(HwpPageControls.Flags(letters));
            return Done(block, "eye", $"Showed {words} on page {page}.", $"{page}쪽 {words} 다시 보임");
        }

        private static ToolResult Restart(WordDocu docu, WordScan scan, WordBlock block, int number)
        {
            var page = block.Page;
            if (page == 1 && number == StartOf(docu))
                throw new ToolInputException($"문서가 이미 {number}번부터 시작해요.");

            foreach (var mark in HwpPageControls.Marks(scan, page, HwpPageControls.NewNumberPrefix).ToList())
                HwpPageControls.Remove(mark.Start);

            HwpPageControls.Add(block.Paragraph, HwpPageControls.NewNumberPrefix + number.ToString(System.Globalization.CultureInfo.InvariantCulture), WordParts.NextBookmarkId(docu));
            return Done(block, "refresh", $"Restarted page numbering at {number} on page {page}.", $"{page}쪽부터 번호를 {number}로 다시 시작");
        }

        private static ToolResult Clear(WordScan scan, WordBlock block)
        {
            var page = block.Page;
            var marks = HwpPageControls.Marks(scan, page, HwpPageControls.NewNumberPrefix).ToList();
            if (marks.Count == 0)
                throw new ToolInputException($"{page}쪽에는 새 번호가 없어요.");

            foreach (var mark in marks)
                HwpPageControls.Remove(mark.Start);

            return Done(block, "refresh", $"Cleared the restarted numbering on page {page}.", $"{page}쪽 새 번호 지움");
        }

        private static void SetHide(IReadOnlyList<(WordBlock Block, BookmarkStart Start)> existing, string flags)
        {
            var first = existing[0].Start;
            foreach (var extra in existing.Skip(1))
                HwpPageControls.Remove(extra.Start);

            if (flags.Length == 0)
            {
                HwpPageControls.Remove(first);
                return;
            }

            first.Name = HwpPageControls.HidePrefix + flags;
        }

        private static int StartOf(WordDocu docu)
        {
            var start = docu.Document.MainDocumentPart.Document.Body.Elements<SectionProperties>().LastOrDefault()?.GetFirstChild<PageNumberType>()?.Start?.Value;
            return start is int value && value > 0 ? value : 1;
        }

        private static ToolResult Done(WordBlock block, string icon, string english, string detail)
        {
            var anchors = new List<string> { WordAnchors.Paragraph(block.Id) };
            return ToolResult.Changed(english, Change($"{block.Page}쪽", icon, anchors), detail);
        }
    }
}
