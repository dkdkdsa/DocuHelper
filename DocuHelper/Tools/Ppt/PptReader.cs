using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public static class PptReader
    {
        private const int MaxTableRows = 6;
        private const int MaxCellLength = 24;
        private const int MaxTableText = 400;

        public static PptDeckInfo Read(PresentationDocument document)
        {
            var deck = new PptDeckInfo();
            var presentation = document?.PresentationPart?.Presentation;
            if (presentation == null)
                return deck;

            var size = presentation.SlideSize;
            if (size?.Cx != null && size.Cx.HasValue && size.Cy != null && size.Cy.HasValue)
            {
                deck.SlideWidth = size.Cx.Value;
                deck.SlideHeight = size.Cy.Value;
            }

            foreach (var slide in PptSlides.List(document))
                deck.Slides.Add(ReadSlide(slide, presentation.DefaultTextStyle));

            return deck;
        }

        public static PptSlideInfo ReadSlide(PresentationDocument document, PptSlideRef slide) =>
            ReadSlide(slide, document?.PresentationPart?.Presentation?.DefaultTextStyle);

        public static PptSlideInfo ReadSlide(PptSlideRef slide, P.DefaultTextStyle defaults)
        {
            var layout = slide.Part.SlideLayoutPart?.SlideLayout;
            var show = slide.Part.Slide?.Show;
            var info = new PptSlideInfo
            {
                Number = slide.Number,
                SlideId = slide.SlideId,
                LayoutName = layout?.CommonSlideData?.Name?.Value,
                LayoutType = layout?.Type != null && layout.Type.HasValue ? layout.Type.InnerText : null,
                IsHidden = show != null && show.HasValue && !show.Value,
                Notes = PptNotes.Read(slide.Part),
            };

            var tree = slide.Tree;
            if (tree != null)
                Walk(tree, slide, new PptSlideStyles(slide.Part, defaults), info.Shapes, 1, 1);

            return info;
        }

        private static void Walk(OpenXmlElement container, PptSlideRef slide, PptSlideStyles styles, List<PptShapeInfo> shapes, double scaleX, double scaleY)
        {
            foreach (var child in container.ChildElements)
            {
                switch (child)
                {
                    case P.Shape shape:
                        shapes.Add(ReadShape(shape, slide, styles, scaleX, scaleY));
                        break;
                    case P.GraphicFrame frame:
                        shapes.Add(ReadFrame(frame, slide));
                        break;
                    case P.Picture picture:
                        shapes.Add(ReadPicture(picture, slide));
                        break;
                    case P.GroupShape group:
                        var (groupX, groupY) = GroupScale(group);
                        Walk(group, slide, styles, shapes, scaleX * groupX, scaleY * groupY);
                        break;
                    case AlternateContent alternate:
                        var choice = alternate.GetFirstChild<AlternateContentChoice>();
                        if (choice != null)
                            Walk(choice, slide, styles, shapes, scaleX, scaleY);

                        break;
                }
            }
        }

        private static PptShapeInfo ReadShape(P.Shape shape, PptSlideRef slide, PptSlideStyles styles, double scaleX, double scaleY)
        {
            var props = shape.NonVisualShapeProperties?.NonVisualDrawingProperties;
            var placeholder = PptPlaceholder.Of(shape);
            var style = styles.For(shape);
            var info = new PptShapeInfo
            {
                ShapeId = props?.Id?.Value ?? 0,
                Name = props?.Name?.Value,
                PlaceholderType = PptPlaceholder.TypeOf(placeholder),
                PlaceholderIndex = PptPlaceholder.IndexOf(placeholder),
            };

            info.Anchor = PptAnchor.Shape(slide.SlideId, info.ShapeId);
            var body = shape.TextBody;
            if (body != null)
            {
                foreach (var paragraph in body.Elements<A.Paragraph>())
                    info.Paragraphs.Add(style.Describe(paragraph));
            }

            var isTextBox = shape.NonVisualShapeProperties?.NonVisualShapeDrawingProperties?.TextBox?.Value == true;
            info.Role = placeholder != null
                ? PptPlaceholder.RoleOf(info.PlaceholderType)
                : info.HasText || isTextBox ? PptShapeRole.TextBox : PptShapeRole.Other;

            var transform = style.Transform;
            if (transform?.Extents?.Cx != null && transform.Extents.Cy != null)
            {
                info.Width = (long)(transform.Extents.Cx.Value * scaleX);
                info.Height = (long)(transform.Extents.Cy.Value * scaleY);
                info.X = transform.Offset?.X?.Value ?? 0;
                info.Y = transform.Offset?.Y?.Value ?? 0;
            }

            info.LeftInset = style.Inset(b => b.LeftInset, 91440);
            info.RightInset = style.Inset(b => b.RightInset, 91440);
            info.TopInset = style.Inset(b => b.TopInset, 45720);
            info.BottomInset = style.Inset(b => b.BottomInset, 45720);
            info.NoWrap = style.NoWrap;
            info.Autofit = style.Autofit;
            return info;
        }

        private static PptShapeInfo ReadFrame(P.GraphicFrame frame, PptSlideRef slide)
        {
            var props = frame.NonVisualGraphicFrameProperties?.NonVisualDrawingProperties;
            var info = new PptShapeInfo
            {
                ShapeId = props?.Id?.Value ?? 0,
                Name = props?.Name?.Value,
                Role = PptShapeRole.Other,
            };

            info.Anchor = PptAnchor.Shape(slide.SlideId, info.ShapeId);
            var data = frame.Graphic?.GraphicData;
            var chartId = data?.GetFirstChild<C.ChartReference>()?.Id?.Value;
            if (!string.IsNullOrEmpty(chartId) && slide.Part.TryGetPartById(chartId, out var part) && part is ChartPart chartPart)
            {
                info.Role = PptShapeRole.Chart;
                try
                {
                    info.Chart = PptChartReader.Read(chartPart);
                }
                catch (Exception)
                {
                    info.Chart = null;
                }

                return info;
            }

            var table = data?.GetFirstChild<A.Table>();
            if (table != null)
            {
                info.Role = PptShapeRole.Table;
                info.Detail = TableText(table);
            }

            return info;
        }

        private static PptShapeInfo ReadPicture(P.Picture picture, PptSlideRef slide)
        {
            var props = picture.NonVisualPictureProperties?.NonVisualDrawingProperties;
            var info = new PptShapeInfo
            {
                ShapeId = props?.Id?.Value ?? 0,
                Name = props?.Name?.Value,
                Role = PptShapeRole.Picture,
                Detail = props?.Description?.Value,
            };

            info.Anchor = PptAnchor.Shape(slide.SlideId, info.ShapeId);
            return info;
        }

        private static (double, double) GroupScale(P.GroupShape group)
        {
            var transform = group.GroupShapeProperties?.TransformGroup;
            var extents = transform?.Extents;
            var child = transform?.ChildExtents;
            if (extents?.Cx == null || extents.Cy == null || child?.Cx == null || child.Cy == null || child.Cx.Value <= 0 || child.Cy.Value <= 0)
                return (1, 1);

            return ((double)extents.Cx.Value / child.Cx.Value, (double)extents.Cy.Value / child.Cy.Value);
        }

        private static string TableText(A.Table table)
        {
            var rows = table.Elements<A.TableRow>().ToList();
            var columns = rows.Count == 0 ? 0 : rows.Max(r => r.Elements<A.TableCell>().Count());
            var lines = rows.Take(MaxTableRows)
                .Select(r => string.Join(" | ", r.Elements<A.TableCell>().Select(CellText)))
                .ToList();

            var text = $"{rows.Count}x{columns}: {string.Join(" / ", lines)}";
            if (rows.Count > MaxTableRows)
                text += " / …";

            return text.Length > MaxTableText ? text.Substring(0, MaxTableText) + "…" : text;
        }

        private static string CellText(A.TableCell cell)
        {
            var paragraphs = cell.TextBody?.Elements<A.Paragraph>().Select(PptText.TextOf) ?? Enumerable.Empty<string>();
            var text = string.Join(" ", paragraphs).Replace('\n', ' ').Trim();
            return text.Length > MaxCellLength ? text.Substring(0, MaxCellLength) + "…" : text;
        }
    }
}
