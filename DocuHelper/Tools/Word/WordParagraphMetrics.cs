using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Wp = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public readonly record struct WordLineBox(double Height, bool PageBreak);

    public sealed class WordParagraphBox
    {
        public WordParagraphBox(IReadOnlyList<WordLineBox> lines, double before, double after, bool pageBefore)
        {
            Lines = lines;
            Before = before;
            After = after;
            PageBefore = pageBefore;
        }

        public IReadOnlyList<WordLineBox> Lines { get; }

        public double Before { get; }

        public double After { get; }

        public bool PageBefore { get; }

        public double Height => Lines.Sum(l => l.Height);
    }

    public static class WordParagraphMetrics
    {
        private const double TwipsPerPt = 20.0;
        private const double EmuPerPt = 12700.0;
        private const double TabStopPt = 36.0;
        private const int SingleLine = 240;
        private const double MinShareOfWidth = 0.1;

        private enum Kind
        {
            Glyph,
            Tab,
            Object,
            NewLine,
            PageBreak,
        }

        private readonly record struct Atom(Kind Kind, double Width, double Height, bool Space, bool Break);

        public static WordParagraphBox Measure(WordLayoutContext context, Paragraph paragraph, double width)
        {
            var spacing = context.Styles.ParagraphValue<SpacingBetweenLines>(paragraph);
            var indent = context.Styles.ParagraphValue<Indentation>(paragraph);
            var before = Twips(spacing, "before") / TwipsPerPt;
            var after = Twips(spacing, "after") / TwipsPerPt;
            var line = Twips(spacing, "line", SingleLine);
            var rule = Attribute(spacing, "lineRule") ?? "auto";
            var left = Twips(indent, "left") / TwipsPerPt;
            var right = Twips(indent, "right") / TwipsPerPt;
            var start = left + Twips(indent, "firstLine") / TwipsPerPt - Twips(indent, "hanging") / TwipsPerPt;
            var firstWidth = Math.Max(width * MinShareOfWidth, width - right - start);
            var restWidth = Math.Max(width * MinShareOfWidth, width - right - left);
            var empty = context.MarkSizePt(paragraph) * LineOf(context, paragraph);
            var atoms = Atoms(context, paragraph);
            var lines = atoms.Count == 0
                ? new List<WordLineBox> { new(empty, false) }
                : Wrap(atoms, firstWidth, restWidth, empty);
            var spaced = lines.Select(l => new WordLineBox(Spaced(l.Height, line, rule), l.PageBreak)).ToList();
            return new WordParagraphBox(spaced, before, after, WordStyles.IsOn(context.Styles.ParagraphValue<PageBreakBefore>(paragraph)));
        }

        public static double BlockHeight(WordLayoutContext context, OpenXmlElement element, double width)
        {
            if (element is Table table)
                return WordTableMetrics.Height(context, table, width);

            if (element is not Paragraph paragraph)
                return 0;

            var box = Measure(context, paragraph, width);
            return box.Before + box.Height + box.After;
        }

        public static double FontSizeOf(WordLayoutContext context, Paragraph paragraph)
        {
            var run = WordText.TextRuns(paragraph).FirstOrDefault();
            return run == null ? context.MarkSizePt(paragraph) : context.SizePt(run, paragraph);
        }

        private static double LineOf(WordLayoutContext context, Paragraph paragraph) =>
            WordFontMetrics.For(context.MarkLatinFamily(paragraph)).LineHeight;

        private static double Spaced(double natural, double line, string rule)
        {
            if (rule == "exact")
                return line / TwipsPerPt;

            if (rule == "atLeast")
                return Math.Max(natural, line / TwipsPerPt);

            return natural * line / SingleLine;
        }

        private static List<Atom> Atoms(WordLayoutContext context, Paragraph paragraph)
        {
            var atoms = new List<Atom>();
            foreach (var run in WordText.Runs(paragraph))
            {
                if (WordText.IsHidden(run))
                    continue;

                var size = context.SizePt(run, paragraph);
                var latin = WordFontMetrics.For(context.LatinFamily(run, paragraph));
                var east = WordFontMetrics.For(context.EastAsiaFamily(run, paragraph));
                foreach (var child in run.ChildElements)
                {
                    switch (child)
                    {
                        case Text text:
                            foreach (var rune in text.Text.EnumerateRunes())
                                atoms.Add(Glyph(rune, size, latin, east));

                            break;
                        case TabChar:
                            atoms.Add(new Atom(Kind.Tab, 0, latin.LineHeight * size, false, false));
                            break;
                        case CarriageReturn:
                            atoms.Add(new Atom(Kind.NewLine, 0, 0, false, false));
                            break;
                        case Break br when br.Type?.Value == BreakValues.Page || br.Type?.Value == BreakValues.Column:
                            atoms.Add(new Atom(Kind.PageBreak, 0, 0, false, false));
                            break;
                        case Break:
                            atoms.Add(new Atom(Kind.NewLine, 0, 0, false, false));
                            break;
                        case Drawing or AlternateContent:
                            foreach (var inline in child.Descendants<Wp.Inline>())
                            {
                                var width = (inline.Extent?.Cx?.Value ?? 0) / EmuPerPt;
                                var height = (inline.Extent?.Cy?.Value ?? 0) / EmuPerPt;
                                atoms.Add(new Atom(Kind.Object, width, height, false, false));
                            }

                            break;
                    }
                }
            }

            return atoms;
        }

        private static Atom Glyph(Rune rune, double size, WordFontMetrics latin, WordFontMetrics east)
        {
            var halfwidth = rune.Value is >= 0xFF61 and <= 0xFFDC;
            var eastAsia = !halfwidth && (rune.Value > 0xFFFF || WordFontMetrics.IsEastAsia((char)rune.Value));
            var metrics = eastAsia ? east : latin;
            var breakAfter = rune.Value == ' ' || rune.Value == '-' || eastAsia;
            return new Atom(Kind.Glyph, metrics.Advance(rune.Value) * size, metrics.LineHeight * size, rune.Value == ' ', breakAfter);
        }

        private static List<WordLineBox> Wrap(List<Atom> atoms, double firstWidth, double restWidth, double empty)
        {
            var lines = new List<WordLineBox>();
            var line = new List<Atom>();
            var width = 0.0;
            var available = firstWidth;
            var closed = false;

            void Finish(bool pageBreak)
            {
                lines.Add(new WordLineBox(line.Count == 0 ? empty : Natural(line), pageBreak));
                line = new List<Atom>();
                width = 0;
                available = restWidth;
            }

            for (var i = 0; i < atoms.Count; i++)
            {
                var atom = atoms[i];
                if (atom.Kind == Kind.NewLine)
                {
                    Finish(false);
                    closed = false;
                    continue;
                }

                if (atom.Kind == Kind.PageBreak)
                {
                    Finish(true);
                    closed = true;
                    continue;
                }

                closed = false;

                var advance = atom.Kind == Kind.Tab ? TabStopPt - width % TabStopPt : atom.Width;
                if (!atom.Space && line.Count > 0 && width + advance > available)
                {
                    var cut = BreakBefore(line);
                    if (cut >= 0)
                    {
                        var carry = line.GetRange(cut + 1, line.Count - cut - 1);
                        line.RemoveRange(cut + 1, line.Count - cut - 1);
                        Finish(false);
                        line.AddRange(carry);
                        width = carry.Sum(a => a.Width);
                    }
                    else
                    {
                        Finish(false);
                    }

                    i--;
                    continue;
                }

                line.Add(atom);
                width += advance;
            }

            if (!closed)
                Finish(false);

            return lines;
        }

        private static int BreakBefore(List<Atom> line)
        {
            for (var k = line.Count - 2; k >= 0; k--)
            {
                if (line[k].Break)
                    return k;
            }

            return -1;
        }

        private static double Natural(List<Atom> line) =>
            line.Count == 0 ? 0 : line.Where(a => a.Kind is Kind.Glyph or Kind.Tab or Kind.Object).Select(a => a.Height).DefaultIfEmpty(0).Max();

        private static double Twips(OpenXmlElement element, string name, int fallback = 0)
        {
            var value = Attribute(element, name);
            return value != null && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : fallback;
        }

        private static string Attribute(OpenXmlElement element, string name) =>
            element?.GetAttributes().FirstOrDefault(a => a.LocalName == name).Value;
    }
}
