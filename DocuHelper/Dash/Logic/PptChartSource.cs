using System;
using System.Globalization;
using System.Linq;
using DocuHelper.Dash.Runtime;
using DocuHelper.Docus;
using DocuHelper.Tools.Ppt;
using DocumentFormat.OpenXml.Packaging;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Dash.Logic
{
    public sealed class PptChartSource : DashSource
    {
        public int Slide { get; set; } = 1;

        public int Chart { get; set; } = 1;

        public override DashTable Read(Docu docu)
        {
            if (docu is not PptDocu ppt)
                throw new DashException($"‘{File}’은 파워포인트 파일이 아니에요.");

            var slides = PptSlides.List(ppt.Document);
            if (Slide < 1 || Slide > slides.Count)
                throw new DashException($"‘{File}’ 파일에서 {Slide}번 슬라이드를 찾을 수 없어요. 슬라이드는 {slides.Count}장이에요.");

            var slide = slides[Slide - 1];
            var charts = PptTargets.All(slide.Tree)
                .OfType<P.GraphicFrame>()
                .Select(frame => frame.Graphic?.GraphicData?.GetFirstChild<C.ChartReference>()?.Id?.Value)
                .Where(id => !string.IsNullOrEmpty(id))
                .Select(id => slide.Part.TryGetPartById(id, out var part) ? part as ChartPart : null)
                .Where(part => part != null)
                .ToList();

            if (Chart < 1 || Chart > charts.Count)
                throw new DashException(charts.Count == 0
                    ? $"{Slide}번 슬라이드에는 차트가 없어요."
                    : $"{Slide}번 슬라이드에는 차트가 {charts.Count}개뿐이라 {Chart}번째 차트를 가져올 수 없어요.");

            var info = PptChartReader.Read(charts[Chart - 1])
                ?? throw new DashException($"{Slide}번 슬라이드의 {Chart}번째 차트에서 자료를 읽을 수 없어요.");

            var headers = new[] { "항목" }.Concat(info.Series.Select(series => series.Name)).ToList();
            var table = new DashTable(DashTables.Names(headers, i => "계열 " + i.ToString(CultureInfo.InvariantCulture)));
            var count = info.Series.Count == 0 ? info.Categories.Count : Math.Max(info.Categories.Count, info.Series.Max(series => series.Values.Count));
            for (var row = 0; row < count; row++)
            {
                var values = new object[headers.Count];
                values[0] = row < info.Categories.Count ? info.Categories[row] : null;
                for (var i = 0; i < info.Series.Count; i++)
                    values[i + 1] = row < info.Series[i].Values.Count ? info.Series[i].Values[row] : null;

                table.Rows.Add(values);
            }

            return table;
        }
    }
}
