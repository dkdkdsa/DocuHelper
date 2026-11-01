using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using DocuHelper.Tools.Excel;
using DocuHelper.Tools.Ppt;

namespace DocuHelperEditor.Components.Paper.Ppt
{
    public static class SlideChartSvg
    {
        private const int MaxPoints = 400;
        private const int MaxSeries = 40;
        private const string DefaultText = "000000";
        private const string DefaultGrid = "000000";

        public static string Build(PptSceneChart chart, double width, double height, string fontFamily)
        {
            width = Math.Max(width, 20);
            height = Math.Max(height, 20);
            var canvas = new Canvas(chart, width, height, fontFamily);
            canvas.Draw();
            return canvas.ToString();
        }

        private sealed class Canvas
        {
            private readonly PptSceneChart _chart;
            private readonly double _width;
            private readonly double _height;
            private readonly string _font;
            private readonly StringBuilder _svg = new();
            private readonly double _size;
            private readonly string _text;
            private readonly List<PptSceneSeries> _series;
            private readonly int _count;

            public Canvas(PptSceneChart chart, double width, double height, string font)
            {
                _chart = chart;
                _width = width;
                _height = height;
                _font = string.IsNullOrEmpty(font) ? "\"맑은 고딕\", \"Malgun Gothic\", sans-serif" : font;
                _size = Math.Clamp(chart.FontSizePt, 4, 48);
                _text = "#" + (chart.TextColor ?? DefaultText);
                _series = chart.Series.Take(MaxSeries).ToList();
                _count = Math.Min(MaxPoints, Math.Max(chart.Categories.Count, _series.Select(s => s.Values.Count).DefaultIfEmpty(0).Max()));
            }

            private bool IsPie => _chart.Type is "pie" or "doughnut";

            private bool IsHorizontal => _chart.Type == "bar";

            private bool ByPoint => IsPie || (_series.Count == 1 && _series[0].PointColors.Count > 0 && _chart.Type is "column" or "bar");

            public void Draw()
            {
                _svg.Append(Invariant($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {N(_width)} {N(_height)}\" width=\"100%\" height=\"100%\" preserveAspectRatio=\"none\" font-family=\"{Attr(_font)}\" font-size=\"{N(_size)}\" fill=\"{_text}\">"));
                if (_chart.ChartFill != null)
                    _svg.Append(Invariant($"<rect width=\"{N(_width)}\" height=\"{N(_height)}\" fill=\"#{_chart.ChartFill}\"/>"));

                var pad = Math.Max(4, _size * 0.6);
                var left = pad;
                var top = pad;
                var right = _width - pad;
                var bottom = _height - pad;
                if (_chart.ShowTitle && !string.IsNullOrWhiteSpace(_chart.Title))
                {
                    var titleSize = Math.Clamp(_chart.TitleSizePt, 4, 72);
                    Text(_width / 2, top + titleSize * 0.95, _chart.Title, titleSize, "middle", _chart.TitleBold, null);
                    top += titleSize * 1.25 + _size * 0.5;
                }

                DrawLegend(ref left, ref top, ref right, ref bottom);
                if (right - left < 10 || bottom - top < 10 || _count == 0)
                {
                    _svg.Append("</svg>");
                    return;
                }

                if (IsPie)
                    DrawPie(left, top, right, bottom);
                else
                    DrawAxes(left, top, right, bottom);

                _svg.Append("</svg>");
            }

            public override string ToString() => _svg.ToString();

            private List<(string Label, string Color, PptSceneSeries Series)> LegendEntries()
            {
                var entries = new List<(string, string, PptSceneSeries)>();
                if (ByPoint)
                {
                    var series = _series[0];
                    for (var i = 0; i < _count; i++)
                        entries.Add((Category(i), PointColor(series, i, 0), null));

                    return entries;
                }

                for (var s = 0; s < _series.Count; s++)
                    entries.Add((string.IsNullOrWhiteSpace(_series[s].Name) ? "계열" + (s + 1).ToString(CultureInfo.InvariantCulture) : _series[s].Name, SeriesColor(s), _series[s]));

                return entries;
            }

