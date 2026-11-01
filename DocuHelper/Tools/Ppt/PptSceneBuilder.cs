using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public sealed class PptSceneContext
    {
        public PptSceneContext(SlidePart part, PptThemeInfo theme, uint slideId)
        {
            Part = part;
            Theme = theme;
            SlideId = slideId;
            ColorMap = PptTheme.ColorMapOf(part);
        }

        public SlidePart Part { get; }

        public PptThemeInfo Theme { get; }

        public IReadOnlyDictionary<string, string> ColorMap { get; }

        public uint SlideId { get; }

        public PptSlideStyles Styles => _styles ??= new PptSlideStyles(Part, (Part.OpenXmlPackage as PresentationDocument)?.PresentationPart?.Presentation?.DefaultTextStyle);

        private PptSlideStyles _styles;

        public string Anchor(uint shapeId) => PptAnchor.Shape(SlideId, shapeId);

        public OpenXmlElement LayoutPlaceholder(string type, uint index)
        {
            var tree = Part.SlideLayoutPart?.SlideLayout?.CommonSlideData?.ShapeTree;
            if (tree == null)
                return null;

            foreach (var shape in tree.Elements<P.Shape>())
            {
                var placeholder = PptPlaceholder.Of(shape);
                if (placeholder == null || PptPlaceholder.IndexOf(placeholder) != index)
                    continue;

                var candidate = PptPlaceholder.TypeOf(placeholder);
                if (type == null || candidate == null || candidate == type)
                    return shape;
            }

            return null;
        }
    }

    public static class PptSceneBuilder
    {
        public static PptScene Build(PresentationDocument document)
        {
            var scene = new PptScene();
            var presentationPart = document?.PresentationPart;
            var presentation = presentationPart?.Presentation;
            if (presentation == null)
                return scene;

            var size = presentation.SlideSize;
            scene.SlideWidthEmu = size?.Cx?.Value ?? 12192000;
            scene.SlideHeightEmu = size?.Cy?.Value ?? 6858000;
            scene.Theme = PptTheme.Read(document);
            scene.Layouts = Layouts(presentationPart);
            scene.Show = ShowOf(presentationPart);
            scene.CustomShows = PptCustomShows.Read(presentationPart);
            scene.Sections = PptSections.Read(presentation);
            foreach (var slide in PptSlides.List(document))
                scene.Slides.Add(SlideOf(slide, scene.Theme, presentationPart));

            return scene;
        }

        private static List<PptLayoutInfo> Layouts(PresentationPart presentation)
        {
            var result = new List<PptLayoutInfo>();
            var masterIndex = 0;
            foreach (var master in presentation?.SlideMasterParts ?? Enumerable.Empty<SlideMasterPart>())
            {
                foreach (var layout in master.SlideLayoutParts)
                {
                    var root = layout.SlideLayout;
                    result.Add(new PptLayoutInfo
                    {
                        Index = result.Count + 1,
                        Name = root?.CommonSlideData?.Name?.Value,
                        Type = root?.Type?.InnerText,
                        MasterIndex = masterIndex,
                    });
                }

                masterIndex++;
            }

            return result;
        }

        private static PptShowInfo ShowOf(PresentationPart presentation)
        {
            var info = new PptShowInfo();
            var show = presentation?.PresentationPropertiesPart?.PresentationProperties?.ShowProperties;
            if (show == null)
                return info;

            info.Loop = Flag(show, "loop", false);
            info.ShowNarration = Flag(show, "showNarration", true);
            info.ShowAnimation = Flag(show, "showAnimation", true);
            info.UseTimings = Flag(show, "useTimings", true);
            info.Type = show.ChildElements.Any(c => c.LocalName == "browse") ? "browse"
                : show.ChildElements.Any(c => c.LocalName == "kiosk") ? "kiosk"
                : "presenter";
            info.MediaControls = true;
            foreach (var ext in show.Descendants().Where(d => d.LocalName == "showMediaCtrls"))
                info.MediaControls = Attribute(ext, "val") != "0";

            var range = show.ChildElements.FirstOrDefault(c => c.LocalName is "sldRg" or "custShow");
            if (range?.LocalName == "sldRg")
            {
                info.Range = new PptShowRange
                {
                    Type = "slides",
                    From = int.TryParse(Attribute(range, "st"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var st) ? st : 1,
                    To = int.TryParse(Attribute(range, "end"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var end) ? end : 1,
                };
            }
            else if (range?.LocalName == "custShow")
            {
                info.Range = new PptShowRange { Type = "custom_show" };
            }
            else
            {
                info.Range = new PptShowRange { Type = "all" };
            }

            return info;
        }

        private static PptSceneSlide SlideOf(PptSlideRef slide, PptThemeInfo theme, PresentationPart presentation)
        {
            var part = slide.Part;
            var root = part.Slide;
            var show = root?.Show;
            var layout = part.SlideLayoutPart?.SlideLayout;
            var scene = new PptSceneSlide
            {
                Number = slide.Number,
                SlideId = slide.SlideId,
                Anchor = PptAnchor.Slide(slide.SlideId),
                IsHidden = show != null && show.HasValue && !show.Value,
                LayoutName = layout?.CommonSlideData?.Name?.Value,
                LayoutType = layout?.Type != null && layout.Type.HasValue ? layout.Type.InnerText : null,
                HideMasterShapes = Attribute(root, "showMasterSp") == "0",
                Background = BackgroundOf(root, part, theme, PptTheme.ColorMapOf(part)),
            };
            var context = new PptSceneContext(part, theme, slide.SlideId);
            scene.Shapes = PptSceneShapes.ReadTree(slide.Tree, context, PptSceneShapes.Map.Identity);
            scene.Transition = Guarded(() => PptTransitionWriter.Read(root), new PptTransitionInfo());
            scene.Animations = Guarded(() => PptAnimationWriter.Read(root).Select(e => e.Info).ToList(), new List<PptAnimationInfo>());
            scene.Comments = Guarded(() => Comments(part, presentation), new List<PptCommentInfo>());
            scene.Notes = Guarded(() => PptNotes.Read(part), new List<string>());
            scene.FooterShown = Guarded(() => FooterOf(slide), null);
            return scene;
        }

        private static PptBackgroundInfo BackgroundOf(P.Slide root, SlidePart part, PptThemeInfo theme, IReadOnlyDictionary<string, string> colorMap)
        {
            var info = new PptBackgroundInfo { Kind = "none" };
            var background = root?.CommonSlideData?.Background;
            if (background == null)
                return info;

            var properties = background.BackgroundProperties;
            if (properties != null)
            {
                var fill = PptSceneShapes.Fill(properties, part, theme, colorMap);
                info.Kind = fill?.Kind ?? "none";
                info.Color = fill?.Color;
                info.Color2 = fill?.Color2;
                info.AngleDeg = fill?.AngleDeg ?? 0;
                info.Alpha = fill?.Alpha ?? 100;
                info.ImageKey = fill?.ImageKey;
                return info;
            }

            var reference = background.BackgroundStyleReference;
            var color = reference?.GetFirstChild<OpenXmlElement>();
            if (color != null)
            {
                info.Kind = "solid";
                info.Color = PptColor.Resolve(color, theme, colorMap, null);
                info.Alpha = PptColor.AlphaOf(color);
            }

            return info;
        }

        private static List<PptCommentInfo> Comments(SlidePart part, PresentationPart presentation)
        {
            var list = new List<PptCommentInfo>();
            foreach (var comment in PptCommentWriter.Comments(part))
            {
                var author = PptCommentWriter.Authors(presentation)?.Elements<P.CommentAuthor>().FirstOrDefault(a => a.Id?.Value == comment.AuthorId?.Value);
                var position = comment.GetFirstChild<P.Position>();
                list.Add(new PptCommentInfo
                {
                    Index = (int)(comment.Index?.Value ?? 0),
                    Author = author?.Name?.Value,
                    Initials = author?.Initials?.Value,
                    Text = comment.GetFirstChild<P.Text>()?.Text,
                    Time = comment.DateTime?.Value,
                    XCm = (position?.X?.Value ?? 0) / PptCommentWriter.UnitsPerCm,
                    YCm = (position?.Y?.Value ?? 0) / PptCommentWriter.UnitsPerCm,
                });
            }

            return list;
        }

        private static PptFooterInfo FooterOf(PptSlideRef slide)
        {
            var tree = slide.Tree;
            if (tree == null)
                return null;

            var footer = new PptFooterInfo();
            foreach (var shape in tree.Elements<P.Shape>())
            {
                var placeholder = PptPlaceholder.Of(shape);
                var type = PptPlaceholder.TypeOf(placeholder);
                switch (type)
                {
                    case "ftr":
                        footer.Footer = true;
                        footer.FooterText = shape.TextBody?.InnerText;
                        break;
                    case "sldNum":
                        footer.SlideNumber = true;
                        break;
                    case "dt":
                        footer.Date = true;
                        var field = shape.TextBody?.Descendants<A.Field>().FirstOrDefault();
                        footer.DateMode = field != null ? "auto" : "fixed";
                        footer.DateFormat = field?.Type?.Value;
                        footer.DateText = field == null ? shape.TextBody?.InnerText : null;
                        break;
                }
            }

            return footer.Footer || footer.SlideNumber || footer.Date ? footer : null;
        }

        private static T Guarded<T>(Func<T> read, T fallback)
        {
            try
            {
                return read();
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        private static bool Flag(OpenXmlElement element, string name, bool fallback)
        {
            var value = Attribute(element, name);
            return value == null ? fallback : value is "1" or "true";
        }

        private static string Attribute(OpenXmlElement element, string name) =>
            element?.GetAttributes().FirstOrDefault(a => a.LocalName == name).Value;
    }
}
