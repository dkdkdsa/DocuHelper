using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using DocuHelper.Dash.Logic;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Elements
{
    public sealed class ChartElement : Element
    {
        public string Data { get; set; }

        [JsonPropertyName("kind")]
        public string ChartKind { get; set; }

        public string Category { get; set; }

        public List<string> Values { get; set; }

        public string Emphasis { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Labels { get; set; }

        public string Range { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool RangeControl { get; set; }

        public override IEnumerable<string> Uses() => With(new[] { Data, Range });

        public override void Validate(DashCheck check)
        {
            base.Validate(check);
            check.RequireTable(this, Data);
            if (Range != null)
                check.RequireParam(this, Range, typeof(RangeParam));
            else if (RangeControl)
                check.Fail($"‘{check.Label(this)}’ 차트의 범위 조절을 쓰려면 ‘range’에 범위 값 이름이 필요해요.");
        }

        public override ElementResult Resolve(DashScope scope)
        {
            var result = scope.Base(this, new ChartResult());
            result.ChartKind = ChartKind ?? "column";
            result.Labels = Labels;

            var table = scope.Table(Data);
            if (table == null)
            {
                result.Problem = $"‘{Data}’ 표를 가져오지 못했어요.";
                return result;
            }

            var category = string.IsNullOrWhiteSpace(Category) ? 0 : table.IndexOf(Category);
            if (category < 0 || table.Columns.Count == 0)
            {
                result.Problem = table.Columns.Count == 0 ? $"‘{Data}’ 표에 칸이 없어요." : $"‘{Category}’ 칸이 없어요.";
                return result;
            }

            var series = new List<int>();
            if (Values != null && Values.Count > 0)
            {
                foreach (var name in Values)
                {
                    var at = table.IndexOf(name);
                    if (at < 0)
                    {
                        result.Problem = $"‘{name}’ 칸이 없어요.";
                        return result;
                    }

                    series.Add(at);
                }
            }
            else
            {
                for (var at = 0; at < table.Columns.Count; at++)
                {
                    if (at != category && DashTables.HasNumber(table, at))
                        series.Add(at);
                }
            }

            var param = string.IsNullOrWhiteSpace(Range) ? null : scope.Document.FindParam(Range) as RangeParam;
            var range = param == null ? null : scope.Runtime.State.Get(Range) as DashRange;

            foreach (var row in table.Rows)
            {
                var raw = DashTable.Get(row, category);
                var day = DashCells.Day(raw);
                result.Categories.Add(DashFormats.Display(raw, null));
                result.Positions.Add(day);
                result.Dimmed.Add(range != null && day.HasValue && !range.Contains(day.Value));
                if (OnClick != null)
                    result.RowItems.Add(table.ValuesOf(row));
            }

            foreach (var column in series)
            {
                var item = new ChartSeriesResult { Name = table.Columns[column] };
                foreach (var row in table.Rows)
                {
                    var number = DashCells.Number(DashTable.Get(row, column));
                    item.Values.Add(number);
                    item.Labels.Add(number.HasValue ? DashFormats.Group(number.Value) : string.Empty);
                }

                result.Series.Add(item);
            }

            result.Emphasis = EmphasisIndex(result);
            if (RangeControl && param != null)
            {
                var current = range ?? new DashRange(param.From, param.To);
                result.Control = new RangeSliderResult
                {
                    Source = this,
                    Key = result.Key + ":range",
                    Visible = result.Visible,
                    Style = new DashStyle(),
                    Param = Range,
                    Label = param.Label,
                    Min = param.Min,
                    Max = param.Max,
                    Step = param.Step,
                    From = current.From,
                    To = current.To,
                    Text = param.Display(current),
                };
            }

            return result;
        }

        private int EmphasisIndex(ChartResult result)
        {
            if (string.Equals(Emphasis, "last", StringComparison.OrdinalIgnoreCase))
                return result.Categories.Count - 1;

            var largest = string.Equals(Emphasis, "max", StringComparison.OrdinalIgnoreCase);
            if ((!largest && !string.Equals(Emphasis, "min", StringComparison.OrdinalIgnoreCase)) || result.Series.Count == 0)
                return -1;

            var values = result.Series[0].Values;
            var best = -1;
            for (var i = 0; i < values.Count; i++)
            {
                if (values[i].HasValue && (best < 0 || (largest ? values[i].Value > values[best].Value : values[i].Value < values[best].Value)))
                    best = i;
            }

            return best;
        }
    }
}