            private void DrawLegend(ref double left, ref double top, ref double right, ref double bottom)
            {
                if (_chart.Legend == "none")
                    return;

                var entries = LegendEntries();
                if (entries.Count == 0)
                    return;

                var swatch = _size * 0.6;
                var gap = _size * 0.35;
                var spacing = _size * 0.9;
                var rowHeight = _size * 1.35;
                var widths = entries.Select(e => swatch + gap + TextWidth(e.Label, _size) + (e.Series != null && IsLine(e.Series) ? swatch : 0)).ToList();
                if (_chart.Legend is "right" or "left")
                {
                    var column = Math.Min(widths.Max(), (right - left) * 0.4);
                    var total = entries.Count * rowHeight;
                    var y = top + Math.Max(0, (bottom - top - total) / 2);
                    var x = _chart.Legend == "right" ? right - column : left;
                    for (var i = 0; i < entries.Count; i++)
                        LegendEntry(x, y + i * rowHeight + rowHeight / 2, entries[i], swatch, gap);

                    if (_chart.Legend == "right")
                        right -= column + spacing;
                    else
                        left += column + spacing;

                    return;
                }

                var rows = new List<List<int>> { new() };
                var line = 0.0;
                var available = right - left;
                for (var i = 0; i < entries.Count; i++)
                {
                    var need = widths[i] + (rows[^1].Count > 0 ? spacing : 0);
                    if (rows[^1].Count > 0 && line + need > available)
                    {
                        rows.Add(new List<int>());
                        line = 0;
                        need = widths[i];
                    }

                    rows[^1].Add(i);
                    line += need;
                }

                var height = rows.Count * rowHeight;
                var start = _chart.Legend == "top" ? top : bottom - height;
                for (var r = 0; r < rows.Count; r++)
                {
                    var rowWidth = rows[r].Sum(i => widths[i]) + spacing * (rows[r].Count - 1);
                    var x = left + (available - rowWidth) / 2;
                    foreach (var i in rows[r])
                    {
                        LegendEntry(x, start + r * rowHeight + rowHeight / 2, entries[i], swatch, gap);
                        x += widths[i] + spacing;
                    }
                }

                if (_chart.Legend == "top")
                    top += height + _size * 0.4;
                else
                    bottom -= height + _size * 0.4;
            }

            private void LegendEntry(double x, double cy, (string Label, string Color, PptSceneSeries Series) entry, double swatch, double gap)
            {
                var color = "#" + (entry.Color ?? DefaultText);
                if (entry.Series != null && IsLine(entry.Series))
                {
                    var width = swatch * 2;
                    if (LineWidth(entry.Series) > 0)
                        _svg.Append(Invariant($"<line x1=\"{N(x)}\" y1=\"{N(cy)}\" x2=\"{N(x + width)}\" y2=\"{N(cy)}\" stroke=\"{color}\" stroke-width=\"{N(LineWidth(entry.Series))}\"/>"));

                    Marker(x + width / 2, cy, entry.Series, entry.Color);
                    Text(x + width + gap, cy + _size * 0.35, entry.Label, _size, "start", false, null);
                    return;
                }

                _svg.Append(Invariant($"<rect x=\"{N(x)}\" y=\"{N(cy - swatch / 2)}\" width=\"{N(swatch)}\" height=\"{N(swatch)}\" fill=\"{color}\"/>"));
                Text(x + swatch + gap, cy + _size * 0.35, entry.Label, _size, "start", false, null);
            }

