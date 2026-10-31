using System;
using DocumentFormat.OpenXml;
using A = DocumentFormat.OpenXml.Drawing;

namespace DocuHelper.Tools.Ppt
{
    public static class PptBodyProps
    {
        public static void SetAnchor(A.BodyProperties body, A.TextAnchoringTypeValues anchor) =>
            body.Anchor = anchor;

        public static void SetAutofit(A.BodyProperties body, PptAutofit mode)
        {
            OpenXmlElement autofit = mode switch
            {
                PptAutofit.Shrink => new A.NormalAutoFit(),
                PptAutofit.Resize => new A.ShapeAutoFit(),
                _ => new A.NoAutoFit(),
            };

            PptSchemaOrder.Replace(body, autofit, PptSchemaOrder.BodyProperties);
        }

        public static void ClearAutofit(A.BodyProperties body) =>
            PptSchemaOrder.Remove(body, "noAutofit", PptSchemaOrder.BodyProperties);

        public static void SetWrap(A.BodyProperties body, bool wrap) =>
            body.Wrap = wrap ? A.TextWrappingValues.Square : A.TextWrappingValues.None;

        public static void SetInsets(A.BodyProperties body, long? left, long? top, long? right, long? bottom)
        {
            if (left != null)
                body.LeftInset = checked((int)left.Value);

            if (top != null)
                body.TopInset = checked((int)top.Value);

            if (right != null)
                body.RightInset = checked((int)right.Value);

            if (bottom != null)
                body.BottomInset = checked((int)bottom.Value);
        }

        public static void SetVertical(A.BodyProperties body, A.TextVerticalValues vertical) =>
            body.Vertical = vertical;

        public static void SetColumns(A.BodyProperties body, int count)
        {
            if (count < 1 || count > 16)
                throw new ArgumentOutOfRangeException(nameof(count));

            body.ColumnCount = count == 1 ? null : count;
        }
    }
}
