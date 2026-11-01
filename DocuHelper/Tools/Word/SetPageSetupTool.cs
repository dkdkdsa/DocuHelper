using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class SetPageSetupTool : WordTool
    {
        public override string Name => "set_page_setup";

        public override string Description =>
            "Change the page setup: paper size, orientation, margins, columns, line numbers, vertical alignment of the text on the page, page borders and page color. "
            + "By default it applies to the whole document; use scope for one section or from a paragraph onward (from_here starts a new section there).";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "scope": {"type": "string", "enum": ["document", "section", "from_here"], "description": "document: every section (default); section: the section that contains `at`; from_here: start a new section at `at` (continuous, or next page when the paper changes) and apply only to it and everything after."},
                "at": {"type": "string", "description": "Paragraph or table id that picks the section (scope section) or where a new section starts (scope from_here)."},
                "size": {"type": "string", "enum": ["A3", "A4", "A5", "B4", "B5", "Letter", "Legal", "Tabloid", "Executive", "custom"], "description": "B4/B5 are the JIS sizes (25.7x36.4 cm, 18.2x25.7 cm). custom needs width_cm and height_cm."},
                "width_cm": {"type": "number", "minimum": 2, "maximum": 55},
                "height_cm": {"type": "number", "minimum": 2, "maximum": 55},
                "orientation": {"type": "string", "enum": ["portrait", "landscape"], "description": "Landscape swaps width and height."},
                "margins": {
                  "description": "Preset or custom values in cm.",
                  "oneOf": [
                    {"type": "string", "enum": ["normal", "narrow", "moderate", "wide", "mirrored", "official"]},
                    {"type": "object", "properties": {"top_cm": {"type": "number"}, "bottom_cm": {"type": "number"}, "left_cm": {"type": "number"}, "right_cm": {"type": "number"}, "gutter_cm": {"type": "number"}, "header_cm": {"type": "number"}, "footer_cm": {"type": "number"}}}
                  ]
                },
                "mirror_margins": {"type": "boolean", "description": "Document-wide; left becomes inside and right becomes outside."},
                "columns": {
                  "type": "object",
                  "properties": {
                    "preset": {"type": "string", "enum": ["one", "two", "three", "left", "right"]},
                    "count": {"type": "integer", "minimum": 1, "maximum": 6},
                    "equal": {"type": "boolean"},
                    "space_cm": {"type": "number", "minimum": 0, "maximum": 5},
                    "line_between": {"type": "boolean"},
                    "widths_cm": {"type": "array", "items": {"type": "number"}}
                  }
                },
                "line_numbers": {
                  "type": "object",
                  "properties": {
                    "mode": {"type": "string", "enum": ["none", "continuous", "restart_page", "restart_section"]},
                    "count_by": {"type": "integer", "minimum": 1},
                    "distance_cm": {"type": "number", "minimum": 0}
                  },
                  "required": ["mode"]
                },
                "vertical_alignment": {"type": "string", "enum": ["top", "center", "justified", "bottom"]},
                "page_borders": {
                  "type": "object",
                  "properties": {
                    "style": {"type": "string", "enum": ["none", "box", "shadow", "3d"]},
                    "line": {"type": "string", "enum": ["single", "double", "dashed", "dotted", "thick"]},
                    "width_pt": {"type": "number", "minimum": 0.25, "maximum": 6},
                    "color": {"type": "string"},
                    "offset_from": {"type": "string", "enum": ["text", "page"]},
                    "sides": {"type": "array", "items": {"type": "string", "enum": ["top", "bottom", "left", "right"]}}
                  }
                },
                "page_color": {"type": "string", "description": "Page background #RRGGBB, or \"none\"."}
              }
            }
            """;

        protected override WordHwpSupport Hwp => WordHwpSupport.Partial;

        protected override ToolResult Run(WordDocu docu, ToolInput input)
        {
            var scope = (input.GetOptionalString("scope") ?? "document").Trim().ToLowerInvariant();
            if (scope is not ("document" or "section" or "from_here"))
                throw new ToolInputException("'scope' 값은 document, section, from_here 중 하나여야 해요.");

            var setup = WordPageSetup.Parse(input);
            if (setup.IsEmpty)
                throw new ToolInputException("바꿀 쪽 설정을 하나 이상 넣어 주세요. 예: size, orientation, margins, columns.");

            RefuseInHwp(docu, "줄 번호", setup.LineNumbers != null);
            RefuseInHwp(docu, "위아래 맞춤", setup.VerticalAlignment != null);
            RefuseInHwp(docu, "마주 보는 여백", setup.Mirror == true || setup.MarginPreset == "mirrored");

            var at = input.GetOptionalString("at");
            var snapshot = docu.Capture();
            try
            {
                return Apply(docu, setup, scope, at);
            }
            catch
            {
                docu.Restore(snapshot);
                throw;
            }
        }

        private static ToolResult Apply(WordDocu docu, WordPageSetup setup, string scope, string at)
        {
            var scan = WordScan.Read(docu.Document);
            var body = scan.Body;
            var targets = new List<SectionProperties>();
            Paragraph carrier = null;
            WordBlock block = null;
            switch (scope)
            {
                case "document":
                    targets.AddRange(WordSections.All(body));
                    break;
                case "section":
                    var all = WordSections.All(body);
                    if (string.IsNullOrWhiteSpace(at))
                    {
                        if (all.Count != 1)
                            throw new ToolInputException("문서에 구역이 여러 개예요. 'at'에 고칠 구역 안의 문단 id를 넣어 주세요.");

                        targets.Add(all[0]);
                    }
                    else
                    {
                        block = scan.Find(at) ?? throw NotFound(at);
                        targets.Add(WordSections.Containing(body, block.Element));
                    }

                    break;
                default:
                    if (string.IsNullOrWhiteSpace(at))
                        throw new ToolInputException("scope가 from_here이면 'at'에 새 구역이 시작할 문단 id를 넣어 주세요.");

                    block = scan.Find(at) ?? throw NotFound(at);
                    var previous = scan.Before(block);
                    SectionProperties first;
                    if (previous == null)
                    {
                        first = WordSections.All(body)[0];
                    }
                    else if (previous.Paragraph?.ParagraphProperties?.SectionProperties != null)
                    {
                        first = WordSections.Containing(body, block.Element);
                    }
                    else
                    {
                        var type = setup.ChangesPaper ? SectionMarkValues.NextPage : SectionMarkValues.Continuous;
                        first = WordSections.Split(body, previous.Element, WordIds.Collect(docu.Document), type, out carrier);
                    }

                    var list = WordSections.All(body);
                    targets.AddRange(list.Skip(list.ToList().IndexOf(first)));
                    break;
            }

            foreach (var section in targets)
                setup.Apply(section);

            var mirror = setup.Mirror ?? (setup.MarginPreset == null ? null : WordPaperSizes.Margins(setup.MarginPreset).Mirror);
            if (mirror != null)
            {
                var settings = WordParts.EnsureSettings(docu);
                settings.RemoveAllChildren<MirrorMargins>();
                if (mirror.Value)
                    settings.AddChild(new MirrorMargins());
            }

            if (setup.PageColor != null)
                PageColor(docu, setup.PageColor);

            var fresh = WordScan.Read(docu.Document);
            var sections = WordSections.All(fresh.Body);
            var numbers = targets.Select(t => sections.ToList().IndexOf(t) + 1).Where(n => n > 0).Distinct().OrderBy(n => n).ToList();
            var anchors = numbers.Select(WordAnchors.Section).ToList();
            if (carrier != null && WordIds.Of(carrier) is string carrierId)
                anchors.Add(WordAnchors.Paragraph(carrierId));

            string location;
            if (scope == "document")
            {
                location = "문서 전체";
            }
            else if (scope == "section")
            {
                location = $"구역 {numbers.FirstOrDefault()}";
            }
            else
            {
                var page = block == null ? 1 : fresh.Find(block.Id)?.Page ?? 1;
                location = $"{page}쪽~";
            }

            var details = setup.Details();
            var summary = scope == "document" ? "the whole document" : scope == "section" ? $"section {numbers.FirstOrDefault()}" : $"section {numbers.FirstOrDefault()} onward";
            return ToolResult.Changed(
                $"Changed the page setup of {summary} ({targets.Count} section{(targets.Count == 1 ? string.Empty : "s")}): {string.Join(", ", details)}.",
                Change(location, "file-lines", anchors),
                string.Join(", ", details));
        }

        private static void PageColor(WordDocu docu, string color)
        {
            var document = docu.Document.MainDocumentPart.Document;
            document.RemoveAllChildren<DocumentBackground>();
            var settings = WordParts.EnsureSettings(docu);
            settings.RemoveAllChildren<DisplayBackgroundShape>();
            if (color == "none")
                return;

            document.InsertAt(new DocumentBackground { Color = color }, 0);
            settings.AddChild(new DisplayBackgroundShape());
        }
    }
}
