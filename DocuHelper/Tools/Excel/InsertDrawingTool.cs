using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using DocuHelper.Docus;
using DocuHelper.Tools.Ppt;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    public class InsertDrawingTool : ExcelTool
    {
        private const int MaxTextLength = 4000;
        private const double MaxPictureWidth = 640;
        private const int DefaultColumns = 3;
        private const int DefaultRows = 2;

        private const int MaxSpanColumns = 40;

        private const int MaxSpanRows = 80;

        public override string Name => "insert_drawing";

        public override string Description =>
            "Place a picture, a shape or a text box on a sheet, like Excel 삽입 > 그림 / 도형 / 텍스트 상자. kind picture needs source (a PNG, JPEG, GIF or BMP file in the document's folder, or an absolute path inside that folder, up to 10 MB). "
            + "kind shape needs shape.geometry (rect, roundRect, ellipse, triangle, rtTriangle, diamond, pentagon, hexagon, rightArrow, leftArrow, upArrow, downArrow, leftRightArrow, star5, heart, cloud, wedgeRectCallout, lightningBolt, line); "
            + "kind text_box needs text. anchor is the top-left cell; columns and rows give the size in cells (shapes and text boxes default to 3 columns by 2 rows; a picture keeps its own proportions unless you give both). "
            + "Move, resize, restyle or delete it later with change_object.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "sheet": {"type": "string", "description": "Sheet name."},
                "kind": {"type": "string", "enum": ["picture", "shape", "text_box"], "description": "picture, shape or text_box."},
                "anchor": {"type": "string", "description": "Top-left cell, e.g. \"H2\"."},
                "columns": {"type": "integer", "description": "Width in columns (shape/text_box default 3).", "minimum": 1, "maximum": 40},
                "rows": {"type": "integer", "description": "Height in rows (shape/text_box default 2).", "minimum": 1, "maximum": 80},
                "source": {"type": "string", "description": "picture: image path (file name in the document's folder or an absolute path inside it)."},
                "alt_text": {"type": "string", "description": "picture/shape: description for screen readers."},
                "shape": {
                  "type": "object",
                  "description": "shape: look.",
                  "properties": {
                    "geometry": {"type": "string", "description": "Preset geometry name."},
                    "fill": {"type": "string", "description": "Hex colour or \"none\"."},
                    "line_color": {"type": "string", "description": "Hex colour or \"none\"."},
                    "line_width": {"type": "number", "description": "Points.", "minimum": 0, "maximum": 20}
                  }
                },
                "text": {"type": "string", "description": "shape/text_box: text inside."},
                "font_size": {"type": "number", "description": "Text size in points.", "minimum": 6, "maximum": 96},
                "font_color": {"type": "string", "description": "Hex colour."},
                "bold": {"type": "boolean", "description": "Bold text."},
                "align": {"type": "string", "description": "Text alignment.", "enum": ["left", "center", "right"]}
              },
              "required": ["sheet", "kind", "anchor"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var sheet = ResolveSheet(book, input, null, true);
            var kind = input.GetString("kind").Trim().ToLowerInvariant();
            if (kind is not ("picture" or "shape" or "text_box"))
                throw new ToolInputException("kind는 picture, shape, text_box 중 하나로 알려 주세요.");

            var anchor = ParseCell(input.GetString("anchor"));
            var columns = input.GetOptionalInt("columns");
            var rows = input.GetOptionalInt("rows");
            if (columns is < 1 or > MaxSpanColumns || rows is < 1 or > MaxSpanRows)
                throw new ToolInputException($"columns는 1부터 {MaxSpanColumns}, rows는 1부터 {MaxSpanRows} 사이로 알려 주세요.");

            var look = Look.Read(input);
            var picture = kind == "picture" ? LoadPicture(docu, input) : null;
            var width = kind == "picture"
                ? PictureWidth(picture, columns, rows, sheet, anchor)
                : ExcelDrawings.WidthOf(sheet, anchor.Column, columns ?? DefaultColumns);
            var height = kind == "picture"
                ? PictureHeight(picture, columns, rows, sheet, anchor)
                : ExcelDrawings.HeightOf(sheet, anchor.Row, rows ?? DefaultRows);

            var part = ExcelDrawings.EnsureDrawings(sheet);
            var prefix = kind == "picture" ? "그림" : kind == "shape" ? "도형" : "텍스트 상자";
            var name = ExcelDrawings.NextName(part, prefix);
            var id = ExcelDrawings.NextId(part);
            var alt = input.GetOptionalString("alt_text")?.Trim() ?? string.Empty;
            var descr = alt.Length == 0 ? string.Empty : $" descr=\"{ExcelDrawings.Text(alt)}\"";
            var extent = $"<a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"{ExcelDrawings.Emu(width)}\" cy=\"{ExcelDrawings.Emu(height)}\"/></a:xfrm>";
            string objectXml;
            if (kind == "picture")
            {
                var relation = ExcelDrawings.ImageRelationship(part, picture.Bytes, picture.Info.Mime);
                objectXml = $"<xdr:pic><xdr:nvPicPr><xdr:cNvPr id=\"{id}\" name=\"{ExcelDrawings.Text(name)}\"{descr}/>"
                    + "<xdr:cNvPicPr><a:picLocks noChangeAspect=\"1\"/></xdr:cNvPicPr></xdr:nvPicPr>"
                    + $"<xdr:blipFill><a:blip r:embed=\"{relation}\"/><a:stretch><a:fillRect/></a:stretch></xdr:blipFill>"
                    + $"<xdr:spPr>{extent}<a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></xdr:spPr></xdr:pic>";
            }
            else
            {
                var text = kind == "text_box";
                var geometry = text ? "rect" : look.Geometry;
                var fill = look.Fill ?? (text ? "none" : "DEEBF7");
                var line = look.Line ?? (text ? "none" : "2F75B5");
                var box = text ? "<xdr:cNvSpPr txBox=\"1\"/>" : "<xdr:cNvSpPr/>";
                objectXml = $"<xdr:sp macro=\"\" textlink=\"\"><xdr:nvSpPr><xdr:cNvPr id=\"{id}\" name=\"{ExcelDrawings.Text(name)}\"{descr}/>{box}</xdr:nvSpPr>"
                    + $"<xdr:spPr>{extent}<a:prstGeom prst=\"{geometry}\"><a:avLst/></a:prstGeom>{FillXml(fill)}{LineXml(line, look.LineWidth ?? 1)}</xdr:spPr>"
                    + $"<xdr:txBody><a:bodyPr wrap=\"square\" rtlCol=\"0\" anchor=\"{(text ? "t" : "ctr")}\"/><a:lstStyle/>{ParagraphsXml(look)}</xdr:txBody></xdr:sp>";
            }

            var xml = $"<xdr:oneCellAnchor {ExcelDrawings.Namespaces}>{ExcelDrawings.Marker("from", anchor.Column, anchor.Row)}"
                + $"<xdr:ext cx=\"{ExcelDrawings.Emu(width)}\" cy=\"{ExcelDrawings.Emu(height)}\"/>{objectXml}<xdr:clientData/></xdr:oneCellAnchor>";
            ExcelDrawings.Add(part, new Xdr.OneCellAnchor(xml));
            sheet.Invalidate();
            var content = $"Added {prefix} ‘{name}’ at {anchor} in ‘{sheet.Name}’, {Math.Round(width)}x{Math.Round(height)} px.";
            var change = new DocuChange
            {
                Location = $"{name} · {anchor}",
                Icon = kind == "picture" ? "image" : kind == "shape" ? "shapes" : "textbox",
                Anchors = new List<string> { ExcelAnchor.Cell(sheet.Name, anchor) },
            };
            return ToolResult.Changed(content, change, prefix);
        }

        private static Picture LoadPicture(ExcelDocu docu, ToolInput input)
        {
            var source = input.GetOptionalString("source")?.Trim();
            if (string.IsNullOrEmpty(source))
                throw new ToolInputException("그림을 넣으려면 source로 파일 이름이나 경로를 알려 주세요.");

            var path = ResolveInsideFolder(docu, source);
            var length = new FileInfo(path).Length;
            if (length > ImageHeader.MaxBytes)
                throw new ToolInputException($"그림이 너무 커요. {ImageHeader.MaxBytes / (1024 * 1024)}MB 이하의 그림만 넣을 수 있어요.");

            var bytes = File.ReadAllBytes(path);
            var info = ImageHeader.Read(bytes, Path.GetFileName(path));
            return new Picture(bytes, info);
        }

        private static string ResolveInsideFolder(ExcelDocu docu, string source)
        {
            var folder = docu.FilePath == null ? null : Path.GetDirectoryName(Path.GetFullPath(docu.FilePath));
            if (folder == null)
                throw new ToolInputException("이 파일의 작업 폴더를 알 수 없어서 그림을 넣을 수 없어요. 파일을 저장한 뒤 다시 해 주세요.");

            var full = Path.GetFullPath(Path.IsPathRooted(source) ? source : Path.Combine(folder, source));
            var inside = string.Equals(Path.GetDirectoryName(full), folder, StringComparison.OrdinalIgnoreCase);
            if (!inside)
                throw new ToolInputException("그림은 작업 폴더 안의 파일만 넣을 수 있어요. 파일을 작업 폴더로 옮긴 뒤 이름만 알려 주세요.");

            if (!File.Exists(full))
                throw new ToolInputException($"‘{Path.GetFileName(full)}’ 그림 파일을 찾을 수 없어요.");

            return full;
        }

        private static double PictureWidth(Picture picture, int? columns, int? rows, ExcelSheet sheet, CellAddress anchor)
        {
            if (columns.HasValue && rows.HasValue)
                return ExcelDrawings.WidthOf(sheet, anchor.Column, columns.Value);

            var natural = picture.Info.WidthPx;
            return Math.Min(natural, MaxPictureWidth);
        }

        private static double PictureHeight(Picture picture, int? columns, int? rows, ExcelSheet sheet, CellAddress anchor)
        {
            if (columns.HasValue && rows.HasValue)
                return ExcelDrawings.HeightOf(sheet, anchor.Row, rows.Value);

            var scale = Math.Min(1.0, MaxPictureWidth / picture.Info.WidthPx);
            return picture.Info.HeightPx * scale;
        }

        private static string FillXml(string fill) =>
            fill == "none" ? "<a:noFill/>" : $"<a:solidFill><a:srgbClr val=\"{fill}\"/></a:solidFill>";

        private static string LineXml(string line, double width) =>
            line == "none" ? "<a:ln><a:noFill/></a:ln>" : $"<a:ln w=\"{Math.Round(width * 12700)}\"><a:solidFill><a:srgbClr val=\"{line}\"/></a:solidFill></a:ln>";

        private static string ParagraphsXml(Look look)
        {
            var text = new StringBuilder();
            foreach (var line in (look.Text ?? string.Empty).Split('\n').Select(l => l.TrimEnd('\r')))
            {
                text.Append($"<a:p><a:pPr algn=\"{look.Align}\"/>");
                if (line.Length == 0)
                {
                    text.Append($"<a:endParaRPr lang=\"ko-KR\" sz=\"{look.Size}\"/></a:p>");
                    continue;
                }

                text.Append($"<a:r><a:rPr lang=\"ko-KR\" sz=\"{look.Size}\" b=\"{(look.Bold ? 1 : 0)}\">");
                if (look.FontColor != null)
                    text.Append($"<a:solidFill><a:srgbClr val=\"{look.FontColor}\"/></a:solidFill>");

                text.Append($"</a:rPr><a:t>{ExcelDrawings.Text(line)}</a:t></a:r></a:p>");
            }

            return text.ToString();
        }

        private sealed record Picture(byte[] Bytes, DocuHelper.Tools.Ppt.PptImageInfo Info);

        private sealed class Look
        {
            public string Geometry { get; private set; } = "rect";

            public string Fill { get; private set; }

            public string Line { get; private set; }

            public double? LineWidth { get; private set; }

            public string Text { get; private set; }

            public int Size { get; private set; } = 1100;

            public bool Bold { get; private set; }

            public string FontColor { get; private set; }

            public string Align { get; private set; } = "l";

            public static Look Read(ToolInput input)
            {
                var look = new Look();
                if (input.Has("shape") && input.Get("shape").ValueKind == JsonValueKind.Object)
                {
                    var shape = input.Get("shape");
                    if (shape.TryGetProperty("geometry", out var geometry) && geometry.ValueKind == JsonValueKind.String)
                    {
                        var name = geometry.GetString();
                        if (!ShapeCatalog.Contains(name))
                            throw new ToolInputException($"도형 모양 ‘{name}’은 알 수 없어요. 있는 모양: {string.Join(", ", ShapeCatalog.Presets)}");

                        look.Geometry = name;
                    }

                    look.Fill = Colour(shape, "fill", "채우기");
                    look.Line = Colour(shape, "line_color", "선 색");
                    if (shape.TryGetProperty("line_width", out var width) && width.ValueKind == JsonValueKind.Number)
                    {
                        var lineWidth = width.GetDouble();
                        if (lineWidth < 0 || lineWidth > 20)
                            throw new ToolInputException("line_width는 0부터 20 사이의 점 크기로 알려 주세요.");

                        look.LineWidth = lineWidth;
                    }
                }

                look.Text = input.GetOptionalString("text");
                if (look.Text != null && look.Text.Length > MaxTextLength)
                    throw new ToolInputException($"글은 {MaxTextLength}자까지 넣을 수 있어요.");

                var size = input.GetOptionalNumber("font_size");
                if (size.HasValue)
                {
                    if (size.Value < 6 || size.Value > 96)
                        throw new ToolInputException("font_size는 6부터 96 사이의 점 크기로 알려 주세요.");

                    look.Size = (int)Math.Round(size.Value * 100);
                }

                look.Bold = input.GetOptionalBool("bold") ?? false;
                var color = input.GetOptionalString("font_color");
                if (!string.IsNullOrWhiteSpace(color))
                    look.FontColor = Hex(color, "글자 색");

                look.Align = input.GetOptionalString("align")?.Trim().ToLowerInvariant() switch
                {
                    null or "" or "left" => "l",
                    "center" => "ctr",
                    "right" => "r",
                    _ => throw new ToolInputException("align은 left, center, right 중 하나로 알려 주세요."),
                };
                return look;
            }

            private static string Colour(JsonElement shape, string field, string what)
            {
                if (!shape.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.String)
                    return null;

                var text = value.GetString()?.Trim() ?? string.Empty;
                return string.Equals(text, "none", StringComparison.OrdinalIgnoreCase) ? "none" : Hex(text, what);
            }

            private static string Hex(string text, string what)
            {
                var hex = text.Trim().TrimStart('#');
                if (!ExcelColors.IsHex(hex))
                    throw new ToolInputException($"{what}은 \"FF0000\" 같은 여섯 자리 색 코드로 알려 주세요.");

                return hex.ToUpperInvariant();
            }
        }
    }
}
