using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using DocuHelper.Tools.Excel;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.Components.Paper.Excel
{
    public static class SheetChartSvg
    {
        private const double Pad = 8;
        private const double PixelsPerPoint = 96.0 / 72.0;

        public static string Build(SheetChartModel chart, double width, double height, string fontFamily)
        {
            var w = Math.Max(40, width);
            var h = Math.Max(30, height);
            var svg = new Svg(chart, w, h, fontFamily);
            svg.Draw();
            return svg.ToString();
        }

        private sealed class Svg
        {
            private readonly SheetChartModel _chart;
            private readonly double _w;
            private readonly double _h;
            private readonly string _font;
            private readonly StringBuilder _out = new();
            private readonly double _text;
            private readonly string _ink;

            public Svg(SheetChartModel chart, double w, double h, string font)
            {
                _chart = chart;
                _w = w;
                _h = h;
                _font = font;
                _text = Math.Max(6, chart.FontSize * PixelsPerPoint);
                _ink = "#" + (chart.TextColor ?? "595959");
            }

            private bool IsPie => _chart.Type is SheetChartType.Pie or SheetChartType.Doughnut;

            private bool IsBar => _chart.Type == SheetChartType.Bar;

            private bool VaryPoints => _chart.VaryColors && _chart.Series.Count == 1 && _chart.Type is not SheetChartType.Area and not SheetChartType.Scatter;

            public override string ToString() => _out.ToString();

            public void Draw()
            {
                _out.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 ").Append(N(_w)).Append(' ').Append(N(_h))
                    .Append("\" style=\"font-family: ").Append(Attr(_font)).Append("\">");
                var radius = _chart.Rounded ? Math.Min(10, Math.Min(_w, _h) / 8) : 0;
                var fill = _chart.ChartFill == null ? "none" : "#" + _chart.ChartFill;
                var stroke = _chart.ChartBorder == null ? "none" : "#" + _chart.ChartBorder;
                _out.Append("<rect x=\"0.5\" y=\"0.5\" width=\"").Append(N(_w - 1)).Append("\" height=\"").Append(N(_h - 1)).Append("\" rx=\"").Append(N(radius))
                    .Append("\" fill=\"").Append(fill).Append("\" stroke=\"").Append(stroke).Append("\" stroke-width=\"1\"/>");

                var top = Pad;
                if (!string.IsNullOrWhiteSpace(_chart.Title))
                {
                    var size = Math.Max(7, _chart.TitleSize * PixelsPerPoint);
                    var title = Fit(_chart.Title, size, _w - (2 * Pad));
                    Text(_w / 2, top + 4 + (size * 0.95), title, size, "middle", _chart.TitleBold);
                    if (!_chart.TitleOverlay)
                        top += (size * 1.25) + 10;
                }

                var area = new Box(Pad, top, _w - Pad, _h - Pad);
                area = DrawLegend(area);
                if (IsPie)
                    DrawPie(area);
                else
                    DrawAxes(area);

                _out.Append("</svg>");
            }

            private List<(string Label, string Color, SheetChartSeries Series)> LegendItems()
            {
                if (IsPie || VaryPoints)
                {
                    var series = _chart.Series.FirstOrDefault();
                    return _chart.Categories.Select((c, i) => (c, PointColor(series, i), (SheetChartSeries)null)).ToList();
                }

                var items = _chart.Series.Select((s, i) => (s.Name ?? "계열" + (i + 1).ToString(CultureInfo.InvariantCulture), s.ColorHex, s)).ToList();
                var stacked = _chart.Subtype != SheetChartSubtype.Clustered && _chart.Type is SheetChartType.Column or SheetChartType.Area or SheetChartType.Bar;
                if ((IsBar || stacked) && _chart.Legend is SheetChartLegend.Right or SheetChartLegend.Left)
                    items.Reverse();

                return items;
            }

            private Box DrawLegend(Box area)
            {
                if (_chart.Legend == SheetChartLegend.None)
                    return area;

                var items = LegendItems();
                if (items.Count == 0)
                    return area;

                var size = _text;
                var key = _chart.Type is SheetChartType.Line or SheetChartType.Scatter ? 20.0 : 7.0;
                var row = size * 1.45;
                if (_chart.Legend is SheetChartLegend.Right or SheetChartLegend.Left)
                {
                    var labelWidth = Math.Min(area.Width * 0.4, items.Max(i => Measure(i.Label, size)));
                    var boxWidth = key + 4 + labelWidth;
                    var visible = Math.Max(1, Math.Min(items.Count, (int)(area.Height / row)));
                    var y = area.Top + ((area.Height - (visible * row)) / 2);
                    var x = _chart.Legend == SheetChartLegend.Right ? area.Right - boxWidth : area.Left;
                    for (var i = 0; i < visible; i++)
                    {
                        LegendKey(x, y + (row / 2), key, items[i].Color, items[i].Series);
                        Text(x + key + 4, y + (row / 2) + (size * 0.35), Fit(items[i].Label, size, labelWidth), size, "start", false);
                        y += row;
                    }

                    return _chart.Legend == SheetChartLegend.Right
                        ? new Box(area.Left, area.Top, area.Right - boxWidth - 8, area.Bottom)
                        : new Box(area.Left + boxWidth + 8, area.Top, area.Right, area.Bottom);
                }

                var widths = items.Select(i => key + 4 + Measure(i.Label, size) + 12).ToList();
                var total = widths.Sum() - 12;
                var start = area.Left + Math.Max(0, (area.Width - total) / 2);
                var lineY = _chart.Legend == SheetChartLegend.Top ? area.Top + (row / 2) : area.Bottom - (row / 2);
                var cursor = start;
                for (var i = 0; i < items.Count && cursor < area.Right; i++)
                {
                    LegendKey(cursor, lineY, key, items[i].Color, items[i].Series);
                    Text(cursor + key + 4, lineY + (size * 0.35), items[i].Label, size, "start", false);
                    cursor += widths[i];
                }

                return _chart.Legend == SheetChartLegend.Top
                    ? new Box(area.Left, area.Top + row + 4, area.Right, area.Bottom)
                    : new Box(area.Left, area.Top, area.Right, area.Bottom - row - 4);
            }

            private void LegendKey(double x, double y, double key, string color, SheetChartSeries series)
            {
                var hex = "#" + (color ?? "808080");
                if (_chart.Type is SheetChartType.Line or SheetChartType.Scatter)
                {
                    var lineHex = series?.LineHex;
                    if (lineHex != null || _chart.Type == SheetChartType.Line)
                        _out.Append("<line x1=\"").Append(N(x)).Append("\" y1=\"").Append(N(y)).Append("\" x2=\"").Append(N(x + key)).Append("\" y2=\"").Append(N(y))
                            .Append("\" stroke=\"#").Append(lineHex ?? color ?? "808080").Append("\" stroke-width=\"").Append(N(Math.Max(1, (series?.LineWidth ?? 2.25) * PixelsPerPoint))).Append("\"/>");

                    if (series?.Marker != null)
                        Marker(x + (key / 2), y, series.Marker, Math.Min(7, MarkerPixels(series)), hex);

                    return;
                }

                _out.Append("<rect x=\"").Append(N(x)).Append("\" y=\"").Append(N(y - (key / 2))).Append("\" width=\"").Append(N(key)).Append("\" height=\"").Append(N(key))
                    .Append("\" fill=\"").Append(hex).Append("\"/>");
            }

            private void DrawPie(Box area)
            {
                var series = _chart.Series.FirstOrDefault();
                if (series == null)
                    return;

                var values = series.Values.Select(v => v.HasValue && v.Value > 0 ? v.Value : 0).ToList();
                var sum = values.Sum();
                if (sum <= 0)
                    return;

                var cx = area.Left + (area.Width / 2);
                var cy = area.Top + (area.Height / 2);
                var r = Math.Max(4, Math.Min(area.Width, area.Height) / 2 * 0.82);
                var inner = _chart.Type == SheetChartType.Doughnut ? r * _chart.HoleSize / 100.0 : 0;
                var angle = -90 + _chart.FirstSliceAngle;
                var labels = new List<(double X, double Y, string Text, string Fill)>();
                for (var i = 0; i < values.Count; i++)
                {
                    if (values[i] <= 0)
                        continue;

                    var sweep = values[i] / sum * 360;
                    var color = "#" + PointColor(series, i);
                    if (sweep >= 359.99)
                    {
                        _out.Append("<circle cx=\"").Append(N(cx)).Append("\" cy=\"").Append(N(cy)).Append("\" r=\"").Append(N(r)).Append("\" fill=\"").Append(color).Append("\"/>");
                    }
                    else
                    {
                        _out.Append("<path d=\"").Append(Slice(cx, cy, r, inner, angle, angle + sweep)).Append("\" fill=\"").Append(color).Append('"');
                        if (series.LineHex != null)
                            _out.Append(" stroke=\"#").Append(series.LineHex).Append("\" stroke-width=\"").Append(N(Math.Max(0.5, series.LineWidth * PixelsPerPoint))).Append('"');

                        _out.Append("/>");
                    }

                    if (_chart.DataLabels)
                    {
                        var middle = (angle + (sweep / 2)) * Math.PI / 180;
                        var distance = inner > 0 ? (inner + r) / 2 : _chart.LabelPosition == "outEnd" ? r + _text : r * 0.62;
                        labels.Add((cx + (Math.Cos(middle) * distance), cy + (Math.Sin(middle) * distance), PieLabel(series, i, values[i] / sum), _ink));
                    }

                    angle += sweep;
                }

                if (inner > 0)
                    _out.Append("<circle cx=\"").Append(N(cx)).Append("\" cy=\"").Append(N(cy)).Append("\" r=\"").Append(N(inner)).Append("\" fill=\"").Append(_chart.ChartFill == null ? "none" : "#" + _chart.ChartFill).Append("\"/>");

                foreach (var label in labels)
                    Text(label.X, label.Y + (_text * 0.32), label.Text, _text * 0.9, "middle", false);
            }

            private string PieLabel(SheetChartSeries series, int index, double share)
            {
                var parts = new List<string>();
                if (_chart.LabelSeries && !string.IsNullOrEmpty(series.Name))
                    parts.Add(series.Name);

                if (_chart.LabelCategory && index < _chart.Categories.Count)
                    parts.Add(_chart.Categories[index]);

                if (_chart.LabelValue)
                    parts.Add(Format(series.Values[index] ?? 0, series.Format));

                if (_chart.LabelPercent)
                    parts.Add(Math.Round(share * 100).ToString("0", CultureInfo.InvariantCulture) + "%");

                return string.Join(", ", parts);
            }

            private static string Slice(double cx, double cy, double r, double inner, double from, double to)
            {
                var a0 = from * Math.PI / 180;
                var a1 = to * Math.PI / 180;
                var large = to - from > 180 ? 1 : 0;
                var x0 = cx + (Math.Cos(a0) * r);
                var y0 = cy + (Math.Sin(a0) * r);
                var x1 = cx + (Math.Cos(a1) * r);
                var y1 = cy + (Math.Sin(a1) * r);
                if (inner <= 0)
                    return "M" + N(cx) + " " + N(cy) + "L" + N(x0) + " " + N(y0) + "A" + N(r) + " " + N(r) + " 0 " + large + " 1 " + N(x1) + " " + N(y1) + "Z";

                var ix0 = cx + (Math.Cos(a1) * inner);
                var iy0 = cy + (Math.Sin(a1) * inner);
                var ix1 = cx + (Math.Cos(a0) * inner);
                var iy1 = cy + (Math.Sin(a0) * inner);
                return "M" + N(x0) + " " + N(y0) + "A" + N(r) + " " + N(r) + " 0 " + large + " 1 " + N(x1) + " " + N(y1)
                    + "L" + N(ix0) + " " + N(iy0) + "A" + N(inner) + " " + N(inner) + " 0 " + large + " 0 " + N(ix1) + " " + N(iy1) + "Z";
            }

            private void DrawAxes(Box area)
            {
                var count = Math.Max(1, Math.Max(_chart.Categories.Count, _chart.Series.Select(s => s.Values.Count).DefaultIfEmpty(0).Max()));
                var stacked = _chart.Subtype != SheetChartSubtype.Clustered && _chart.Type is SheetChartType.Column or SheetChartType.Bar or SheetChartType.Area or SheetChartType.Line;
                var percent = _chart.Subtype == SheetChartSubtype.Percent && stacked;
                var (low, high) = DataRange(count, stacked, percent);
                var valueTitle = string.IsNullOrWhiteSpace(_chart.ValueTitle) ? null : _chart.ValueTitle;
                var categoryTitle = string.IsNullOrWhiteSpace(_chart.CategoryTitle) ? null : _chart.CategoryTitle;
                var titleSize = _text;
                var labelSize = _text;
                var format = percent ? "0%" : _chart.ValueFormat;

                var plot = area;
                if (IsBar)
                {
                    if (valueTitle != null)
                        plot = plot with { Bottom = plot.Bottom - (titleSize * 1.4) };

                    if (categoryTitle != null)
                        plot = plot with { Left = plot.Left + (titleSize * 1.4) };

                    var scale = Scale(low, high, plot.Width, true, labelSize, format);
                    var categoryWidth = _chart.ShowCategoryAxis ? Math.Min(plot.Width * 0.35, _chart.Categories.Select(c => Measure(c, labelSize)).DefaultIfEmpty(0).Max() + 8) : 0;
                    plot = plot with { Left = plot.Left + categoryWidth, Bottom = plot.Bottom - (_chart.ShowValueAxis ? labelSize * 1.5 : 0), Right = plot.Right - (Measure(Format(scale.High, format), labelSize) / 2) };
                    if (valueTitle != null)
                        Text(plot.Left + (plot.Width / 2), area.Bottom - 2, valueTitle, titleSize, "middle", true);

                    if (categoryTitle != null)
                        RotatedText(area.Left + (titleSize * 0.9), plot.Top + (plot.Height / 2), categoryTitle, titleSize, true);

                    PlotFill(plot);
                    DrawValueGrid(plot, scale, true, labelSize, format);
                    DrawCategoryAxis(plot, count, true, labelSize);
                    DrawBars(plot, scale, count, true);
                    return;
                }

                if (valueTitle != null)
                    plot = plot with { Left = plot.Left + (titleSize * 1.4) };

                if (categoryTitle != null)
                    plot = plot with { Bottom = plot.Bottom - (titleSize * 1.4) };

                var categoryHeight = _chart.ShowCategoryAxis ? CategoryLabelHeight(plot.Width, count, labelSize) : 0;
                var provisional = Scale(low, high, plot.Height - categoryHeight, false, labelSize, format);
                var valueWidth = _chart.ShowValueAxis ? provisional.Ticks.Select(t => Measure(Format(t, format), labelSize)).DefaultIfEmpty(0).Max() + 8 : 0;
                plot = plot with { Left = plot.Left + valueWidth, Bottom = plot.Bottom - categoryHeight, Top = plot.Top + (labelSize * 0.5) };
                var valueScale = Scale(low, high, plot.Height, false, labelSize, format);
                if (valueTitle != null)
                    RotatedText(area.Left + (titleSize * 0.9), plot.Top + (plot.Height / 2), valueTitle, titleSize, true);

                if (categoryTitle != null)
                    Text(plot.Left + (plot.Width / 2), area.Bottom - 2, categoryTitle, titleSize, "middle", true);

                PlotFill(plot);
                DrawValueGrid(plot, valueScale, false, labelSize, format);
                DrawCategoryAxis(plot, count, false, labelSize);
                switch (_chart.Type)
                {
                    case SheetChartType.Column:
                        DrawBars(plot, valueScale, count, false);
                        break;
                    case SheetChartType.Area:
                        DrawArea(plot, valueScale, count, percent);
                        break;
                    default:
                        DrawLines(plot, valueScale, count, stacked, percent);
                        break;
                }
            }

            private void PlotFill(Box plot)
            {
                if (_chart.PlotFill == null)
                    return;

                _out.Append("<rect x=\"").Append(N(plot.Left)).Append("\" y=\"").Append(N(plot.Top)).Append("\" width=\"").Append(N(plot.Width)).Append("\" height=\"").Append(N(plot.Height))
                    .Append("\" fill=\"#").Append(_chart.PlotFill).Append("\"/>");
            }

            private (double Low, double High) DataRange(int count, bool stacked, bool percent)
            {
                if (percent)
                    return (0, 1);

                var low = double.MaxValue;
                var high = double.MinValue;
                if (stacked)
                {
                    for (var i = 0; i < count; i++)
                    {
                        var positive = _chart.Series.Sum(s => i < s.Values.Count && s.Values[i] is > 0 ? s.Values[i].Value : 0);
                        var negative = _chart.Series.Sum(s => i < s.Values.Count && s.Values[i] is < 0 ? s.Values[i].Value : 0);
                        high = Math.Max(high, positive);
                        low = Math.Min(low, negative);
                    }
                }
                else
                {
                    foreach (var value in _chart.Series.SelectMany(s => s.Values).Where(v => v.HasValue))
                    {
                        high = Math.Max(high, value.Value);
                        low = Math.Min(low, value.Value);
                    }
                }

                if (low == double.MaxValue)
                    return (0, 1);

                return (low, high);
            }

            private ValueScale Scale(double low, double high, double length, bool horizontal, double labelSize, string format)
            {
                var min = _chart.Minimum;
                var max = _chart.Maximum;
                var lo = min ?? (low >= 0 ? (high > 0 && (high - low) / high < 1.0 / 6 ? low : 0) : low);
                var hi = max ?? (high <= 0 ? (low < 0 && (high - low) / -low < 1.0 / 6 ? high : 0) : high);
                if (hi <= lo)
                    hi = lo + 1;

                double unit;
                if (_chart.MajorUnit is > 0)
                {
                    unit = _chart.MajorUnit.Value;
                }
                else
                {
                    var spacing = horizontal ? Math.Max(48, (Measure(Format(hi, format), labelSize) * 1.8) + 10) : labelSize * 1.6;
                    var limit = Math.Clamp((int)Math.Floor(length / spacing), 2, 10);
                    unit = Nice((hi - lo) / limit);
                }

                if (min == null)
                    lo = Math.Floor((lo / unit) + 1e-9) * unit;

                if (max == null)
                    hi = Math.Ceiling((hi / unit) - 1e-9) * unit;

                if (hi <= lo)
                    hi = lo + unit;

                var ticks = new List<double>();
                for (var value = lo; value <= hi + (unit * 1e-6) && ticks.Count < 60; value += unit)
                    ticks.Add(Math.Round(value / unit) * unit);

                return new ValueScale(lo, hi, ticks);
            }

            private static double Nice(double rough)
            {
                if (rough <= 0 || double.IsNaN(rough) || double.IsInfinity(rough))
                    return 1;

                var power = Math.Pow(10, Math.Floor(Math.Log10(rough)));
                var fraction = rough / power;
                var step = fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10;
                return step * power;
            }

            private void DrawValueGrid(Box plot, ValueScale scale, bool horizontal, double labelSize, string format)
            {
                foreach (var tick in scale.Ticks)
                {
                    var p = horizontal ? plot.Left + ((tick - scale.Low) / (scale.High - scale.Low) * plot.Width) : plot.Bottom - ((tick - scale.Low) / (scale.High - scale.Low) * plot.Height);
                    if (_chart.ValueGridlines)
                    {
                        if (horizontal)
                            Line(p, plot.Top, p, plot.Bottom, _chart.GridColor, 0.75);
                        else
                            Line(plot.Left, p, plot.Right, p, _chart.GridColor, 0.75);
                    }

                    if (!_chart.ShowValueAxis)
                        continue;

                    if (_chart.TickMarks)
                    {
                        if (horizontal)
                            Line(p, plot.Bottom, p, plot.Bottom + 4, _chart.AxisColor, 0.75);
                        else
                            Line(plot.Left - 4, p, plot.Left, p, _chart.AxisColor, 0.75);
                    }

                    var label = Format(tick, format);
                    if (horizontal)
                        Text(p, plot.Bottom + (labelSize * 1.15), label, labelSize, "middle", false);
                    else
                        Text(plot.Left - 6, p + (labelSize * 0.35), label, labelSize, "end", false);
                }

                if (_chart.ValueAxisLine && _chart.ShowValueAxis)
                {
                    if (horizontal)
                        Line(plot.Left, plot.Bottom, plot.Right, plot.Bottom, _chart.AxisColor, 0.75);
                    else
                        Line(plot.Left, plot.Top, plot.Left, plot.Bottom, _chart.AxisColor, 0.75);
                }
            }

            private double CategoryLabelHeight(double width, int count, double size)
            {
                var bin = width / Math.Max(1, count);
                var widest = _chart.Categories.Select(c => Measure(c, size)).DefaultIfEmpty(0).Max();
                if (widest <= bin - 4)
                    return size * 1.6;

                return Math.Min(widest + 8, size * 6);
            }

            private void DrawCategoryAxis(Box plot, int count, bool horizontal, double size)
            {
                var between = _chart.BetweenCategories || _chart.Type == SheetChartType.Column || IsBar;
                var length = horizontal ? plot.Height : plot.Width;
                var bin = between ? length / count : length / Math.Max(1, count - 1);
                if (_chart.CategoryAxisLine && _chart.ShowCategoryAxis)
                {
                    if (horizontal)
                        Line(plot.Left, plot.Top, plot.Left, plot.Bottom, _chart.AxisColor, 0.75);
                    else
                        Line(plot.Left, plot.Bottom, plot.Right, plot.Bottom, _chart.AxisColor, 0.75);
                }

                if (_chart.TickMarks && _chart.ShowCategoryAxis)
                {
                    var marks = between ? count + 1 : count;
                    for (var i = 0; i < marks; i++)
                    {
                        var p = i * bin;
                        if (horizontal)
                            Line(plot.Left - 4, plot.Bottom - p, plot.Left, plot.Bottom - p, _chart.AxisColor, 0.75);
                        else
                            Line(plot.Left + p, plot.Bottom, plot.Left + p, plot.Bottom + 4, _chart.AxisColor, 0.75);
                    }
                }

                if (_chart.CategoryGridlines)
                {
                    for (var i = 0; i <= count; i++)
                    {
                        var p = i * bin;
                        if (horizontal)
                            Line(plot.Left, plot.Bottom - p, plot.Right, plot.Bottom - p, _chart.GridColor, 0.75);
                        else
                            Line(plot.Left + p, plot.Top, plot.Left + p, plot.Bottom, _chart.GridColor, 0.75);
                    }
                }

                if (!_chart.ShowCategoryAxis)
                    return;

                var widest = _chart.Categories.Select(c => Measure(c, size)).DefaultIfEmpty(0).Max();
                var rotate = !horizontal && widest > bin - 4;
                var step = 1;
                if (horizontal)
                    step = Math.Max(1, (int)Math.Ceiling(size * 1.2 / Math.Max(1, bin)));
                else if (rotate)
                    step = Math.Max(1, (int)Math.Ceiling(size * 1.2 / Math.Max(1, bin)));

                for (var i = 0; i < count; i += step)
                {
                    var label = i < _chart.Categories.Count ? _chart.Categories[i] : (i + 1).ToString(CultureInfo.InvariantCulture);
                    var p = between ? (i + 0.5) * bin : i * bin;
                    if (horizontal)
                    {
                        Text(plot.Left - 6, plot.Bottom - p + (size * 0.35), label, size, "end", false);
                    }
                    else if (rotate)
                    {
                        var x = plot.Left + p + (size * 0.35);
                        var y = plot.Bottom + 6;
                        _out.Append("<text x=\"").Append(N(x)).Append("\" y=\"").Append(N(y)).Append("\" transform=\"rotate(-90 ").Append(N(x)).Append(' ').Append(N(y))
                            .Append(")\" text-anchor=\"end\" font-size=\"").Append(N(size)).Append("\" fill=\"").Append(_ink).Append("\">").Append(WebUtility.HtmlEncode(label)).Append("</text>");
                    }
                    else
                    {
                        Text(plot.Left + p, plot.Bottom + (size * 1.25), label, size, "middle", false);
                    }
                }
            }

            private void DrawBars(Box plot, ValueScale scale, int count, bool horizontal)
            {
                var stacked = _chart.Subtype != SheetChartSubtype.Clustered;
                var percent = _chart.Subtype == SheetChartSubtype.Percent;
                var series = _chart.Series;
                var k = stacked ? 1 : Math.Max(1, series.Count);
                var overlap = stacked ? 1 : _chart.Overlap / 100.0;
                var gap = _chart.GapWidth / 100.0;
                var length = horizontal ? plot.Height : plot.Width;
                var bin = length / count;
                var bar = bin / (k - ((k - 1) * overlap) + gap);
                var span = horizontal ? plot.Width : plot.Height;
                double Position(double value) => (Math.Clamp(value, scale.Low, scale.High) - scale.Low) / (scale.High - scale.Low) * span;
                var baseline = Position(Math.Clamp(0, scale.Low, scale.High));
                var labels = new List<(double X, double Y, string Text, string Anchor)>();
                for (var i = 0; i < count; i++)
                {
                    var total = percent ? series.Sum(s => i < s.Values.Count ? Math.Abs(s.Values[i] ?? 0) : 0) : 1;
                    var positive = 0.0;
                    var negative = 0.0;
                    for (var s = 0; s < series.Count; s++)
                    {
                        var item = series[s];
                        if (i >= item.Values.Count || item.Values[i] is not double raw)
                            continue;

                        var value = percent ? (total == 0 ? 0 : raw / total) : raw;
                        double from;
                        double to;
                        if (stacked)
                        {
                            var start = value >= 0 ? positive : negative;
                            from = Position(start);
                            to = Position(start + value);
                            if (value >= 0)
                                positive += value;
                            else
                                negative += value;
                        }
                        else
                        {
                            from = baseline;
                            to = Position(value);
                        }

                        var offset = (i * bin) + (gap * bar / 2) + (stacked ? 0 : s * bar * (1 - overlap));
                        var color = "#" + (VaryPoints ? PointColor(item, i) : item.PointColors.ElementAtOrDefault(i) ?? item.ColorHex ?? "808080");
                        var low = Math.Min(from, to);
                        var size = Math.Abs(to - from);
                        string x;
                        string y;
                        string wText;
                        string hText;
                        if (horizontal)
                        {
                            x = N(plot.Left + low);
                            y = N(plot.Bottom - offset - bar);
                            wText = N(size);
                            hText = N(bar);
                        }
                        else
                        {
                            x = N(plot.Left + offset);
                            y = N(plot.Bottom - low - size);
                            wText = N(bar);
                            hText = N(size);
                        }

                        _out.Append("<rect x=\"").Append(x).Append("\" y=\"").Append(y).Append("\" width=\"").Append(wText).Append("\" height=\"").Append(hText).Append("\" fill=\"").Append(color).Append('"');
                        if (item.LineHex != null)
                            _out.Append(" stroke=\"#").Append(item.LineHex).Append("\" stroke-width=\"").Append(N(Math.Max(0.5, item.LineWidth * PixelsPerPoint))).Append('"');

                        _out.Append("/>");
                        if (!_chart.DataLabels)
                            continue;

                        var text = BarLabel(item, i, raw, value);
                        var center = stacked || _chart.LabelPosition is "ctr" or "inEnd" or "inBase";
                        var mid = offset + (bar / 2);
                        if (horizontal)
                        {
                            var end = to >= from ? low + size : low;
                            labels.Add(center
                                ? (plot.Left + low + (size / 2), plot.Bottom - mid + (_text * 0.32), text, "middle")
                                : (plot.Left + end + (value >= 0 ? 4 : -4), plot.Bottom - mid + (_text * 0.32), text, value >= 0 ? "start" : "end"));
                        }
                        else
                        {
                            labels.Add(center
                                ? (plot.Left + mid, plot.Bottom - low - (size / 2) + (_text * 0.32), text, "middle")
                                : (plot.Left + mid, value >= 0 ? plot.Bottom - low - size - 4 : plot.Bottom - low + _text, text, "middle"));
                        }
                    }
                }

                foreach (var label in labels)
                    Text(label.X, label.Y, label.Text, _text * 0.9, label.Anchor, false);
            }

            private string BarLabel(SheetChartSeries series, int index, double raw, double value)
            {
                var parts = new List<string>();
                if (_chart.LabelSeries && !string.IsNullOrEmpty(series.Name))
                    parts.Add(series.Name);

                if (_chart.LabelCategory && index < _chart.Categories.Count)
                    parts.Add(_chart.Categories[index]);

                if (_chart.LabelValue)
                    parts.Add(Format(raw, series.Format));

                if (_chart.LabelPercent)
                    parts.Add(Math.Round(value * 100).ToString("0", CultureInfo.InvariantCulture) + "%");

                return string.Join(", ", parts);
            }

            private List<(double X, double Y)> SeriesPoints(Box plot, ValueScale scale, int count, int s, double[] running, bool percent, double[] totals)
            {
                var between = _chart.BetweenCategories;
                var bin = between ? plot.Width / count : plot.Width / Math.Max(1, count - 1);
                var points = new List<(double X, double Y)>();
                var item = _chart.Series[s];
                for (var i = 0; i < count; i++)
                {
                    var raw = i < item.Values.Count ? item.Values[i] : null;
                    if (running == null && raw == null)
                    {
                        points.Add((double.NaN, double.NaN));
                        continue;
                    }

                    var value = raw ?? 0;
                    if (percent)
                        value = totals[i] == 0 ? 0 : value / totals[i];

                    if (running != null)
                    {
                        running[i] += value;
                        value = running[i];
                    }

                    var x = plot.Left + (between ? (i + 0.5) * bin : i * bin);
                    var y = plot.Bottom - ((Math.Clamp(value, scale.Low, scale.High) - scale.Low) / (scale.High - scale.Low) * plot.Height);
                    points.Add((x, y));
                }

                return points;
            }

            private double[] Totals(int count) =>
                Enumerable.Range(0, count).Select(i => _chart.Series.Sum(s => i < s.Values.Count ? Math.Abs(s.Values[i] ?? 0) : 0)).ToArray();

            private void DrawLines(Box plot, ValueScale scale, int count, bool stacked, bool percent)
            {
                var running = stacked ? new double[count] : null;
                var totals = Totals(count);
                var all = new List<(SheetChartSeries Series, List<(double X, double Y)> Points)>();
                for (var s = 0; s < _chart.Series.Count; s++)
                    all.Add((_chart.Series[s], SeriesPoints(plot, scale, count, s, running, percent, totals)));

                foreach (var (series, points) in all)
                {
                    if (series.LineHex != null || _chart.Type == SheetChartType.Line)
                    {
                        var hex = series.LineHex ?? series.ColorHex ?? "808080";
                        if (VaryPoints)
                        {
                            for (var i = 1; i < points.Count; i++)
                            {
                                if (double.IsNaN(points[i].X) || double.IsNaN(points[i - 1].X))
                                    continue;

                                _out.Append("<path d=\"").Append(PolyPath(new List<(double X, double Y)> { points[i - 1], points[i] })).Append("\" fill=\"none\" stroke=\"#").Append(PointColor(series, i))
                                    .Append("\" stroke-width=\"").Append(N(Math.Max(0.75, series.LineWidth * PixelsPerPoint))).Append("\" stroke-linecap=\"round\"/>");
                            }

                            continue;
                        }

                        foreach (var run in Runs(points))
                        {
                            _out.Append("<path d=\"").Append(series.Smooth ? SmoothPath(run) : PolyPath(run)).Append("\" fill=\"none\" stroke=\"#").Append(hex)
                                .Append("\" stroke-width=\"").Append(N(Math.Max(0.75, series.LineWidth * PixelsPerPoint))).Append("\" stroke-linejoin=\"round\" stroke-linecap=\"round\"/>");
                        }
                    }

                    if (series.Marker != null)
                    {
                        foreach (var point in points.Where(p => !double.IsNaN(p.X)))
                            Marker(point.X, point.Y, series.Marker, MarkerPixels(series), "#" + (series.ColorHex ?? "808080"));
                    }
                }

                if (!_chart.DataLabels)
                    return;

                foreach (var (series, points) in all)
                {
                    for (var i = 0; i < points.Count; i++)
                    {
                        if (double.IsNaN(points[i].X) || i >= series.Values.Count || series.Values[i] is not double raw)
                            continue;

                        Text(points[i].X, points[i].Y - 6, BarLabel(series, i, raw, raw), _text * 0.9, "middle", false);
                    }
                }
            }

            private void DrawArea(Box plot, ValueScale scale, int count, bool percent)
            {
                var stacked = _chart.Subtype != SheetChartSubtype.Clustered;
                var running = stacked ? new double[count] : null;
                var totals = Totals(count);
                var baseY = plot.Bottom - ((Math.Clamp(0, scale.Low, scale.High) - scale.Low) / (scale.High - scale.Low) * plot.Height);
                var previous = Enumerable.Range(0, count).Select(i => (X: double.NaN, Y: baseY)).ToList();
                for (var s = 0; s < _chart.Series.Count; s++)
                {
                    var points = SeriesPoints(plot, scale, count, s, running, percent, totals);
                    var path = new StringBuilder();
                    var valid = points.Where(p => !double.IsNaN(p.X)).ToList();
                    if (valid.Count == 0)
                        continue;

                    path.Append('M').Append(N(valid[0].X)).Append(' ').Append(N(valid[0].Y));
                    foreach (var point in valid.Skip(1))
                        path.Append('L').Append(N(point.X)).Append(' ').Append(N(point.Y));

                    var lower = stacked && s > 0 ? previous.Where(p => !double.IsNaN(p.X)).Reverse().ToList() : new List<(double X, double Y)> { (valid[^1].X, baseY), (valid[0].X, baseY) };
                    foreach (var point in lower)
                        path.Append('L').Append(N(point.X)).Append(' ').Append(N(point.Y));

                    path.Append('Z');
                    var series = _chart.Series[s];
                    _out.Append("<path d=\"").Append(path).Append("\" fill=\"#").Append(series.ColorHex ?? "808080").Append('"');
                    if (series.LineHex != null && !series.LineHex.Equals(series.ColorHex, StringComparison.OrdinalIgnoreCase))
                        _out.Append(" stroke=\"#").Append(series.LineHex).Append("\" stroke-width=\"").Append(N(Math.Max(0.5, series.LineWidth * PixelsPerPoint))).Append('"');

                    _out.Append("/>");
                    previous = points;
                }
            }

            private static IEnumerable<List<(double X, double Y)>> Runs(List<(double X, double Y)> points)
            {
                var run = new List<(double X, double Y)>();
                foreach (var point in points)
                {
                    if (double.IsNaN(point.X))
                    {
                        if (run.Count > 1)
                            yield return run;

                        run = new List<(double X, double Y)>();
                        continue;
                    }

                    run.Add(point);
                }

                if (run.Count > 1)
                    yield return run;
            }

            private static string PolyPath(List<(double X, double Y)> points)
            {
                var path = new StringBuilder();
                for (var i = 0; i < points.Count; i++)
                    path.Append(i == 0 ? 'M' : 'L').Append(N(points[i].X)).Append(' ').Append(N(points[i].Y));

                return path.ToString();
            }

            private static string SmoothPath(List<(double X, double Y)> points)
            {
                var path = new StringBuilder();
                path.Append('M').Append(N(points[0].X)).Append(' ').Append(N(points[0].Y));
                for (var i = 0; i < points.Count - 1; i++)
                {
                    var p0 = points[Math.Max(0, i - 1)];
                    var p1 = points[i];
                    var p2 = points[i + 1];
                    var p3 = points[Math.Min(points.Count - 1, i + 2)];
                    var c1 = (X: p1.X + ((p2.X - p0.X) / 6), Y: p1.Y + ((p2.Y - p0.Y) / 6));
                    var c2 = (X: p2.X - ((p3.X - p1.X) / 6), Y: p2.Y - ((p3.Y - p1.Y) / 6));
                    path.Append('C').Append(N(c1.X)).Append(' ').Append(N(c1.Y)).Append(' ').Append(N(c2.X)).Append(' ').Append(N(c2.Y)).Append(' ').Append(N(p2.X)).Append(' ').Append(N(p2.Y));
                }

                return path.ToString();
            }

            private static double MarkerPixels(SheetChartSeries series) => Math.Clamp(series.MarkerSize * PixelsPerPoint, 3, 30);

            private void Marker(double x, double y, string symbol, double size, string fill)
            {
                var r = size / 2;
                switch (symbol)
                {
                    case "square":
                        _out.Append("<rect x=\"").Append(N(x - r)).Append("\" y=\"").Append(N(y - r)).Append("\" width=\"").Append(N(size)).Append("\" height=\"").Append(N(size)).Append("\" fill=\"").Append(fill).Append("\"/>");
                        break;
                    case "triangle":
                        _out.Append("<path d=\"M").Append(N(x)).Append(' ').Append(N(y - r)).Append('L').Append(N(x + r)).Append(' ').Append(N(y + r)).Append('L').Append(N(x - r)).Append(' ').Append(N(y + r)).Append("Z\" fill=\"").Append(fill).Append("\"/>");
                        break;
                    case "circle":
                        _out.Append("<circle cx=\"").Append(N(x)).Append("\" cy=\"").Append(N(y)).Append("\" r=\"").Append(N(r)).Append("\" fill=\"").Append(fill).Append("\"/>");
                        break;
                    case "x":
                    case "star":
                    case "plus":
                    case "dash":
                        _out.Append("<path d=\"M").Append(N(x - r)).Append(' ').Append(N(y - r)).Append('L').Append(N(x + r)).Append(' ').Append(N(y + r)).Append('M').Append(N(x + r)).Append(' ').Append(N(y - r)).Append('L').Append(N(x - r)).Append(' ').Append(N(y + r))
                            .Append("\" stroke=\"").Append(fill).Append("\" stroke-width=\"1.5\" fill=\"none\"/>");
                        break;
                    default:
                        _out.Append("<path d=\"M").Append(N(x)).Append(' ').Append(N(y - r)).Append('L').Append(N(x + r)).Append(' ').Append(N(y)).Append('L').Append(N(x)).Append(' ').Append(N(y + r)).Append('L').Append(N(x - r)).Append(' ').Append(N(y)).Append("Z\" fill=\"").Append(fill).Append("\"/>");
                        break;
                }
            }

            private string PointColor(SheetChartSeries series, int index)
            {
                var color = series?.PointColors.ElementAtOrDefault(index);
                if (color != null)
                    return color;

                if (VaryPoints || IsPie)
                    return Accent(index);

                return series?.ColorHex ?? "808080";
            }

            private string Accent(int index)
            {
                var series = _chart.Series.FirstOrDefault();
                var known = series?.PointColors.Where(c => c != null).ToList();
                if (known != null && known.Count > 0)
                    return known[index % known.Count];

                return series?.ColorHex ?? "808080";
            }

            private void Line(double x1, double y1, double x2, double y2, string color, double widthPt)
            {
                var crisp = Math.Max(1, widthPt * PixelsPerPoint) % 2 == 1 ? 0.5 : 0;
                if (x1 == x2)
                    x1 = x2 = Math.Round(x1) + crisp;

                if (y1 == y2)
                    y1 = y2 = Math.Round(y1) + crisp;

                _out.Append("<line x1=\"").Append(N(x1)).Append("\" y1=\"").Append(N(y1)).Append("\" x2=\"").Append(N(x2)).Append("\" y2=\"").Append(N(y2))
                    .Append("\" stroke=\"#").Append(color ?? "D9D9D9").Append("\" stroke-width=\"").Append(N(Math.Max(1, widthPt * PixelsPerPoint))).Append("\"/>");
            }

            private void Text(double x, double y, string text, double size, string anchor, bool bold)
            {
                if (string.IsNullOrEmpty(text))
                    return;

                _out.Append("<text x=\"").Append(N(x)).Append("\" y=\"").Append(N(y)).Append("\" text-anchor=\"").Append(anchor).Append("\" font-size=\"").Append(N(size))
                    .Append("\" fill=\"").Append(_ink).Append('"');
                if (bold)
                    _out.Append(" font-weight=\"700\"");

                _out.Append('>').Append(WebUtility.HtmlEncode(text)).Append("</text>");
            }

            private void RotatedText(double x, double y, string text, double size, bool bold)
            {
                _out.Append("<text x=\"").Append(N(x)).Append("\" y=\"").Append(N(y)).Append("\" transform=\"rotate(-90 ").Append(N(x)).Append(' ').Append(N(y))
                    .Append(")\" text-anchor=\"middle\" font-size=\"").Append(N(size)).Append("\" fill=\"").Append(_ink).Append('"');
                if (bold)
                    _out.Append(" font-weight=\"700\"");

                _out.Append('>').Append(WebUtility.HtmlEncode(text)).Append("</text>");
            }

            private static string Fit(string text, double size, double width)
            {
                if (Measure(text, size) <= width || text.Length < 2)
                    return text;

                var cut = text;
                while (cut.Length > 1 && Measure(cut + "…", size) > width)
                    cut = cut.Substring(0, cut.Length - 1);

                return cut + "…";
            }

            private static double Measure(string text, double size)
            {
                if (string.IsNullOrEmpty(text))
                    return 0;

                var units = 0.0;
                foreach (var ch in text)
                {
                    if (ch >= 0x1100 && (ch <= 0x11FF || (ch >= 0x3000 && ch <= 0x9FFF) || (ch >= 0xAC00 && ch <= 0xD7A3) || (ch >= 0xFF00 && ch <= 0xFFEF)))
                        units += 0.93;
                    else if (ch is '.' or ',' or ':' or ';' or '\'' or '|' or 'i' or 'l' or 'j' or '!')
                        units += 0.28;
                    else if (ch == ' ')
                        units += 0.3;
                    else if (char.IsUpper(ch) || ch is 'm' or 'w' or '%' or '@' or '₩')
                        units += 0.68;
                    else
                        units += 0.55;
                }

                return units * size;
            }

            private static string Format(double value, string code)
            {
                if (string.IsNullOrEmpty(code) || ExcelNumberFormat.IsGeneral(code))
                    return ExcelNumberFormat.FormatGeneral(Math.Round(value, 10));

                return ExcelNumberFormat.Format(value, code);
            }

            private static string Attr(string text) => WebUtility.HtmlEncode(text ?? string.Empty);

            private static string N(double value) => double.IsNaN(value) || double.IsInfinity(value) ? "0" : value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private readonly record struct Box(double Left, double Top, double Right, double Bottom)
        {
            public double Width => Math.Max(1, Right - Left);

            public double Height => Math.Max(1, Bottom - Top);
        }

        private sealed record ValueScale(double Low, double High, List<double> Ticks);
    }
}
