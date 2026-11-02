using System;
using System.Collections.Generic;
using System.Text;

namespace DocuHelper.Importers.Legacy.Ppt
{
    internal static class PptTextBuilder
    {
        private const int MaxRuns = 100000;
        private const int MaxLevel = 4;

        public static string ReadText(PptRecord record)
        {
            var text = record.Type == PptTypes.TextCharsAtom ? record.Utf16(0, record.Length / 2) : record.Latin1(0, record.Length);
            return text.Replace("\0", string.Empty);
        }

        public static PptTextBody Build(int textType, string text, PptRecord style, PptTextStyles styles, Dictionary<int, string> fields = null)
        {
            var body = new PptTextBody { TextType = textType };
            var paragraphRuns = new List<(int Start, int Count, int Level, PptParaProps Props)>();
            var characterRuns = new List<(int Start, int Count, PptCharProps Props)>();
            if (style != null)
                ReadStyle(style, text.Length + 1, paragraphRuns, characterRuns);

            var paragraphIndex = 0;
            var characterIndex = 0;
            var runCount = 0;
            var position = 0;
            while (position <= text.Length)
            {
                var end = text.IndexOf('\r', position);
                var last = end < 0;
                if (last)
                    end = text.Length;

                while (paragraphIndex + 1 < paragraphRuns.Count && paragraphRuns[paragraphIndex].Start + paragraphRuns[paragraphIndex].Count <= position)
                    paragraphIndex++;

                var level = 0;
                PptParaProps explicitProps = null;
                if (paragraphIndex < paragraphRuns.Count && Covers(paragraphRuns[paragraphIndex].Start, paragraphRuns[paragraphIndex].Count, position))
                {
                    level = paragraphRuns[paragraphIndex].Level;
                    explicitProps = paragraphRuns[paragraphIndex].Props;
                }

                level = level < 0 ? 0 : level > MaxLevel ? MaxLevel : level;
                var paragraph = new PptParagraph { Level = level, Props = styles.Paragraph(textType, level).With(explicitProps) };
                var baseCharacter = styles.Character(textType, level);
                var cursor = position;
                while (cursor < end && runCount < MaxRuns)
                {
                    while (characterIndex + 1 < characterRuns.Count && characterRuns[characterIndex].Start + characterRuns[characterIndex].Count <= cursor)
                        characterIndex++;

                    PptCharProps explicitCharacter = null;
                    var stop = end;
                    if (characterIndex < characterRuns.Count && Covers(characterRuns[characterIndex].Start, characterRuns[characterIndex].Count, cursor))
                    {
                        explicitCharacter = characterRuns[characterIndex].Props;
                        stop = System.Math.Min(end, characterRuns[characterIndex].Start + characterRuns[characterIndex].Count);
                    }

                    var properties = baseCharacter.With(explicitCharacter);
                    AddRuns(paragraph, text, cursor, stop, properties, fields, ref runCount);
                    cursor = stop;
                }

                var endCharacter = position < text.Length && characterRuns.Count > 0 ? FindCharacter(characterRuns, position) : null;
                paragraph.EndProps = baseCharacter.With(endCharacter);
                body.Paragraphs.Add(paragraph);
                if (last)
                    break;

                position = end + 1;
            }

            if (body.Paragraphs.Count > 1 && text.Length > 0 && text[^1] == '\r')
                body.Paragraphs.RemoveAt(body.Paragraphs.Count - 1);

            return body;
        }

        private static bool Covers(int start, int count, int position) => position >= start && position < start + count;

        private static PptCharProps FindCharacter(List<(int Start, int Count, PptCharProps Props)> runs, int position)
        {
            foreach (var run in runs)
            {
                if (Covers(run.Start, run.Count, position))
                    return run.Props;
            }

            return null;
        }

        private static void AddRuns(PptParagraph paragraph, string text, int start, int end, PptCharProps properties, Dictionary<int, string> fields, ref int runCount)
        {
            var builder = new StringBuilder();
            for (var i = start; i < end; i++)
            {
                var c = text[i];
                if (fields != null && fields.TryGetValue(i, out var field))
                {
                    Flush(paragraph, builder, properties, ref runCount);
                    if (field.Length > 0)
                    {
                        paragraph.Runs.Add(new PptRun { Field = field, Text = field == "slidenum" ? "\u2039#\u203A" : DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), Props = properties });
                        runCount++;
                    }
                }
                else if (c == '\v' || c == '\n')
                {
                    Flush(paragraph, builder, properties, ref runCount);
                    paragraph.Runs.Add(new PptRun { IsBreak = true, Props = properties });
                    runCount++;
                }
                else if (c == '\t' || c >= ' ')
                {
                    builder.Append(c);
                }
            }

            Flush(paragraph, builder, properties, ref runCount);
        }

        private static void Flush(PptParagraph paragraph, StringBuilder builder, PptCharProps properties, ref int runCount)
        {
            if (builder.Length == 0)
                return;

            var text = builder.ToString();
            builder.Clear();
            var previous = paragraph.Runs.Count > 0 ? paragraph.Runs[^1] : null;
            if (previous != null && !previous.IsBreak && previous.Field == null && previous.Props.SameAs(properties))
            {
                previous.Text += text;
                return;
            }

            paragraph.Runs.Add(new PptRun { Text = text, Props = properties });
            runCount++;
        }

        private static void ReadStyle(
            PptRecord style,
            int total,
            List<(int Start, int Count, int Level, PptParaProps Props)> paragraphRuns,
            List<(int Start, int Count, PptCharProps Props)> characterRuns)
        {
            var at = 0;
            var covered = 0L;
            while (covered < total && at + 6 <= style.Length)
            {
                var count = style.UInt32(at);
                var level = style.UInt16(at + 4);
                at += 6;
                var props = PptParaProps.Read(style, ref at);
                if (count == 0 || count > int.MaxValue)
                    break;

                paragraphRuns.Add(((int)covered, (int)count, level, props));
                covered += count;
            }

            covered = 0;
            while (covered < total && at + 4 <= style.Length)
            {
                var count = style.UInt32(at);
                at += 4;
                var props = PptCharProps.Read(style, ref at);
                if (count == 0 || count > int.MaxValue)
                    break;

                characterRuns.Add(((int)covered, (int)count, props));
                covered += count;
            }
        }
    }
}