            private void DrawAxes(double left, double top, double right, double bottom)
            {
                var probe = Format(_series.SelectMany(s => s.Values).Where(v => v.HasValue).Select(v => Math.Abs(v.Value)).DefaultIfEmpty(1).Max(), _chart.ValueFormat ?? _series.Select(s => s.Format).FirstOrDefault(f => !string.IsNullOrEmpty(f)));
                var room = IsHorizontal ? (right - left) * 0.75 / (TextWidth(probe, _size) + _size * 1.5) : 10;
                var scale = Scale(Math.Floor(room));
                var ticks = new List<double>();
                for (var value = scale.Min; value <= scale.Max + scale.Unit * 1e-6 && ticks.Count < 200; value += scale.Unit)
                    ticks.Add(Math.Abs(value) < scale.Unit * 1e-9 ? 0 : value);

                var format = _chart.PercentStacked ? "0%" : _chart.ValueFormat ?? _series.Select(s => s.Format).FirstOrDefault(f => !string.IsNullOrEmpty(f));
                var tickLabels = ticks.Select(t => Format(t, format)).ToList();
                var categories = Enumerable.Range(0, _count).Select(Category).ToList();
                var gap = _size * 0.45;
                if (IsHorizontal)
                {
                    var categoryWidth = _chart.ShowCategoryAxis ? Math.Min(categories.Select(c => TextWidth(c, _size)).DefaultIfEmpty(0).Max() + gap, (right - left) * 0.4) : 0;
                    var valueHeight = _chart.ShowValueAxis ? _size * 1.3 + gap * 0.5 : 0;
                    var lastHalf = _chart.ShowValueAxis ? TextWidth(tickLabels.LastOrDefault() ?? string.Empty, _size) / 2 : 0;
                    var plot = new Rect(left + categoryWidth, top + _size * 0.3, right - Math.Max(lastHalf, _size * 0.3), bottom - valueHeight);
                    if (plot.W < 5 || plot.H < 5)
                        return;

                    PlotFill(plot);
                    for (var i = 0; i < ticks.Count; i++)
                    {
                        var x = plot.X0 + (ticks[i] - scale.Min) / (scale.Max - scale.Min) * plot.W;
                        if (_chart.Gridlines)
                            Line(x, plot.Y0, x, plot.Y1, "#" + (_chart.GridColor ?? DefaultGrid), 0.75);

                        if (_chart.ShowValueAxis)
                            Text(x, plot.Y1 + gap * 0.5 + _size * 0.95, tickLabels[i], _size, "middle", false, null);
                    }

                    var zero = plot.X0 + (Math.Clamp(0, scale.Min, scale.Max) - scale.Min) / (scale.Max - scale.Min) * plot.W;
                    if (_chart.ValueLineColor != null)
                        Line(plot.X0, plot.Y1, plot.X1, plot.Y1, "#" + _chart.ValueLineColor, 0.75);

                    var band = plot.H / _count;
                    for (var i = 0; i < _count; i++)
                    {
                        var center = BandCenter(plot.Y1, -band, i);
                        if (_chart.ShowCategoryAxis)
                            Text(plot.X0 - gap, center + _size * 0.35, categories[i], _size, "end", false, null);
                    }

                    DrawBars(plot, scale, band, true);
                    if (_chart.CategoryLineColor != null)
                        Line(zero, plot.Y0, zero, plot.Y1, "#" + _chart.CategoryLineColor, 0.75);

                    return;
                }

                var valueWidth = _chart.ShowValueAxis ? tickLabels.Select(t => TextWidth(t, _size)).DefaultIfEmpty(0).Max() + gap : 0;
                var plotLeft = left + valueWidth;
                var plotRight = right - _size * 0.3;
                var bandWidth = (plotRight - plotLeft) / Math.Max(1, _count);
                var widest = categories.Select(c => TextWidth(c, _size)).DefaultIfEmpty(0).Max();
                var rotation = _chart.CategoryLabelRotation ?? (widest > bandWidth * 0.92 ? -45 : 0);
                var radians = Math.Abs(rotation) * Math.PI / 180;
                var labelHeight = _chart.ShowCategoryAxis
                    ? rotation == 0 ? _size * 1.3 : widest * Math.Sin(radians) + _size * Math.Cos(radians) + _size * 0.2
                    : 0;
                labelHeight = Math.Min(labelHeight, (bottom - top) * 0.45);
                var plotArea = new Rect(plotLeft, top + _size * 0.6, plotRight, bottom - labelHeight - gap * 0.5);
                if (plotArea.W < 5 || plotArea.H < 5)
                    return;

                PlotFill(plotArea);
                for (var i = 0; i < ticks.Count; i++)
                {
                    var y = plotArea.Y1 - (ticks[i] - scale.Min) / (scale.Max - scale.Min) * plotArea.H;
                    if (_chart.Gridlines)
                        Line(plotArea.X0, y, plotArea.X1, y, "#" + (_chart.GridColor ?? DefaultGrid), 0.75);

                    if (_chart.ShowValueAxis)
                        Text(plotArea.X0 - gap, y + _size * 0.35, tickLabels[i], _size, "end", false, null);
                }

                if (_chart.ValueLineColor != null)
                    Line(plotArea.X0, plotArea.Y0, plotArea.X0, plotArea.Y1, "#" + _chart.ValueLineColor, 0.75);

                var step = 1;
                if (_chart.ShowCategoryAxis)
                {
                    var spacing = rotation == 0 ? bandWidth : _size * 1.15 / Math.Max(0.2, Math.Sin(Math.Max(radians, 0.2)));
                    var need = rotation == 0 ? widest : _size * 1.15;
                    step = Math.Max(1, (int)Math.Ceiling(need / Math.Max(1, spacing) - 0.05));
                    if (rotation == 0)
                        step = 1;
                }

                var mid = _chart.MidCategory && _count > 1;
                for (var i = 0; i < _count && _chart.ShowCategoryAxis; i += step)
                {
                    var x = mid ? CategoryX(plotArea, i, true) : BandCenter(plotArea.X0, bandWidth, i);
                    var y = plotArea.Y1 + gap * 0.5 + _size * 0.95;
                    if (rotation == 0)
                    {
                        Text(x, y, categories[i], _size, "middle", false, null);
                        continue;
                    }

                    var anchorY = plotArea.Y1 + gap * 0.5 + _size * 0.6;
                    _svg.Append(Invariant($"<text x=\"{N(x)}\" y=\"{N(anchorY)}\" text-anchor=\"end\" transform=\"rotate({N(rotation)} {N(x)} {N(anchorY - _size * 0.35)})\">{Esc(categories[i])}</text>"));
                }

                var columns = _series.Where(s => (s.Type ?? _chart.Type) == "column").ToList();
                if (columns.Count > 0)
                    DrawBars(plotArea, scale, bandWidth, false);

                foreach (var area in _series.Where(s => (s.Type ?? _chart.Type) == "area").Reverse())
                    DrawArea(plotArea, scale, area, mid);

                var zeroY = plotArea.Y1 - (Math.Clamp(0, scale.Min, scale.Max) - scale.Min) / (scale.Max - scale.Min) * plotArea.H;
                if (_chart.CategoryLineColor != null)
                    Line(plotArea.X0, zeroY, plotArea.X1, zeroY, "#" + _chart.CategoryLineColor, 0.75);

                foreach (var series in _series.Where(s => (s.Type ?? _chart.Type) is "line" or "scatter" or "radar"))
                    DrawLine(plotArea, scale, series, mid);
            }

