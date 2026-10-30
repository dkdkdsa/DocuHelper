using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordStyler
    {
        private static readonly HashSet<Type> s_baseRun = new()
        {
            typeof(RunFonts),
            typeof(FontSize),
            typeof(FontSizeComplexScript),
            typeof(Languages),
            typeof(Kern),
            typeof(Spacing),
            typeof(CharacterScale),
        };

        public static Paragraph Create(WordScan scan, WordKind kind, string text, WordInsertion insertion, Paragraph lastNew, WordKind lastKind, HashSet<string> used)
        {
            ParagraphProperties properties = null;
            RunProperties run = null;
            switch (kind)
            {
                case WordKind.Title:
                case WordKind.Heading1:
                case WordKind.Heading2:
                {
                    var template = Template(scan, insertion, b => b.Kind == kind && !b.IsEmpty);
                    if (template != null)
                    {
                        properties = Sanitize(template.Paragraph.ParagraphProperties, true);
                        run = HeadingRun(template.Paragraph);
                    }
                    else
                    {
                        properties = new ParagraphProperties(new ParagraphStyleId { Val = scan.Styles.EnsureStyle(kind) });
                    }

                    break;
                }

                case WordKind.Bullet:
                case WordKind.Number:
                {
                    var neighbor = lastNew != null && lastKind == kind
                        ? lastNew
                        : insertion.Previous?.Kind == kind ? insertion.Previous.Paragraph
                        : insertion.Next?.Kind == kind ? insertion.Next.Paragraph
                        : null;
                    if (neighbor != null)
                    {
                        properties = Sanitize(neighbor.ParagraphProperties, true);
                        run = BaseRun(neighbor);
                        if (properties?.NumberingProperties == null)
                        {
                            scan.Numbering.Of(neighbor, out var numId, out var level);
                            properties ??= new ParagraphProperties();
                            properties.NumberingProperties = NewNumbering(numId, level);
                        }
                    }
                    else
                    {
                        var body = Template(scan, insertion, IsBodyTemplate);
                        run = body == null ? null : BaseRun(body.Paragraph);
                        var numId = kind == WordKind.Bullet ? scan.Numbering.BulletList() : scan.Numbering.NumberList();
                        properties = new ParagraphProperties { NumberingProperties = NewNumbering(numId, 0) };
                    }

                    break;
                }

                default:
                {
                    var template = Template(scan, insertion, b => IsBodyTemplate(b) && !scan.Styles.IsCentered(b.Paragraph)) ?? Template(scan, insertion, IsBodyTemplate);
                    if (template != null)
                    {
                        properties = Sanitize(template.Paragraph.ParagraphProperties, false);
                        run = BaseRun(template.Paragraph);
                    }

                    break;
                }
            }

            var paragraph = new Paragraph();
            if (properties != null && properties.HasChildren)
                paragraph.AppendChild(properties);

            var value = WordText.Clean(text);
            if (value.Length > 0)
            {
                paragraph.AppendChild(WordText.NewRun(value, run));
            }
            else if (run != null && run.HasChildren)
            {
                paragraph.ParagraphProperties ??= new ParagraphProperties();
                paragraph.ParagraphProperties.ParagraphMarkRunProperties = WordText.ToMark(run);
            }

            WordIds.Assign(paragraph, used);
            return paragraph;
        }

        public static void ApplyKind(WordScan scan, WordBlock block, WordKind kind)
        {
            var paragraph = block.Paragraph;
            var properties = paragraph.ParagraphProperties ?? (paragraph.ParagraphProperties = new ParagraphProperties());
            properties.RemoveAllChildren<OutlineLevel>();
            var styleId = properties.ParagraphStyleId?.Val?.Value;
            switch (kind)
            {
                case WordKind.Title:
                case WordKind.Heading1:
                case WordKind.Heading2:
                    properties.NumberingProperties = null;
                    properties.ParagraphStyleId = new ParagraphStyleId { Val = scan.Styles.EnsureStyle(kind) };
                    break;
                case WordKind.Bullet:
                case WordKind.Number:
                {
                    if (styleId != null && scan.Styles.IsHeadingStyle(styleId))
                        properties.ParagraphStyleId = null;

                    var numId = 0;
                    var level = 0;
                    var neighbor = new[] { scan.Before(block), scan.After(block) }.FirstOrDefault(b => b != null && b.Kind == kind);
                    if (neighbor != null)
                        scan.Numbering.Of(neighbor.Paragraph, out numId, out level);

                    if (numId <= 0)
                    {
                        numId = kind == WordKind.Bullet ? scan.Numbering.BulletList() : scan.Numbering.NumberList();
                        level = 0;
                    }

                    properties.NumberingProperties = NewNumbering(numId, level);
                    break;
                }

                default:
                {
                    properties.NumberingProperties = null;
                    if (styleId != null && (scan.Styles.IsHeadingStyle(styleId) || scan.Styles.StyleNumbering(paragraph) != null))
                        properties.ParagraphStyleId = null;

                    if (scan.Numbering.ListKind(paragraph, out _) != WordKind.Body)
                        properties.NumberingProperties = new NumberingProperties(new NumberingId { Val = 0 });

                    break;
                }
            }

            if (!properties.HasChildren)
                paragraph.ParagraphProperties = null;
        }

        public static void SetFlag<TOn, TComplex>(WordScan scan, IEnumerable<Paragraph> paragraphs, bool on)
            where TOn : OnOffType, new()
            where TComplex : OnOffType, new()
        {
            foreach (var paragraph in paragraphs)
            {
                foreach (var run in WordText.Runs(paragraph).ToList())
                {
                    var props = run.RunProperties ?? (run.RunProperties = new RunProperties());
                    Toggle<TOn>(props, on, WordStyles.IsOn(scan.Styles.InheritedRunValue<TOn>(run, paragraph)));
                    Toggle<TComplex>(props, on, WordStyles.IsOn(scan.Styles.InheritedRunValue<TComplex>(run, paragraph)));
                }

                var mark = Mark(paragraph);
                if (mark == null)
                    continue;

                Toggle<TOn>(mark, on, scan.Styles.StyleIsOn<TOn>(paragraph));
                Toggle<TComplex>(mark, on, scan.Styles.StyleIsOn<TComplex>(paragraph));
            }
        }

        public static void SetUnderline(WordScan scan, IEnumerable<Paragraph> paragraphs, bool on)
        {
            foreach (var paragraph in paragraphs)
            {
                foreach (var run in WordText.Runs(paragraph).ToList())
                {
                    var props = run.RunProperties ?? (run.RunProperties = new RunProperties());
                    Underline(props, on, WordStyles.IsUnderlined(scan.Styles.InheritedRunValue<Underline>(run, paragraph)));
                }

                var mark = Mark(paragraph);
                if (mark != null)
                    Underline(mark, on, scan.Styles.StyleUnderlined(paragraph));
            }
        }

        public static void SetAlignment(WordScan scan, Paragraph paragraph, bool center)
        {
            var properties = paragraph.ParagraphProperties ?? (paragraph.ParagraphProperties = new ParagraphProperties());
            properties.Justification = null;
            if (center)
                properties.Justification = new Justification { Val = JustificationValues.Center };
            else if (scan.Styles.IsStyleCentered(paragraph))
                properties.Justification = new Justification { Val = JustificationValues.Left };

            if (!properties.HasChildren)
                paragraph.ParagraphProperties = null;
        }

        public static NumberingProperties NewNumbering(int numId, int level) =>
            new(new NumberingLevelReference { Val = level }, new NumberingId { Val = numId });

        private static bool IsBodyTemplate(WordBlock block) => block.Kind == WordKind.Body && !block.IsEmpty;

        private static WordBlock Template(WordScan scan, WordInsertion insertion, Func<WordBlock, bool> match)
        {
            for (var block = insertion.Previous; block != null; block = scan.Before(block))
            {
                if (!block.IsTable && match(block))
                    return block;
            }

            for (var block = insertion.Next; block != null; block = scan.After(block))
            {
                if (!block.IsTable && match(block))
                    return block;
            }

            return null;
        }

        private static ParagraphProperties Sanitize(ParagraphProperties source, bool keepNumbering)
        {
            if (source == null)
                return null;

            var properties = (ParagraphProperties)source.CloneNode(true);
            properties.RemoveAllChildren<SectionProperties>();
            properties.RemoveAllChildren<PageBreakBefore>();
            properties.RemoveAllChildren<FrameProperties>();
            properties.RemoveAllChildren<ParagraphPropertiesChange>();
            properties.RemoveAllChildren<ConditionalFormatStyle>();
            if (!keepNumbering)
                properties.RemoveAllChildren<NumberingProperties>();

            var mark = properties.ParagraphMarkRunProperties;
            if (mark != null)
            {
                mark.RemoveAllChildren<Inserted>();
                mark.RemoveAllChildren<Deleted>();
                mark.RemoveAllChildren<MoveFrom>();
                mark.RemoveAllChildren<MoveTo>();
                mark.RemoveAllChildren<ParagraphMarkRunPropertiesChange>();
                if (!mark.HasChildren)
                    properties.ParagraphMarkRunProperties = null;
            }

            return properties.HasChildren ? properties : null;
        }

        private static RunProperties BaseRun(Paragraph paragraph)
        {
            var source = WordText.TemplateProperties(paragraph);
            if (source == null)
                return null;

            var kept = source.ChildElements.Where(c => s_baseRun.Contains(c.GetType())).Select(c => c.CloneNode(true)).ToList();
            return kept.Count == 0 ? null : new RunProperties(kept);
        }

        private static RunProperties HeadingRun(Paragraph paragraph)
        {
            var source = WordText.TemplateProperties(paragraph);
            if (source == null)
                return null;

            source.RemoveAllChildren<Vanish>();
            source.RemoveAllChildren<WebHidden>();
            source.RemoveAllChildren<SpecVanish>();
            return source.HasChildren ? source : null;
        }

        private static ParagraphMarkRunProperties Mark(Paragraph paragraph)
        {
            var existing = paragraph.ParagraphProperties?.ParagraphMarkRunProperties;
            if (existing != null)
                return existing;

            if (WordText.TextRuns(paragraph).Any())
                return null;

            var properties = paragraph.ParagraphProperties ?? (paragraph.ParagraphProperties = new ParagraphProperties());
            properties.ParagraphMarkRunProperties = new ParagraphMarkRunProperties();
            return properties.ParagraphMarkRunProperties;
        }

        private static void Toggle<T>(OpenXmlCompositeElement props, bool on, bool inherited) where T : OnOffType, new()
        {
            props.RemoveAllChildren<T>();
            if (on && !inherited)
                props.AddChild(new T());
            else if (!on && inherited)
                props.AddChild(new T { Val = false });
        }

        private static void Underline(OpenXmlCompositeElement props, bool on, bool inherited)
        {
            props.RemoveAllChildren<Underline>();
            if (on && !inherited)
                props.AddChild(new Underline { Val = UnderlineValues.Single });
            else if (!on && inherited)
                props.AddChild(new Underline { Val = UnderlineValues.None });
        }
    }
}
