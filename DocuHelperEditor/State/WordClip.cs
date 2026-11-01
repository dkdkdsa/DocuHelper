using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using DocuHelper.Tools.Word;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State
{
    public sealed record WordClipRun(string Text, WordCharLook Look);

    public sealed record WordClipParagraph(IReadOnlyList<WordClipRun> Runs)
    {
        public string Text => string.Concat(Runs.Select(r => r.Text));
    }

    public sealed class WordClip
    {
        public WordClip(IReadOnlyList<WordClipParagraph> paragraphs)
        {
            Paragraphs = paragraphs;
        }

        public IReadOnlyList<WordClipParagraph> Paragraphs { get; }

        public string Text => string.Join("\n", Paragraphs.Select(p => p.Text));

        public bool HasFormat => Paragraphs.Any(p => p.Runs.Any(r => !IsPlain(r.Look)));

        public string Html
        {
            get
            {
                var html = new StringBuilder("<html><body>");
                foreach (var paragraph in Paragraphs)
                {
                    html.Append("<p>");
                    foreach (var run in paragraph.Runs)
                    {
                        if (run.Text.Length == 0)
                            continue;

                        var style = StyleOf(run.Look);
                        html.Append(style.Length == 0 ? WebUtility.HtmlEncode(run.Text) : $"<span style=\"{WebUtility.HtmlEncode(style)}\">{WebUtility.HtmlEncode(run.Text)}</span>");
                    }

                    if (paragraph.Text.Length == 0)
                        html.Append("&nbsp;");

                    html.Append("</p>");
                }

                return html.Append("</body></html>").ToString();
            }
        }

        public static bool IsPlain(WordCharLook look) => look == null || look.Equals(WordCharLook.Plain);

        public static WordClip Of(WordPaperModel model, WordSelection selection)
        {
            var paragraphs = new List<WordClipParagraph>();
            if (model == null || selection == null || !selection.HasText)
                return new WordClip(paragraphs);

            foreach (var range in selection.Ranges)
            {
                var block = model.FindBlock(range.BlockId);
                if (block == null || block.IsTable)
                    continue;

                paragraphs.Add(new WordClipParagraph(RunsOf(block, range.Start, range.End)));
            }

            return new WordClip(paragraphs);
        }

        public static IReadOnlyList<WordClipRun> RunsOf(WordBlockModel block, int start, int end)
        {
            var text = block.Text ?? string.Empty;
            var from = Math.Clamp(start, 0, text.Length);
            var to = Math.Clamp(end, from, text.Length);
            var runs = new List<WordClipRun>();
            if (block.Segments.Count == 0)
            {
                runs.Add(new WordClipRun(text.Substring(from, to - from), WordCharLook.Plain));
                return runs;
            }

            var position = 0;
            foreach (var segment in block.Segments)
            {
                var segmentStart = position;
                var segmentEnd = position + segment.Text.Length;
                position = segmentEnd;
                if (segment.Text.Length == 0 || segmentEnd <= from || segmentStart >= to)
                    continue;

                var cut = Math.Max(from, segmentStart) - segmentStart;
                var stop = Math.Min(to, segmentEnd) - segmentStart;
                runs.Add(new WordClipRun(segment.Text.Substring(cut, stop - cut), segment.Look));
            }

            return runs;
        }

        public static string StyleOf(WordCharLook look)
        {
            if (IsPlain(look))
                return string.Empty;

            var css = new List<string>();
            if (look.Bold)
                css.Add("font-weight:700");

            if (look.Italic)
                css.Add("font-style:italic");

            var decorations = new List<string>();
            if (look.Underline != null && look.Underline != "none")
                decorations.Add("underline");

            if (look.Strike)
                decorations.Add("line-through");

            if (decorations.Count > 0)
                css.Add("text-decoration:" + string.Join(" ", decorations));

            if (look.Color != null)
                css.Add("color:#" + look.Color);

            if (look.SizePt > 0)
                css.Add("font-size:" + look.SizePt.ToString("0.##", CultureInfo.InvariantCulture) + "pt");

            var font = look.Font == null ? string.Empty : new string(look.Font.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_').ToArray()).Trim();
            if (font.Length > 0)
                css.Add("font-family:'" + font + "'");

            return string.Join(";", css);
        }
    }
}
