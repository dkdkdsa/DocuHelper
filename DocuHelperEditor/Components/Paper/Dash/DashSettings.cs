using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using DocuHelper.Dash;
using DocuHelper.Dash.Elements;
using DocuHelper.Dash.Logic;
using DocuHelper.Dash.Runtime;
using DocuHelperEditor.State.Papers;
using DocuHelperEditor.State.Ribbon;

namespace DocuHelperEditor.Components.Paper.Dash
{
    internal static class DashSettings
    {
        private const double DefaultSize = 16;
        private const double MaxSize = 500;
        private const string WhenReason = "‘언제 보일지’ 식을 읽을 수 없어요. 예: 보기=\"거래처별\"";
        private const string BoxReason = "너비와 높이는 0보다 크게 써 주세요.";
        private const string SizeReason = "글자 크기는 1에서 500 사이로 써 주세요.";
        private const string UnsetLabel = "정하지 않았어요";
        private const string MissingLabel = "(없는 이름)";

        private static readonly string[] s_none = Array.Empty<string>();
        private static readonly string[] s_boxKeys = { "x", "y", "w", "h" };

        public static (IReadOnlyList<DialogField> Fields, IReadOnlyDictionary<string, JsonNode> Defaults) Form(DashPaperModel model, Element element)
        {
            var document = model.Document;
            var style = new DashScope(model.Runtime).StyleOf(element);
            var fields = new List<DialogField>();
            var defaults = new Dictionary<string, JsonNode>(StringComparer.Ordinal);

            void Text(string name, string label, string value)
            {
                fields.Add(new DialogField(name, label, "text", null, s_none, s_none));
                defaults[name] = JsonValue.Create(value ?? string.Empty);
            }

            void Number(string name, string label, double? value)
            {
                fields.Add(new DialogField(name, label, "number", null, s_none, s_none));
                if (value.HasValue)
                    defaults[name] = JsonValue.Create(value.Value);
            }

            void Color(string name, string label, string value)
            {
                fields.Add(new DialogField(name, label, "color", null, s_none, s_none));
                defaults[name] = JsonValue.Create(Hex(value));
            }

            void Choice(string name, string label, string current, IEnumerable<(string Value, string Label)> options)
            {
                var list = options.ToList();
                if (string.IsNullOrEmpty(current))
                    list.Insert(0, (string.Empty, UnsetLabel));
                else if (!list.Any(option => option.Value == current))
                    list.Insert(0, (current, current + " " + MissingLabel));

                fields.Add(new DialogField(name, label, "choice", null, list.Select(option => option.Value).ToList(), list.Select(option => option.Label).ToList()));
                defaults[name] = JsonValue.Create(current ?? string.Empty);
            }

            Text("name", "이름", element.Name);
            switch (element)
            {
                case ChartElement chart:
                    Choice("data", "표", chart.Data, Tables(document));
                    break;
                case TableElement table:
                    Choice("data", "표", table.Data, Tables(document));
                    break;
                case RepeatElement repeat:
                    Choice("data", "표", repeat.Data, Tables(document));
                    break;
                case SliderElement slider:
                    Choice("param", "조절값", slider.Param, Params<NumberParam>(document));
                    break;
                case RangeSliderElement range:
                    Choice("param", "조절값", range.Param, Params<RangeParam>(document));
                    break;
                case ToggleElement toggle:
                    Choice("param", "조절값", toggle.Param, Params<BoolParam>(document));
                    break;
                case SegmentedElement segmented:
                    Choice("param", "조절값", segmented.Param, Params<PickParam>(document));
                    break;
                case SelectElement dropdown:
                    Choice("param", "조절값", dropdown.Param, Params<PickParam>(document));
                    break;
                case MeterElement meter:
                    Choice("value", "값", meter.Value, Values(document));
                    break;
                case TextElement text when SingleLine(text.Text):
                    Text("text", "글", text.Text);
                    break;
                case ShapeElement shape when SingleLine(shape.Text):
                    Text("text", "글", shape.Text);
                    break;
                case ButtonElement button:
                    Text("label", "단추 글", button.Label);
                    break;
            }

            Text("when", "언제 보일지 (비우면 항상. 예: 보기=\"거래처별\")", element.When);
            if (document.Root.Children.Contains(element))
            {
                Number("x", "왼쪽에서 (px)", style.X ?? 0);
                Number("y", "위에서 (px)", style.Y ?? 0);
                Number("w", "너비 (px)", style.W ?? DashCss.DefaultWidth);
                Number("h", "높이 (px)", style.H ?? DashCss.DefaultHeight);
            }

            Color("fill", "채우기 색 (골라야 바뀌어요)", style.Fill);
            Color("line", "선 색 (골라야 바뀌어요)", style.Line);
            Number("fontSize", "글자 크기", style.FontSize ?? DefaultSize);
            return (fields, defaults);
        }

        public static string Validate(IReadOnlyDictionary<string, JsonNode> values)
        {
            var when = TextOf(values, "when");
            if (!string.IsNullOrWhiteSpace(when) && !DashNames.TryParse(when, out _))
                return WhenReason;

            if (s_boxKeys.Skip(2).Any(key => NumberOf(values, key) is <= 0))
                return BoxReason;

            return NumberOf(values, "fontSize") is < 1 or > MaxSize ? SizeReason : null;
        }

