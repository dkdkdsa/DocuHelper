using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocuHelper.Dash;
using DocuHelper.Dash.Logic;
using DocuHelper.Dash.Runtime;

namespace DocuHelperEditor.Components.Paper.Dash
{
    internal static class DashStepText
    {
        private const string UnsetColumn = "정해지지 않은 칸";

        public static string Label(DashStep step) => step switch
        {
            null => "빈 단계",
            AliasStep => "이름 합치기",
            FilterStep filter => Named(filter.Column, "거르기"),
            LookupStep => "가져오기",
            GroupStep group => Grouped(group),
            ShareStep => "비율 셈",
            CompareStep compare => Compared(compare),
            SortStep sort => Named(sort.Column, "줄 세우기"),
            LimitStep limit => limit.Count.ToString(CultureInfo.InvariantCulture) + "개만",
            HideStep => "칸 숨기기",
            TotalStep => "합계 줄",
            CalcStep calc => Named(calc.As, "셈"),
            _ => "모르는 단계",
        };

        public static string Sentence(DashStep step) => step switch
        {
            null => "비어 있는 단계예요. 빼도 괜찮아요.",
            AliasStep alias => Alias(alias),
            FilterStep filter => Filter(filter),
            LookupStep lookup => Lookup(lookup),
            GroupStep group => Group(group),
            ShareStep share => Share(share),
            CompareStep compare => Compare(compare),
            SortStep sort => $"{Col(sort.Column)}을 {(sort.Descending ? "큰 것부터" : "작은 것부터")} 줄 세워요.",
            LimitStep limit => Limit(limit),
            HideStep hide => Hide(hide),
            TotalStep total => Total(total),
            CalcStep calc => Calc(calc),
            _ => "이 앱이 모르는 단계예요. 셈할 때는 건너뛰어요.",
        };

        public static string Origin(DashDocument document, StepsTable table)
        {
            if (string.IsNullOrWhiteSpace(table.Source))
                return "가져올 곳이 아직 정해지지 않았어요.";

            var kind = document.FindSource(table.Source) != null ? "원본" : "표";
            return $"{Q(table.Source)} {kind}에서 줄을 가져와요.";
        }

        public static string Source(DashSource source)
        {
            var file = Q(source.File);
            return source switch
            {
                ExcelSheetSource excel => string.IsNullOrWhiteSpace(excel.Sheet)
                    ? $"{file} 파일의 첫 시트를 읽어요. 머리글은 {Number(excel.Header)}번째 줄이에요."
                    : $"{file} 파일의 {Q(excel.Sheet)} 시트를 읽어요. 머리글은 {Number(excel.Header)}번째 줄이에요.",
                WordTableSource word => $"{file} 파일의 {Number(word.Table)}번째 표를 읽어요.",
                PptTableSource table => $"{file} 파일의 {Number(table.Slide)}번 슬라이드, {Number(table.Table)}번째 표를 읽어요.",
                PptChartSource chart => $"{file} 파일의 {Number(chart.Slide)}번 슬라이드, {Number(chart.Chart)}번째 차트를 읽어요.",
                _ => "이 앱이 모르는 종류의 원본이에요.",
            };
        }

        public static string Value(DashValue value)
        {
            switch (value)
            {
                case ExpressionValue:
                    return "엑셀 수식처럼 쓴 식으로 셈한 값이에요.";
                case PickValue pick:
                    return Picked(pick);
                case FixedValue fixedValue:
                    var text = fixedValue.Number.HasValue ? DashFormats.Group(fixedValue.Number.Value) : fixedValue.Text ?? string.Empty;
                    var noun = fixedValue.Number.HasValue ? "숫자" : "글";
                    var shown = fixedValue.Number.HasValue ? text : Q(text);
                    return $"정해 둔 {noun} {shown}{KoreanParticles.Attach(text, "이에요.")}";
                default:
                    return "이 앱이 모르는 종류의 값이에요.";
            }
        }