            private double BandCenter(double start, double band, int index)
            {
                var position = _chart.ReverseCategories ? _count - 1 - index : index;
                return start + band * (position + 0.5);
            }

            private double CategoryX(Rect plot, int index, bool mid)
            {
                if (!mid)
                    return BandCenter(plot.X0, plot.W / Math.Max(1, _count), index);

                var position = _chart.ReverseCategories ? _count - 1 - index : index;
                return plot.X0 + plot.W * position / Math.Max(1, _count - 1);
            }

            private void DrawBars(Rect plot, AxisScale scale, double band, bool horizontal)
            {
                var bars = _series.Select((s, i) => (Series: s, Index: i)).Where(p => (p.Series.Type ?? _chart.Type) is "column" or "bar").ToList();
                if (bars.Count == 0)
                    return;

                var gapRatio = Math.Clamp(_chart.GapWidth, 0, 500) / 100.0;
                var overlap = _chart.Stacked ? 1 : Math.Clamp(_chart.Overlap, -100, 100) / 100.0;
                var slots = _chart.Stacked ? 1 : bars.Count;
                var group = Math.Abs(band) / (1 + gapRatio);
                var barSize = group / (slots - (slots - 1) * overlap);
                var span = scale.Max - scale.Min;
                double Coord(double value) => horizontal
                    ? plot.X0 + (Math.Clamp(value, scale.Min, scale.Max) - scale.Min) / span * plot.W
                    : plot.Y1 - (Math.Clamp(value, scale.Min, scale.Max) - scale.Min) / span * plot.H;

                for (var i = 0; i < _count; i++)
                {
                    var center = horizontal ? BandCenter(plot.Y1, -Math.Abs(band), i) : BandCenter(plot.X0, band, i);
                    var positive = 0.0;
                    var negative = 0.0;
                    var total = _chart.PercentStacked ? bars.Sum(b => Math.Abs(ValueAt(b.Series, i) ?? 0)) : 1;
                    for (var k = 0; k < bars.Count; k++)
                    {
                        var raw = ValueAt(bars[k].Series, i);
                        if (raw == null)
                            continue;

                        var value = _chart.PercentStacked ? (total == 0 ? 0 : raw.Value / total) : raw.Value;
                        double from;
                        double to;
                        if (_chart.Stacked)
                        {
                            from = value >= 0 ? positive : negative;
                            to = from + value;
                            if (value >= 0)
                                positive = to;
                            else
                                negative = to;
                        }
                        else
                        {
                            from = Math.Clamp(0, scale.Min, scale.Max);
                            to = value;
                        }

                        var slot = _chart.Stacked ? 0 : k;
                        var offset = -group / 2 + slot * barSize * (1 - overlap) + barSize / 2;
                        if (horizontal)
                            offset = -offset;

                        var a = Coord(from);
                        var b = Coord(to);
                        var color = "#" + PointColor(bars[k].Series, i, bars[k].Index);
                        if (horizontal)
                        {
                            var y = center + offset - barSize / 2;
                            _svg.Append(Invariant($"<rect x=\"{N(Math.Min(a, b))}\" y=\"{N(y)}\" width=\"{N(Math.Abs(b - a))}\" height=\"{N(barSize)}\" fill=\"{color}\"/>"));
                            if (_chart.DataLabels)
                                Label(Math.Max(a, b) + _size * 0.3, y + barSize / 2 + _size * 0.35, raw.Value, value, bars[k].Series, i, "start");
                        }
                        else
                        {
                            var x = center + offset - barSize / 2;
                            _svg.Append(Invariant($"<rect x=\"{N(x)}\" y=\"{N(Math.Min(a, b))}\" width=\"{N(barSize)}\" height=\"{N(Math.Abs(b - a))}\" fill=\"{color}\"/>"));
                            if (_chart.DataLabels)
                            {
                                var labelY = _chart.Stacked ? (a + b) / 2 + _size * 0.35 : Math.Min(a, b) - _size * 0.3;
                                Label(x + barSize / 2, labelY, raw.Value, value, bars[k].Series, i, "middle");
                            }
                        }
                    }
                }
            }

