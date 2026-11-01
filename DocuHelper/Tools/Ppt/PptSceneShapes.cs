using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public static class PptSceneShapes
    {
        public readonly struct Map
        {
            public Map(long offsetX, long offsetY, double scaleX, double scaleY, long chX, long chY)
            {
                OffsetX = offsetX;
                OffsetY = offsetY;
                ScaleX = scaleX;
                ScaleY = scaleY;
                ChX = chX;
                ChY = chY;
            }

            public long OffsetX { get; }

            public long OffsetY { get; }

            public double ScaleX { get; }

            public double ScaleY { get; }

            public long ChX { get; }

            public long ChY { get; }

            public static Map Identity => new(0, 0, 1, 1, 0, 0);

            public long X(long value) => OffsetX + (long)Math.Round((value - ChX) * ScaleX);

            public long Y(long value) => OffsetY + (long)Math.Round((value - ChY) * ScaleY);
        }

        private const int MaxTreeDepth = 20;

        public static List<PptSceneShape> ReadTree(OpenXmlElement tree, PptSceneContext context, Map map, int depth = 0)
        {
            var result = new List<PptSceneShape>();
            if (tree == null || depth > MaxTreeDepth)
                return result;

            foreach (var child in tree.ChildElements)
            {
                PptSceneShape shape;
                try
                {
                    shape = child switch
                    {
                        P.Shape s => ReadShape(s, context, map),
                        P.Picture p => ReadPicture(p, context, map),
                        P.GraphicFrame f => ReadFrame(f, context, map),
                        P.GroupShape g => ReadGroup(g, context, map, depth),
                        P.ConnectionShape c => ReadConnection(c, context, map),
                        AlternateContent a => AlternateChoice(a, context, map, result, depth),
                        _ => null,
                    };
                }
                catch (Exception)
                {
                    shape = Other(child, context, map);
                }

                if (shape != null)
                    result.Add(shape);
            }

            return result;
        }

        private static PptSceneShape AlternateChoice(AlternateContent alternate, PptSceneContext context, Map map, List<PptSceneShape> result, int depth)
        {
            var choice = alternate.GetFirstChild<AlternateContentChoice>();
            if (choice != null)
                result.AddRange(ReadTree(choice, context, map, depth + 1));

            return null;
        }

        private static PptSceneShape ReadShape(P.Shape shape, PptSceneContext context, Map map)
        {
            var props = shape.NonVisualShapeProperties?.NonVisualDrawingProperties;
            var placeholder = PptPlaceholder.Of(shape);
            var preset = shape.ShapeProperties?.GetFirstChild<A.PresetGeometry>()?.Preset?.InnerText;
            var hasText = shape.TextBody != null && shape.TextBody.Descendants<A.Text>().Any(t => !string.IsNullOrEmpty(t.Text));
            var textBox = shape.NonVisualShapeProperties?.NonVisualShapeDrawingProperties?.TextBox?.Value == true;
            var kind = preset == "line" ? PptShapeKind.Line : placeholder != null || hasText || textBox ? PptShapeKind.Text : PptShapeKind.Shape;
            var result = Base(props, kind, context, shape);
            result.IsPlaceholder = placeholder != null;
            result.PlaceholderType = PptPlaceholder.TypeOf(placeholder);
            var box = Box(shape, context, placeholder, map, out var inherited);
            result.Inherited = inherited;
            ApplyBox(result, box, shape);
            result.Geometry = Geometry(shape.ShapeProperties);
            result.Fill = Fill(shape.ShapeProperties, context.Part, context.Theme, context.ColorMap);
            result.Line = Line(shape.ShapeProperties, context.Theme, context.ColorMap);
            ApplyStyle(result, shape, context.Theme, context.ColorMap);
            result.Shadow = Shadow(shape.ShapeProperties, context.Theme, context.ColorMap);
            result.Text = shape.TextBody == null ? null : TextOf(shape.TextBody, context, context.Styles.For(shape));
            ApplyFontStyle(result, shape.ShapeStyle, context.Theme, context.ColorMap);
            result.AltText = props?.Description?.Value;
            return result;
        }

        private static PptSceneShape ReadPicture(P.Picture picture, PptSceneContext context, Map map)
        {
            var props = picture.NonVisualPictureProperties?.NonVisualDrawingProperties;
            var result = Base(props, PptShapeKind.Picture, context, picture);
            var box = Box(picture, context, null, map, out _);
            ApplyBox(result, box, picture);
            var blip = picture.BlipFill?.GetFirstChild<A.Blip>();
            var crop = picture.BlipFill?.GetFirstChild<A.SourceRectangle>();
            result.Picture = new PptScenePicture
            {
                ImageKey = blip?.Embed?.Value,
                CropL = Percent(crop?.Left?.Value),
                CropT = Percent(crop?.Top?.Value),
                CropR = Percent(crop?.Right?.Value),
                CropB = Percent(crop?.Bottom?.Value),
                Alt = props?.Description?.Value,
            };
            result.Geometry = Geometry(picture.ShapeProperties);
            result.Line = Line(picture.ShapeProperties, context.Theme, context.ColorMap);
            result.AltText = props?.Description?.Value;
            return result;
        }

        private static PptSceneShape ReadFrame(P.GraphicFrame frame, PptSceneContext context, Map map)
        {
            var props = frame.NonVisualGraphicFrameProperties?.NonVisualDrawingProperties;
            var data = frame.Graphic?.GraphicData;
            var table = data?.GetFirstChild<A.Table>();
            var chartRef = data?.GetFirstChild<C.ChartReference>()?.Id?.Value;
            if (table != null)
            {
                var result = Base(props, PptShapeKind.Table, context, frame);
                ApplyFrame(result, frame, map);
                result.Table = Table(table, context);
                return result;
            }

            if (!string.IsNullOrEmpty(chartRef) && context.Part.TryGetPartById(chartRef, out var part) && part is ChartPart chart)
            {
                var result = Base(props, PptShapeKind.Chart, context, frame);
                ApplyFrame(result, frame, map);
                result.Chart = Chart(PptChartReader.Read(chart));
                return result;
            }

            var other = Base(props, PptShapeKind.Other, context, frame);
            ApplyFrame(other, frame, map);
            return other;
        }

        private static PptSceneShape ReadGroup(P.GroupShape group, PptSceneContext context, Map map, int depth)
        {
            var props = group.NonVisualGroupShapeProperties?.NonVisualDrawingProperties;
            var result = Base(props, PptShapeKind.Group, context, group);
            var transform = group.GroupShapeProperties?.TransformGroup;
            if (transform?.Offset != null && transform.Extents != null && transform.ChildOffset != null && transform.ChildExtents != null)
            {
                var sx = transform.ChildExtents.Cx.Value == 0 ? 1 : (double)transform.Extents.Cx.Value / transform.ChildExtents.Cx.Value;
                var sy = transform.ChildExtents.Cy.Value == 0 ? 1 : (double)transform.Extents.Cy.Value / transform.ChildExtents.Cy.Value;
                var inner = new Map(map.X(transform.Offset.X.Value) - 0, map.Y(transform.Offset.Y.Value), sx * map.ScaleX, sy * map.ScaleY, transform.ChildOffset.X.Value, transform.ChildOffset.Y.Value);
                result.Children = ReadTree(group, context, inner, depth + 1);
                result.X = map.X(transform.Offset.X.Value);
                result.Y = map.Y(transform.Offset.Y.Value);
                result.W = (long)Math.Round(transform.Extents.Cx.Value * map.ScaleX);
                result.H = (long)Math.Round(transform.Extents.Cy.Value * map.ScaleY);
            }
            else
            {
                result.Children = ReadTree(group, context, map, depth + 1);
            }

            return result;
        }

        private static PptSceneShape ReadConnection(P.ConnectionShape connection, PptSceneContext context, Map map)
        {
            var props = connection.NonVisualConnectionShapeProperties?.NonVisualDrawingProperties;
            var result = Base(props, PptShapeKind.Line, context, connection);
            var box = Box(connection, context, null, map, out _);
            ApplyBox(result, box, connection);
            result.Geometry = Geometry(connection.ShapeProperties);
            result.Line = Line(connection.ShapeProperties, context.Theme, context.ColorMap);
            ApplyStyle(result, connection.ShapeProperties, connection.ShapeStyle, context.Theme, context.ColorMap);
            return result;
        }

        private static PptSceneShape Other(OpenXmlElement element, PptSceneContext context, Map map)
        {
            var props = element.Descendants<P.NonVisualDrawingProperties>().FirstOrDefault();
            var result = new PptSceneShape
            {
                Kind = PptShapeKind.Other,
                Name = props?.Name?.Value,
                Id = props?.Id?.Value ?? 0,
                Anchor = context.Anchor(props?.Id?.Value ?? 0),
            };
            return result;
        }

        private static PptSceneShape Base(P.NonVisualDrawingProperties props, PptShapeKind kind, PptSceneContext context, OpenXmlElement element)
        {
            var id = props?.Id?.Value ?? 0;
            var result = new PptSceneShape
            {
                Id = id,
                Anchor = context.Anchor(id),
                Name = props?.Name?.Value,
                Kind = kind,
                Inherited = PptInheritedFrom.None,
            };
            result.Link = Link(element, context);
            return result;
        }

        private static PptSceneLink Link(OpenXmlElement element, PptSceneContext context)
        {
            var click = element.Descendants<A.HyperlinkOnClick>().FirstOrDefault();
            if (click == null)
                return null;

            return PptLinkRelations.Describe(context.Part, click);
        }

        private static void ApplyBox(PptSceneShape result, PptBox? box, OpenXmlElement element)
        {
            if (box is PptBox value)
            {
                result.X = value.X;
                result.Y = value.Y;
                result.W = value.Width;
                result.H = value.Height;
            }

            result.RotationDeg = PptXfrm.ReadRotation(element);
            result.FlipH = PptXfrm.ReadFlipH(element);
            result.FlipV = PptXfrm.ReadFlipV(element);
        }

        private static void ApplyFrame(PptSceneShape result, P.GraphicFrame frame, Map map)
        {
            var transform = frame.Transform;
            if (transform?.Offset == null || transform.Extents == null)
                return;

            result.X = map.X(transform.Offset.X.Value);
            result.Y = map.Y(transform.Offset.Y.Value);
            result.W = (long)Math.Round(transform.Extents.Cx.Value * map.ScaleX);
            result.H = (long)Math.Round(transform.Extents.Cy.Value * map.ScaleY);
        }

        private static PptBox? Box(OpenXmlElement element, PptSceneContext context, P.PlaceholderShape placeholder, Map map, out PptInheritedFrom inherited)
        {
            inherited = PptInheritedFrom.None;
            var own = PptXfrm.Read(element);
            if (own is PptBox ownBox)
                return Mapped(ownBox, map);

            if (placeholder == null || element is not P.Shape shape)
                return null;

            var transform = new PptSlideStyles(context.Part, null).For(shape).Transform;
            var offset = transform?.GetFirstChild<A.Offset>();
            var extents = transform?.GetFirstChild<A.Extents>();
            if (offset?.X == null || offset.Y == null || extents?.Cx == null || extents.Cy == null)
                return null;

            inherited = PptInheritedFrom.Layout;
            return Mapped(new PptBox(offset.X.Value, offset.Y.Value, extents.Cx.Value, extents.Cy.Value), map);
        }

        private static PptBox Mapped(PptBox box, Map map) => new(
            map.X(box.X),
            map.Y(box.Y),
            (long)Math.Round(box.Width * map.ScaleX),
            (long)Math.Round(box.Height * map.ScaleY));

        private static double Percent(int? value) => value.HasValue ? value.Value / 1000.0 : 0;

        private static PptSceneGeometry Geometry(OpenXmlElement properties)
        {
            if (properties == null)
                return null;

            var preset = properties.GetFirstChild<A.PresetGeometry>();
            if (preset != null)
            {
                var geometry = new PptSceneGeometry { Preset = preset.Preset?.InnerText };
                foreach (var guide in preset.Descendants<A.ShapeGuide>())
                {
                    var formula = guide.Formula?.Value ?? string.Empty;
                    var parts = formula.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2 && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                        geometry.Adjust.Add(value);
                }

                return geometry;
            }

            var custom = properties.GetFirstChild<A.CustomGeometry>();
            if (custom != null)
            {
                var path = custom.Descendants<A.Path>().FirstOrDefault();
                return new PptSceneGeometry
                {
                    Preset = "custom",
                    CustomPath = new PptSceneCustomPath
                    {
                        W = path?.Width?.Value ?? 0,
                        H = path?.Height?.Value ?? 0,
                        D = string.Empty,
                    },
                };
            }

            return null;
        }

        public static PptSceneFill Fill(OpenXmlElement properties, OpenXmlPart part, PptThemeInfo theme, IReadOnlyDictionary<string, string> colorMap)
        {
            if (properties == null)
                return null;

            var solid = properties.Elements<A.SolidFill>().FirstOrDefault();
            if (solid != null)
                return SolidOf(solid, theme, colorMap);

            var gradient = properties.Elements<A.GradientFill>().FirstOrDefault();
            if (gradient != null)
            {
                var fill = new PptSceneFill { Kind = "gradient" };
                foreach (var stop in gradient.GradientStopList?.Elements<A.GradientStop>() ?? Enumerable.Empty<A.GradientStop>())
                {
                    var element = stop.GetFirstChild<OpenXmlElement>();
                    var color = element == null ? null : PptColor.Resolve(element, theme, colorMap, null);
                    fill.Stops.Add(new PptSceneStop { Pos = (stop.Position?.Value ?? 0) / 1000.0, Color = color, Alpha = element == null ? 100 : PptColor.AlphaOf(element) });
                }

                fill.Color = fill.Stops.FirstOrDefault()?.Color;
                fill.Color2 = fill.Stops.LastOrDefault()?.Color;
                fill.AngleDeg = (gradient.GetFirstChild<A.LinearGradientFill>()?.Angle?.Value ?? 0) / 60000.0;
                return fill;
            }

            var blip = properties.Elements<A.BlipFill>().FirstOrDefault();
            if (blip != null)
                return new PptSceneFill { Kind = "picture", ImageKey = blip.Descendants<A.Blip>().FirstOrDefault()?.Embed?.Value };

            return new PptSceneFill { Kind = "none" };
        }

        private static PptSceneFill SolidOf(OpenXmlElement solid, PptThemeInfo theme, IReadOnlyDictionary<string, string> colorMap) => new()
        {
            Kind = "solid",
            Color = PptColor.Resolve(solid.GetFirstChild<OpenXmlElement>(), theme, colorMap, null),
            Alpha = PptColor.AlphaOf(solid.GetFirstChild<OpenXmlElement>()),
        };

        private static PptSceneLine Line(OpenXmlElement properties, PptThemeInfo theme, IReadOnlyDictionary<string, string> colorMap)
        {
            var line = properties?.GetFirstChild<A.Outline>();
            if (line == null)
                return null;

            var solid = line.GetFirstChild<A.SolidFill>();
            var colorElement = solid?.GetFirstChild<OpenXmlElement>();
            return new PptSceneLine
            {
                Color = colorElement == null ? null : PptColor.Resolve(colorElement, theme, colorMap, null),
                Alpha = colorElement == null ? 100 : PptColor.AlphaOf(colorElement),
                WidthPt = (line.Width?.Value ?? 0) / 12700.0,
                Dash = line.GetFirstChild<A.PresetDash>()?.Val?.InnerText,
                Head = line.GetFirstChild<A.HeadEnd>()?.Type?.InnerText,
                Tail = line.GetFirstChild<A.TailEnd>()?.Type?.InnerText,
            };
        }

        private static void ApplyStyle(PptSceneShape result, P.Shape shape, PptThemeInfo theme, IReadOnlyDictionary<string, string> colorMap) =>
            ApplyStyle(result, shape.ShapeProperties, shape.ShapeStyle, theme, colorMap);

        private static void ApplyStyle(PptSceneShape result, OpenXmlElement properties, P.ShapeStyle style, PptThemeInfo theme, IReadOnlyDictionary<string, string> colorMap)
        {
            if (properties == null || style == null)
                return;

            var fillElement = StyleColorElement(style.FillReference);
            var fillColor = fillElement == null ? null : PptColor.Resolve(fillElement, theme, colorMap, null);
            if (IsHexColor(fillColor) && !HasExplicitFill(properties))
                result.Fill = new PptSceneFill { Kind = "solid", Color = fillColor, Alpha = PptColor.AlphaOf(fillElement) };

            var outline = properties.GetFirstChild<A.Outline>();
            if (outline?.GetFirstChild<A.NoFill>() != null || outline?.GetFirstChild<A.SolidFill>() != null)
                return;

            var lineElement = StyleColorElement(style.LineReference);
            var lineColor = lineElement == null ? null : PptColor.Resolve(lineElement, theme, colorMap, null);
            if (!IsHexColor(lineColor))
                return;

            result.Line ??= new PptSceneLine { WidthPt = 0.75 };
            result.Line.Color = lineColor;
            result.Line.Alpha = PptColor.AlphaOf(lineElement);
        }

        private static void ApplyFontStyle(PptSceneShape result, P.ShapeStyle style, PptThemeInfo theme, IReadOnlyDictionary<string, string> colorMap)
        {
            if (result.Text == null || style == null)
                return;

            var element = FontColorElement(style.FontReference);
            var color = element == null ? null : PptColor.Resolve(element, theme, colorMap, null);
            if (!IsHexColor(color))
                return;

            foreach (var run in result.Text.Paragraphs.SelectMany(p => p.Runs))
                run.Color ??= color;
        }

        private static OpenXmlElement FontColorElement(OpenXmlElement reference)
        {
            if (reference == null)
                return null;

            var index = reference.GetAttributes().FirstOrDefault(a => a.LocalName == "idx").Value;
            return index is null or "none" ? null : reference.GetFirstChild<OpenXmlElement>();
        }

        private static bool IsHexColor(string color) =>
            color != null && color.Length == 6 && color.All(Uri.IsHexDigit);

        private static bool HasExplicitFill(OpenXmlElement properties) =>
            properties.GetFirstChild<A.NoFill>() != null
            || properties.GetFirstChild<A.SolidFill>() != null
            || properties.GetFirstChild<A.GradientFill>() != null
            || properties.GetFirstChild<A.BlipFill>() != null
            || properties.GetFirstChild<A.PatternFill>() != null;

        private static OpenXmlElement StyleColorElement(OpenXmlElement reference)
        {
            if (reference == null)
                return null;

            var index = reference.GetAttributes().FirstOrDefault(a => a.LocalName == "idx").Value;
            return int.TryParse(index, out var value) && value > 0 ? reference.GetFirstChild<OpenXmlElement>() : null;
        }

        private static PptSceneShadow Shadow(OpenXmlElement properties, PptThemeInfo theme, IReadOnlyDictionary<string, string> colorMap)
        {
            var shadow = properties?.Descendants<A.OuterShadow>().FirstOrDefault();
            if (shadow == null)
                return null;

            var colorElement = shadow.GetFirstChild<OpenXmlElement>();
            return new PptSceneShadow
            {
                Color = colorElement == null ? null : PptColor.Resolve(colorElement, theme, colorMap, null),
                Alpha = colorElement == null ? 100 : PptColor.AlphaOf(colorElement),
                BlurPt = (shadow.BlurRadius?.Value ?? 0) / 12700.0,
                DistPt = (shadow.Distance?.Value ?? 0) / 12700.0,
                AngleDeg = (shadow.Direction?.Value ?? 0) / 60000.0,
            };
        }

        public static PptSceneText TextOf(OpenXmlElement body, PptSceneContext context, PptShapeStyle style = null)
        {
            var props = body.GetFirstChild<A.BodyProperties>();
            var anchor = props?.Anchor != null && props.Anchor.HasValue ? props.Anchor.InnerText : style?.TextAnchor;
            var text = new PptSceneText
            {
                Anchor = anchor switch
                {
                    "ctr" => "ctr",
                    "b" => "b",
                    _ => "t",
                },
                Wrap = props?.Wrap?.InnerText != "none",
                Vertical = props?.Vertical?.InnerText,
                Columns = props?.ColumnCount?.Value ?? 1,
            };
            text.InsetL = Inset(props?.LeftInset, style, b => b.LeftInset, text.InsetL);
            text.InsetT = Inset(props?.TopInset, style, b => b.TopInset, text.InsetT);
            text.InsetR = Inset(props?.RightInset, style, b => b.RightInset, text.InsetR);
            text.InsetB = Inset(props?.BottomInset, style, b => b.BottomInset, text.InsetB);
            text.Autofit = Autofit(props);
            foreach (var paragraph in body.Elements<A.Paragraph>())
                text.Paragraphs.Add(ParagraphOf(paragraph, context, style));

            return text;
        }

        private static long Inset(Int32Value own, PptShapeStyle style, Func<A.BodyProperties, Int32Value> pick, long fallback)
        {
            if (own != null && own.HasValue)
                return own.Value;

            return style == null ? fallback : style.Inset(pick, fallback);
        }

        private static PptSceneAutofit Autofit(A.BodyProperties props)
        {
            if (props == null)
                return null;

            if (props.GetFirstChild<A.NormalAutoFit>() is { } normal)
                return new PptSceneAutofit { Mode = "shrink", FontScale = (normal.FontScale?.Value ?? 100000) / 1000.0, LineReduction = (normal.LineSpaceReduction?.Value ?? 0) / 1000.0 };

            if (props.GetFirstChild<A.ShapeAutoFit>() != null)
                return new PptSceneAutofit { Mode = "resize" };

            return new PptSceneAutofit { Mode = "none" };
        }

        public static PptSceneParagraph ParagraphOf(A.Paragraph paragraph, PptSceneContext context, PptShapeStyle style = null)
        {
            var properties = paragraph.ParagraphProperties;
            var level = properties?.Level?.Value ?? 0;
            T Pick<T>(Func<A.TextParagraphPropertiesType, T> pick)
                where T : class => style != null ? style.ParagraphValue(paragraph, level, pick) : properties == null ? null : pick(properties);

            var align = Pick(p => p.Alignment);
            var margin = Pick(p => p.LeftMargin);
            var indent = Pick(p => p.Indent);
            var result = new PptSceneParagraph
            {
                Level = level,
                Align = align != null && align.HasValue ? align.InnerText switch
                {
                    "ctr" => "ctr",
                    "r" => "r",
                    "just" => "just",
                    _ => "l",
                } : "l",
                MarginLeftEmu = margin != null && margin.HasValue ? margin.Value : 0,
                IndentEmu = indent != null && indent.HasValue ? indent.Value : 0,
                Bullet = Bullet(properties, context) ?? InheritedBullet(style, level, context),
            };
            var lineSpacing = Pick(p => p.LineSpacing);
            if (lineSpacing?.SpacingPercent?.Val != null)
                result.LineSpacing = new PptSceneSpacing { Kind = "pct", Value = lineSpacing.SpacingPercent.Val.Value / 1000.0 };
            else if (lineSpacing?.SpacingPoints?.Val != null)
                result.LineSpacing = new PptSceneSpacing { Kind = "pt", Value = lineSpacing.SpacingPoints.Val.Value / 100.0 };

            result.SpaceBeforePt = Pick(p => p.SpaceBefore)?.SpacingPoints?.Val?.Value / 100.0 ?? 0;
            result.SpaceAfterPt = Pick(p => p.SpaceAfter)?.SpacingPoints?.Val?.Value / 100.0 ?? 0;
            foreach (var element in paragraph.Elements())
            {
                switch (element)
                {
                    case A.Run run:
                        result.Runs.Add(RunOf(run.RunProperties, run.Text?.Text, context, null, style, level));
                        break;
                    case A.Field field:
                        result.Runs.Add(RunOf(field.RunProperties, field.Text?.Text, context, field.Type?.Value, style, level));
                        break;
                    case A.Break:
                        result.Runs.Add(new PptSceneRun { IsBreak = true, Text = "\n" });
                        break;
                }
            }

            return result;
        }

        private static PptSceneBullet InheritedBullet(PptShapeStyle style, int level, PptSceneContext context)
        {
            if (style == null)
                return null;

            foreach (var props in style.Levels(level))
            {
                var bullet = Bullet(props, context);
                if (bullet != null)
                    return bullet;
            }

            return null;
        }

        private static PptSceneBullet Bullet(A.TextParagraphPropertiesType properties, PptSceneContext context)
        {
            if (properties == null)
                return null;

            if (properties.GetFirstChild<A.NoBullet>() != null)
                return new PptSceneBullet { Kind = "none" };

            if (properties.GetFirstChild<A.CharacterBullet>() is { } character)
                return new PptSceneBullet { Kind = "char", Char = character.Char?.Value, Color = BulletColor(properties, context), FontName = properties.GetFirstChild<A.BulletFont>()?.Typeface?.Value, SizePct = properties.GetFirstChild<A.BulletSizePercentage>() is { } size ? size.Val?.Value / 1000.0 ?? 100 : 100 };

            if (properties.GetFirstChild<A.AutoNumberedBullet>() != null)
                return new PptSceneBullet { Kind = "number", Color = BulletColor(properties, context) };

            return null;
        }

        private static string BulletColor(A.TextParagraphPropertiesType properties, PptSceneContext context)
        {
            var color = properties.GetFirstChild<A.BulletColor>()?.GetFirstChild<OpenXmlElement>();
            return color == null ? null : PptColor.Resolve(color, context.Theme, context.ColorMap, null);
        }

        private static PptSceneRun RunOf(A.RunProperties properties, string text, PptSceneContext context, string fieldType, PptShapeStyle style = null, int level = 0)
        {
            var run = new PptSceneRun { Text = text ?? string.Empty, FieldType = fieldType };
            if (properties == null && style == null)
                return run;

            T Pick<T>(Func<A.TextCharacterPropertiesType, T> pick)
                where T : class => style != null ? style.RunValue(properties, level, pick) : properties == null ? null : pick(properties);

            var size = Pick(r => r.FontSize);
            var bold = Pick(r => r.Bold);
            var italic = Pick(r => r.Italic);
            var underline = Pick(r => r.Underline);
            var strike = Pick(r => r.Strike);
            var baseline = Pick(r => r.Baseline);
            run.SizePt = size != null && size.HasValue ? size.Value / 100.0 : null;
            run.Bold = bold != null && bold.HasValue && bold.Value;
            run.Italic = italic != null && italic.HasValue && italic.Value;
            run.Underline = underline != null && underline.HasValue && underline.Value != A.TextUnderlineValues.None;
            run.Strike = strike != null && strike.HasValue && strike.Value != A.TextStrikeValues.NoStrike;
            run.Baseline = baseline != null && baseline.HasValue ? baseline.Value / 1000.0 : 0;
            run.FontLatin = ThemeFont(Pick(r => r.GetFirstChild<A.LatinFont>())?.Typeface?.Value, context.Theme);
            run.FontEa = ThemeFont(Pick(r => r.GetFirstChild<A.EastAsianFont>())?.Typeface?.Value, context.Theme);
            var fill = Pick(r => r.GetFirstChild<A.SolidFill>())?.GetFirstChild<OpenXmlElement>();
            run.Color = fill == null ? null : PptColor.Resolve(fill, context.Theme, context.ColorMap, null);
            var highlight = properties?.GetFirstChild<A.Highlight>()?.GetFirstChild<OpenXmlElement>();
            run.Highlight = highlight == null ? null : PptColor.Resolve(highlight, context.Theme, context.ColorMap, null);
            var link = properties?.GetFirstChild<A.HyperlinkOnClick>();
            if (link != null)
                run.Link = PptLinkRelations.Describe(context.Part, link);

            return run;
        }

        private static string ThemeFont(string typeface, PptThemeInfo theme)
        {
            if (string.IsNullOrEmpty(typeface) || typeface[0] != '+')
                return typeface;

            var name = typeface switch
            {
                "+mj-lt" => theme?.MajorLatin,
                "+mn-lt" => theme?.MinorLatin,
                "+mj-ea" => string.IsNullOrEmpty(theme?.MajorEa) ? theme?.MajorHang : theme.MajorEa,
                "+mn-ea" => string.IsNullOrEmpty(theme?.MinorEa) ? theme?.MinorHang : theme.MinorEa,
                _ => null,
            };

            return string.IsNullOrEmpty(name) ? null : name;
        }

        public static PptSceneTable Table(A.Table table, PptSceneContext context)
        {
            var properties = table.TableProperties;
            var result = new PptSceneTable
            {
                StyleKey = PptTableStyles.KeyOf(properties?.GetFirstChild<A.TableStyleId>()?.Text ?? string.Empty),
                FirstRow = properties?.FirstRow?.Value == true,
                BandRow = properties?.BandRow?.Value == true,
                FirstCol = properties?.FirstColumn?.Value == true,
                LastRow = properties?.LastRow?.Value == true,
                LastCol = properties?.LastColumn?.Value == true,
            };
            result.Columns = table.TableGrid?.Elements<A.GridColumn>().Select(c => c.Width?.Value ?? 0L).ToList() ?? new List<long>();
            foreach (var row in table.Elements<A.TableRow>())
            {
                var rowScene = new PptSceneTableRow { HeightEmu = row.Height?.Value ?? 0 };
                foreach (var cell in row.Elements<A.TableCell>())
                {
                    var properties2 = cell.TableCellProperties;
                    var sceneCell = new PptSceneTableCell
                    {
                        GridSpan = cell.GridSpan?.Value ?? 1,
                        RowSpan = cell.RowSpan?.Value ?? 1,
                        HMerge = cell.HorizontalMerge?.Value == true,
                        VMerge = cell.VerticalMerge?.Value == true,
                        Anchor = properties2?.Anchor?.InnerText switch
                        {
                            "ctr" => "ctr",
                            "b" => "b",
                            _ => "t",
                        },
                        Fill = properties2 == null ? null : Fill(properties2, context.Part, context.Theme, context.ColorMap),
                        BorderL = Border(properties2?.LeftBorderLineProperties, context),
                        BorderT = Border(properties2?.TopBorderLineProperties, context),
                        BorderR = Border(properties2?.RightBorderLineProperties, context),
                        BorderB = Border(properties2?.BottomBorderLineProperties, context),
                    };
                    if (cell.TextBody != null)
                    {
                        foreach (var paragraph in cell.TextBody.Elements<A.Paragraph>())
                            sceneCell.Paragraphs.Add(ParagraphOf(paragraph, context));
                    }

                    rowScene.Cells.Add(sceneCell);
                }

                result.Rows.Add(rowScene);
            }

            return result;
        }

        private static PptSceneBorder Border(OpenXmlElement line, PptSceneContext context)
        {
            if (line == null)
                return null;

            var solid = line.GetFirstChild<A.SolidFill>()?.GetFirstChild<OpenXmlElement>();
            var width = Attribute(line, "w");
            return new PptSceneBorder
            {
                Color = solid == null ? null : PptColor.Resolve(solid, context.Theme, context.ColorMap, null),
                WidthPt = width != null && double.TryParse(width, NumberStyles.Float, CultureInfo.InvariantCulture, out var emu) ? emu / 12700.0 : 0,
                Dash = line.GetFirstChild<A.PresetDash>()?.Val?.InnerText,
            };
        }

        private static string Attribute(OpenXmlElement element, string name) =>
            element?.GetAttributes().FirstOrDefault(a => a.LocalName == name).Value;

        public static PptSceneChart Chart(PptChartInfo info)
        {
            if (info == null)
                return null;

            return new PptSceneChart
            {
                Type = info.Type,
                Title = info.Title,
                Categories = info.Categories.ToList(),
                Series = info.Series.Select(s => new PptSceneSeries { Name = s.Name, Values = s.Values.ToList(), Color = s.Color }).ToList(),
                Legend = info.Legend,
                DataLabels = info.DataLabels,
                Stacked = info.Stacked,
                Horizontal = info.Type == "bar",
            };
        }

    }
}
