using System;
using System.Linq;
using DocumentFormat.OpenXml;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public static class PptFillWriter
    {
        private const double GradientLighten = 40;

        public static PptFillSpec Parse(ToolInput input)
        {
            var type = PptInputs.Enum(
                input,
                "type",
                ("solid", PptFillType.Solid),
                ("gradient", PptFillType.Gradient),
                ("none", PptFillType.None),
                ("inherit", PptFillType.Inherit))
                ?? throw new ToolInputException("'fill'에는 type이 필요해요. solid, gradient, none, inherit 중 하나로 알려 주세요.");

            var color = Color(input, "color");
            var color2 = Color(input, "color2");
            var angle = PptInputs.Number(input, "angle", 0, 359);
            var transparency = PptInputs.Number(input, "transparency", 0, 100);

            if (type is PptFillType.None or PptFillType.Inherit)
            {
                if (color != null || color2 != null || angle != null || transparency != null)
                    throw new ToolInputException("채우기 없음이나 테마 따라가기에는 색, 각도, 투명도를 함께 쓸 수 없어요.");
            }
            else if (type == PptFillType.Solid && (color2 != null || angle != null))
            {
                throw new ToolInputException("color2와 angle은 그라데이션(gradient) 채우기에만 쓸 수 있어요.");
            }

            return new PptFillSpec(type, color, color2, angle, transparency);
        }

        public static PptFillSpec FromColor(PptColorSpec color) =>
            color.IsNone ? new PptFillSpec(PptFillType.None) : new PptFillSpec(PptFillType.Solid, color);

        public static void Apply(P.ShapeProperties properties, PptFillSpec fill)
        {
            switch (fill.Type)
            {
                case PptFillType.Inherit:
                    PptSchemaOrder.Remove(properties, "solidFill", PptSchemaOrder.ShapeProperties);
                    return;
                case PptFillType.None:
                    PptSchemaOrder.Replace(properties, new A.NoFill(), PptSchemaOrder.ShapeProperties);
                    return;
            }

            var existing = Existing(properties);
            var existingTransparency = existing == null ? 0 : 100 - PptColor.AlphaOf(existing.Source);
            var color = fill.Color ?? existing?.Color
                ?? throw new ToolInputException("채우기 색 color가 필요해요. #RRGGBB나 accent1 같은 색으로 알려 주세요.");

            var transparency = fill.Transparency ?? (fill.Color == null ? existingTransparency : 0);
            if (fill.Type == PptFillType.Solid)
            {
                var solid = new A.SolidFill(WithAlpha(color, transparency));
                PptSchemaOrder.Replace(properties, solid, PptSchemaOrder.ShapeProperties);
                return;
            }

            var second = fill.Color2 ?? (fill.Color == null ? existing?.Color2 : null) ?? Lighter(color);
            var angle = fill.Angle ?? (fill.Color == null ? existing?.Angle : null) ?? 90;
            var gradient = new A.GradientFill(
                new A.GradientStopList(
                    new A.GradientStop(WithAlpha(color, transparency)) { Position = 0 },
                    new A.GradientStop(WithAlpha(second, transparency)) { Position = 100000 }),
                new A.LinearGradientFill { Angle = (int)Math.Round(angle * PptUnits.AnglePerDegree), Scaled = false })
            {
                RotateWithShape = true,
            };

            PptSchemaOrder.Replace(properties, gradient, PptSchemaOrder.ShapeProperties);
        }

        public static PptColorSpec Lighter(PptColorSpec color)
        {
            var percent = color.Percent < 60 ? color.Percent + GradientLighten : color.Percent - GradientLighten;
            return color.IsHex ? PptColorSpec.FromHex(color.Hex, percent) : PptColorSpec.FromScheme(color.Scheme, percent);
        }

        public static OpenXmlCompositeElement WithAlpha(PptColorSpec color, double transparency)
        {
            var element = PptColor.ToElement(color);
            if (transparency > 0)
                element.AppendChild(new A.Alpha { Val = PptUnits.PercentToThousandths(100 - transparency) });

            return element;
        }

        private static PptColorSpec Color(ToolInput input, string name)
        {
            var spec = PptInputs.Color(input, name);
            if (spec != null && !spec.HasColor)
                throw new ToolInputException($"'{name}' 값을 색으로 읽지 못했어요. {PptColor.InvalidMessage}");

            return spec;
        }

        private static Current Existing(P.ShapeProperties properties)
        {
            var solid = properties.GetFirstChild<A.SolidFill>();
            if (solid != null)
                return new Current(PptColor.Read(solid), null, null, solid);

            var gradient = properties.GetFirstChild<A.GradientFill>();
            if (gradient == null)
                return null;

            var stops = gradient.GradientStopList?.Elements<A.GradientStop>().ToList() ?? new();
            var angle = gradient.GetFirstChild<A.LinearGradientFill>()?.Angle?.Value;
            return new Current(
                stops.Count > 0 ? PptColor.Read(stops[0]) : null,
                stops.Count > 1 ? PptColor.Read(stops[^1]) : null,
                angle == null ? null : angle.Value / (double)PptUnits.AnglePerDegree,
                stops.Count > 0 ? stops[0] : gradient);
        }

        private sealed record Current(PptColorSpec Color, PptColorSpec Color2, double? Angle, OpenXmlElement Source);
    }
}
