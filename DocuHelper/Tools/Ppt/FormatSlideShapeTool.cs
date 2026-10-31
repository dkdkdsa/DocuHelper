using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public class FormatSlideShapeTool : Tool
    {
        private const double SizeLimitFactor = 4;

        private const int MaxNameLength = 200;

        private const int MaxAltLength = 2000;

        public override string Name => "format_slide_shape";

        public override string Description =>
            "Change shapes, pictures, tables, charts or groups: position and size (x, y, w, h in cm from the slide top-left corner, or dx/dy to nudge), rotation, fill, outline, name, alt text. "
            + "Give only what should change. Colors are #RRGGBB or a theme color like accent1. Give only w or only h to keep a picture's proportions. "
            + "With several shapes x, y place their combined box and w, h resize each. Use set_font / format_slide_paragraphs for text.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "slide": {"type": "integer", "minimum": 1, "description": "Slide number (1-based)."},
                "shapes": {
                  "description": "Shape id or list of ids from read_slides.",
                  "anyOf": [
                    {"type": "string"},
                    {"type": "array", "items": {"type": "string"}}
                  ]
                },
                "x": {"type": "number", "description": "Left, cm."},
                "y": {"type": "number", "description": "Top, cm."},
                "w": {"type": "number", "minimum": 0, "description": "Width, cm."},
                "h": {"type": "number", "minimum": 0, "description": "Height, cm."},
                "dx": {"type": "number", "description": "Move right, cm."},
                "dy": {"type": "number", "description": "Move down, cm."},
                "rotation": {"type": "number", "minimum": -360, "maximum": 360, "description": "Degrees clockwise."},
                "fill": {
                  "type": "object",
                  "properties": {
                    "type": {"type": "string", "enum": ["solid", "gradient", "none", "inherit"], "description": "none = transparent, inherit = follow the theme."},
                    "color": {"type": "string", "description": "#RRGGBB, or theme color accent1-6, dark1, light1, dark2, light2, hyperlink with optional +N lighter / -N darker percent, e.g. accent1+40."},
                    "color2": {"type": "string", "description": "Second gradient color."},
                    "angle": {"type": "number", "minimum": 0, "maximum": 359, "description": "Gradient degrees, 90 = top to bottom."},
                    "transparency": {"type": "number", "minimum": 0, "maximum": 100, "description": "Percent."}
                  },
                  "required": ["type"]
                },
                "line": {
                  "type": "object",
                  "properties": {
                    "color": {"type": "string", "description": "Color or \"none\"."},
                    "width": {"type": "number", "minimum": 0, "maximum": 100, "description": "Points."},
                    "dash": {"type": "string", "enum": ["solid", "dash", "dot", "dash_dot"]}
                  }
                },
                "name": {"type": "string", "description": "Shape name."},
                "alt_text": {"type": "string", "description": "Alternative text."}
              },
              "required": ["slide", "shapes"]
            }
            """;

        public override ToolResult Execute(Docu docu, ToolInput input)
        {
            var document = PptSlides.DocumentOf(docu);
            var slide = PptSlides.Get(document, input.GetInt("slide"));
            var targets = PptTargets.FindMany(slide, input, "shapes");

            var size = document.PresentationPart.Presentation.SlideSize;
            var maxX = PptUnits.ToCm(size?.Cx?.Value ?? 12192000L) * SizeLimitFactor;
            var maxY = PptUnits.ToCm(size?.Cy?.Value ?? 6858000L) * SizeLimitFactor;
            var request = new Request
            {
                X = PptInputs.Emu(input, "x", -maxX, maxX),
                Y = PptInputs.Emu(input, "y", -maxY, maxY),
                Width = PptInputs.Emu(input, "w", 0, maxX),
                Height = PptInputs.Emu(input, "h", 0, maxY),
                DeltaX = PptInputs.Emu(input, "dx", -maxX, maxX),
                DeltaY = PptInputs.Emu(input, "dy", -maxY, maxY),
                Rotation = PptInputs.Number(input, "rotation", -360, 360),
                Name = Text(input, "name", MaxNameLength),
                Alt = Text(input, "alt_text", MaxAltLength),
            };

            var fillInput = PptInputs.Object(input, "fill");
            if (fillInput != null)
                request.Fill = PptFillWriter.Parse(fillInput);

            var lineInput = PptInputs.Object(input, "line");
            if (lineInput != null)
                request.Line = PptLineWriter.Parse(lineInput);

            if (request.IsEmpty)
                throw new ToolInputException("바꿀 내용을 하나 이상 알려 주세요. (x, y, w, h, dx, dy, rotation, fill, line, name, alt_text)");

            if ((request.X != null && request.DeltaX != null) || (request.Y != null && request.DeltaY != null))
                throw new ToolInputException("x와 dx(또는 y와 dy)는 함께 쓸 수 없어요. 위치를 정하거나 조금 옮기거나 하나만 알려 주세요.");

            if (request.Name != null && targets.Count > 1)
                throw new ToolInputException("이름은 도형 하나에만 정해 줄 수 있어요. 도형을 하나만 골라 주세요.");

            var plan = Validate(slide, targets, request, maxX, maxY);
            var before = string.Concat(targets.Select(t => t.OuterXml));
            Apply(slide, targets, request, plan);

            if (string.Concat(targets.Select(t => t.OuterXml)) == before)
                return ToolResult.Ok($"Slide {slide.Number}: nothing changed, the shape{(targets.Count == 1 ? " already has" : "s already have")} these settings.");

            return Report(slide, targets, request);
        }

        private static string Text(ToolInput input, string name, int maxLength)
        {
            var value = PptInputs.Text(input, name, maxLength);
            if (value != null && value.Length == 0 && name == "name")
                throw new ToolInputException("'name' 값이 비어 있어요.");

            return value;
        }

        private static Dictionary<OpenXmlCompositeElement, PptBox> Validate(PptSlideRef slide, List<OpenXmlCompositeElement> targets, Request request, double maxX, double maxY)
        {
            var plan = new Dictionary<OpenXmlCompositeElement, PptBox>();
            foreach (var target in targets)
            {
                var label = Label(target);
                if (request.MovesBox && !ReferenceEquals(target.Parent, slide.Tree))
                    throw new ToolInputException($"{label}은 묶음 안에 있어서 위치나 크기를 따로 바꿀 수 없어요. 묶음 전체를 고르거나 묶음을 먼저 풀어 주세요.");

                if (request.Rotation != null && (target is P.GraphicFrame || !ReferenceEquals(target.Parent, slide.Tree)))
                    throw new ToolInputException($"{label}은 돌릴 수 없어요. 표와 차트는 돌릴 수 없고, 묶음 안의 도형은 묶음째 돌려야 해요.");

                if (request.Fill != null && !CanFill(target))
                    throw new ToolInputException($"{label}은 채우기 색을 바꿀 수 없어요. 표는 edit_slide_table, 선은 line 색으로 바꿔 주세요.");

                if (request.Line != null && !CanOutline(target))
                    throw new ToolInputException($"{label}은 윤곽선을 바꿀 수 없어요.");
            }

            if (!request.MovesBox)
                return plan;

            var current = new Dictionary<OpenXmlCompositeElement, PptBox>();
            foreach (var target in targets)
            {
                var box = PptXfrm.Resolve(slide, target)
                    ?? throw new ToolInputException($"{Label(target)}은 위치와 크기를 알 수 없어서 바꿀 수 없어요.");

                current[target] = box;
            }

            var bounds = PptBox.Bounds(current.Values);
            foreach (var target in targets)
            {
                var box = current[target];
                var width = request.Width ?? box.Width;
                var height = request.Height ?? box.Height;
                if (target is P.Picture && box.Width > 0 && box.Height > 0)
                {
                    if (request.Width != null && request.Height == null)
                        height = (long)Math.Round(box.Height * (double)width / box.Width);
                    else if (request.Height != null && request.Width == null)
                        width = (long)Math.Round(box.Width * (double)height / box.Height);
                }

                var flat = PptTargets.KindOf(target) == PptShapeKind.Line;
                if (request.Resizes && !flat && (width <= 0 || height <= 0))
                    throw new ToolInputException($"{Label(target)}의 너비와 높이는 0보다 커야 해요.");

                if (width > PptUnits.ToEmu(maxX) || height > PptUnits.ToEmu(maxY))
                    throw new ToolInputException($"{Label(target)}이 슬라이드보다 너무 커져요. 크기를 줄여 주세요.");

                var left = box.X;
                var top = box.Y;
                if (request.X != null)
                    left = targets.Count == 1 ? request.X.Value : box.X + (request.X.Value - bounds.X);

                if (request.Y != null)
                    top = targets.Count == 1 ? request.Y.Value : box.Y + (request.Y.Value - bounds.Y);

                left += request.DeltaX ?? 0;
                top += request.DeltaY ?? 0;
                plan[target] = new PptBox(left, top, width, height);
            }

            return plan;
        }

        private static void Apply(PptSlideRef slide, List<OpenXmlCompositeElement> targets, Request request, Dictionary<OpenXmlCompositeElement, PptBox> plan)
        {
            foreach (var target in targets)
            {
                if (plan.TryGetValue(target, out var box))
                {
                    var old = PptXfrm.Ensure(slide, target);
                    PptXfrm.Write(target, box);
                    if (target is P.GraphicFrame frame && PptTargets.KindOf(frame) == PptShapeKind.Table && (old.Width != box.Width || old.Height != box.Height))
                        ScaleTable(frame, box);
                }

                if (request.Rotation != null)
                {
                    PptXfrm.Ensure(slide, target);
                    PptXfrm.SetRotation(target, request.Rotation.Value);
                }

                if (request.Fill != null)
                {
                    foreach (var owner in Fillable(target))
                        PptFillWriter.Apply(PropertiesOf(owner), request.Fill);
                }

                if (request.Line != null)
                {
                    foreach (var owner in Outlinable(target))
                        PptLineWriter.Apply(PropertiesOf(owner), request.Line, PptLineWriter.StyleDrawsLine(owner));
                }

                var properties = PptTargets.PropertiesOf(target);
                if (request.Name != null)
                    properties.Name = request.Name;

                if (request.Alt is { Length: 0 })
                    properties.Description = null;
                else if (request.Alt != null)
                    properties.Description = request.Alt;
            }
        }

        private static void ScaleTable(P.GraphicFrame frame, PptBox box)
        {
            var table = frame.Descendants<A.Table>().FirstOrDefault();
            if (table == null)
                return;

            var columns = table.TableGrid?.Elements<A.GridColumn>().ToList() ?? new List<A.GridColumn>();
            Distribute(columns.Select(c => c.Width?.Value ?? 0).ToList(), box.Width, (i, value) => columns[i].Width = value);
            var rows = table.Elements<A.TableRow>().ToList();
            Distribute(rows.Select(r => r.Height?.Value ?? 0).ToList(), box.Height, (i, value) => rows[i].Height = value);
        }

        private static void Distribute(List<long> sizes, long total, Action<int, long> set)
        {
            var sum = sizes.Sum();
            if (sizes.Count == 0 || sum <= 0)
                return;

            long used = 0;
            for (var i = 0; i < sizes.Count; i++)
            {
                var value = i == sizes.Count - 1 ? Math.Max(1, total - used) : Math.Max(1, (long)Math.Round(sizes[i] * (double)total / sum));
                set(i, value);
                used += value;
            }
        }

        private static ToolResult Report(PptSlideRef slide, List<OpenXmlCompositeElement> targets, Request request)
        {
            var phrases = new List<string>();
            if (request.X != null || request.Y != null)
                phrases.Add("moved");

            if (request.DeltaX != null || request.DeltaY != null)
                phrases.Add("nudged");

            if (request.Width != null || request.Height != null)
                phrases.Add("resized");

            if (request.Rotation != null)
                phrases.Add($"rotated to {request.Rotation.Value.ToString("0.##", CultureInfo.InvariantCulture)} degrees");

            if (request.Fill != null)
                phrases.Add("fill changed");

            if (request.Line != null)
                phrases.Add("outline changed");

            if (request.Name != null)
                phrases.Add("renamed");

            if (request.Alt != null)
                phrases.Add("alt text set");

            var ids = targets.Select(t => PptTargets.IdOf(t).ToString(CultureInfo.InvariantCulture)).ToList();
            var subject = targets.Count == 1
                ? $"[{ids[0]}] {PptTargets.KindOf(targets[0]).ToString().ToLowerInvariant()}"
                : $"{targets.Count} shapes ({string.Join(", ", ids.Select(i => "[" + i + "]"))})";

            var content = $"Slide {slide.Number} {subject}: {string.Join(", ", phrases)}.";
            if (request.MovesBox || request.Rotation != null)
            {
                var bounds = PptBox.Bounds(targets.Select(t => PptXfrm.Read(t)).Where(b => b != null).Select(b => b.Value));
                content += $" Now x={PptUnits.CmText(bounds.X)} y={PptUnits.CmText(bounds.Y)} w={PptUnits.CmText(bounds.Width)} h={PptUnits.CmText(bounds.Height)} cm.";
            }

            var change = new DocuChange
            {
                Location = targets.Count == 1
                    ? $"{slide.Number.ToString(CultureInfo.InvariantCulture)}번"
                    : $"{slide.Number.ToString(CultureInfo.InvariantCulture)}번 도형 {targets.Count.ToString(CultureInfo.InvariantCulture)}개",
                Icon = request.MovesBox || request.Rotation != null ? "move" : request.Fill != null || request.Line != null ? "fill-color" : "shapes",
            };

            change.Anchors.AddRange(targets.Select(t => PptAnchor.Shape(slide.SlideId, PptTargets.IdOf(t))));
            return ToolResult.Changed(content, change, request.Detail());
        }

        private static string Label(OpenXmlElement target) =>
            $"{PptTargets.IdOf(target).ToString(CultureInfo.InvariantCulture)}번({PptTargets.KoreanName(target)})";

        private static bool CanFill(OpenXmlCompositeElement target) => target switch
        {
            P.Shape or P.Picture => true,
            P.GroupShape => Fillable(target).Any(),
            _ => false,
        };

        private static bool CanOutline(OpenXmlCompositeElement target) => target switch
        {
            P.Shape or P.Picture or P.ConnectionShape => true,
            P.GroupShape => Outlinable(target).Any(),
            _ => false,
        };

        private static IEnumerable<OpenXmlCompositeElement> Fillable(OpenXmlCompositeElement target) =>
            target is P.GroupShape
                ? target.Descendants().Where(e => e is P.Shape or P.Picture).Cast<OpenXmlCompositeElement>()
                : new[] { target };

        private static IEnumerable<OpenXmlCompositeElement> Outlinable(OpenXmlCompositeElement target) =>
            target is P.GroupShape
                ? target.Descendants().Where(e => e is P.Shape or P.Picture or P.ConnectionShape).Cast<OpenXmlCompositeElement>()
                : new[] { target };

        private static P.ShapeProperties PropertiesOf(OpenXmlCompositeElement owner)
        {
            switch (owner)
            {
                case P.Shape shape:
                    shape.ShapeProperties ??= new P.ShapeProperties();
                    return shape.ShapeProperties;
                case P.Picture picture:
                    picture.ShapeProperties ??= new P.ShapeProperties();
                    return picture.ShapeProperties;
                default:
                    var connector = (P.ConnectionShape)owner;
                    connector.ShapeProperties ??= new P.ShapeProperties();
                    return connector.ShapeProperties;
            }
        }

        private sealed class Request
        {
            public long? X { get; set; }

            public long? Y { get; set; }

            public long? Width { get; set; }

            public long? Height { get; set; }

            public long? DeltaX { get; set; }

            public long? DeltaY { get; set; }

            public double? Rotation { get; set; }

            public PptFillSpec Fill { get; set; }

            public PptLineSpec Line { get; set; }

            public string Name { get; set; }

            public string Alt { get; set; }

            public bool Moves => X != null || Y != null || DeltaX != null || DeltaY != null;

            public bool Resizes => Width != null || Height != null;

            public bool MovesBox => Moves || Resizes;

            public bool IsEmpty => !MovesBox && Rotation == null && Fill == null && Line == null && Name == null && Alt == null;

            public string Detail()
            {
                var kinds = new List<string>();
                if (Moves)
                    kinds.Add("위치 바꿈");

                if (Resizes)
                    kinds.Add("크기 바꿈");

                if (Rotation != null)
                    kinds.Add("회전");

                if (Fill != null || Line != null)
                    kinds.Add("색 바꿈");

                if (Name != null)
                    kinds.Add("이름 바꿈");

                if (Alt != null)
                    kinds.Add("대체 텍스트");

                return kinds.Count == 1 ? kinds[0] : "도형 서식";
            }
        }
    }
}
