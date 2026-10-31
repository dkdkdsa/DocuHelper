using System;
using System.Globalization;
using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public class AddSlideShapeTool : Tool
    {
        public const int MaxShapesPerSlide = 500;

        private const double SizeLimitFactor = 4;

        private const long CascadeStep = 180000;

        private const int MaxCascade = 40;

        private const int MaxText = 20000;

        private const int MaxNameLength = 200;

        private const int MaxAltLength = 2000;

        public override string Name => "add_slide_shape";

        public override string Description =>
            "Add a shape, text box or line to a slide. x, y, w, h are cm from the slide top-left corner; leave them out for a default in the middle. "
            + "fill and line take #RRGGBB or a theme color like accent1 (default: theme accent). `text` fills the shape (\\n = new paragraph). "
            + "place \"master\" puts it on the slide master so it shows on every slide (logo, bar). Lines take no text or fill.";

        public override string InputSchema => $$"""
            {
              "type": "object",
              "properties": {
                "slide": {"type": "integer", "minimum": 1, "description": "Slide number (1-based)."},
                "shape_type": {"type": "string", "enum": [{{string.Join(", ", PptShapeCatalog.All.Select(s => "\"" + s.Token + "\""))}}]},
                "x": {"type": "number", "description": "Left, cm."},
                "y": {"type": "number", "description": "Top, cm."},
                "w": {"type": "number", "minimum": 0, "description": "Width, cm."},
                "h": {"type": "number", "minimum": 0, "description": "Height, cm."},
                "text": {"type": "string", "description": "Text inside the shape."},
                "fill": {"type": "string", "description": "Fill color or \"none\"."},
                "line": {"type": "string", "description": "Outline color or \"none\"."},
                "line_width": {"type": "number", "minimum": 0, "maximum": 100, "description": "Outline points."},
                "rotation": {"type": "number", "minimum": -360, "maximum": 360, "description": "Degrees clockwise."},
                "name": {"type": "string", "description": "Shape name."},
                "alt_text": {"type": "string", "description": "Alternative text."},
                "place": {"type": "string", "enum": ["slide", "master"]}
              },
              "required": ["slide", "shape_type"]
            }
            """;

        public override ToolResult Execute(Docu docu, ToolInput input)
        {
            var document = PptSlides.DocumentOf(docu);
            var slide = PptSlides.Get(document, input.GetInt("slide"));
            var spec = PptShapeCatalog.Find(input.GetString("shape_type"));
            var place = PptInputs.Choice(input, "place", "slide", "master") ?? "slide";

            var size = document.PresentationPart.Presentation.SlideSize;
            var deckWidth = size?.Cx?.Value ?? 12192000L;
            var deckHeight = size?.Cy?.Value ?? 6858000L;
            var maxX = PptUnits.ToCm(deckWidth) * SizeLimitFactor;
            var maxY = PptUnits.ToCm(deckHeight) * SizeLimitFactor;

            var x = PptInputs.Emu(input, "x", -maxX, maxX);
            var y = PptInputs.Emu(input, "y", -maxY, maxY);
            var width = PptInputs.Emu(input, "w", 0, maxX);
            var height = PptInputs.Emu(input, "h", 0, maxY);
            var text = PptInputs.Text(input, "text", MaxText);
            var fill = ColorOption(input, "fill");
            var line = ColorOption(input, "line");
            var lineWidth = PptInputs.Number(input, "line_width", 0, PptLineWriter.MaxWidthPoints);
            var rotation = PptInputs.Number(input, "rotation", -360, 360);
            var name = Label(input, "name", MaxNameLength);
            var alt = Label(input, "alt_text", MaxAltLength);

            if (spec.IsLine)
            {
                if (fill != null)
                    throw new ToolInputException("선에는 채우기 색을 쓸 수 없어요. 선 색은 'line'으로 알려 주세요.");

                if (!string.IsNullOrEmpty(text))
                    throw new ToolInputException("선에는 글을 넣을 수 없어요. 글은 글상자나 도형에 넣어 주세요.");
            }

            var tree = TreeOf(document, slide, place);
            if (PptTargets.All(tree).Count() >= MaxShapesPerSlide)
                throw new ToolInputException($"한 장에는 도형을 {MaxShapesPerSlide.ToString(CultureInfo.InvariantCulture)}개까지만 둘 수 있어요. 안 쓰는 도형을 먼저 지워 주세요.");

            var box = SizeOf(spec, text, width, height);
            var left = x ?? 0;
            var top = y ?? 0;
            if (x == null || y == null)
            {
                var centered = box.CenteredIn(new PptBox(0, 0, deckWidth, deckHeight));
                left = x ?? centered.X;
                top = y ?? centered.Y;
                (left, top) = Cascade(tree, left, top, x != null, y != null);
            }

            box = box.MoveTo(left, top);
            var id = PptSlides.NextShapeId(tree);
            var label = name ?? $"{spec.KoreanName} {(id - 1).ToString(CultureInfo.InvariantCulture)}";
            OpenXmlCompositeElement element;
            if (spec.IsLine)
            {
                var connector = PptShapeBuilder.CreateConnector(spec.Preset, id, label, alt, box, false, false, spec.HeadEnd, spec.TailEnd);
                ApplyLine(connector.ShapeProperties, line, lineWidth, true);
                element = connector;
            }
            else
            {
                var shape = spec.IsTextBox
                    ? PptShapeBuilder.CreateTextBox(id, label, alt, box)
                    : PptShapeBuilder.CreateAuto(spec.Preset, id, label, alt, box);

                if (fill != null)
                    PptFillWriter.Apply(shape.ShapeProperties, PptFillWriter.FromColor(fill));

                ApplyLine(shape.ShapeProperties, line, lineWidth, !spec.IsTextBox);
                if (!string.IsNullOrEmpty(text))
                    PptShapeBuilder.SetText(shape, text);

                if (fill != null && !spec.IsTextBox)
                    PptShapeBuilder.SetTextColor(shape, TextColorFor(document, slide, fill));

                element = shape;
            }

            if (rotation != null)
                PptXfrm.SetRotation(element, rotation.Value);

            PptSlides.AppendToTree(tree, element);

            var where = place == "master" ? " on the slide master" : string.Empty;
            var content = $"Added [{id.ToString(CultureInfo.InvariantCulture)}] {spec.Token}{where} at x={PptUnits.CmText(box.X)} y={PptUnits.CmText(box.Y)} w={PptUnits.CmText(box.Width)} h={PptUnits.CmText(box.Height)} cm.";
            if (place == "master")
                content += " It shows on every slide whose layout shows master shapes.";

            var change = new DocuChange
            {
                Location = $"{slide.Number.ToString(CultureInfo.InvariantCulture)}번",
                Icon = "shapes",
                Anchors = { place == "master" ? PptAnchor.Slide(slide.SlideId) : PptAnchor.Shape(slide.SlideId, id) },
            };

            return ToolResult.Changed(content, change, spec.Detail);
        }

        private static P.ShapeTree TreeOf(PresentationDocument document, PptSlideRef slide, string place)
        {
            if (place != "master")
                return slide.Tree ?? throw new ToolInputException($"{slide.Number}번 슬라이드를 읽지 못했어요.");

            var master = slide.Part.SlideLayoutPart?.SlideMasterPart ?? document.PresentationPart.SlideMasterParts.FirstOrDefault();
            return master?.SlideMaster?.CommonSlideData?.ShapeTree
                ?? throw new ToolInputException("이 파일에는 슬라이드 마스터가 없어서 마스터에 넣을 수 없어요.");
        }

        private static PptBox SizeOf(PptShapeSpec spec, string text, long? width, long? height)
        {
            if (spec.IsLine)
            {
                var lineWidth = width ?? (spec.HeightCm == 0 && height != null ? 0 : PptUnits.ToEmu(spec.WidthCm));
                var lineHeight = height ?? PptUnits.ToEmu(spec.HeightCm);
                if (lineWidth == 0 && lineHeight == 0)
                    throw new ToolInputException("선의 길이가 0이에요. w나 h 중 하나는 0보다 크게 알려 주세요.");

                return new PptBox(0, 0, lineWidth, lineHeight);
            }

            if (width == 0 || height == 0)
                throw new ToolInputException("도형의 너비(w)와 높이(h)는 0보다 커야 해요.");

            var w = width ?? PptUnits.ToEmu(spec.WidthCm);
            var h = height ?? (spec.IsTextBox ? PptShapeBuilder.TextBoxHeight(text, w) : PptUnits.ToEmu(spec.HeightCm));
            return new PptBox(0, 0, w, h);
        }

        private static (long X, long Y) Cascade(P.ShapeTree tree, long x, long y, bool fixedX, bool fixedY)
        {
            var taken = PptTargets.TopLevel(tree)
                .Select(PptXfrm.Read)
                .Where(b => b != null)
                .Select(b => (b.Value.X, b.Value.Y))
                .ToHashSet();

            for (var i = 0; i < MaxCascade && taken.Contains((x, y)); i++)
            {
                if (!fixedX)
                    x += CascadeStep;

                if (!fixedY)
                    y += CascadeStep;
            }

            return (x, y);
        }

        private static PptColorSpec TextColorFor(PresentationDocument document, PptSlideRef slide, PptColorSpec fill)
        {
            if (fill.IsNone)
                return PptColorSpec.FromScheme("tx1");

            var theme = PptTheme.Read(document, slide.Part.SlideLayoutPart?.SlideMasterPart);
            return PptColorSpec.FromScheme(PptShapeBuilder.PrefersDarkText(fill, theme, PptTheme.ColorMapOf(slide.Part)) ? "tx1" : "bg1");
        }

        private static PptColorSpec ColorOption(ToolInput input, string name)
        {
            var spec = PptInputs.Color(input, name, true);
            if (spec != null && spec.IsAuto)
                throw new ToolInputException($"'{name}' 값을 색으로 읽지 못했어요. {PptColor.InvalidMessage}");

            return spec;
        }

        private static string Label(ToolInput input, string name, int maxLength)
        {
            var value = PptInputs.Text(input, name, maxLength);
            if (value == null)
                return null;

            return value.Length == 0 ? throw new ToolInputException($"'{name}' 값이 비어 있어요.") : value;
        }

        private static void ApplyLine(P.ShapeProperties properties, PptColorSpec color, double? width, bool styleDrawsLine)
        {
            if (color == null && width == null)
                return;

            PptLineWriter.Apply(properties, PptLineWriter.FromParts(color, width), styleDrawsLine);
        }
    }
}