        private static string Picked(PickValue pick)
        {
            var place = pick.Row >= 0
                ? $"{Number(pick.Row)}번째 줄"
                : $"뒤에서 {Number(-pick.Row)}번째 줄";
            return $"{Q(pick.Table)} 표의 {place}에서 {Col(pick.Column)}의 값을 가져와요.";
        }

        private static string Alias(AliasStep step)
        {
            if (string.IsNullOrWhiteSpace(step.Column))
                return "이름을 합칠 칸이 아직 정해지지 않았어요.";

            var count = step.Map?.Count ?? 0;
            return count == 0
                ? $"{Col(step.Column)}에서 합칠 이름이 아직 없어요."
                : $"{Col(step.Column)}에서 다르게 적힌 이름 {Number(count)}개를 하나로 합쳐요.";
        }

        private static string Filter(FilterStep step)
        {
            var day = string.Equals(step.Part, "day", StringComparison.OrdinalIgnoreCase);
            var column = Col(step.Column);
            var parts = new List<string>();
            foreach (var param in step.Uses().Distinct(StringComparer.Ordinal))
            {
                parts.Add(day
                    ? $"{column}에서 몇 일인지만 보고 {Q(param)} 조절값에 맞춰 걸러요."
                    : $"{column}을 {Q(param)} 조절값에 맞춰 걸러요.");
            }

            if (step.Eq != null && DashNames.ParamOf(step.Eq) == null)
                parts.Add($"{column}이 {Q(step.Eq)}인 줄만 남겨요.");

            var listed = Listed(step.In);
            if (listed.Count > 0)
                parts.Add($"{column}이 {string.Join(", ", listed.Select(Q))} 중 하나인 줄만 남겨요.");

            if (step.Min.HasValue || step.Max.HasValue)
                parts.Add(Between(day ? column + "의 일" : column, step.Min, step.Max));

            return parts.Count == 0 ? "거를 조건이 아직 없어서 모든 줄을 그대로 둬요." : string.Join(" ", parts);
        }

        private static string Between(string subject, double? min, double? max)
        {
            if (min.HasValue && max.HasValue)
                return $"{subject}이 {DashFormats.Group(min.Value)} 이상 {DashFormats.Group(max.Value)} 이하인 줄만 남겨요.";

            return min.HasValue
                ? $"{subject}이 {DashFormats.Group(min.Value)} 이상인 줄만 남겨요."
                : $"{subject}이 {DashFormats.Group(max.Value)} 이하인 줄만 남겨요.";
        }

        private static string Lookup(LookupStep step)
        {
            if (string.IsNullOrWhiteSpace(step.From))
                return "가져올 표가 아직 정해지지 않았어요.";

            var foreign = string.IsNullOrWhiteSpace(step.ForeignKey) ? step.Key : step.ForeignKey;
            var match = string.Equals(foreign, step.Key, StringComparison.Ordinal)
                ? $"{Col(step.Key)}이 같은 줄을"
                : $"{Col(foreign)}이 이쪽 {Col(step.Key)}과 같은 줄을";
            var taken = Listed(step.Take);
            var what = step.Take == null ? "나머지 칸을 모두" : taken.Count == 0 ? "가져올 칸이 없어서 아무것도" : Cols(taken) + "을";
            return $"{Q(step.From)} 표에서 {match} 찾아 {what} 가져와요.";
        }

        private static string Group(GroupStep step)
        {
            var by = Listed(step.By);
            if (by.Count == 0)
                return "묶을 칸이 아직 정해지지 않았어요.";

            var sums = Listed(step.Sum);
            var counted = !string.IsNullOrWhiteSpace(step.Count);
            var lead = $"{Cols(by)}별로 묶어";
            if (sums.Count == 0 && !counted)
                return lead + "요.";

            if (sums.Count == 0)
                return $"{lead}서 줄 수를 {Q(step.Count)} 칸에 적어요.";

            return counted
                ? $"{lead}서 {Cols(sums)}은 더하고, 줄 수는 {Q(step.Count)} 칸에 적어요."
                : $"{lead}서 {Cols(sums)}은 더해요.";
        }

