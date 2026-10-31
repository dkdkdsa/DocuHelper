using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public static class PptPlaceholderMapper
    {
        public static PptRemapResult Apply(PresentationDocument document, PptSlideRef slide, SlideLayoutPart layout)
        {
            var presentation = document.PresentationPart.Presentation;
            var result = new PptRemapResult();
            var oldStyles = new PptSlideStyles(slide.Part, presentation.DefaultTextStyle);
            var tree = slide.Tree;
            var layoutShapes = Placeholders(layout);
            var items = tree.ChildElements.Where(e => PptTargets.PlaceholderOf(e) != null).Cast<OpenXmlCompositeElement>().ToList();
            var assignment = Match(items, layoutShapes);

            var fallback = FallbackBox(presentation);
            var unmatched = items.Where(i => !assignment.ContainsKey(i)).ToList();
            var bakes = new Dictionary<OpenXmlCompositeElement, PptBox>();
            foreach (var item in unmatched)
            {
                if (IsFooter(item) || IsEmpty(item))
                    continue;

                bakes[item] = BoxOf(slide, oldStyles, item) ?? fallback;
            }

            var recorded = new HashSet<uint>();
            foreach (var pair in assignment)
            {
                var target = PptPlaceholder.Of(pair.Value);
                var current = PptTargets.PlaceholderOf(pair.Key);
                var replacement = (P.PlaceholderShape)target.CloneNode(true);
                replacement.HasCustomPrompt = null;
                result.Matched++;
                if (current.OuterXml == replacement.OuterXml)
                    continue;

                current.InsertBeforeSelf(replacement);
                current.Remove();
                recorded.Add(PptTargets.IdOf(pair.Key));
            }

            foreach (var item in unmatched)
            {
                if (IsFooter(item))
                    continue;

                var id = PptTargets.IdOf(item);
                if (!bakes.TryGetValue(item, out var box))
                {
                    PptTimingRefs.RemoveShapes(slide, new[] { id });
                    item.Remove();
                    result.Dropped++;
                    continue;
                }

                Convert(item, oldStyles, box);
                result.Converted++;
                recorded.Add(id);
            }

            FixFooterIndexes(items.Where(i => i.Parent != null).ToList(), assignment);

            var used = new HashSet<P.Shape>(assignment.Values);
            foreach (var layoutShape in layoutShapes.Where(s => !used.Contains(s)))
            {
                var role = PptPlaceholder.RoleOf(PptPlaceholder.TypeOf(PptPlaceholder.Of(layoutShape)));
                if (role is not (PptShapeRole.Title or PptShapeRole.Subtitle or PptShapeRole.Body))
                    continue;

                var shape = PptSlideFactory.PlaceholderFrom(layoutShape, PptSlides.NextShapeId(tree));
                if (role == PptShapeRole.Title)
                    tree.GetFirstChild<P.GroupShapeProperties>().InsertAfterSelf(shape);
                else
                    PptSlides.AppendToTree(tree, shape);

                result.Created++;
                recorded.Add(PptTargets.IdOf(shape));
            }

            var oldLayout = slide.Part.SlideLayoutPart;
            if (!ReferenceEquals(oldLayout, layout))
            {
                var relationshipId = oldLayout == null ? null : slide.Part.GetIdOfPart(oldLayout);
                if (oldLayout != null)
                    slide.Part.DeletePart(oldLayout);

                if (relationshipId == null)
                    slide.Part.AddPart(layout);
                else
                    slide.Part.AddPart(layout, relationshipId);

                result.LayoutChanged = true;
                if (!ReferenceEquals(oldLayout?.SlideMasterPart, layout.SlideMasterPart))
                    slide.Part.Slide.ColorMapOverride = new P.ColorMapOverride(new A.MasterColorMapping());
            }

            result.Changed.AddRange(recorded.OrderBy(i => i));
            return result;
        }

        public static List<uint> Reset(PresentationDocument document, PptSlideRef slide)
        {
            var styles = new PptSlideStyles(slide.Part, document.PresentationPart.Presentation.DefaultTextStyle);
            var changed = new List<uint>();
            foreach (var shape in slide.Tree.Elements<P.Shape>().Where(s => PptPlaceholder.Of(s) != null).ToList())
            {
                var before = shape.OuterXml;
                var placeholder = PptPlaceholder.Of(shape);
                var transform = shape.ShapeProperties?.Transform2D;
                if (transform != null && Inherited(styles, placeholder) != null)
                    transform.Remove();

                var body = shape.TextBody;
                if (body != null)
                {
                    body.ListStyle?.RemoveAllChildren();
                    foreach (var paragraph in body.Elements<A.Paragraph>())
                        ResetParagraph(paragraph);

                    foreach (var props in body.Descendants<A.TextCharacterPropertiesType>().ToList())
                        PptRunProps.ClearFormatting(props);

                    PptSlides.ResetAutofit(shape);
                }

                if (shape.OuterXml != before)
                    changed.Add(PptTargets.IdOf(shape));
            }

            return changed;
        }

        public static PptBox? Inherited(PptSlideStyles styles, P.PlaceholderShape placeholder)
        {
            var layoutShape = styles.FindLayoutPlaceholder(placeholder);
            var layoutBox = PptXfrm.Read(layoutShape);
            if (layoutBox != null && !layoutBox.Value.IsEmpty)
                return layoutBox;

            var type = PptPlaceholder.TypeOf(layoutShape != null ? PptPlaceholder.Of(layoutShape) : placeholder);
            var masterBox = PptXfrm.Read(styles.FindMasterPlaceholder(type));
            return masterBox != null && !masterBox.Value.IsEmpty ? masterBox : null;
        }

        private static void ResetParagraph(A.Paragraph paragraph)
        {
            var props = paragraph.ParagraphProperties;
            if (props == null)
                return;

            props.Alignment = null;
            props.LeftMargin = null;
            props.RightMargin = null;
            props.Indent = null;
            props.DefaultTabSize = null;
            foreach (var child in props.ChildElements.Where(c => c.LocalName != "extLst").ToList())
                child.Remove();

            if (!props.HasAttributes && !props.HasChildren)
                props.Remove();
        }

        private static List<P.Shape> Placeholders(SlideLayoutPart layout) =>
            layout.SlideLayout?.CommonSlideData?.ShapeTree?.Elements<P.Shape>().Where(s => PptPlaceholder.Of(s) != null).ToList()
            ?? new List<P.Shape>();

        private static bool IsFooter(OpenXmlElement item) =>
            PptPlaceholder.RoleOf(PptPlaceholder.TypeOf(PptTargets.PlaceholderOf(item))) == PptShapeRole.Footer;

        private static bool IsText(OpenXmlElement item)
        {
            if (item is not P.Shape)
                return false;

            return PptPlaceholder.TypeOf(PptTargets.PlaceholderOf(item)) is "subTitle" or "body" or "obj";
        }

        private static bool IsEmpty(OpenXmlElement item)
        {
            if (item is not P.Shape shape)
                return false;

            var body = shape.TextBody;
            if (body == null)
                return true;

            return body.Elements<A.Paragraph>().All(p => string.IsNullOrWhiteSpace(PptText.TextOf(p)) && !p.Elements<A.Field>().Any());
        }

        private static Dictionary<OpenXmlCompositeElement, P.Shape> Match(List<OpenXmlCompositeElement> items, List<P.Shape> layoutShapes)
        {
            var assignment = new Dictionary<OpenXmlCompositeElement, P.Shape>();
            var used = new HashSet<P.Shape>();

            void Pass(Func<OpenXmlCompositeElement, P.Shape, bool> accepts)
            {
                foreach (var item in items)
                {
                    if (assignment.ContainsKey(item))
                        continue;

                    var candidate = layoutShapes.FirstOrDefault(s => !used.Contains(s) && accepts(item, s));
                    if (candidate == null)
                        continue;

                    assignment[item] = candidate;
                    used.Add(candidate);
                }
            }

            Pass((i, s) => SameType(i, s) && IndexOf(i) == PptPlaceholder.IndexOf(PptPlaceholder.Of(s)));
            Pass(SameType);
            Pass((i, s) => Compatible(i, s));
            return assignment;
        }

        private static uint IndexOf(OpenXmlElement item) => PptPlaceholder.IndexOf(PptTargets.PlaceholderOf(item));

        private static bool SameType(OpenXmlElement item, P.Shape layoutShape)
        {
            var left = PptPlaceholder.TypeOf(PptTargets.PlaceholderOf(item));
            var right = PptPlaceholder.TypeOf(PptPlaceholder.Of(layoutShape));
            if (PptPlaceholder.Family(left) == "title" && PptPlaceholder.Family(right) == "title")
                return true;

            return left == right;
        }

        private static bool Compatible(OpenXmlElement item, P.Shape layoutShape)
        {
            var target = PptPlaceholder.TypeOf(PptPlaceholder.Of(layoutShape));
            if (IsText(item))
                return target is "subTitle" or "body" or "obj";

            var type = PptPlaceholder.TypeOf(PptTargets.PlaceholderOf(item));
            return target == "obj" && PptPlaceholder.RoleOf(type) != PptShapeRole.Footer && PptPlaceholder.Family(type) != "title";
        }

        private static void FixFooterIndexes(List<OpenXmlCompositeElement> items, Dictionary<OpenXmlCompositeElement, P.Shape> assignment)
        {
            var taken = new HashSet<uint>();
            foreach (var item in items.Where(i => assignment.ContainsKey(i)))
                taken.Add(IndexOf(item));

            foreach (var item in items.Where(i => !assignment.ContainsKey(i) && IsFooter(i)))
            {
                var placeholder = PptTargets.PlaceholderOf(item);
                var index = PptPlaceholder.IndexOf(placeholder);
                if (index != 0 && taken.Contains(index))
                {
                    var next = Math.Max(taken.Max(), index) + 1;
                    placeholder.Index = next;
                    index = next;
                }

                taken.Add(index);
            }
        }

        private static PptBox FallbackBox(P.Presentation presentation)
        {
            var width = presentation.SlideSize?.Cx?.Value ?? 12192000;
            var height = presentation.SlideSize?.Cy?.Value ?? 6858000;
            return new PptBox(width / 12, height / 8, width - width / 6, height / 4);
        }

        private static PptBox? BoxOf(PptSlideRef slide, PptSlideStyles styles, OpenXmlCompositeElement item)
        {
            var own = PptXfrm.Read(item);
            if (own != null)
                return own;

            return Inherited(styles, PptTargets.PlaceholderOf(item));
        }

        private static void Convert(OpenXmlCompositeElement item, PptSlideStyles styles, PptBox box)
        {
            var style = item is P.Shape source ? styles.For(source) : null;
            if (!PptXfrm.Has(item))
                PptXfrm.Write(item, box);

            PptTargets.PlaceholderOf(item).Remove();
            if (item is not P.Shape shape)
                return;

            var drawing = shape.NonVisualShapeProperties.NonVisualShapeDrawingProperties;
            drawing.TextBox = true;
            var locks = drawing.GetFirstChild<A.ShapeLocks>();
            if (locks != null)
            {
                locks.NoGrouping = null;
                if (!locks.HasAttributes)
                    locks.Remove();
            }

            var properties = shape.ShapeProperties ??= new P.ShapeProperties();
            if (properties.GetFirstChild<A.PresetGeometry>() == null && properties.GetFirstChild<A.CustomGeometry>() == null)
                PptSchemaOrder.Insert(properties, new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }, PptSchemaOrder.ShapeProperties);

            var body = PptSlides.EnsureTextBody(shape);
            body.ListStyle ??= new A.ListStyle();
            PptListStyleMerge.Bake(body.ListStyle, style.ListStyles);
            BakeBody(body, style);
        }

        private static void BakeBody(P.TextBody body, PptShapeStyle style)
        {
            var props = body.BodyProperties ??= new A.BodyProperties();
            props.LeftInset ??= Value(style.BodyValue(b => b.LeftInset));
            props.TopInset ??= Value(style.BodyValue(b => b.TopInset));
            props.RightInset ??= Value(style.BodyValue(b => b.RightInset));
            props.BottomInset ??= Value(style.BodyValue(b => b.BottomInset));
            if (props.Anchor == null)
            {
                var anchor = style.BodyValue(b => b.Anchor);
                if (anchor != null && anchor.HasValue)
                    props.Anchor = anchor.Value;
            }

            if (props.Wrap == null)
            {
                var wrap = style.BodyValue(b => b.Wrap);
                if (wrap != null && wrap.HasValue)
                    props.Wrap = wrap.Value;
            }

            var hasAutofit = props.GetFirstChild<A.NormalAutoFit>() != null || props.GetFirstChild<A.NoAutoFit>() != null || props.GetFirstChild<A.ShapeAutoFit>() != null;
            if (hasAutofit)
                return;

            foreach (var source in style.Bodies)
            {
                var autofit = (OpenXmlElement)source.GetFirstChild<A.NormalAutoFit>() ?? (OpenXmlElement)source.GetFirstChild<A.NoAutoFit>() ?? source.GetFirstChild<A.ShapeAutoFit>();
                if (autofit == null)
                    continue;

                PptSchemaOrder.Insert(props, autofit.CloneNode(true), PptSchemaOrder.BodyProperties);
                return;
            }
        }

        private static Int32Value Value(Int32Value source) =>
            source != null && source.HasValue ? new Int32Value(source.Value) : null;
    }
}
