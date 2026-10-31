using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using DocumentFormat.OpenXml;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public static class PptTargets
    {
        public const int MaxTargets = 200;

        private const int MaxChoices = 30;

        public static OpenXmlCompositeElement Find(PptSlideRef slide, string key)
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
                if (found == null)
                    throw new ToolInputException($"{slide.Number}번 슬라이드에는 {PptPlaceholder.KoreanName(role.Value)} 상자가 없어요. {Choices(slide)}");

                return found;
            }

            if (!uint.TryParse(key.Trim().TrimStart('#'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                throw new ToolInputException("'shape' 값은 도형 번호(예: 3)나 title, subtitle, body 중 하나여야 해요.");

            var element = FindById(tree, id);
            if (element == null)
                throw new ToolInputException($"{slide.Number}번 슬라이드에 {id}번 도형이 없어요. {Choices(slide)}");

            return element;
        }

        public static List<OpenXmlCompositeElement> FindMany(PptSlideRef slide, JsonElement value)
        {
            var keys = new List<string>();
            switch (value.ValueKind)
            {
                case JsonValueKind.String:
                    keys.AddRange((value.GetString() ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    break;
                case JsonValueKind.Number:
                    keys.Add(value.GetRawText());
                    break;
                case JsonValueKind.Array:
                    foreach (var item in value.EnumerateArray())
                    {
                        if (item.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                            keys.Add(ToolInput.AsText(item));
                        else
                            throw new ToolInputException("'shapes' 값은 도형 번호 목록이어야 해요.");
                    }

                    break;
                default:
                    throw new ToolInputException("'shapes' 값은 도형 번호나 도형 번호 목록이어야 해요.");
            }

            if (keys.Count == 0)
                throw new ToolInputException("도형 번호를 하나 이상 알려 주세요.");

            if (keys.Count > MaxTargets)
                throw new ToolInputException($"한 번에 도형 {MaxTargets}개까지만 고를 수 있어요.");

            var result = new List<OpenXmlCompositeElement>();
            foreach (var key in keys)
            {
                var element = Find(slide, key);
                if (!result.Contains(element))
                    result.Add(element);
            }

            return result;
        }

        public static List<OpenXmlCompositeElement> FindMany(PptSlideRef slide, ToolInput input, string name)
        {
            if (!input.Has(name))
                throw new ToolInputException($"'{name}' 값이 필요해요.");

            return FindMany(slide, input.Get(name));
        }

        public static bool IsTarget(OpenXmlElement element) =>
            element is P.Shape or P.Picture or P.GraphicFrame or P.GroupShape or P.ConnectionShape;

        public static IEnumerable<OpenXmlCompositeElement> All(OpenXmlElement container)
        {
            if (container == null)
                yield break;

            foreach (var child in container.ChildElements)
            {
                if (IsTarget(child))
                {
                    yield return (OpenXmlCompositeElement)child;
                    if (child is P.GroupShape)
                    {
                        foreach (var nested in All(child))
                            yield return nested;
                    }
                }
                else if (child is AlternateContent)
                {
                    var choice = child.GetFirstChild<AlternateContentChoice>();
                    foreach (var nested in All(choice))
                        yield return nested;
                }
            }
        }

        public static IEnumerable<OpenXmlCompositeElement> TopLevel(OpenXmlElement container) =>
            container == null
                ? Enumerable.Empty<OpenXmlCompositeElement>()
                : container.ChildElements.Where(IsTarget).Cast<OpenXmlCompositeElement>();

        public static P.NonVisualDrawingProperties PropertiesOf(OpenXmlElement target) => target switch
        {
            P.Shape shape => shape.NonVisualShapeProperties?.NonVisualDrawingProperties,
            P.Picture picture => picture.NonVisualPictureProperties?.NonVisualDrawingProperties,
            P.GraphicFrame frame => frame.NonVisualGraphicFrameProperties?.NonVisualDrawingProperties,
            P.GroupShape group => group.NonVisualGroupShapeProperties?.NonVisualDrawingProperties,
            P.ConnectionShape connector => connector.NonVisualConnectionShapeProperties?.NonVisualDrawingProperties,
            _ => null,
        };

        public static uint IdOf(OpenXmlElement target) => PropertiesOf(target)?.Id?.Value ?? 0;

        public static string NameOf(OpenXmlElement target) => PropertiesOf(target)?.Name?.Value;

        public static P.PlaceholderShape PlaceholderOf(OpenXmlElement target) => target switch
        {
            P.Shape shape => shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape,
            P.Picture picture => picture.NonVisualPictureProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape,
            P.GraphicFrame frame => frame.NonVisualGraphicFrameProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape,
            _ => null,
        };

        public static PptShapeKind KindOf(OpenXmlElement target)
        {
            switch (target)
            {
                case P.ConnectionShape:
                    return PptShapeKind.Line;
                case P.Picture:
                    return PptShapeKind.Picture;
                case P.GroupShape:
                    return PptShapeKind.Group;
                case P.GraphicFrame frame:
                    var data = frame.Graphic?.GraphicData;
                    if (data?.GetFirstChild<C.ChartReference>() != null)
                        return PptShapeKind.Chart;

                    return data?.GetFirstChild<A.Table>() != null ? PptShapeKind.Table : PptShapeKind.Other;
                case P.Shape shape:
                    var placeholder = PptPlaceholder.Of(shape);
                    if (placeholder != null)
                    {
                        var role = PptPlaceholder.RoleOf(PptPlaceholder.TypeOf(placeholder));
                        return role == PptShapeRole.Other ? PptShapeKind.Other : PptShapeKind.Text;
                    }

                    if (shape.NonVisualShapeProperties?.NonVisualShapeDrawingProperties?.TextBox?.Value == true)
                        return PptShapeKind.Text;

                    return shape.ShapeProperties?.GetFirstChild<A.PresetGeometry>()?.Preset?.InnerText == "line"
                        ? PptShapeKind.Line
                        : PptShapeKind.Shape;
                default:
                    return PptShapeKind.Other;
            }
        }

        public static string KoreanName(OpenXmlElement target)
        {
            if (target is P.Shape shape)
            {
                var placeholder = PptPlaceholder.Of(shape);
                if (placeholder != null)
                {
                    var role = PptPlaceholder.RoleOf(PptPlaceholder.TypeOf(placeholder));
                    if (role != PptShapeRole.Other)
                        return PptPlaceholder.KoreanName(role);
                }
            }

            return KindOf(target) switch
            {
                PptShapeKind.Text => "글상자",
                PptShapeKind.Shape => "도형",
                PptShapeKind.Line => "선",
                PptShapeKind.Picture => "그림",
                PptShapeKind.Table => "표",
                PptShapeKind.Chart => "차트",
                PptShapeKind.Group => "묶음",
                _ => "개체",
            };
        }

        public static string Choices(PptSlideRef slide)
        {
            var items = All(slide.Tree)
                .Where(t => !IsFooterPlaceholder(t))
                .Select(t => $"{IdOf(t).ToString(CultureInfo.InvariantCulture)}({KoreanName(t)})")
                .ToList();

            if (items.Count == 0)
                return "이 슬라이드에는 고를 수 있는 도형이 없어요.";

            return items.Count > MaxChoices
                ? $"고를 수 있는 도형: {string.Join(", ", items.Take(MaxChoices))} 외 {items.Count - MaxChoices}개."
                : $"고를 수 있는 도형: {string.Join(", ", items)}.";
        }

        private static bool IsFooterPlaceholder(OpenXmlElement target) =>
            target is P.Shape shape
            && PptPlaceholder.Of(shape) != null
            && PptPlaceholder.RoleOf(PptPlaceholder.TypeOf(PptPlaceholder.Of(shape))) == PptShapeRole.Footer;

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

        private static OpenXmlCompositeElement FindById(P.ShapeTree tree, uint id)
        {
            foreach (var target in All(tree))
            {
                if (IdOf(target) == id)
                    return target;
            }

            return null;
        }
    }
}
