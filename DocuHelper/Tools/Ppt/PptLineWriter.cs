using DocumentFormat.OpenXml;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public static class PptLineWriter
    {
        public const double MaxWidthPoints = 100;

        public static PptLineSpec Parse(ToolInput input)
        {
            var color = PptInputs.Color(input, "color", true);
            if (color != null && color.IsAuto)
                throw new ToolInputException($"'color' 값을 색으로 읽지 못했어요. {PptColor.InvalidMessage}");

            var width = PptInputs.Number(input, "width", 0, MaxWidthPoints);
            var dash = PptInputs.Choice(input, "dash", "solid", "dash", "dot", "dash_dot");
            var spec = new PptLineSpec(color, width, dash);
            if (spec.IsEmpty)
                throw new ToolInputException("'line'에는 color, width, dash 중 하나 이상이 필요해요.");

            return spec;
        }

        public static PptLineSpec FromParts(PptColorSpec color, double? widthPoints) => new(color, widthPoints, null);

        public static void Apply(OpenXmlCompositeElement properties, PptLineSpec line, bool styleDrawsLine)
        {
            var outline = properties.GetFirstChild<A.Outline>();
            if (outline == null)
            {
                outline = new A.Outline();
                PptSchemaOrder.Insert(properties, outline, PptSchemaOrder.ShapeProperties);
            }

            if (line.WidthPoints != null)
                outline.Width = (int)PptUnits.PointsToEmu(line.WidthPoints.Value);

            if (line.Color != null)
            {
                OpenXmlElement fill = line.Color.IsNone ? new A.NoFill() : new A.SolidFill(PptColor.ToElement(line.Color));
                PptSchemaOrder.Replace(outline, fill, PptSchemaOrder.LineProperties);
            }
            else if (!styleDrawsLine && !HasFill(outline))
            {
                PptSchemaOrder.Replace(outline, new A.SolidFill(PptColor.ToElement(PptColorSpec.FromScheme("tx1"))), PptSchemaOrder.LineProperties);
            }

            if (line.Dash != null)
                PptSchemaOrder.Replace(outline, new A.PresetDash { Val = DashOf(line.Dash) }, PptSchemaOrder.LineProperties);
        }

        public static void Remove(OpenXmlCompositeElement properties) =>
            properties.GetFirstChild<A.Outline>()?.Remove();

        public static bool StyleDrawsLine(OpenXmlElement owner)
        {
            var reference = owner switch
            {
                P.Shape shape => shape.ShapeStyle?.LineReference,
                P.ConnectionShape connector => connector.ShapeStyle?.LineReference,
                _ => null,
            };

            return reference?.Index != null && reference.Index.Value > 0;
        }

        private static bool HasFill(A.Outline outline) =>
            outline.GetFirstChild<A.SolidFill>() != null || outline.GetFirstChild<A.NoFill>() != null || outline.GetFirstChild<A.GradientFill>() != null;

        private static A.PresetLineDashValues DashOf(string dash) => dash switch
        {
            "dash" => A.PresetLineDashValues.Dash,
            "dot" => A.PresetLineDashValues.SystemDot,
            "dash_dot" => A.PresetLineDashValues.DashDot,
            _ => A.PresetLineDashValues.Solid,
        };
    }
}
