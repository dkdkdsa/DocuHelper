using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocuHelper.Docus;
using DocuHelper.Importers.Hwp;
using DocuHelper.Tools;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using Wp = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class EditObjectTool : WordTool
    {
        private const double MaxCm = 100;

        private const double MinCm = 0.1;

        private const double MaxOffsetCm = 500;

        private const double MaxDistanceCm = 100;

        private const double MaxRotationDeg = 3600;

        private const uint BaseHeight = 251658240;

        private static readonly string[] s_wraps = { "inline", "square", "tight", "through", "top_bottom", "behind", "in_front" };

        public override string Name => "edit_object";

        public override string Description =>
            "Insert, change or delete a picture, shape, text box or chart. insert needs kind and at; format and delete need the object id from read_document (what=objects). "
            + "A picture comes from a file path in the working folder or from base64 data; a chart takes categories and series, or the numbers of a table (from_table).";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "action": {"type": "string", "enum": ["insert", "format", "delete"]},
                "kind": {"type": "string", "enum": ["picture", "shape", "textbox", "chart"], "description": "insert only."},
                "id": {"type": "string", "description": "format, delete: object id like \"o:12\" from read_document."},
                "at": {"description": "insert: paragraph or table id (a new paragraph is added after it), {id, offset} inside a paragraph, or \"start\"/\"end\".", "oneOf": [{"type": "string"}, {"type": "object", "properties": {"id": {"type": "string"}, "offset": {"type": "integer"}}, "required": ["id"]}]},
                "picture": {"type": "object", "properties": {"path": {"type": "string", "description": "Image file (png, jpg, gif, bmp) inside the working folder, relative to it or absolute. Files elsewhere must be sent by the app as data_base64."}, "data_base64": {"type": "string", "description": "Alternative to path, up to 8 MB."}, "alt": {"type": "string"}}},
                "shape": {"type": "object", "properties": {"preset": {"type": "string", "description": "rect, roundRect, ellipse, triangle, diamond, pentagon, hexagon, star5, rightArrow, leftArrow, upArrow, downArrow, line, straightArrow, wedgeRoundRectCallout, flowChartProcess, flowChartDecision."}, "fill": {"type": "string", "description": "#RRGGBB or \"none\"."}, "line_color": {"type": "string"}, "line_width_pt": {"type": "number"}, "line_dash": {"type": "string", "enum": ["solid", "dash", "dot"]}, "text": {"type": "string"}, "text_color": {"type": "string"}, "font_size_pt": {"type": "number"}, "align": {"type": "string", "enum": ["left", "center", "right"]}, "text_direction": {"type": "string", "enum": ["horizontal", "vertical"]}}},
                "chart": {"type": "object", "properties": {"type": {"type": "string", "enum": ["column", "bar", "line", "pie", "area", "scatter", "doughnut"]}, "title": {"type": "string"}, "categories": {"type": "array", "items": {"type": "string"}}, "series": {"type": "array", "items": {"type": "object", "properties": {"name": {"type": "string"}, "values": {"type": "array", "items": {"type": "number"}}}, "required": ["name", "values"]}}, "from_table": {"type": "string", "description": "Table id: first row = series names, first column = categories."}, "legend": {"type": "string", "enum": ["none", "bottom", "right", "top"]}, "data_labels": {"type": "boolean"}}},
                "layout": {"type": "object", "properties": {"wrap": {"type": "string", "enum": ["inline", "square", "tight", "through", "top_bottom", "behind", "in_front"]}, "horizontal": {"type": "string", "enum": ["left", "center", "right", "inside", "outside"]}, "vertical": {"type": "string", "enum": ["top", "center", "bottom"]}, "offset_x_cm": {"type": "number"}, "offset_y_cm": {"type": "number"}, "relative_to": {"type": "string", "enum": ["margin", "page", "column", "paragraph"]}, "distance_cm": {"type": "number", "minimum": 0}}},
                "z_order": {"type": "string", "enum": ["bring_forward", "bring_to_front", "send_backward", "send_to_back", "in_front_of_text", "behind_text"]},
                "width_cm": {"type": "number", "minimum": 0.1, "maximum": 100},
                "height_cm": {"type": "number", "minimum": 0.1, "maximum": 100},
                "lock_aspect": {"type": "boolean"},
                "rotation_deg": {"type": "number"},
                "alt": {"type": "string"}
              },
              "required": ["action"]
            }
            """;

        protected override WordHwpSupport Hwp => WordHwpSupport.Partial;

        protected override ToolResult Run(WordDocu docu, ToolInput input)
        {
            var action = (input.GetString("action") ?? string.Empty).Trim().ToLowerInvariant();
            var snapshot = docu.Capture();
            try
            {
                return action switch
                {
                    "insert" => Insert(docu, input),
                    "format" => Format(docu, input),
                    "delete" => Delete(docu, input),
                    _ => throw new ToolInputException("action은 insert, format, delete 중 하나로 알려 주세요."),
                };
            }
            catch
            {
                docu.Restore(snapshot);
                throw;
            }
        }

        private ToolResult Insert(WordDocu docu, ToolInput input)
        {
            var kind = (input.GetOptionalString("kind") ?? string.Empty).Trim().ToLowerInvariant();
            if (kind is not ("picture" or "shape" or "textbox" or "chart"))
                throw new ToolInputException("kind는 picture, shape, textbox, chart 중 하나로 알려 주세요.");

            var document = docu.Document;
            var scan = WordScan.Read(document);
            if (docu is HwpDocu && kind != "picture")
                return InsertInHwp(docu, scan, input, kind);

            if (docu is HwpDocu)
                HwpGuard.Require(docu, HwpFeature.Pictures, "그림");

            var at = input.Has("at") ? input.Get("at") : default;
            if (at.ValueKind == JsonValueKind.Undefined || at.ValueKind == JsonValueKind.Null)
                throw new ToolInputException("개체를 넣을 위치를 at으로 알려 주세요. 문단 id, \"start\", \"end\", 또는 {id, offset}을 넣어 주세요.");

            var id = WordDrawings.NextDocPrId(document);
            var layout = input.Has("layout") ? input.Get("layout") : default;
            var main = document.MainDocumentPart;
            var wrap = Wrap(layout);
            Run run;
            string name;
            string detail;
            string icon;
            switch (kind)
            {
                case "picture":
                    var picture = input.Get("picture");
                    var image = ReadPicture(docu, picture);
                    var info = WordImageSniffer.Read(image.Bytes, image.Name);
                    var (width, height) = PictureSize(info, input, scan);
                    var frame = Frame(id, $"그림 {id}", AltText(picture, input), wrap, width, height, document, layout);
                    var part = WordDrawings.Image(main, image.Bytes, info.Mime, out var relationship);
                    run = WordDrawings.RunOf(WordDrawings.Build(frame, WordDrawings.Picture(relationship, frame.Name, Rotation(input), frame.Cx, frame.Cy)));
                    name = "그림";
                    detail = "그림 1개";
                    icon = "image";
                    break;
                case "shape":
                case "textbox":
                    var textBox = kind == "textbox";
                    var look = WordShapeXml.Parse(input.Has("shape") ? input.Get("shape") : default, textBox);
                    var (shapeWidth, shapeHeight) = Explicit(input, textBox ? 5 : 4, textBox ? 3 : 4);
                    var shapeFrame = Frame(id, textBox ? $"텍스트 상자 {id}" : $"도형 {id}", AltText(default, input), wrap, shapeWidth, shapeHeight, document, layout);
                    var graphic = WordShapeXml.Graphic(look, textBox, Rotation(input), shapeFrame.Cx, shapeFrame.Cy);
                    run = new Run(new AlternateContent(WordDrawings.AlternateContent(WordDrawings.Envelope(WordDrawings.Container(shapeFrame, graphic)))));
                    name = textBox ? "글상자" : "도형";
                    detail = "도형 1개";
                    icon = "shapes";
                    break;
                default:
                    var chart = input.Get("chart");
                    var prepared = WordChartXml.Prepare(chart, scan);
                    var (chartWidth, chartHeight) = Explicit(input, 15, 9);
                    var chartPart = main.AddNewPart<ChartPart>();
                    var workbook = chartPart.AddEmbeddedPackagePart(WordEmbeddedWorkbook.ContentType);
                    using (var stream = new MemoryStream(prepared.Workbook, false))
                        workbook.FeedData(stream);

                    chartPart.ChartSpace = new C.ChartSpace(WordChartXml.WithData(prepared.Xml, chartPart.GetIdOfPart(workbook)));
                    var chartRelationship = main.GetIdOfPart(chartPart);
                    var chartFrame = Frame(id, $"차트 {id}", AltText(default, input), wrap, chartWidth, chartHeight, document, layout);
                    run = WordDrawings.RunOf(WordDrawings.Build(chartFrame, WordDrawings.Chart(chartRelationship)));
                    name = "차트";
                    detail = "차트 1개";
                    icon = "chart-column";
                    break;
            }

            var track = WordRevisions.Tracking(docu);
            var ctx = track ? WordRevisions.Active(docu) : null;
            var hostParagraph = Place(docu, scan, at, run, out var fresh);
            if (track)
            {
                WordInline.Track(ctx, new[] { run });
                if (fresh)
                    WordRevisions.MarkParagraphMark(hostParagraph, true, ctx);
            }

            var hostId = WordIds.Of(hostParagraph) ?? WordIds.Assign(hostParagraph, WordIds.Collect(document));
            var rescan = WordScan.Read(document);
            var location = WordInline.LocationOf(rescan, hostParagraph) ?? "문서";
            var content = $"Inserted {name} o:{id} after {hostId}.";
            return ToolResult.Changed(content, Change(location, icon, HostAnchors(hostId, id)), detail);
        }

        private ToolResult Format(WordDocu docu, ToolInput input)
        {
            var document = docu.Document;
            var id = ObjectId(input);
            var (drawing, properties) = WordDrawings.Find(document, id);
            if (drawing == null)
                throw NotFound(id);

            var extent = drawing.Descendants<Wp.Extent>().First();
            var changed = new List<string>();
            var layout = input.Has("layout") ? input.Get("layout") : default;
            var width = Number(input, "width_cm");
            var height = Number(input, "height_cm");
            if (width.HasValue || height.HasValue)
            {
                var (cx, cy) = Resize(extent.Cx.Value, extent.Cy.Value, width, height, input.GetOptionalBool("lock_aspect") ?? true);
                SetSize(drawing, cx, cy);
                changed.Add("크기");
            }

            if (input.Has("alt"))
            {
                properties.Description = input.GetOptionalString("alt") ?? string.Empty;
                changed.Add("대체 텍스트");
            }

            var rotation = Rotation(input);
            if (rotation != null)
            {
                foreach (var transform in drawing.Descendants<A.Transform2D>())
                    transform.Rotation = int.Parse(rotation, CultureInfo.InvariantCulture);

                changed.Add("회전");
            }

            if (input.Has("layout"))
            {
                Rewrap(document, drawing, layout);
                changed.Add("배치");
            }

            if (input.Has("z_order"))
            {
                ZOrder(document, drawing, input.GetString("z_order"));
                changed.Add("앞뒤 순서");
            }

            if (changed.Count == 0)
                throw new ToolInputException("바꿀 것을 알려 주세요. width_cm, height_cm, alt, rotation_deg, layout, z_order 중 하나를 넣어 주세요.");

            var paragraph = drawing.Ancestors<Paragraph>().FirstOrDefault();
            var hostId = WordIds.Of(paragraph) ?? string.Empty;
            var location = WordInline.LocationOf(WordScan.Read(document), paragraph) ?? "문서";
            return ToolResult.Changed(
                $"Changed o:{id}: {string.Join(", ", changed)}.",
                Change(location, "pencil", HostAnchors(hostId, id)),
                string.Join(", ", changed));
        }

        private ToolResult Delete(WordDocu docu, ToolInput input)
        {
            var document = docu.Document;
            var id = ObjectId(input);
            var (drawing, _) = WordDrawings.Find(document, id);
            if (drawing == null)
                throw NotFound(id);

            var paragraph = drawing.Ancestors<Paragraph>().FirstOrDefault();
            var hostId = WordIds.Of(paragraph) ?? string.Empty;
            var location = WordInline.LocationOf(WordScan.Read(document), paragraph) ?? "문서";
            var relationships = WordDrawings.RelationshipsOf(drawing).ToList();
            var owner = WordDrawings.StoryOf(document, drawing);
            var target = drawing.Ancestors<AlternateContent>().FirstOrDefault() ?? (OpenXmlElement)drawing;
            var run = target.Ancestors<Run>().FirstOrDefault();
            if (WordRevisions.Tracking(docu) && run != null)
            {
                WordRevisions.MarkDeleted(new[] { run }, WordRevisions.Active(docu));
            }
            else
            {
                target.Remove();
                if (run != null && !run.ChildElements.Any(e => e is not RunProperties))
                    run.Remove();

                WordDrawings.ReleaseUnused(owner, relationships);
            }

            return ToolResult.Changed(
                $"Deleted o:{id} from {hostId}.",
                Change(location, "trash", HostAnchors(hostId, id)),
                "개체 1개 지움");
        }

        private ToolResult InsertInHwp(WordDocu docu, WordScan scan, ToolInput input, string kind)
        {
            if (kind == "textbox")
            {
                var text = input.Has("shape") ? input.Get("shape") : default;
                var look = WordShapeXml.Parse(text, true);
                var insertion = WordInsertion.Resolve(scan, input.GetOptionalString("at"));
                var table = WordTables.Build(new List<IReadOnlyList<string>> { new List<string> { look.Text ?? string.Empty } }, false, WordPageWidth(scan), WordIds.Collect(docu.Document));
                insertion.Insert(table);
                var rescan = WordScan.Read(docu.Document);
                var block = rescan.Of(table);
                var id = block?.Id ?? WordTables.IdOf(table);
                return ToolResult.Changed(
                    $"Inserted a 1x1 table {id} in place of the text box (text boxes are not kept in 한글 files).",
                    Change(WordScan.PageLabel(new[] { block }), "textbox", new[] { WordAnchors.Table(id) }),
                    "글상자 1개(표로 넣음)");
            }

            var label = kind == "picture" ? "그림" : kind == "shape" ? "도형" : "차트";
            HwpGuard.Refuse(docu, label, "한글에서 직접 넣어 주세요.");
            return null;
        }

        private static int WordPageWidth(WordScan scan)
        {
            WordPager.TextArea(scan.Body, out var width, out _);
            return width;
        }

        private static Paragraph Place(WordDocu docu, WordScan scan, JsonElement at, Run run, out bool fresh)
        {
            if (at.ValueKind == JsonValueKind.Object)
            {
                var id = at.TryGetProperty("id", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                var offset = 0;
                if (at.TryGetProperty("offset", out var position) && !position.TryGetInt32(out offset))
                    throw new ToolInputException("offset은 정수로 알려 주세요.");
                var block = scan.Find(id ?? string.Empty) ?? throw new ToolInputException($"‘{id}’ 문단을 찾지 못했어요. read_paragraphs로 id를 다시 확인해 주세요.");
                if (block.Paragraph == null)
                    throw new ToolInputException("개체는 문단 안 위치(offset)로만 넣을 수 있어요. 문단 id를 알려 주세요.");

                var map = WordTextMap.Build(block.Paragraph);
                if (offset < 0 || offset > map.Length)
                    throw new ToolInputException($"offset은 0~{map.Length} 사이로 알려 주세요.");

                WordInline.Place(map, offset, new OpenXmlElement[] { run });
                fresh = false;
                return block.Paragraph;
            }

            var key = at.ValueKind == JsonValueKind.String ? at.GetString() : null;
            var insertion = WordInsertion.Resolve(scan, key);
            var paragraph = new Paragraph(run);
            WordIds.Assign(paragraph, WordIds.Collect(docu.Document));
            insertion.Insert(paragraph);
            fresh = true;
            return paragraph;
        }

        private static WordDrawings.Frame Frame(uint id, string name, string alt, string wrap, double width, double height, WordprocessingDocument document, JsonElement layout)
        {
            var frame = new WordDrawings.Frame
            {
                Wrap = wrap,
                Cx = WordDrawings.Emu(width),
                Cy = WordDrawings.Emu(height),
                DocPrId = id,
                Name = name,
                Alt = alt,
                RelativeHeight = NextHeight(document),
            };
            ApplyPosition(frame, layout);
            return frame;
        }

        private static void ApplyPosition(WordDrawings.Frame frame, JsonElement layout)
        {
            if (layout.ValueKind != JsonValueKind.Object)
                return;

            var horizontal = Text(layout, "horizontal");
            if (horizontal != null)
            {
                if (horizontal is not ("left" or "center" or "right" or "inside" or "outside"))
                    throw new ToolInputException("horizontal은 left, center, right, inside, outside 중 하나로 알려 주세요.");

                frame.HAlign = horizontal;
            }

            var vertical = Text(layout, "vertical");
            if (vertical != null)
            {
                if (vertical is not ("top" or "center" or "bottom"))
                    throw new ToolInputException("vertical은 top, center, bottom 중 하나로 알려 주세요.");

                frame.VAlign = vertical;
            }

            var x = Cm(layout, "offset_x_cm");
            if (x.HasValue)
            {
                frame.HOffset = WordDrawings.Emu(x.Value);
                frame.HAlign = null;
            }

            var y = Cm(layout, "offset_y_cm");
            if (y.HasValue)
            {
                frame.VOffset = WordDrawings.Emu(y.Value);
                frame.VAlign = null;
            }

            var relative = Text(layout, "relative_to");
            if (relative != null)
            {
                if (relative is not ("margin" or "page" or "column" or "paragraph"))
                    throw new ToolInputException("relative_to는 margin, page, column, paragraph 중 하나로 알려 주세요.");

                frame.HRelative = relative == "paragraph" ? "column" : relative;
                frame.VRelative = relative == "column" ? "paragraph" : relative;
            }

            var distance = Cm(layout, "distance_cm");
            if (distance.HasValue)
                frame.Distance = WordDrawings.Emu(distance.Value);
        }

        private static string Wrap(JsonElement layout)
        {
            if (layout.ValueKind != JsonValueKind.Object)
                return "inline";

            var wrap = Text(layout, "wrap") ?? "inline";
            if (!s_wraps.Contains(wrap))
                throw new ToolInputException($"wrap은 {string.Join(", ", s_wraps)} 중 하나로 알려 주세요.");

            return wrap;
        }

        private static uint NextHeight(WordprocessingDocument document)
        {
            var anchors = document.MainDocumentPart?.Document?.Descendants<Wp.Anchor>().Select(a => a.RelativeHeight?.Value ?? 0u).ToList() ?? new List<uint>();
            return anchors.Count == 0 ? BaseHeight : anchors.Max() + 1024;
        }

        private static (double Width, double Height) PictureSize(DocuHelper.Tools.Ppt.PptImageInfo info, ToolInput input, WordScan scan)
        {
            var naturalWidth = info.WidthPx / 96.0 * 2.54;
            var naturalHeight = info.HeightPx / 96.0 * 2.54;
            var textWidth = WordPageWidth(scan) / 1440.0 * 2.54;
            var width = Number(input, "width_cm");
            var height = Number(input, "height_cm");
            if (width.HasValue && height.HasValue)
                return (width.Value, height.Value);

            if (width.HasValue)
                return (width.Value, width.Value * naturalHeight / naturalWidth);

            if (height.HasValue)
                return (height.Value * naturalWidth / naturalHeight, height.Value);

            var scale = Math.Min(1.0, textWidth / naturalWidth);
            return (naturalWidth * scale, naturalHeight * scale);
        }

        private static (double Width, double Height) Explicit(ToolInput input, double width, double height)
        {
            var w = Number(input, "width_cm");
            var h = Number(input, "height_cm");
            return (w ?? width, h ?? height);
        }

        private static (long Cx, long Cy) Resize(long cx, long cy, double? width, double? height, bool lockAspect)
        {
            var w = width.HasValue ? WordDrawings.Emu(width.Value) : (long?)null;
            var h = height.HasValue ? WordDrawings.Emu(height.Value) : (long?)null;
            if (w.HasValue && h.HasValue)
                return (w.Value, h.Value);

            if (!lockAspect)
                return (w ?? cx, h ?? cy);

            if (w.HasValue)
                return (w.Value, (long)Math.Round(w.Value * (double)cy / cx));

            return ((long)Math.Round(h.Value * (double)cx / cy), h.Value);
        }

        private static void SetSize(OpenXmlElement drawing, long cx, long cy)
        {
            foreach (var extent in drawing.Descendants<Wp.Extent>())
            {
                extent.Cx = cx;
                extent.Cy = cy;
            }

            var transform = drawing.Descendants<A.Extents>().FirstOrDefault();
            if (transform != null)
            {
                transform.Cx = cx;
                transform.Cy = cy;
            }
        }

        private static void Rewrap(WordprocessingDocument document, OpenXmlElement drawing, JsonElement layout)
        {
            var old = drawing;
            var graphic = drawing.Descendants<A.Graphic>().First().OuterXml;
            var extent = drawing.Descendants<Wp.Extent>().First();
            var properties = drawing.Descendants<Wp.DocProperties>().First();
            var anchor = drawing.Descendants<Wp.Anchor>().FirstOrDefault();
            var frame = new WordDrawings.Frame
            {
                Wrap = Text(layout, "wrap") == null ? ExistingWrap(anchor) : Wrap(layout),
                Cx = extent.Cx.Value,
                Cy = extent.Cy.Value,
                DocPrId = properties.Id.Value,
                Name = properties.Name?.Value,
                Alt = properties.Description?.Value,
                RelativeHeight = anchor?.RelativeHeight?.Value ?? NextHeight(document),
            };
            if (anchor != null)
            {
                var horizontal = anchor.Descendants().FirstOrDefault(d => d.LocalName == "positionH");
                var vertical = anchor.Descendants().FirstOrDefault(d => d.LocalName == "positionV");
                frame.HRelative = Attribute(horizontal, "relativeFrom") ?? frame.HRelative;
                frame.VRelative = Attribute(vertical, "relativeFrom") ?? frame.VRelative;
                frame.HAlign = Child(horizontal, "align");
                frame.VAlign = Child(vertical, "align");
                frame.HOffset = Offset(horizontal);
                frame.VOffset = Offset(vertical);
                if (long.TryParse(Attribute(anchor, "distT"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var distance))
                    frame.Distance = distance;
            }

            ApplyPosition(frame, layout);
            var replacement = WordDrawings.Build(frame, graphic);
            old.InsertAfterSelf(replacement);
            old.Remove();
        }

        private static string ExistingWrap(Wp.Anchor anchor)
        {
            if (anchor == null)
                return "inline";

            var wrap = anchor.ChildElements.FirstOrDefault(c => c.LocalName.StartsWith("wrap", StringComparison.Ordinal))?.LocalName;
            return wrap switch
            {
                "wrapSquare" => "square",
                "wrapTight" => "tight",
                "wrapThrough" => "through",
                "wrapTopAndBottom" => "top_bottom",
                _ => Attribute(anchor, "behindDoc") is "1" or "true" ? "behind" : "in_front",
            };
        }

        private static long? Offset(OpenXmlElement position)
        {
            var text = Child(position, "posOffset");
            return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : (long?)null;
        }

        private static void ZOrder(WordprocessingDocument document, OpenXmlElement drawing, string order)
        {
            var anchors = document.MainDocumentPart.Document.Descendants<Wp.Anchor>().ToList();
            var target = drawing.Descendants<Wp.Anchor>().FirstOrDefault() ?? drawing.Ancestors<Wp.Anchor>().FirstOrDefault();
            if (target == null)
                throw new ToolInputException("z_order는 글 둘레에 띄운 개체에만 쓸 수 있어요. 글과 같은 줄에 있는 개체는 앞뒤 순서를 바꿀 수 없어요.");

            if (order is "in_front_of_text" or "behind_text")
            {
                target.BehindDoc = order == "behind_text";
                return;
            }

            var ordered = anchors.OrderBy(a => a.RelativeHeight?.Value ?? 0u).ToList();
            var index = ordered.IndexOf(target);
            ordered.RemoveAt(index);
            switch (order)
            {
                case "bring_to_front":
                    ordered.Add(target);
                    break;
                case "send_to_back":
                    ordered.Insert(0, target);
                    break;
                case "bring_forward":
                    ordered.Insert(Math.Min(ordered.Count, index + 1), target);
                    break;
                case "send_backward":
                    ordered.Insert(Math.Max(0, index - 1), target);
                    break;
                default:
                    throw new ToolInputException("z_order는 bring_forward, bring_to_front, send_backward, send_to_back, in_front_of_text, behind_text 중 하나로 알려 주세요.");
            }

            for (var i = 0; i < ordered.Count; i++)
                ordered[i].RelativeHeight = BaseHeight + (uint)i * 1024;
        }

        private static uint ObjectId(ToolInput input)
        {
            var text = (input.GetOptionalString("id") ?? string.Empty).Trim();
            if (text.StartsWith("o:", StringComparison.OrdinalIgnoreCase))
                text = text.Substring(2);

            return uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0
                ? id
                : throw new ToolInputException("id는 read_document에서 본 개체 id(예: \"o:12\")로 알려 주세요.");
        }

        private static IEnumerable<string> HostAnchors(string hostId, uint id)
        {
            if (!string.IsNullOrEmpty(hostId))
                yield return WordAnchors.Paragraph(hostId);

            yield return WordAnchors.Object((int)id);
        }

        private static ToolInputException NotFound(uint id) =>
            new($"o:{id} 개체를 찾지 못했어요. read_document(what=objects)로 개체 id를 다시 확인해 주세요.");

        private static ReadPictureResult ReadPicture(WordDocu docu, JsonElement picture)
        {
            if (picture.ValueKind != JsonValueKind.Object)
                throw new ToolInputException("그림을 넣으려면 picture에 path나 data_base64를 알려 주세요.");

            var path = Text(picture, "path");
            var data = Text(picture, "data_base64");
            if ((path == null) == (data == null))
                throw new ToolInputException("picture에는 path나 data_base64 중 하나만 넣어 주세요.");

            if (data != null)
            {
                var value = data.Contains(';') && data.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ? data.Substring(data.IndexOf(',') + 1) : data;
                if (value.Length / 4L * 3 > WordImageSniffer.MaxBytes + 3)
                    throw new ToolInputException($"그림이 너무 커요. {WordImageSniffer.MaxBytes / (1024 * 1024)}MB 이하의 그림만 넣을 수 있어요.");

                try
                {
                    return new ReadPictureResult(Convert.FromBase64String(value), "그림");
                }
                catch (FormatException)
                {
                    throw new ToolInputException("data_base64 값을 읽지 못했어요. 그림 파일 내용을 base64로 알려 주세요.");
                }
            }

            var folder = docu.FilePath == null ? null : Path.GetDirectoryName(Path.GetFullPath(docu.FilePath));
            if (folder == null)
                throw new ToolInputException("이 파일의 작업 폴더를 알 수 없어서 그림을 넣을 수 없어요. 파일을 저장한 뒤 다시 해 주세요.");

            var full = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(folder, path));
            if (!string.Equals(Path.GetDirectoryName(full), folder, StringComparison.OrdinalIgnoreCase))
                throw new ToolInputException("그림은 작업 폴더 안의 파일만 넣을 수 있어요. 다른 위치의 그림은 앱이 data_base64로 보내야 해요.");

            if (!File.Exists(full))
                throw new ToolInputException($"‘{Path.GetFileName(full)}’ 그림 파일을 찾을 수 없어요.");

            if (new FileInfo(full).Length > WordImageSniffer.MaxBytes)
                throw new ToolInputException($"그림이 너무 커요. {WordImageSniffer.MaxBytes / (1024 * 1024)}MB 이하의 그림만 넣을 수 있어요.");

            return new ReadPictureResult(File.ReadAllBytes(full), Path.GetFileName(full));
        }

        private static string AltText(JsonElement picture, ToolInput input)
        {
            var alt = Text(picture, "alt") ?? input.GetOptionalString("alt");
            return WordText.Clean(alt ?? string.Empty);
        }

        private static string Rotation(ToolInput input)
        {
            var degrees = Number(input, "rotation_deg");
            return degrees.HasValue ? ((long)Math.Round(degrees.Value * 60000)).ToString(CultureInfo.InvariantCulture) : null;
        }

        private static double? Number(ToolInput input, string name)
        {
            if (!input.Has(name))
                return null;

            var value = input.Get(name);
            if (value.ValueKind != JsonValueKind.Number)
                throw new ToolInputException($"{name}은 숫자로 알려 주세요.");

            var number = value.GetDouble();
            if (name is "width_cm" or "height_cm" && (number < MinCm || number > MaxCm))
                throw new ToolInputException($"{name}은 {MinCm}~{MaxCm} 사이로 알려 주세요.");

            if (name == "rotation_deg" && Math.Abs(number) > MaxRotationDeg)
                throw new ToolInputException($"rotation_deg는 -{MaxRotationDeg}~{MaxRotationDeg} 사이로 알려 주세요.");

            return number;
        }

        private static double? Cm(JsonElement element, string name)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number)
                return null;

            var number = value.GetDouble();
            var limit = name == "distance_cm" ? MaxDistanceCm : MaxOffsetCm;
            var min = name == "distance_cm" ? 0 : -limit;
            if (number < min || number > limit)
                throw new ToolInputException($"{name}은 {min}~{limit} 사이로 알려 주세요.");

            return number;
        }

        private static string Text(JsonElement element, string name) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()?.Trim()
                : null;

        private static string Attribute(OpenXmlElement element, string name) =>
            element?.GetAttributes().FirstOrDefault(a => a.LocalName == name).Value;

        private static string Child(OpenXmlElement parent, string name) =>
            parent?.Descendants().FirstOrDefault(d => d.LocalName == name)?.InnerText;

        private sealed record ReadPictureResult(byte[] Bytes, string Name);
    }
}
