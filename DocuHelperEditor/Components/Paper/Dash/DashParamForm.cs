using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using DocuHelper.Dash.Logic;

namespace DocuHelperEditor.Components.Paper.Dash
{
    internal sealed class DashParamForm
    {
        public const string NumberKind = "number";
        public const string RangeKind = "range";
        public const string PickKind = "pick";
        public const string BoolKind = "bool";

        private const double Tolerance = 1e-9;

        public string Kind { get; init; }

        public string Name { get; set; }

        public string Label { get; set; }

        public string Min { get; set; }

        public string Max { get; set; }

        public string Step { get; set; }

        public string Initial { get; set; }

        public string From { get; set; }

        public string To { get; set; }

        public string Options { get; set; }

        public string OptionsFrom { get; init; }

        public string All { get; set; }

        public string Choice { get; set; }

        public bool On { get; set; }

        public static string KindLabel(string kind) => kind switch
        {
            PickKind => "고르기",
            RangeKind => "범위",
            NumberKind => "숫자",
            BoolKind => "켜고 끄기",
            _ => "조절값",
        };

        public static string Show(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);

        public static DashParamForm Of(DashParam param) => param switch
        {
            NumberParam number => new DashParamForm
            {
                Kind = NumberKind,
                Name = number.Name,
                Label = number.Label,
                Min = Show(number.Min),
                Max = Show(number.Max),
                Step = Show(number.Step),
                Initial = Show(number.Default),
            },
            RangeParam range => new DashParamForm
            {
                Kind = RangeKind,
                Name = range.Name,
                Label = range.Label,
                Min = Show(range.Min),
                Max = Show(range.Max),
                From = Show(range.From),
                To = Show(range.To),
            },
            PickParam pick => new DashParamForm
            {
                Kind = PickKind,
                Name = pick.Name,
                Label = pick.Label,
                Options = string.Join("\n", pick.Options ?? new List<string>()),
                OptionsFrom = pick.OptionsFrom,
                All = pick.All,
                Choice = pick.Default,
            },
            BoolParam flag => new DashParamForm
            {
                Kind = BoolKind,
                Name = flag.Name,
                Label = flag.Label,
                On = flag.Default,
            },
            _ => null,
        };

        public static DashParamForm Read(string kind, IReadOnlyDictionary<string, JsonNode> values)
        {
            string Text(string key) => DashFormBuilder.TextOf(values, key);
            string Number(string key) => DashFormBuilder.NumberOf(values, key) is { } number ? Show(number) : string.Empty;

            return new DashParamForm
            {
                Kind = kind,
                Name = Text("name"),
                Label = Text("label"),
                Min = Number("min"),
                Max = Number("max"),
                Step = Number("step"),
                Initial = Number("initial"),
                From = Number("from"),
                To = Number("to"),
                Options = string.Join("\n", Text("options").Split(',', '\n', '\r')),
                All = Text("all"),
                Choice = Text("choice"),
                On = DashFormBuilder.FlagOf(values, "on"),
            };
        }

