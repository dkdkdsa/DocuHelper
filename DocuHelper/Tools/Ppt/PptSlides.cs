using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public static class PptSlides
    {
        public static PresentationDocument DocumentOf(Docu docu)
        {
            if (docu is not PptDocu ppt || ppt.Document?.PresentationPart?.Presentation == null)
                throw new ToolInputException("파워포인트 파일이 아니라서 이 도구로는 열 수 없어요.");

            return ppt.Document;
        }

        public static List<PptSlideRef> List(PresentationDocument document)
        {
            var result = new List<PptSlideRef>();
            var presentationPart = document?.PresentationPart;
            var list = presentationPart?.Presentation?.SlideIdList;
            if (list == null)
                return result;

            foreach (var slideId in list.Elements<P.SlideId>())
            {
                var relationshipId = slideId.RelationshipId?.Value;
                if (string.IsNullOrEmpty(relationshipId) || !presentationPart.TryGetPartById(relationshipId, out var part) || part is not SlidePart slidePart)
                    continue;

                result.Add(new PptSlideRef(result.Count + 1, slideId, slidePart));
            }

            return result;
        }

        public static PptSlideRef Get(PresentationDocument document, int number)
        {
            var slides = List(document);
            if (slides.Count == 0)
                throw new ToolInputException("이 파일에는 아직 슬라이드가 없어요. add_slide로 먼저 슬라이드를 만들어 주세요.");

            if (number < 1 || number > slides.Count)
                throw new ToolInputException($"{number}번 슬라이드는 없어요. 슬라이드는 1번부터 {slides.Count}번까지 있어요.");

            return slides[number - 1];
        }

        public static P.Shape Resolve(PresentationDocument document, PptSlideRef slide, string key, bool create)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ToolInputException("'shape' 값이 필요해요.");

            var tree = slide.Tree;
            if (tree == null)
                throw new ToolInputException($"{slide.Number}번 슬라이드를 읽지 못했어요.");

            var role = RoleOf(key);
            if (role != null)
            {
                var found = FindByRole(tree, role.Value);
                if (found != null)
                    return found;

                if (create)
                {
                    var made = CreateFromLayout(document, slide, role.Value);
                    if (made != null)
                        return made;
                }

                throw new ToolInputException($"{slide.Number}번 슬라이드에는 {PptPlaceholder.KoreanName(role.Value)} 상자가 없어요. {Choices(slide)}");
            }

            if (!uint.TryParse(key.Trim().TrimStart('#'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                throw new ToolInputException("'shape' 값은 상자 번호(예: 3)나 title, subtitle, body 중 하나여야 해요.");

            var element = FindById(tree, id);
            if (element == null)
                throw new ToolInputException($"{slide.Number}번 슬라이드에 {id}번 상자가 없어요. {Choices(slide)}");

            if (element is not P.Shape shape)
                throw new ToolInputException($"{id}번은 그림이나 표, 차트라서 글을 바꿀 수 없어요. {Choices(slide)}");

            var placeholder = PptPlaceholder.Of(shape);
            if (placeholder != null && PptPlaceholder.RoleOf(PptPlaceholder.TypeOf(placeholder)) == PptShapeRole.Other)
                throw new ToolInputException($"{id}번은 그림이나 표를 넣는 자리라서 글을 넣을 수 없어요. {Choices(slide)}");

            return shape;
        }

        public static P.TextBody EnsureTextBody(P.Shape shape)
        {
            if (shape.TextBody != null)
            {
                if (!shape.TextBody.Elements<A.Paragraph>().Any())
                    shape.TextBody.AppendChild(PptText.EmptyParagraph());

                return shape.TextBody;
            }

            shape.TextBody = new P.TextBody(new A.BodyProperties(), new A.ListStyle(), PptText.EmptyParagraph());
            return shape.TextBody;
        }

        public static void ResetAutofit(P.Shape shape)
        {
            var autofit = shape?.TextBody?.BodyProperties?.GetFirstChild<A.NormalAutoFit>();
            if (autofit == null)
                return;

            autofit.FontScale = null;
            autofit.LineSpaceReduction = null;
        }

        public static string RoleKey(PptShapeRole role) => role switch
        {
            PptShapeRole.Title => "title",
            PptShapeRole.Subtitle => "subtitle",
            _ => "body",
        };

        public static uint NextShapeId(OpenXmlElement tree)
        {
            uint max = 1;
            foreach (var props in tree.Descendants<P.NonVisualDrawingProperties>())
            {
                var id = props.Id?.Value ?? 0;
                if (id > max)
                    max = id;
            }

            return max + 1;
        }

        public static void AppendToTree(OpenXmlCompositeElement tree, OpenXmlElement element)
        {
            var extensions = tree.ChildElements.LastOrDefault(e => e.LocalName == "extLst");
            if (extensions != null)
                extensions.InsertBeforeSelf(element);
            else
                tree.AppendChild(element);
        }

        public static string Choices(PptSlideRef slide)
        {
            var tree = slide.Tree;
            if (tree == null)
                return string.Empty;

            var items = new List<string>();
            foreach (var shape in tree.Descendants<P.Shape>())
            {
                var placeholder = PptPlaceholder.Of(shape);
                var role = placeholder != null
                    ? PptPlaceholder.RoleOf(PptPlaceholder.TypeOf(placeholder))
                    : shape.TextBody != null ? PptShapeRole.TextBox : PptShapeRole.Other;

                if (role is PptShapeRole.Other or PptShapeRole.Footer)
                    continue;

                var id = shape.NonVisualShapeProperties?.NonVisualDrawingProperties?.Id?.Value ?? 0;
                items.Add($"{id}({PptPlaceholder.KoreanName(role)})");
            }

            return items.Count == 0
                ? "이 슬라이드에는 글을 넣을 상자가 없어요."
                : $"고를 수 있는 상자: {string.Join(", ", items)}.";
        }

        private static PptShapeRole? RoleOf(string key) => key.Trim().ToLowerInvariant() switch
        {
            "title" or "제목" => PptShapeRole.Title,
            "subtitle" or "sub_title" or "sub-title" or "부제목" => PptShapeRole.Subtitle,
            "body" or "content" or "본문" or "내용" => PptShapeRole.Body,
            _ => null,
        };

        private static P.Shape FindByRole(P.ShapeTree tree, PptShapeRole role) =>
            tree.Elements<P.Shape>()
                .Where(s => PptPlaceholder.Of(s) != null && PptPlaceholder.RoleOf(PptPlaceholder.TypeOf(PptPlaceholder.Of(s))) == role)
                .OrderBy(s => PptPlaceholder.IndexOf(PptPlaceholder.Of(s)))
                .FirstOrDefault();

        private static OpenXmlElement FindById(P.ShapeTree tree, uint id)
        {
            foreach (var props in tree.Descendants<P.NonVisualDrawingProperties>())
            {
                if (props.Id?.Value != id)
                    continue;

                var owner = props.Parent?.Parent;
                if (owner is P.Shape or P.Picture or P.GraphicFrame or P.GroupShape or P.ConnectionShape)
                    return owner;
            }

            return null;
        }

        private static P.Shape CreateFromLayout(PresentationDocument document, PptSlideRef slide, PptShapeRole role)
        {
            var styles = new PptSlideStyles(slide.Part, document.PresentationPart.Presentation.DefaultTextStyle);
            var layoutShape = styles.LayoutPlaceholders
                .Where(s => PptPlaceholder.RoleOf(PptPlaceholder.TypeOf(PptPlaceholder.Of(s))) == role)
                .OrderBy(s => PptPlaceholder.IndexOf(PptPlaceholder.Of(s)))
                .FirstOrDefault();

            if (layoutShape == null)
                return null;

            var shape = PptSlideFactory.PlaceholderFrom(layoutShape, NextShapeId(slide.Tree));
            AppendToTree(slide.Tree, shape);
            return shape;
        }
    }
}
