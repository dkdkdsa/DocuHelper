using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Tools.Word;
using DocuHelperEditor.State.Papers;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelperEditor.State.Mapping
{
    public static class WordReviewRuns
    {
        public static bool HasRevisions(Paragraph paragraph) =>
            paragraph != null && (paragraph.Descendants<InsertedRun>().Any() || paragraph.Descendants<DeletedRun>().Any());

        public static IReadOnlyList<WordViewSegment> Of(WordBlock block, WordBlockModel model, IReadOnlyList<WordCommentInfo> comments, Func<string, int> toneOf)
        {
            if (block.IsTable || block.Paragraph == null)
                return Array.Empty<WordViewSegment>();

            var anchored = comments.Where(c => !string.IsNullOrEmpty(c.Anchored) && WordIds.ParagraphAnchor(c.ParagraphId) == model.Id).ToList();
            var revised = HasRevisions(block.Paragraph);
            if (!revised && anchored.Count == 0)
                return Array.Empty<WordViewSegment>();

            var segments = revised
                ? Revised(block.Paragraph, toneOf)
                : Plain(model.Segments, block.Text);
            foreach (var comment in anchored)
                segments = Highlight(segments, comment.Anchored, comment.Id);

            return segments;
        }

        private static IReadOnlyList<WordViewSegment> Revised(Paragraph paragraph, Func<string, int> toneOf)
        {
            var segments = new List<WordViewSegment>();
            foreach (var run in paragraph.Descendants<Run>())
            {
                var text = TextOf(run);
                if (text.Length == 0)
                    continue;

                var inserted = run.Ancestors<InsertedRun>().FirstOrDefault();
                var deleted = inserted == null ? run.Ancestors<DeletedRun>().FirstOrDefault() : null;
                var holder = (OpenXmlElement)inserted ?? deleted;
                var kind = inserted != null ? WordRevisionKinds.Insert : deleted != null ? WordRevisionKinds.Delete : null;
                var id = holder == null ? null : AttributeOf(holder, "id");
                var tone = kind == null ? 0 : toneOf(AttributeOf(holder, "author"));
                Append(segments, new WordViewSegment(text, WordLooks.CharOf(run.RunProperties, false), kind, id, tone, null));
            }

            return segments;
        }

        private static IReadOnlyList<WordViewSegment> Plain(IReadOnlyList<WordCharSegment> segments, string text)
        {
            if (segments.Count == 0)
                return new[] { new WordViewSegment(text ?? string.Empty, WordCharLook.Plain, null, null, 0, null) };

            return segments.Select(s => new WordViewSegment(s.Text, s.Look, null, null, 0, null)).ToList();
        }

        private static IReadOnlyList<WordViewSegment> Highlight(IReadOnlyList<WordViewSegment> segments, string anchored, string commentId)
        {
            var text = string.Concat(segments.Select(s => s.Text));
            var start = text.IndexOf(anchored, StringComparison.Ordinal);
            if (start < 0)
                return segments;

            var end = start + anchored.Length;
            var result = new List<WordViewSegment>();
            var position = 0;
            foreach (var segment in segments)
            {
                var from = position;
                var to = position + segment.Text.Length;
                position = to;
                if (segment.Text.Length == 0 || to <= start || from >= end)
                {
                    result.Add(segment);
                    continue;
                }

                var cut = Math.Clamp(start - from, 0, segment.Text.Length);
                var stop = Math.Clamp(end - from, 0, segment.Text.Length);
                if (cut > 0)
                    result.Add(segment with { Text = segment.Text.Substring(0, cut) });

                result.Add(segment with { Text = segment.Text.Substring(cut, stop - cut), CommentId = segment.CommentId ?? commentId });
                if (stop < segment.Text.Length)
                    result.Add(segment with { Text = segment.Text.Substring(stop) });
            }

            return result;
        }

        private static void Append(List<WordViewSegment> segments, WordViewSegment next)
        {
            if (segments.Count > 0)
            {
                var last = segments[^1];
                if (last.Look.Equals(next.Look) && last.Kind == next.Kind && last.RevisionId == next.RevisionId && last.Tone == next.Tone && last.CommentId == next.CommentId)
                {
                    segments[^1] = last with { Text = last.Text + next.Text };
                    return;
                }
            }

            segments.Add(next);
        }

        private static string TextOf(Run run)
        {
            var text = string.Empty;
            foreach (var child in run.ChildElements)
            {
                text += child switch
                {
                    Text value => value.Text,
                    DeletedText value => value.Text,
                    TabChar => "\t",
                    _ => string.Empty,
                };
            }

            return text;
        }

        private static string AttributeOf(OpenXmlElement element, string name) =>
            element.GetAttributes().FirstOrDefault(a => a.LocalName == name).Value;
    }
}
