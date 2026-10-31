using System.Collections.Generic;
using System.Linq;

namespace DocuHelper.Tools.Ppt
{
    public static class PptChartKinds
    {
        public static readonly IReadOnlyList<PptChartKind> All = new[]
        {
            new PptChartKind("column", "barChart", "col", "clustered", "세로 막대 차트"),
            new PptChartKind("stacked_column", "barChart", "col", "stacked", "누적 세로 막대 차트"),
            new PptChartKind("bar", "barChart", "bar", "clustered", "가로 막대 차트"),
            new PptChartKind("stacked_bar", "barChart", "bar", "stacked", "누적 가로 막대 차트"),
            new PptChartKind("line", "lineChart", null, "standard", "꺾은선형 차트"),
            new PptChartKind("pie", "pieChart", null, null, "원형 차트"),
            new PptChartKind("doughnut", "doughnutChart", null, null, "도넛형 차트"),
            new PptChartKind("area", "areaChart", null, "standard", "영역형 차트"),
        };

        public static PptChartKind Find(string token)
        {
            var key = PptInputs.Normalize(token);
            return All.FirstOrDefault(k => k.Token == key)
                ?? throw new ToolInputException($"'chart_type' 값은 {string.Join(", ", All.Select(k => k.Token))} 중 하나여야 해요.");
        }

        public static PptChartKind Of(PptChartInfo info)
        {
            var token = info.Type switch
            {
                "column" or "bar" => (info.Stacked ? "stacked_" : string.Empty) + info.Type,
                "line" or "pie" or "doughnut" or "area" => info.Type,
                _ => null,
            };

            return token == null ? null : All.First(k => k.Token == token);
        }
    }
}
