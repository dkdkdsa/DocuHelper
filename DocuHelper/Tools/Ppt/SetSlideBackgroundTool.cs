using System.Collections.Generic;
using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml.Packaging;

namespace DocuHelper.Tools.Ppt
{
    public class SetSlideBackgroundTool : Tool
    {
        public override string Name => "set_slide_background";

        public override string Description =>
            "Change slide backgrounds. `slides`: numbers, or \"all\" to set the slide master(s) so every slide without its own background follows (own backgrounds are cleared). "
            + "fill.type: solid (color), gradient (color to color2 at angle), picture (attachment, path or base64), inherit (follow layout and master again). "
            + "hide_graphics hides the decorative shapes of layout and master (not placeholders).";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "slides": {
                  "description": "Slide numbers (1-based), or \"all\".",
                  "anyOf": [
                    {"type": "string", "enum": ["all"]},
                    {"type": "array", "items": {"type": "integer", "minimum": 1}}
                  ]
                },
                "fill": {
                  "type": "object",
                  "properties": {
                    "type": {"type": "string", "enum": ["solid", "gradient", "picture", "inherit"]},
                    "color": {"type": "string", "description": "#RRGGBB, or theme color accent1-6, dark1, light1, dark2, light2, hyperlink with optional +N lighter / -N darker percent, e.g. accent1+40."},
                    "color2": {"type": "string", "description": "Second gradient color."},
                    "angle": {"type": "number", "description": "Gradient degrees, 90 = top to bottom.", "minimum": 0, "maximum": 359},
                    "transparency": {"type": "number", "description": "Percent.", "minimum": 0, "maximum": 100},
                    "image_base64": {"type": "string", "description": "Image bytes, base64 (the app uses this)."},
                    "attachment": {"type": "string", "description": "Name of an image attached to the chat."},
                    "path": {"type": "string", "description": "Image file in the deck folder (png jpg gif bmp tif)."}
                  },
                  "required": ["type"]
                },
                "hide_graphics": {"type": "boolean", "description": "Hide master and layout graphics."}
              },
              "required": ["slides"]
            }
            """;

        public override ToolResult Execute(Docu docu, ToolInput input)
        {
            var document = PptSlides.DocumentOf(docu);
            var presentation = document.PresentationPart
                ?? throw new ToolInputException("이 파일은 슬라이드 문서로 열 수 없어요.");
            var whole = PptBackgrounds.IsAll(input);
            var targets = whole ? PptSlides.List(document) : PptBackgrounds.Numbers(document, input).ToList();
            var fillInput = PptInputs.Object(input, "fill");
            var hide = PptInputs.Bool(input, "hide_graphics");
            if (fillInput == null && hide == null)
                throw new ToolInputException("바꿀 배경(fill)이나 배경 그래픽 숨김(hide_graphics) 중 하나를 알려 주세요.");

            var fill = fillInput == null ? null : PptBackgrounds.Parse(fillInput, input, docu);
            var snapshot = docu.Capture();
            try
            {
                if (fill != null)
                {
                    if (whole && fill.Type != "inherit")
                        SetMasters(presentation, fill);

                    foreach (var slide in targets)
                        SetSlide(slide, whole || fill.Type == "inherit" ? null : fill);
                }

                if (hide != null)
                {
                    foreach (var slide in targets)
                        slide.Part.Slide.ShowMasterShapes = hide == true ? false : null;
                }

                var numbers = string.Join(", ", targets.Select(t => t.Number.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                var location = whole ? "전체 배경" : $"{numbers}번 배경";
                var parts = new List<string>();
                if (fill != null)
                    parts.Add(fill.Type == "inherit" ? "followed the layout and master background again" : $"set a {fill.Type} background");

                if (hide != null)
                    parts.Add(hide == true ? "hid the layout and master graphics" : "showed the layout and master graphics");

                var content = (whole ? "Changed every slide (own backgrounds are cleared, and the slide master gets the background)" : $"Changed slide(s) {numbers}")
                    + ": " + string.Join("; ", parts) + ".";
                var change = new DocuChange
                {
                    Location = location,
                    Icon = "bg",
                };
                change.Anchors.AddRange(targets.Select(t => PptAnchor.Slide(t.SlideId)));
                var detail = fill == null ? "배경 그래픽 숨김" : fill.Type == "inherit" ? "배경 따라가기" : "배경 바꿈";
                return ToolResult.Changed(content, change, detail);
            }
            catch
            {
                docu.Restore(snapshot);
                throw;
            }
        }

        private static void SetMasters(PresentationPart presentation, PptBackgroundFill fill)
        {
            foreach (var master in presentation.SlideMasterParts)
            {
                var root = master.SlideMaster;
                var common = root.CommonSlideData;
                var oldEmbed = PptBackgrounds.EmbedOf(common.Background);
                common.Background = PptBackgrounds.Build(fill, master);
                PptBackgrounds.Release(master, oldEmbed, root);
            }
        }

        private static void SetSlide(PptSlideRef slide, PptBackgroundFill own)
        {
            var part = slide.Part;
            var root = part.Slide;
            var common = root.CommonSlideData;
            var oldEmbed = PptBackgrounds.EmbedOf(common.Background);
            common.Background = own == null ? null : PptBackgrounds.Build(own, part);
            PptBackgrounds.Release(part, oldEmbed, root);
        }
    }
}
