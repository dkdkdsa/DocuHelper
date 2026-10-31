using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public class SetSlideFooterTool : Tool
    {
        private const int MaxTextLength = 200;

        public override string Name => "set_slide_footer";

        public override string Description =>
            "Show or hide footer text, slide number and date on slides (the 머리글/바닥글 settings). `slides` are slide numbers or \"all\" (default all). "
            + "footer: {show, text}; slide_number: true/false; date: {show, mode auto|fixed, format short|long|time, text}; "
            + "skip_title_slide: do not show them on slides with a title slide layout. The shapes come from the layout placeholders, so they sit where the layout puts them.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "slides": {
                  "description": "Slide numbers (1-based), or \"all\".",
                  "anyOf": [
                    {"type": "string", "enum": ["all"]},
                    {"type": "array", "items": {"type": "integer", "minimum": 1}}
                  ]
                },
                "footer": {
                  "type": "object",
                  "properties": {
                    "show": {"type": "boolean", "description": "Show the footer."},
                    "text": {"type": "string", "description": "Footer text."}
                  }
                },
                "slide_number": {"type": "boolean", "description": "Show the slide number."},
                "date": {
                  "type": "object",
                  "properties": {
                    "show": {"type": "boolean", "description": "Show the date."},
                    "mode": {"type": "string", "enum": ["auto", "fixed"], "description": "auto updates to today when opened."},
                    "format": {"type": "string", "enum": ["short", "long", "time"]},
                    "text": {"type": "string", "description": "Text for mode fixed."}
                  }
                },
                "skip_title_slide": {"type": "boolean", "description": "Do not show on title slides."}
              }
            }
            """;

        public override ToolResult Execute(Docu docu, ToolInput input)
        {
            var document = PptSlides.DocumentOf(docu);
            var presentation = document.PresentationPart
                ?? throw new ToolInputException("이 파일은 슬라이드 문서로 열 수 없어요.");
            var whole = !input.Has("slides") || PptBackgrounds.IsAll(input);
            var targets = whole ? PptSlides.List(document) : PptBackgrounds.Numbers(document, input).ToList();
            if (targets.Count == 0)
                throw new ToolInputException("이 파일에는 아직 슬라이드가 없어요. add_slide로 먼저 슬라이드를 만들어 주세요.");

            var footer = PptInputs.Object(input, "footer");
            var date = PptInputs.Object(input, "date");
            var dateInput = date ?? ToolInput.From(new { });
            var footerShow = footer?.GetOptionalBool("show");
            var footerText = footer == null ? null : PptInputs.Text(footer, "text", MaxTextLength);
            if (footerText != null && footerShow == null)
                footerShow = true;

            var numberShow = PptInputs.Bool(input, "slide_number");
            var dateShow = date?.GetOptionalBool("show");
            var dateMode = PptInputs.Choice(dateInput, "mode", "auto", "fixed") ?? "auto";
            var dateFormat = PptInputs.Choice(dateInput, "format", "short", "long", "time") ?? "short";
            var dateText = PptInputs.Text(dateInput, "text", MaxTextLength);
            var skipTitle = PptInputs.Bool(input, "skip_title_slide");
            if (footerShow == null && numberShow == null && dateShow == null && skipTitle == null)
                throw new ToolInputException("바꿀 바닥글 항목(footer, slide_number, date, skip_title_slide) 중 하나를 알려 주세요.");

            if (dateShow == true && dateMode == "fixed" && string.IsNullOrWhiteSpace(dateText))
                throw new ToolInputException("고정 날짜는 date.text에 날짜 글을 알려 주세요.");

            var shows = new Dictionary<string, bool?>
            {
                [PptFooters.Footer] = footerShow,
                [PptFooters.SlideNumber] = numberShow,
                [PptFooters.Date] = dateShow,
            };
            var now = DateTime.Now;
            var snapshot = docu.Capture();
            try
            {
                var changedSlides = new List<int>();
                var hiddenAnchors = new List<string>();
                var createdAnchors = new List<string>();
                var skipped = new List<int>();
                var kinds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var slide in targets)
                {
                    var tree = slide.Part.Slide.CommonSlideData.ShapeTree;
                    var layout = slide.Part.SlideLayoutPart?.SlideLayout;
                    var layoutTree = layout?.CommonSlideData?.ShapeTree;
                    var titleLayout = layout != null && PptFooters.TokenOf(layout) == "title";
                    var changed = false;
                    var createdHere = 0;
                    foreach (var kind in PptFooters.Kinds)
                    {
                        var show = skipTitle == true && titleLayout ? (bool?)false : shows[kind];
                        if (show == null)
                            continue;

                        var existing = PptFooters.Of(tree, kind);
                        if (show == false)
                        {
                            if (existing.Count == 0)
                                continue;

                            foreach (var shape in existing)
                                shape.Remove();

                            changed = true;
                            kinds.Add(KindName(kind));
                            continue;
                        }

                        var placeholder = PptFooters.LayoutPlaceholder(layoutTree, kind);
                        if (placeholder == null)
                        {
                            skipped.Add(slide.Number);
                            continue;
                        }

                        foreach (var shape in existing)
                            shape.Remove();

                        var id = PptSlides.NextShapeId(tree);
                        PptSlides.AppendToTree(tree, PptFooterBuilder.Create(id, kind, placeholder, ParagraphFor(kind, slide.Number, dateMode, dateFormat, dateText, footerText, now)));
                        createdAnchors.Add(PptAnchor.Shape(slide.SlideId, id));
                        createdHere++;
                        changed = true;
                        kinds.Add(KindName(kind));
                    }

                    if (changed)
                        changedSlides.Add(slide.Number);

                    if (changed && createdHere == 0)
                        hiddenAnchors.Add(PptAnchor.Slide(slide.SlideId));
                }

                if (whole)
                    UpdateMasters(presentation, numberShow, footerShow, dateShow, footerText);

                if (skipTitle != null)
                    presentation.Presentation.ShowSpecialPlaceholderOnTitleSlide = !skipTitle.Value;

                var numbers = string.Join(", ", targets.Select(t => t.Number.ToString(CultureInfo.InvariantCulture)));
                var parts = new List<string>();
                if (changedSlides.Count > 0)
                    parts.Add($"Updated the footer settings on {changedSlides.Count.ToString(CultureInfo.InvariantCulture)} slide(s).");

                if (skipped.Count > 0)
                    parts.Add($"Skipped slide(s) {string.Join(", ", skipped.Distinct())} because their layout has no matching placeholder.");

                if (whole && changedSlides.Count == 0)
                    parts.Add("The slide master and layouts now carry the header and footer settings for new slides.");

                var content = string.Join(" ", parts);
                if (changedSlides.Count == 0 && !whole)
                    return ToolResult.Ok(content.Length > 0 ? content : "Nothing changed.", "바뀐 바닥글이 없어요.");

                var change = new DocuChange
                {
                    Location = whole ? "전체 바닥글" : $"{numbers}번 바닥글",
                    Icon = "footer",
                };
                change.Anchors.AddRange(createdAnchors);
                change.Anchors.AddRange(hiddenAnchors);
                return ToolResult.Changed(content.Length > 0 ? content : "Footer settings checked.", change, kinds.Count == 0 ? "바닥글" : string.Join(", ", kinds));
            }
            catch
            {
                docu.Restore(snapshot);
                throw;
            }
        }

        private static void UpdateMasters(PresentationPart presentation, bool? numberShow, bool? footerShow, bool? dateShow, string footerText)
        {
            foreach (var master in presentation.SlideMasterParts)
            {
                var root = master.SlideMaster;
                var current = root.HeaderFooter;
                root.HeaderFooter = new P.HeaderFooter
                {
                    SlideNumber = numberShow ?? current?.SlideNumber?.Value ?? false,
                    Footer = footerShow ?? current?.Footer?.Value ?? false,
                    DateTime = dateShow ?? current?.DateTime?.Value ?? false,
                    Header = false,
                };
                if (footerText == null || footerShow != true)
                    continue;

                SetFooterText(PptFooters.Of(root.CommonSlideData?.ShapeTree, PptFooters.Footer), footerText);
                foreach (var layout in master.SlideLayoutParts)
                    SetFooterText(PptFooters.Of(layout.SlideLayout?.CommonSlideData?.ShapeTree, PptFooters.Footer), footerText);
            }
        }

        private static void SetFooterText(IReadOnlyList<P.Shape> shapes, string text)
        {
            foreach (var shape in shapes)
            {
                if (shape.TextBody == null)
                    continue;

                shape.TextBody.RemoveAllChildren<A.Paragraph>();
                shape.TextBody.AppendChild(PptFooterBuilder.Text(text));
            }
        }

        private static A.Paragraph ParagraphFor(string kind, int number, string mode, string format, string dateText, string footerText, DateTime now)
        {
            if (kind == PptFooters.Footer)
                return PptFooterBuilder.Text(footerText ?? string.Empty);

            if (kind == PptFooters.SlideNumber)
                return PptFooterBuilder.Number(number);

            if (mode == "fixed")
                return PptFooterBuilder.Text(dateText);

            var (type, picture) = PptFooterBuilder.DateKind(format);
            return PptFooterBuilder.Date(type, PptFooterBuilder.Cached(now, picture));
        }

        private static string KindName(string kind) => kind switch
        {
            PptFooters.Footer => "바닥글",
            PptFooters.SlideNumber => "슬라이드 번호",
            _ => "날짜",
        };
    }
}