            private void DrawArea(Rect plot, AxisScale scale, PptSceneSeries series, bool mid)
            {
                var points = new List<string>();
                var zero = plot.Y1 - (Math.Clamp(0, scale.Min, scale.Max) - scale.Min) / (scale.Max - scale.Min) * plot.H;
                var index = _series.IndexOf(series);
                for (var i = 0; i < _count; i++)
                {
                    var value = StackedValue(series, i) ?? 0;
                    var x = CategoryX(plot, i, mid);
                    var y = plot.Y1 - (Math.Clamp(value, scale.Min, scale.Max) - scale.Min) / (scale.Max - scale.Min) * plot.H;
                    if (i == 0)
                        points.Add(N(x) + "," + N(zero));

                    points.Add(N(x) + "," + N(y));
                    if (i == _count - 1)
                        points.Add(N(x) + "," + N(zero));
                }

                _svg.Append(Invariant($"<polygon points=\"{string.Join(" ", points)}\" fill=\"#{SeriesColor(index)}\"/>"));
            }

            private void DrawLine(Rect plot, AxisScale scale, PptSceneSeries series, bool mid)
            {
                var index = _series.IndexOf(series);
                var color = "#" + SeriesColor(index);
                var coords = new List<(double X, double Y, int I, double Value)>();
                for (var i = 0; i < _count; i++)
                {
                    var value = StackedValue(series, i);
                    if (value == null)
                        continue;

                    var x = CategoryX(plot, i, mid);
                    var y = plot.Y1 - (Math.Clamp(value.Value, scale.Min, scale.Max) - scale.Min) / (scale.Max - scale.Min) * plot.H;
                    coords.Add((x, y, i, ValueAt(series, i) ?? 0));
                }

                var width = LineWidth(series);
                if (coords.Count > 1 && width > 0)
                {
                    var path = new StringBuilder();
                    path.Append(Invariant($"M{N(coords[0].X)},{N(coords[0].Y)}"));
                    for (var i = 1; i < coords.Count; i++)
                    {
                        if (series.Smooth)
                        {
                            var p0 = coords[Math.Max(0, i - 2)];
                            var p1 = coords[i - 1];
                            var p2 = coords[i];
                            var p3 = coords[Math.Min(coords.Count - 1, i + 1)];
                            var c1x = p1.X + (p2.X - p0.X) / 6;
                            var c1y = p1.Y + (p2.Y - p0.Y) / 6;
                            var c2x = p2.X - (p3.X - p1.X) / 6;
                            var c2y = p2.Y - (p3.Y - p1.Y) / 6;
                            path.Append(Invariant($" C{N(c1x)},{N(c1y)} {N(c2x)},{N(c2y)} {N(p2.X)},{N(p2.Y)}"));
                        }
                        else
                        {
                            path.Append(Invariant($" L{N(coords[i].X)},{N(coords[i].Y)}"));
                        }
                    }

                    _svg.Append(Invariant($"<path d=\"{path}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"{N(width)}\" stroke-linejoin=\"round\" stroke-linecap=\"round\"/>"));
                }

                foreach (var point in coords)
                {
                    Marker(point.X, point.Y, series, PointColor(series, point.I, index));
                    if (_chart.DataLabels)
                        Label(point.X + _size * 0.4, point.Y + _size * 0.35, point.Value, point.Value, series, point.I, "start");
                }
            }

