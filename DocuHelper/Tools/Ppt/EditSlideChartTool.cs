using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using DocuHelper.Docus;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public class EditSlideChartTool : Tool
    {
        private const double SizeLimitFactor = 4;

        private const int MaxTextLength = 255;

        private const int MaxTitleLength = 200;

        private static readonly string[] s_legends = { "none", "bottom", "right", "top", "left" };

        public override string Name => "edit_slide_chart";

        public override string Description =>
            "Create a chart or update an existing one (data, type, title). "
            + "chart_type: column, stacked_column, bar (horizontal), stacked_bar, line, pie, doughnut, area. "
            + "categories = labels; each series has a name and one value per category (null = empty); pie and doughnut use the first series only. "
            + "x, y, w, h in cm; default is the free area under the title. The chart stays editable in PowerPoint. Never invent numbers.";

        public override string InputSchema => $$$"""
            {
              "type": "object",
              "properties": {
                "slide": {"type": "integer", "minimum": 1, "description": "Slide number (1-based)."},
                "action": {"type": "string", "enum": ["create", "update"], "description": "Default create."},
                "shape": {"type": "string", "description": "Chart shape id (update)."},
                "chart_type": {"type": "string", "enum": [{{{string.Join(", ", PptChartKinds.All.Select(k => "\"" + k.Token + "\""))}}}]},
                "title": {"type": "string", "description": "Chart title; empty removes it."},
                "categories": {"type": "array", "items": {"type": "string"}, "maxItems": 200},
                "series": {
                  "type": "array",
                  "maxItems": 12,
                  "items": {
                    "type": "object",
                    "properties": {
                      "name": {"type": "string"},
                      "values": {"type": "array", "items": {"type": ["number", "null"]}},
                      "color": {"type": "string", "description": "#RRGGBB, or theme color accent1-6, dark1, light1, dark2, light2, hyperlink with optional +N lighter / -N darker percent, e.g. accent1+40."}
                    },
                    "required": ["name", "values"]
                  }
                },
                "legend": {"type": "string", "enum": ["none", "bottom", "right", "top", "left"]},
                "data_labels": {"type": "boolean", "description": "Show values."},
                "x": {"type": "number", "description": "create: left, cm."},
                "y": {"type": "number", "description": "create: top, cm."},
                "w": {"type": "number", "minimum": 1, "description": "create: width, cm."},
                "h": {"type": "number", "minimum": 1, "description": "create: height, cm."}
              },
              "required": ["slide"]
            }
            """;

        public override ToolResult Execute(Docu docu, ToolInput input)
        {
            var document = PptSlides.DocumentOf(docu);
            var slide = PptSlides.Get(document, input.GetInt("slide"));
            var action = PptInputs.Choice(input, "action", "create", "update") ?? "create";
            var request = Request.Read(input);
            return action == "create" ? Create(document, slide, input, request) : Update(slide, input, request);
        }

        private static ToolResult Create(PresentationDocument document, PptSlideRef slide, ToolInput input, Request request)
        {
            if (request.Categories == null || request.Series == null)
                throw new ToolInputException("차트를 만들려면 categories(항목 이름)와 series(이름과 값)를 알려 주세요.");

            var kind = request.Kind ?? PptChartKinds.Find("column");
            var model = new PptChartInfo
            {
                Title = request.Title,
                Categories = request.Categories,
                Series = request.Series,
                Legend = request.Legend ?? (request.Series.Count > 1 || kind.IsPie ? "bottom" : "none"),
                DataLabels = request.DataLabels ?? false,
            };

            Complete(model);

            var deck = PptFrames.DeckOf(document);
            var maxX = PptUnits.ToCm(deck.Width) * SizeLimitFactor;
            var maxY = PptUnits.ToCm(deck.Height) * SizeLimitFactor;
            var x = PptInputs.Emu(input, "x", -maxX, maxX);
            var y = PptInputs.Emu(input, "y", -maxY, maxY);
            var width = PptInputs.Emu(input, "w", 1, maxX);
            var height = PptInputs.Emu(input, "h", 1, maxY);
            var tree = slide.Tree ?? throw new ToolInputException($"{slide.Number}번 슬라이드를 읽지 못했어요.");
            var explicitFrame = x != null || y != null || width != null || height != null;
            var empty = explicitFrame ? null : PptFrames.EmptyPlaceholder(slide, "chart");
            if (PptFrames.Free(tree, empty) >= AddSlideShapeTool.MaxShapesPerSlide)
                throw new ToolInputException($"한 장에는 도형을 {AddSlideShapeTool.MaxShapesPerSlide.ToString(CultureInfo.InvariantCulture)}개까지만 둘 수 있어요. 안 쓰는 도형을 먼저 지워 주세요.");

            var area = PptFrames.Area(slide, deck, empty);
            var left = x ?? area.X;
            var top = y ?? area.Y;
            var box = new PptBox(left, top, width ?? Math.Max(PptUnits.EmuPerCm, area.Right - left), height ?? Math.Max(PptUnits.EmuPerCm, area.Bottom - top));
            var used = kind.IsPie ? model.Series.Take(1).ToList() : model.Series;
            var workbook = PptChartWorkbook.Create(model, used);

            var part = slide.Part.AddNewPart<ChartPart>();
            string relationshipId;
            try
            {
                relationshipId = StoreWorkbook(part, workbook);
                part.ChartSpace = PptChartBuilder.Build(kind, model, relationshipId);
            }
            catch
            {
                slide.Part.DeletePart(part);
                throw;
            }

            var id = PptSlides.NextShapeId(tree);
            var frame = new P.GraphicFrame(
                new P.NonVisualGraphicFrameProperties(
                    new P.NonVisualDrawingProperties { Id = id, Name = $"차트 {(id - 1).ToString(CultureInfo.InvariantCulture)}" },
                    new P.NonVisualGraphicFrameDrawingProperties(),
                    new P.ApplicationNonVisualDrawingProperties()),
                new P.Transform(new A.Offset { X = box.X, Y = box.Y }, new A.Extents { Cx = box.Width, Cy = box.Height }),
                new A.Graphic(new A.GraphicData(new C.ChartReference { Id = slide.Part.GetIdOfPart(part) }) { Uri = PptChartBuilder.GraphicUri }));

            PptFrames.Place(slide, tree, empty, frame);

            var content = $"Created {kind.Token} chart [{id.ToString(CultureInfo.InvariantCulture)}] with {model.Categories.Count.ToString(CultureInfo.InvariantCulture)} categories and {used.Count.ToString(CultureInfo.InvariantCulture)} series at x={PptUnits.CmText(box.X)} y={PptUnits.CmText(box.Y)} w={PptUnits.CmText(box.Width)} h={PptUnits.CmText(box.Height)} cm.";
            if (kind.IsPie && model.Series.Count > 1)
                content += " Only the first series is drawn in a pie or doughnut chart.";

            if (empty != null)
                content += " It took the place of the empty content placeholder.";

            return ToolResult.Changed(content, Change(slide, id), "차트 넣기");
        }

        private static ToolResult Update(PptSlideRef slide, ToolInput input, Request request)
        {
            var target = PptTargets.Find(slide, input.GetString("shape"));
            var id = PptTargets.IdOf(target);
            var rid = (target as P.GraphicFrame)?.Graphic?.GraphicData?.GetFirstChild<C.ChartReference>()?.Id?.Value;
            if (rid == null || !slide.Part.TryGetPartById(rid, out var found) || found is not ChartPart part)
                throw new ToolInputException($"{id.ToString(CultureInfo.InvariantCulture)}번은 차트가 아니에요. 차트 번호를 알려 주세요. {PptTargets.Choices(slide)}");

            if (request.IsEmpty)
                throw new ToolInputException("바꿀 내용을 알려 주세요. chart_type, title, categories, series, legend, data_labels 중 하나 이상이 필요해요.");

            var current = PptChartReader.Read(part)
                ?? throw new ToolInputException($"{id.ToString(CultureInfo.InvariantCulture)}번 차트의 내용을 읽지 못해서 고칠 수 없어요.");

            var currentKind = PptChartKinds.Of(current);
            var chart = part.ChartSpace.GetFirstChild<C.Chart>();
            if (request.Categories == null && request.Series == null && (request.Kind == null || request.Kind == currentKind))
            {
                if (request.Title != null)
                    PptChartEdits.SetTitle(chart, request.Title);

                if (request.Legend != null)
                    PptChartEdits.SetLegend(chart, request.Legend);

                if (request.DataLabels != null)
                    PptChartEdits.SetDataLabels(chart, request.DataLabels.Value);

                return ToolResult.Changed(
                    $"Updated chart [{id.ToString(CultureInfo.InvariantCulture)}] in place (title, legend or data labels); data and formatting are unchanged.",
                    Change(slide, id),
                    "차트 고침");
            }

            if (currentKind == null || current.GroupCount > 1)
                throw new ToolInputException($"{id.ToString(CultureInfo.InvariantCulture)}번 차트는 종류가 달라서 데이터나 종류를 고칠 수 없어요. 제목, 범례, 값 표시만 고칠 수 있어요. 새 차트를 만들어 주세요.");

            var kind = request.Kind ?? currentKind;
            var categories = request.Categories ?? current.Categories;
            var model = new PptChartInfo
            {
                Title = request.Title != null ? request.Title : current.HasTitle ? current.Title : null,
                Categories = categories,
                Legend = request.Legend ?? current.Legend,
                DataLabels = request.DataLabels ?? current.DataLabels,
            };

            if (request.Series != null)
            {
                for (var i = 0; i < request.Series.Count; i++)
                {
                    if (request.Series[i].Color == null && i < current.Series.Count)
                        request.Series[i].Color = current.Series[i].Color;
                }

                model.Series = request.Series;
            }
            else
            {
                model.Series = current.Series.Select((s, i) => new PptSeriesInfo
                {
                    Name = string.IsNullOrWhiteSpace(s.Name) ? DefaultName(i) : s.Name,
                    Values = s.Values.ToList(),
                    Color = s.Color,
                }).ToList();
            }

            if (model.Categories.Count == 0 || model.Series.Count == 0)
                throw new ToolInputException("차트에 항목(categories)과 계열(series)이 하나 이상 있어야 해요.");

            Complete(model);

            var used = kind.IsPie ? model.Series.Take(1).ToList() : model.Series;
            var workbook = PptChartWorkbook.Create(model, used);
            var externalId = part.ChartSpace?.GetFirstChild<C.ExternalData>()?.Id?.Value;
            OpenXmlPart embedded = null;
            if (externalId != null && part.TryGetPartById(externalId, out var existing) && existing.ContentType == PptChartWorkbook.ContentType)
                embedded = existing;

            string relationshipId;
            if (embedded != null)
            {
                using (var stream = new MemoryStream(workbook, false))
                    embedded.FeedData(stream);

                relationshipId = externalId;
            }
            else
            {
                relationshipId = StoreWorkbook(part, workbook);
            }

            part.ChartSpace = PptChartBuilder.Build(kind, model, relationshipId);

            var content = $"Updated {kind.Token} chart [{id.ToString(CultureInfo.InvariantCulture)}]: {model.Categories.Count.ToString(CultureInfo.InvariantCulture)} categories, {used.Count.ToString(CultureInfo.InvariantCulture)} series. The chart was rebuilt from this data, so custom formatting of the old chart (fonts, 3D, axis options) is replaced.";
            if (kind.IsPie && model.Series.Count > 1)
                content += " Only the first series is drawn in a pie or doughnut chart.";

            return ToolResult.Changed(content, Change(slide, id), "차트 고침");
        }

        private static void Complete(PptChartInfo model)
        {
            if (model.Categories.Count == 0)
                throw new ToolInputException("categories에 항목 이름을 하나 이상 알려 주세요.");

            if (model.Series.Count == 0)
                throw new ToolInputException("series에 계열을 하나 이상 알려 주세요.");

            if (model.Categories.Count * model.Series.Count > PptChartBuilder.MaxPoints)
                throw new ToolInputException($"차트에는 값을 {PptChartBuilder.MaxPoints.ToString(CultureInfo.InvariantCulture)}개까지만 넣을 수 있어요.");

            for (var i = 0; i < model.Series.Count; i++)
            {
                var series = model.Series[i];
                if (string.IsNullOrWhiteSpace(series.Name))
                    series.Name = DefaultName(i);

                if (series.Values.Count > model.Categories.Count)
                {
                    throw new ToolInputException(
                        $"'{series.Name}' 계열의 값이 {series.Values.Count.ToString(CultureInfo.InvariantCulture)}개인데 항목(categories)은 {model.Categories.Count.ToString(CultureInfo.InvariantCulture)}개예요. 항목 하나에 값 하나씩 알려 주세요.");
                }

                while (series.Values.Count < model.Categories.Count)
                    series.Values.Add(null);
            }
        }

        private static string StoreWorkbook(ChartPart part, byte[] workbook)
        {
            var embedded = part.AddExtendedPart(PptChartWorkbook.RelationshipType, PptChartWorkbook.ContentType, ".xlsx");
            using (var stream = new MemoryStream(workbook, false))
                embedded.FeedData(stream);

            return part.GetIdOfPart(embedded);
        }

        private static string DefaultName(int index) => $"계열 {(index + 1).ToString(CultureInfo.InvariantCulture)}";

        private static DocuChange Change(PptSlideRef slide, uint id) => new()
        {
            Location = $"{slide.Number.ToString(CultureInfo.InvariantCulture)}번",
            Icon = "chart",
            Anchors = { PptAnchor.Shape(slide.SlideId, id) },
        };

        private sealed class Request
        {
            public PptChartKind Kind { get; private set; }

            public string Title { get; private set; }

            public List<string> Categories { get; private set; }

            public List<PptSeriesInfo> Series { get; private set; }

            public string Legend { get; private set; }

            public bool? DataLabels { get; private set; }

            public bool IsEmpty => Kind == null && Title == null && Categories == null && Series == null && Legend == null && DataLabels == null;

            public static Request Read(ToolInput input)
            {
                var request = new Request
                {
                    Kind = input.Has("chart_type") ? PptChartKinds.Find(input.GetString("chart_type")) : null,
                    Title = PptInputs.Text(input, "title", MaxTitleLength),
                    Legend = PptInputs.Choice(input, "legend", s_legends),
                    DataLabels = PptInputs.Bool(input, "data_labels"),
                    Categories = input.Has("categories") ? ReadCategories(input.Get("categories")) : null,
                    Series = input.Has("series") ? ReadSeries(input.Get("series")) : null,
                };

                return request;
            }

            private static List<string> ReadCategories(JsonElement value)
            {
                if (value.ValueKind != JsonValueKind.Array)
                    throw new ToolInputException("'categories' 값은 항목 이름 목록이어야 해요.");

                if (value.GetArrayLength() > PptChartBuilder.MaxCategories)
                    throw new ToolInputException($"항목(categories)은 {PptChartBuilder.MaxCategories.ToString(CultureInfo.InvariantCulture)}개까지만 쓸 수 있어요.");

                var result = new List<string>();
                foreach (var item in value.EnumerateArray())
                {
                    if (item.ValueKind is not (JsonValueKind.String or JsonValueKind.Number))
                        throw new ToolInputException("'categories' 값은 항목 이름(글) 목록이어야 해요.");

                    var text = PptText.Clean(ToolInput.AsText(item) ?? string.Empty).Replace('\n', ' ').Trim();
                    if (text.Length > MaxTextLength)
                        throw new ToolInputException($"항목 이름은 {MaxTextLength.ToString(CultureInfo.InvariantCulture)}자 안으로 줄여 주세요.");

                    result.Add(text);
                }

                return result;
            }

            private static List<PptSeriesInfo> ReadSeries(JsonElement value)
            {
                if (value.ValueKind != JsonValueKind.Array)
                    throw new ToolInputException("'series' 값은 계열 목록이어야 해요. 예: [{\"name\":\"매출\",\"values\":[1,2,3]}]");

                if (value.GetArrayLength() > PptChartBuilder.MaxSeries)
                    throw new ToolInputException($"계열(series)은 {PptChartBuilder.MaxSeries.ToString(CultureInfo.InvariantCulture)}개까지만 쓸 수 있어요.");

                var result = new List<PptSeriesInfo>();
                foreach (var item in value.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Array)
                        throw new ToolInputException("계열마다 name(이름)과 values(값 목록)이 필요해요. 예: {\"name\":\"매출\",\"values\":[1,2,3]}");

                    var series = new PptSeriesInfo();
                    if (item.TryGetProperty("name", out var name) && name.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                        series.Name = PptText.Clean(ToolInput.AsText(name) ?? string.Empty).Replace('\n', ' ').Trim();

                    if (series.Name != null && series.Name.Length > MaxTextLength)
                        throw new ToolInputException($"계열 이름은 {MaxTextLength.ToString(CultureInfo.InvariantCulture)}자 안으로 줄여 주세요.");

                    foreach (var number in values.EnumerateArray())
                        series.Values.Add(ReadValue(number));

                    if (item.TryGetProperty("color", out var color) && color.ValueKind == JsonValueKind.String)
                    {
                        if (!PptColor.TryParse(color.GetString(), false, out var spec) || !spec.HasColor)
                            throw new ToolInputException($"'color' 값을 색으로 읽지 못했어요. {PptColor.InvalidMessage}");

                        series.Color = spec.ToString();
                    }

                    result.Add(series);
                }

                return result;
            }

            private static double? ReadValue(JsonElement number)
            {
                switch (number.ValueKind)
                {
                    case JsonValueKind.Null:
                        return null;
                    case JsonValueKind.Number:
                        var real = number.GetDouble();
                        if (double.IsFinite(real))
                            return real;

                        break;
                    case JsonValueKind.String:
                        var text = (number.GetString() ?? string.Empty).Trim();
                        if (text.Length == 0)
                            return null;

                        if (double.TryParse(text.Replace(",", string.Empty), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && double.IsFinite(parsed))
                            return parsed;

                        break;
                }

                throw new ToolInputException("값(values)에는 숫자나 null만 넣을 수 있어요.");
            }
        }
    }
}
