using System.Linq;
using DocumentFormat.OpenXml;
using C = DocumentFormat.OpenXml.Drawing.Charts;

namespace DocuHelper.Tools.Ppt
{
    public static class PptChartEdits
    {
        private static readonly string[] s_afterLabels = { "trendline", "errBars", "cat", "val", "xVal", "yVal", "bubbleSize", "shape", "smooth", "extLst" };

        public static void SetTitle(C.Chart chart, string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                chart.Title?.Remove();
                if (chart.AutoTitleDeleted == null)
                    chart.PrependChild(new C.AutoTitleDeleted { Val = true });
                else
                    chart.AutoTitleDeleted.Val = true;

                return;
            }

            var made = new C.Title(PptChartBuilder.WithNamespaces(PptChartBuilder.TitleXml(title)));
            if (chart.Title != null)
            {
                chart.Title.GetFirstChild<C.ChartText>()?.Remove();
                chart.Title.PrependChild(made.GetFirstChild<C.ChartText>().CloneNode(true));
            }
            else
            {
                chart.PrependChild(made);
            }

            if (chart.AutoTitleDeleted == null)
                chart.Title.InsertAfterSelf(new C.AutoTitleDeleted { Val = false });
            else
                chart.AutoTitleDeleted.Val = false;
        }

        public static void SetLegend(C.Chart chart, string legend)
        {
            if (legend == "none")
            {
                chart.Legend?.Remove();
                return;
            }

            var position = legend switch
            {
                "bottom" => C.LegendPositionValues.Bottom,
                "right" => C.LegendPositionValues.Right,
                "top" => C.LegendPositionValues.Top,
                _ => C.LegendPositionValues.Left,
            };

            if (chart.Legend == null)
            {
                chart.PlotArea.InsertAfterSelf(new C.Legend(PptChartBuilder.WithNamespaces(PptChartBuilder.LegendXml(legend))));
                return;
            }

            var existing = chart.Legend.GetFirstChild<C.LegendPosition>();
            if (existing == null)
                chart.Legend.PrependChild(new C.LegendPosition { Val = position });
            else
                existing.Val = position;
        }

        public static void SetDataLabels(C.Chart chart, bool on)
        {
            foreach (var labels in chart.PlotArea.Descendants<C.DataLabels>().ToList())
                labels.Remove();

            if (!on)
                return;

            foreach (var series in chart.PlotArea.ChildElements.SelectMany(g => g.ChildElements).Where(e => e.LocalName == "ser").ToList())
            {
                var labels = new C.DataLabels(PptChartBuilder.WithNamespaces(PptChartBuilder.DataLabelsXml()));
                var anchor = series.ChildElements.FirstOrDefault(e => s_afterLabels.Contains(e.LocalName));
                if (anchor != null)
                    anchor.InsertBeforeSelf(labels);
                else
                    series.AppendChild(labels);
            }
        }
    }
}