            private void DrawPie(double left, double top, double right, double bottom)
            {
                var series = _series.FirstOrDefault();
                if (series == null)
                    return;

                var values = Enumerable.Range(0, _count).Select(i => Math.Max(0, ValueAt(series, i) ?? 0)).ToList();
                var total = values.Sum();
                var cx = (left + right) / 2;
                var cy = (top + bottom) / 2;
                var radius = Math.Min(right - left, bottom - top) / 2 * 0.92;
                if (total <= 0 || radius <= 2)
                    return;

                var hole = _chart.Type == "doughnut" ? radius * Math.Clamp(_chart.HoleSize, 10, 90) / 100.0 : 0;
                var angle = (_chart.FirstSliceAngle - 90) * Math.PI / 180;
                for (var i = 0; i < values.Count; i++)
                {
                    if (values[i] <= 0)
                        continue;

                    var sweep = values[i] / total * Math.PI * 2;
                    var color = "#" + PointColor(series, i, 0);
                    if (sweep >= Math.PI * 2 - 1e-6)
                    {
                        _svg.Append(Invariant($"<circle cx=\"{N(cx)}\" cy=\"{N(cy)}\" r=\"{N(radius)}\" fill=\"{color}\"/>"));
                    }
                    else
                    {
                        var large = sweep > Math.PI ? 1 : 0;
                        var a = Polar(cx, cy, radius, angle);
                        var b = Polar(cx, cy, radius, angle + sweep);
                        string d;
                        if (hole > 0)
                        {
                            var c = Polar(cx, cy, hole, angle + sweep);
                            var e = Polar(cx, cy, hole, angle);
                            d = Invariant($"M{N(a.X)},{N(a.Y)} A{N(radius)},{N(radius)} 0 {large} 1 {N(b.X)},{N(b.Y)} L{N(c.X)},{N(c.Y)} A{N(hole)},{N(hole)} 0 {large} 0 {N(e.X)},{N(e.Y)} Z");
                        }
                        else
                        {
                            d = Invariant($"M{N(cx)},{N(cy)} L{N(a.X)},{N(a.Y)} A{N(radius)},{N(radius)} 0 {large} 1 {N(b.X)},{N(b.Y)} Z");
                        }

                        _svg.Append(Invariant($"<path d=\"{d}\" fill=\"{color}\" stroke=\"#FFFFFF\" stroke-width=\"0.75\" stroke-linejoin=\"round\"/>"));
                    }

                    if (_chart.DataLabels)
                    {
                        var mid = angle + sweep / 2;
                        var distance = hole > 0 ? (radius + hole) / 2 : radius * 0.68;
                        var at = Polar(cx, cy, distance, mid);
                        Label(at.X, at.Y + _size * 0.35, values[i], values[i] / total, series, i, "middle");
                    }

                    angle += sweep;
                }

                if (hole > 0 && values.Count(v => v > 0) == 1)
                    _svg.Append(Invariant($"<circle cx=\"{N(cx)}\" cy=\"{N(cy)}\" r=\"{N(hole)}\" fill=\"#FFFFFF\"/>"));
            }

            private void Label(double x, double y, double raw, double share, PptSceneSeries series, int index, string anchor)
            {
                var parts = new List<string>();
                if (_chart.LabelCategory)
                    parts.Add(Category(index));

                if (_chart.LabelValue)
                {
                    var format = _chart.LabelFormat ?? series.Format;
                    parts.Add(Format(raw, format));
                }

                if (_chart.LabelPercent)
                {
                    var format = _chart.LabelFormat != null && _chart.LabelFormat.Contains('%') ? _chart.LabelFormat : "0%";
                    parts.Add(Format(IsPie ? share : raw, format));
                }

                if (parts.Count > 0)
                    Text(x, y, string.Join(", ", parts), _size, anchor, false, null);
            }

            private void Marker(double x, double y, PptSceneSeries series, string colorHex)
            {
                var symbol = series.Marker;
                if (string.IsNullOrEmpty(symbol) || symbol == "none")
                    return;

                var size = Math.Clamp(series.MarkerSizePt, 2, 30);
                var r = size / 2;
                var color = "#" + (series.MarkerColor ?? colorHex ?? DefaultText);
                switch (symbol)
                {
                    case "square":
                        _svg.Append(Invariant($"<rect x=\"{N(x - r)}\" y=\"{N(y - r)}\" width=\"{N(size)}\" height=\"{N(size)}\" fill=\"{color}\"/>"));
                        break;
                    case "diamond":
                        _svg.Append(Invariant($"<polygon points=\"{N(x)},{N(y - r)} {N(x + r)},{N(y)} {N(x)},{N(y + r)} {N(x - r)},{N(y)}\" fill=\"{color}\"/>"));
                        break;
                    case "triangle":
                        _svg.Append(Invariant($"<polygon points=\"{N(x)},{N(y - r)} {N(x + r)},{N(y + r)} {N(x - r)},{N(y + r)}\" fill=\"{color}\"/>"));
                        break;
                    case "x":
                    case "star":
                    case "plus":
                        _svg.Append(Invariant($"<path d=\"M{N(x - r)},{N(y - r)} L{N(x + r)},{N(y + r)} M{N(x + r)},{N(y - r)} L{N(x - r)},{N(y + r)}\" stroke=\"{color}\" stroke-width=\"1\"/>"));
                        break;
                    case "dash":
                        _svg.Append(Invariant($"<rect x=\"{N(x - r)}\" y=\"{N(y - r / 4)}\" width=\"{N(size)}\" height=\"{N(r / 2)}\" fill=\"{color}\"/>"));
                        break;
                    case "dot":
                        _svg.Append(Invariant($"<circle cx=\"{N(x)}\" cy=\"{N(y)}\" r=\"{N(r / 2)}\" fill=\"{color}\"/>"));
                        break;
                    default:
                        _svg.Append(Invariant($"<circle cx=\"{N(x)}\" cy=\"{N(y)}\" r=\"{N(r)}\" fill=\"{color}\"/>"));
                        break;
                }
            }