        public List<string> OptionList() =>
            (Options ?? string.Empty).Split('\n', '\r')
                .Select(option => option.Trim())
                .Where(option => option.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();

        public string Problem() => Kind switch
        {
            NumberKind => NumberProblem(),
            RangeKind => RangeProblem(),
            PickKind => PickProblem(),
            _ => null,
        };

        public JsonObject Item()
        {
            var item = new JsonObject { ["type"] = Kind, ["name"] = (Name ?? string.Empty).Trim() };
            var label = (Label ?? string.Empty).Trim();
            if (label.Length > 0)
                item["label"] = label;

            switch (Kind)
            {
                case NumberKind:
                    item["min"] = Value(Min);
                    item["max"] = Value(Max);
                    item["step"] = Value(Step);
                    item["default"] = Value(Initial);
                    break;
                case RangeKind:
                    item["min"] = Value(Min);
                    item["max"] = Value(Max);
                    item["from"] = Value(From);
                    item["to"] = Value(To);
                    break;
                case PickKind:
                    item["options"] = Strings(OptionList());
                    if (!string.IsNullOrWhiteSpace(All))
                        item["all"] = All.Trim();

                    if (!string.IsNullOrWhiteSpace(Choice))
                        item["default"] = Choice.Trim();

                    break;
                case BoolKind:
                    item["default"] = On;
                    break;
            }

            return item;
        }

        public JsonObject Patch(DashParam current)
        {
            var patch = new JsonObject();
            var label = (Label ?? string.Empty).Trim();
            if (!string.Equals(label, current.Label ?? string.Empty, StringComparison.Ordinal))
                patch["label"] = label.Length == 0 ? null : label;

            switch (current)
            {
                case NumberParam number:
                    Change(patch, "min", Min, number.Min);
                    Change(patch, "max", Max, number.Max);
                    Change(patch, "step", Step, number.Step);
                    Change(patch, "default", Initial, number.Default);
                    break;
                case RangeParam range:
                    Change(patch, "min", Min, range.Min);
                    Change(patch, "max", Max, range.Max);
                    Change(patch, "from", From, range.From);
                    Change(patch, "to", To, range.To);
                    break;
                case PickParam pick:
                    if (string.IsNullOrEmpty(OptionsFrom) && !OptionList().SequenceEqual(pick.Options ?? new List<string>(), StringComparer.Ordinal))
                        patch["options"] = Strings(OptionList());

                    Change(patch, "all", All, pick.All);
                    Change(patch, "default", Choice, pick.Default);
                    break;
                case BoolParam flag:
                    if (On != flag.Default)
                        patch["default"] = On;

                    break;
            }

            return patch;
        }

        private static void Change(JsonObject patch, string key, string text, double current)
        {
            if (TryNumber(text, out var number) && Math.Abs(number - current) > Tolerance)
                patch[key] = number;
        }

        private static void Change(JsonObject patch, string key, string text, string current)
        {
            var value = (text ?? string.Empty).Trim();
            if (!string.Equals(value, current ?? string.Empty, StringComparison.Ordinal))
                patch[key] = value.Length == 0 ? null : value;
        }

        private static JsonArray Strings(IEnumerable<string> items)
        {
            var array = new JsonArray();
            foreach (var item in items)
                array.Add(item);

            return array;
        }

        private static JsonNode Value(string text) => TryNumber(text, out var number) ? number : 0;

        private static bool TryNumber(string text, out double number) =>
            double.TryParse((text ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out number) && double.IsFinite(number);

        private static string Missing(string label) => $"‘{label}’에는 숫자를 써 주세요.";

        private string NumberProblem()
        {
            if (!TryNumber(Min, out var min))
                return Missing("가장 작은 값");

            if (!TryNumber(Max, out var max))
                return Missing("가장 큰 값");

            if (!TryNumber(Step, out var step))
                return Missing("한 칸 크기");

            if (!TryNumber(Initial, out var initial))
                return Missing("처음 값");

            if (min > max)
                return "가장 작은 값이 가장 큰 값보다 클 수 없어요.";

            if (step <= 0)
                return "한 칸 크기는 0보다 크게 써 주세요.";

            return initial < min || initial > max ? "처음 값은 가장 작은 값과 가장 큰 값 사이에 써 주세요." : null;
        }

        private string RangeProblem()
        {
            if (!TryNumber(Min, out var min))
                return Missing("가장 작은 값");

            if (!TryNumber(Max, out var max))
                return Missing("가장 큰 값");

            if (!TryNumber(From, out var from))
                return Missing("처음 시작");

            if (!TryNumber(To, out var to))
                return Missing("처음 끝");

            if (min > max)
                return "가장 작은 값이 가장 큰 값보다 클 수 없어요.";

            if (from > to)
                return "처음 시작이 처음 끝보다 클 수 없어요.";

            return from < min || to > max ? "처음 시작과 처음 끝은 가장 작은 값과 가장 큰 값 사이에 써 주세요." : null;
        }

        private string PickProblem()
        {
            if (!string.IsNullOrEmpty(OptionsFrom))
                return null;

            var options = OptionList();
            if (options.Count == 0)
                return "고를 것을 하나 이상 써 주세요.";

            var choice = (Choice ?? string.Empty).Trim();
            var all = (All ?? string.Empty).Trim();
            return choice.Length > 0 && !options.Contains(choice) && !string.Equals(choice, all, StringComparison.Ordinal)
                ? "처음 값은 고를 것 중에서 써 주세요."
                : null;
        }
    }
}