        public static JsonObject Patch(DashPaperModel model, Element element, IReadOnlyList<DialogField> fields, IReadOnlyDictionary<string, JsonNode> values)
        {
            var patch = new JsonObject();
            var style = new JsonObject();
            var effective = new DashScope(model.Runtime).StyleOf(element);
            foreach (var field in fields)
            {
                switch (field.Name)
                {
                    case "name":
                        Change(patch, "name", element.Name, TextOf(values, "name")?.Trim(), true);
                        break;
                    case "when":
                        Change(patch, "when", element.When, TextOf(values, "when")?.Trim(), true);
                        break;
                    case "text":
                        Change(patch, "text", element is TextElement plain ? plain.Text : (element as ShapeElement)?.Text, TextOf(values, "text"), true);
                        break;
                    case "label":
                        Change(patch, "label", (element as ButtonElement)?.Label, TextOf(values, "label"), true);
                        break;
                    case "data":
                    case "param":
                    case "value":
                        Change(patch, field.Name, BindingOf(element), TextOf(values, field.Name), false);
                        break;
                    case "x":
                    case "y":
                    case "w":
                    case "h":
                    case "fontSize":
                        Move(style, field.Name, BoxOf(effective, field.Name), element.Style, NumberOf(values, field.Name));
                        break;
                    case "fill":
                    case "line":
                        Paint(style, field.Name, field.Name == "fill" ? effective.Fill : effective.Line, TextOf(values, field.Name));
                        break;
                }
            }

            if (style.Count > 0)
                patch["style"] = style;

            return patch;
        }

        private static void Change(JsonObject patch, string key, string current, string value, bool removable)
        {
            if (value == null || string.Equals(value, current ?? string.Empty, StringComparison.Ordinal))
                return;

            if (value.Length > 0)
                patch[key] = value;
            else if (removable)
                patch[key] = null;
        }

        private static void Move(JsonObject style, string key, double current, DashStyle own, double? value)
        {
            if (value.HasValue)
            {
                if (Math.Abs(value.Value - current) > 0.0001)
                    style[key] = Math.Round(value.Value, 2);

                return;
            }

            if (OwnHas(own, key))
                style[key] = null;
        }

        private static void Paint(JsonObject style, string key, string current, string value)
        {
            var hex = Hex(value);
            if (hex.Length > 0 && !string.Equals(hex, Hex(current), StringComparison.Ordinal))
                style[key] = "#" + hex;
        }

        private static bool OwnHas(DashStyle own, string key) => key switch
        {
            "x" => own?.X != null,
            "y" => own?.Y != null,
            "w" => own?.W != null,
            "h" => own?.H != null,
            _ => own?.FontSize != null,
        };

        private static double BoxOf(DashStyle style, string key) => key switch
        {
            "x" => style.X ?? 0,
            "y" => style.Y ?? 0,
            "w" => style.W ?? DashCss.DefaultWidth,
            "h" => style.H ?? DashCss.DefaultHeight,
            _ => style.FontSize ?? DefaultSize,
        };

        private static string BindingOf(Element element) => element switch
        {
            ChartElement chart => chart.Data,
            TableElement table => table.Data,
            RepeatElement repeat => repeat.Data,
            SliderElement slider => slider.Param,
            RangeSliderElement range => range.Param,
            ToggleElement toggle => toggle.Param,
            SegmentedElement segmented => segmented.Param,
            SelectElement dropdown => dropdown.Param,
            MeterElement meter => meter.Value,
            _ => null,
        };

        private static bool SingleLine(string text) => text == null || text.IndexOfAny(new[] { '\r', '\n' }) < 0;

        private static string Hex(string color) => (color ?? string.Empty).Trim().TrimStart('#').ToUpperInvariant();

        private static string TextOf(IReadOnlyDictionary<string, JsonNode> values, string key) =>
            values.TryGetValue(key, out var node) && node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

        private static double? NumberOf(IReadOnlyDictionary<string, JsonNode> values, string key) =>
            values.TryGetValue(key, out var node) && node is JsonValue value && value.TryGetValue<double>(out var number) ? number : null;

        private static IEnumerable<(string Value, string Label)> Tables(DashDocument document) =>
            document.Values.OfType<StepsTable>().Where(table => !string.IsNullOrWhiteSpace(table.Name)).Select(table => (table.Name, table.Name + " · 표"))
                .Concat(document.Sources.Where(source => source != null && !string.IsNullOrWhiteSpace(source.Name)).Select(source => (source.Name, source.Name + " · 원본")));

        private static IEnumerable<(string Value, string Label)> Params<T>(DashDocument document) where T : DashParam =>
            document.Params.OfType<T>().Where(param => !string.IsNullOrWhiteSpace(param.Name)).Select(param => (param.Name, param.Name));

        private static IEnumerable<(string Value, string Label)> Values(DashDocument document) =>
            document.Values.Where(value => value != null && value is not StepsTable && !string.IsNullOrWhiteSpace(value.Name)).Select(value => (value.Name, value.Name));
    }
}