            private AxisScale Scale(double room)
            {
                var values = new List<double>();
                if (_chart.PercentStacked)
                {
                    values.Add(0);
                    values.Add(1);
                }
                else if (_chart.Stacked)
                {
                    for (var i = 0; i < _count; i++)
                    {
                        values.Add(_series.Sum(s => Math.Max(0, ValueAt(s, i) ?? 0)));
                        values.Add(_series.Sum(s => Math.Min(0, ValueAt(s, i) ?? 0)));
                    }
                }
                else
                {
                    foreach (var series in _series)
                    {
                        for (var i = 0; i < _count; i++)
                        {
                            if (ValueAt(series, i) is double value)
                                values.Add(value);
                        }
                    }
                }

                var low = values.Count == 0 ? 0 : values.Min();
                var high = values.Count == 0 ? 1 : values.Max();
                if (_chart.PercentStacked)
                    return new AxisScale(_chart.AxisMin ?? 0, _chart.AxisMax ?? 1, _chart.MajorUnit ?? 0.1);

                var lineOnly = _series.All(s => (s.Type ?? _chart.Type) is "line" or "scatter");
                if (low >= 0)
                    low = lineOnly && high > 0 && (high - low) / high < 1.0 / 6 ? low : 0;

                if (high <= 0)
                    high = 0;

                if (high <= low)
                    high = low + (Math.Abs(low) > 0 ? Math.Abs(low) : 1);

                var min = _chart.AxisMin ?? low;
                var max = _chart.AxisMax ?? high;
                var padHigh = _chart.AxisMax ?? (high > 0 ? high + (high - low) * 0.05 : high);
                var padLow = _chart.AxisMin ?? (low < 0 ? low - (high - low) * 0.05 : low);
                var intervals = Math.Clamp(room, 2, 10);
                var unit = _chart.MajorUnit is double given && given > 0 ? given : Nice((padHigh - padLow) / intervals);
                if (_chart.AxisMin == null && low != 0)
                    min = Math.Floor(padLow / unit) * unit;

                if (_chart.AxisMax == null)
                    max = Math.Ceiling(padHigh / unit) * unit;

                if (max <= min)
                    max = min + unit;

                if ((max - min) / unit > 200)
                    unit = (max - min) / 10;

                return new AxisScale(min, max, unit);
            }

            private static double Nice(double rough)
            {
                if (rough <= 0 || !double.IsFinite(rough))
                    return 1;

                var exponent = Math.Floor(Math.Log10(rough));
                var fraction = rough / Math.Pow(10, exponent);
                var nice = fraction <= 1.0001 ? 1 : fraction <= 2.0001 ? 2 : fraction <= 5.0001 ? 5 : 10;
                return nice * Math.Pow(10, exponent);
            }

            private double? StackedValue(PptSceneSeries series, int index)
            {
                var own = ValueAt(series, index);
                if (own == null || !_chart.Stacked)
                    return own;

                var type = series.Type ?? _chart.Type;
                var sum = 0.0;
                var total = 0.0;
                foreach (var other in _series.Where(s => (s.Type ?? _chart.Type) == type))
                {
                    var value = ValueAt(other, index) ?? 0;
                    total += Math.Abs(value);
                    if (_series.IndexOf(other) <= _series.IndexOf(series))
                        sum += value;
                }

                return _chart.PercentStacked ? (total == 0 ? 0 : sum / total) : sum;
            }