        private static string Share(ShareStep step)
        {
            if (string.IsNullOrWhiteSpace(step.Column))
                return "비율을 낼 칸이 아직 정해지지 않았어요.";

            var name = string.IsNullOrWhiteSpace(step.As) ? step.Column.Trim() + " 비율" : step.As.Trim();
            return $"{Col(step.Column)}이 전체에서 차지하는 비율을 {Q(name)} 칸에 셈해요.";
        }

        private static string Compare(CompareStep step)
        {
            if (string.IsNullOrWhiteSpace(step.Source))
                return "견줄 표가 아직 정해지지 않았어요.";

            var columns = Listed(step.Columns);
            var what = columns.Count == 0 ? "숫자 칸을 모두" : Cols(columns) + "을";
            var keys = Listed(step.Key);
            var pairing = keys.Count == 0 ? "이름이 같은 칸을 기준으로 짝지어요." : $"짝은 {Cols(keys)}이 같은 줄로 맞춰요.";
            return $"{Q(step.Source)} 표의 {what} 옆에 붙여서 견줘요. {pairing}";
        }

        private static string Limit(LimitStep step)
        {
            var text = $"앞에서 {Number(step.Count)}개 줄만 남겨요.";
            return string.IsNullOrEmpty(step.Rest) ? text : text + $" 나머지는 {Q(step.Rest)} 한 줄로 합쳐요.";
        }

        private static string Hide(HideStep step)
        {
            var columns = Listed(step.Columns);
            return columns.Count == 0 ? "숨길 칸이 아직 없어요." : $"{Cols(columns)}을 숨겨요.";
        }

        private static string Total(TotalStep step)
        {
            var label = string.IsNullOrWhiteSpace(step.Label) ? "합계" : step.Label.Trim();
            var columns = Listed(step.Columns);
            var target = columns.Count == 0 ? "숫자 칸마다" : Cols(columns) + "마다";
            var what = (step.Op ?? "sum").Trim().ToLowerInvariant() switch
            {
                "avg" => "평균을",
                "count" => "개수를",
                "min" => "가장 작은 값을",
                "max" => "가장 큰 값을",
                _ => "더한 값을",
            };
            return $"맨 아래에 {Q(label)} 줄을 더하고, {target} {what} 적어요.";
        }

        private static string Calc(CalcStep step)
        {
            var target = string.IsNullOrWhiteSpace(step.As) ? "새 칸" : $"새 칸 {Q(step.As)}";
            return string.IsNullOrWhiteSpace(step.Expression)
                ? $"{target}에 넣을 식이 아직 없어요."
                : $"{target}에 {Q(step.Expression)} 식의 결과를 넣어요.";
        }

        private static string Grouped(GroupStep step)
        {
            var by = Listed(step.By);
            return by.Count == 0 ? "묶기" : string.Join(", ", by) + "별로 묶기";
        }

        private static string Compared(CompareStep step)
        {
            if (string.IsNullOrWhiteSpace(step.Source))
                return "견주기";

            var source = step.Source.Trim();
            return source + KoreanParticles.Attach(source, "와 견주기");
        }

        private static string Named(string column, string rest) =>
            string.IsNullOrWhiteSpace(column) ? rest : column.Trim() + " " + rest;

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Q(string text) => "‘" + (text ?? string.Empty).Trim() + "’";

        private static string Col(string column) => string.IsNullOrWhiteSpace(column) ? UnsetColumn : Q(column) + " 칸";

        private static string Cols(IEnumerable<string> names) => string.Join(", ", names.Select(Q)) + " 칸";

        private static List<string> Listed(IEnumerable<string> names) =>
            (names ?? Enumerable.Empty<string>()).Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()).ToList();
    }
}
