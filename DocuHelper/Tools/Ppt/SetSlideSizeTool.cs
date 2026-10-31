using System.Globalization;
using System.Linq;
using DocuHelper.Docus;
using DocuHelper.Tools;
using DocumentFormat.OpenXml.Presentation;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public class SetSlideSizeTool : Tool
    {
        public override string Name => "set_slide_size";

        public override string Description =>
            "Change the slide size of the whole deck: standard_4_3 (25.4 x 19.05 cm), widescreen_16_9 (33.867 x 19.05 cm), widescreen_16_10 (30.48 x 19.05 cm), "
            + "a4 (29.7 x 21 cm), letter (27.94 x 21.59 cm), or custom width_cm x height_cm (2.54-142.24). Every shape of every slide, layout and master is scaled. "
            + "scaling \"ensure_fit\" (default) keeps everything inside the new size; \"maximize\" fills the new size and may push content off the edge.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "preset": {
                  "type": "string",
                  "enum": ["standard_4_3", "widescreen_16_9", "widescreen_16_10", "a4", "letter", "custom"]
                },
                "width_cm": {"type": "number", "minimum": 2.54, "maximum": 142.24, "description": "custom: width in cm, 2.54-142.24."},
                "height_cm": {"type": "number", "minimum": 2.54, "maximum": 142.24, "description": "custom: height in cm, 2.54-142.24."},
                "scaling": {"type": "string", "enum": ["ensure_fit", "maximize"], "description": "Default ensure_fit."}
              },
              "required": ["preset"]
            }
            """;

        public override ToolResult Execute(Docu docu, ToolInput input)
        {
            var document = PptSlides.DocumentOf(docu);
            var presentation = document.PresentationPart
                ?? throw new ToolInputException("이 파일은 슬라이드 문서로 열 수 없어요.");
            var preset = PptInputs.Choice(input, "preset", PptSlideSizes.Presets.ToArray());
            if (preset == null)
                throw new ToolInputException("'preset' 값이 필요해요. standard_4_3, widescreen_16_9, widescreen_16_10, a4, letter, custom 중 하나를 골라 주세요.");

            var target = preset == "custom" ? CustomSize(input) : PptSlideSizes.Preset(preset);
            var ensureFit = (PptInputs.Choice(input, "scaling", "ensure_fit", "maximize") ?? "ensure_fit") == "ensure_fit";
            var deck = PptFrames.DeckOf(document);
            if (deck.Width == target.Width && deck.Height == target.Height)
                return ToolResult.Ok("The slide size is already the same, so nothing changed.", "이미 같은 슬라이드 크기예요.");

            var snapshot = docu.Capture();
            try
            {
                var scale = PptSizeScaler.Factors(deck.Width, deck.Height, target.Width, target.Height, ensureFit);
                var slides = PptSlides.List(document);
                foreach (var slide in slides)
                    PptSizeScaler.Root(slide.Part.Slide, slide.Tree, scale);

                foreach (var master in presentation.SlideMasterParts)
                {
                    PptSizeScaler.Root(master.SlideMaster, master.SlideMaster?.CommonSlideData?.ShapeTree, scale);
                    foreach (var layout in master.SlideLayoutParts)
                        PptSizeScaler.Root(layout.SlideLayout, layout.SlideLayout?.CommonSlideData?.ShapeTree, scale);
                }

                presentation.Presentation.SlideSize = SizeElement(target);
                var anchors = slides.Select(s => PptAnchor.Slide(s.SlideId)).ToList();
                var content = $"Changed the slide size to {target.Aspect} ({PptUnits.CmText(target.Width)} x {PptUnits.CmText(target.Height)} cm) and scaled "
                    + $"{slides.Count.ToString(CultureInfo.InvariantCulture)} slide(s), their layouts and masters by {scale.S.ToString("0.###", CultureInfo.InvariantCulture)} "
                    + $"({(ensureFit ? "fit" : "maximize")}). Notes and animations were not scaled; check_overflow can find text that no longer fits.";
                var change = new DocuChange
                {
                    Location = "전체",
                    Icon = "size",
                };
                change.Anchors.AddRange(anchors);
                return ToolResult.Changed(content, change, $"슬라이드 크기 {target.Aspect}");
            }
            catch
            {
                docu.Restore(snapshot);
                throw;
            }
        }

        private static PptSlideSize CustomSize(ToolInput input)
        {
            var width = PptInputs.Cm(input, "width_cm", PptSlideSizes.MinCm, PptSlideSizes.MaxCm);
            var height = PptInputs.Cm(input, "height_cm", PptSlideSizes.MinCm, PptSlideSizes.MaxCm);
            if (width == null || height == null)
                throw new ToolInputException("사용자 지정 크기에는 width_cm과 height_cm을 함께 알려 주세요. 각각 2.54~142.24cm예요.");

            return PptSlideSizes.Custom(PptUnits.ToEmu(width.Value), PptUnits.ToEmu(height.Value));
        }

        private static SlideSize SizeElement(PptSlideSize size)
        {
            var element = new SlideSize { Cx = (int)size.Width, Cy = (int)size.Height };
            switch (size.Type)
            {
                case "screen4x3":
                    element.Type = SlideSizeValues.Screen4x3;
                    break;
                case "A4":
                    element.Type = SlideSizeValues.A4;
                    break;
                case "letter":
                    element.Type = SlideSizeValues.Letter;
                    break;
            }

            return element;
        }
    }
}
