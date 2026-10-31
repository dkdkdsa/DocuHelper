using System.Globalization;
using DocumentFormat.OpenXml;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public static class PptXfrm
    {
        public static bool Has(OpenXmlElement target) => Existing(target) != null;

        public static PptBox? Read(OpenXmlElement target) => BoxOf(Existing(target));

        public static PptBox? Resolve(PptSlideRef slide, OpenXmlElement target)
        {
            var own = Read(target);
            if (own != null || target is not P.Shape shape || PptPlaceholder.Of(shape) == null)
                return own;

            var inherited = new PptSlideStyles(slide.Part, null).For(shape).Transform;
            return BoxOf(inherited);
        }

        public static PptBox Ensure(PptSlideRef slide, OpenXmlElement target)
        {
            var own = Read(target);
            if (own != null)
                return own.Value;

            var resolved = Resolve(slide, target);
            if (resolved == null)
                throw new ToolInputException($"{PptTargets.IdOf(target).ToString(CultureInfo.InvariantCulture)}번 도형은 위치와 크기를 알 수 없어서 바꿀 수 없어요.");

            Write(target, resolved.Value);
            return resolved.Value;
        }

        public static void Write(OpenXmlElement target, PptBox box)
        {
            var transform = Existing(target) ?? Create(target, box);
            var offset = transform.GetFirstChild<A.Offset>();
            if (offset == null)
            {
                offset = new A.Offset();
                transform.PrependChild(offset);
            }

            offset.X = box.X;
            offset.Y = box.Y;

            var extents = transform.GetFirstChild<A.Extents>();
            if (extents == null)
            {
                extents = new A.Extents();
                offset.InsertAfterSelf(extents);
            }

            extents.Cx = box.Width;
            extents.Cy = box.Height;
        }

        public static double ReadRotation(OpenXmlElement target)
        {
            var transform = Existing(target);
            var text = transform == null ? null : AttributeOf(transform, "rot");
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var angle) ? PptUnits.AngleToDegrees(angle) : 0;
        }

        public static bool ReadFlipH(OpenXmlElement target) => ReadFlag(target, "flipH");

        public static bool ReadFlipV(OpenXmlElement target) => ReadFlag(target, "flipV");

        public static void SetRotation(OpenXmlElement target, double degrees)
        {
            var transform = Required(target);
            var angle = PptUnits.DegreesToAngle(degrees);
            if (angle == 0)
                transform.RemoveAttribute("rot", string.Empty);
            else
                transform.SetAttribute(new OpenXmlAttribute("rot", string.Empty, angle.ToString(CultureInfo.InvariantCulture)));
        }

        public static void SetFlip(OpenXmlElement target, bool? horizontal, bool? vertical)
        {
            var transform = Required(target);
            SetFlag(transform, "flipH", horizontal);
            SetFlag(transform, "flipV", vertical);
        }

        private static OpenXmlCompositeElement Existing(OpenXmlElement target) => target switch
        {
            P.Shape shape => shape.ShapeProperties?.Transform2D,
            P.Picture picture => picture.ShapeProperties?.Transform2D,
            P.ConnectionShape connector => connector.ShapeProperties?.Transform2D,
            P.GroupShape group => group.GroupShapeProperties?.TransformGroup,
            P.GraphicFrame frame => frame.Transform,
            _ => null,
        };

        private static OpenXmlCompositeElement Required(OpenXmlElement target) =>
            Existing(target) ?? throw new System.InvalidOperationException("The target has no explicit transform; call PptXfrm.Ensure or PptXfrm.Write first.");

        private static OpenXmlCompositeElement Create(OpenXmlElement target, PptBox box)
        {
            var offset = new A.Offset { X = box.X, Y = box.Y };
            var extents = new A.Extents { Cx = box.Width, Cy = box.Height };
            switch (target)
            {
                case P.Shape shape:
                    shape.ShapeProperties ??= new P.ShapeProperties();
                    return Attach(shape.ShapeProperties, new A.Transform2D(offset, extents));
                case P.Picture picture:
                    picture.ShapeProperties ??= new P.ShapeProperties();
                    return Attach(picture.ShapeProperties, new A.Transform2D(offset, extents));
                case P.ConnectionShape connector:
                    connector.ShapeProperties ??= new P.ShapeProperties();
                    return Attach(connector.ShapeProperties, new A.Transform2D(offset, extents));
                case P.GroupShape group:
                    group.GroupShapeProperties ??= new P.GroupShapeProperties();
                    var transform = new A.TransformGroup(
                        offset,
                        extents,
                        new A.ChildOffset { X = box.X, Y = box.Y },
                        new A.ChildExtents { Cx = box.Width, Cy = box.Height });

                    group.GroupShapeProperties.TransformGroup = transform;
                    return transform;
                case P.GraphicFrame frame:
                    var frameTransform = new P.Transform(offset, extents);
                    frame.Transform = frameTransform;
                    return frameTransform;
                default:
                    throw new ToolInputException("이 개체는 위치와 크기를 바꿀 수 없어요.");
            }
        }

        private static OpenXmlCompositeElement Attach(P.ShapeProperties properties, A.Transform2D transform)
        {
            PptSchemaOrder.Replace(properties, transform, PptSchemaOrder.ShapeProperties);
            return transform;
        }

        private static PptBox? BoxOf(OpenXmlElement transform)
        {
            var offset = transform?.GetFirstChild<A.Offset>();
            var extents = transform?.GetFirstChild<A.Extents>();
            if (offset?.X == null || offset.Y == null || extents?.Cx == null || extents.Cy == null)
                return null;

            return new PptBox(offset.X.Value, offset.Y.Value, extents.Cx.Value, extents.Cy.Value);
        }

        private static bool ReadFlag(OpenXmlElement target, string name)
        {
            var transform = Existing(target);
            var text = transform == null ? null : AttributeOf(transform, name);
            return text is "1" or "true";
        }

        private static void SetFlag(OpenXmlElement transform, string name, bool? value)
        {
            if (value == null)
                return;

            if (value.Value)
                transform.SetAttribute(new OpenXmlAttribute(name, string.Empty, "1"));
            else
                transform.RemoveAttribute(name, string.Empty);
        }

        private static string AttributeOf(OpenXmlElement element, string name)
        {
            foreach (var attribute in element.GetAttributes())
            {
                if (attribute.LocalName == name && string.IsNullOrEmpty(attribute.NamespaceUri))
                    return attribute.Value;
            }

            return null;
        }
    }
}