            private static double? ValueAt(PptSceneSeries series, int index) =>
                index < series.Values.Count && series.Values[index] is double value && double.IsFinite(value) ? value : null;

            private bool IsLine(PptSceneSeries series) => (series.Type ?? _chart.Type) is "line" or "scatter" or "radar";

            private static double LineWidth(PptSceneSeries series) => series.LineWidthPt is double width ? Math.Clamp(width, 0, 20) : 2.25;

            private string SeriesColor(int index) => _series[index].Color ?? DefaultText;

            private string PointColor(PptSceneSeries series, int point, int index) =>
                series.PointColors.TryGetValue(point, out var color) && color != null ? color : series.Color ?? DefaultText;

            private string Category(int index) =>
                index < _chart.Categories.Count && !string.IsNullOrWhiteSpace(_chart.Categories[index])
                    ? _chart.Categories[index]
                    : (index + 1).ToString(CultureInfo.InvariantCulture);

            private void PlotFill(Rect plot)
            {
                if (_chart.PlotFill != null)
                    _svg.Append(Invariant($"<rect x=\"{N(plot.X0)}\" y=\"{N(plot.Y0)}\" width=\"{N(plot.W)}\" height=\"{N(plot.H)}\" fill=\"#{_chart.PlotFill}\"/>"));
            }

            private void Line(double x1, double y1, double x2, double y2, string color, double width) =>
                _svg.Append(Invariant($"<line x1=\"{N(x1)}\" y1=\"{N(y1)}\" x2=\"{N(x2)}\" y2=\"{N(y2)}\" stroke=\"{color}\" stroke-width=\"{N(width)}\"/>"));

            private void Text(double x, double y, string text, double size, string anchor, bool bold, string color)
            {
                var extra = new StringBuilder();
                if (Math.Abs(size - _size) > 0.01)
                    extra.Append(Invariant($" font-size=\"{N(size)}\""));

                if (bold)
                    extra.Append(" font-weight=\"700\"");

                if (color != null)
                    extra.Append(" fill=\"#" + color + "\"");

                _svg.Append(Invariant($"<text x=\"{N(x)}\" y=\"{N(y)}\" text-anchor=\"{anchor}\"{extra}>{Esc(text)}</text>"));
            }

            private static (double X, double Y) Polar(double cx, double cy, double radius, double angle) =>
                (cx + radius * Math.Cos(angle), cy + radius * Math.Sin(angle));

            private static string Format(double value, string code)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(code) || ExcelNumberFormat.IsGeneral(code))
                        return ExcelNumberFormat.FormatGeneral(Math.Round(value, 10));

                    return ExcelNumberFormat.Format(value, code);
                }
                catch (Exception)
                {
                    return value.ToString("0.##", CultureInfo.InvariantCulture);
                }
            }

            public static double TextWidth(string text, double size)
            {
                var width = 0.0;
                foreach (var c in text ?? string.Empty)
                {
                    width += c switch
                    {
                        >= (char)0x1100 and <= (char)0x11FF => 1.0,
                        >= (char)0x2E80 and <= (char)0x9FFF => 1.0,
                        >= (char)0xAC00 and <= (char)0xD7AF => 0.95,
                        >= (char)0xFF00 and <= (char)0xFFEF => 1.0,
                        ' ' => 0.3,
                        '.' or ',' or ':' or ';' or '\'' or '|' or '!' or 'i' or 'l' or 'I' or 'j' => 0.28,
                        >= '0' and <= '9' => 0.56,
                        >= 'A' and <= 'Z' => 0.64,
                        'm' or 'w' or 'M' or 'W' => 0.85,
                        _ => 0.52,
                    };
                }

                return width * size;
            }

            private static string N(double value) => double.IsFinite(value) ? value.ToString("0.##", CultureInfo.InvariantCulture) : "0";

            private static string Esc(string text) => WebUtility.HtmlEncode(text ?? string.Empty);

            private static string Attr(string text) => (text ?? string.Empty).Replace("\"", "&quot;");

            private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
        }

        private readonly struct AxisScale
        {
            public AxisScale(double min, double max, double unit)
            {
                Min = min;
                Max = max;
                Unit = unit;
            }

            public double Min { get; }

            public double Max { get; }

            public double Unit { get; }
        }

        private readonly struct Rect
        {
            public Rect(double x0, double y0, double x1, double y1)
            {
                X0 = x0;
                Y0 = y0;
                X1 = x1;
                Y1 = y1;
            }

            public double X0 { get; }

            public double Y0 { get; }

            public double X1 { get; }

            public double Y1 { get; }

            public double W => X1 - X0;

            public double H => Y1 - Y0;
        }
    }
}
