using System.Collections.Generic;
using System.Linq;
using System.Text;
using DocuHelper.Tools.Word;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal static class DocWordNumbering
    {
        private const int LevelCount = 9;
        private const int BulletFormat = 23;
        private const string DefaultBullet = "•";

        public static HashSet<int> Write(WordprocessingDocument word, DocListTable lists, DocFontTable fonts)
        {
            var valid = new HashSet<int>();
            if (lists.Definitions.Count == 0 || lists.Overrides.Count == 0)
                return valid;

            var main = word.MainDocumentPart;
            var part = main.NumberingDefinitionsPart ?? main.AddNewPart<NumberingDefinitionsPart>();
            var root = part.Numbering ??= new Numbering();
            root.RemoveAllChildren();

            for (var i = 0; i < lists.Definitions.Count; i++)
                root.AppendChild(WordNumbering.NewAbstract(i, Levels(lists.Definitions[i], fonts)));

            for (var i = 0; i < lists.Overrides.Count; i++)
            {
                var item = lists.Overrides[i];
                var abstractId = lists.Definitions.FindIndex(d => d.Id == item.ListId);
                if (abstractId < 0)
                    continue;

                var instance = WordNumbering.NewInstance(i + 1, abstractId, false);
                foreach (var over in item.Levels.Where(l => l.Level < LevelCount))
                {
                    if (over.StartAt == null && over.Format == null)
                        continue;

                    var level = new LevelOverride { LevelIndex = over.Level };
                    if (over.StartAt.HasValue)
                        level.AppendChild(new StartOverrideNumberingValue { Val = over.StartAt.Value });

                    if (over.Format != null)
                        level.AppendChild(Level(over.Level, over.Format, fonts));

                    instance.AppendChild(level);
                }

                root.AppendChild(instance);
                valid.Add(i + 1);
            }

            return valid;
        }

        private static List<Level> Levels(DocListDefinition definition, DocFontTable fonts)
        {
            var levels = new List<Level>();
            for (var i = 0; i < LevelCount; i++)
            {
                var source = i < definition.Levels.Count ? definition.Levels[i] : new DocListLevel { Text = "%" + (i + 1) + ".", Pap = Indented(i) };
                levels.Add(Level(i, source, fonts));
            }

            return levels;
        }

        private static DocPap Indented(int level) => new DocPap { Left = 720 * (level + 1), First = -360 };

        private static Level Level(int index, DocListLevel source, DocFontTable fonts)
        {
            var level = new Level { LevelIndex = index };
            level.AppendChild(new StartNumberingValue { Val = source.Start });
            level.AppendChild(new NumberingFormat { Val = Format(source.Format) });
            if (source.Follow == 1)
                level.AppendChild(new LevelSuffix { Val = LevelSuffixValues.Space });
            else if (source.Follow == 2)
                level.AppendChild(new LevelSuffix { Val = LevelSuffixValues.Nothing });

            level.AppendChild(new LevelText { Val = Text(source) });
            level.AppendChild(new LevelJustification
            {
                Val = source.Justification == 1 ? LevelJustificationValues.Center : source.Justification == 2 ? LevelJustificationValues.Right : LevelJustificationValues.Left,
            });

            if (source.Pap.Left.HasValue || source.Pap.First.HasValue)
            {
                var properties = new PreviousParagraphProperties();
                DocWordParagraphs.Fill(properties, new DocPap { Left = source.Pap.Left, First = source.Pap.First }, null, 0);
                level.AppendChild(properties);
            }

            var run = new NumberingSymbolRunProperties();
            DocWordRuns.Fill(run, source.Chp, fonts);
            if (run.HasChildren)
                level.AppendChild(run);

            return level;
        }

        private static string Text(DocListLevel source)
        {
            if (source.Format == BulletFormat && string.IsNullOrEmpty(source.Text))
                return DefaultBullet;

            var builder = new StringBuilder();
            foreach (var c in source.Text)
            {
                if (c < LevelCount)
                    builder.Append('%').Append((int)c + 1);
                else if (c == '%')
                    builder.Append("%%");
                else if (c >= ' ')
                    builder.Append(c);
            }

            return DocText.Clean(builder.ToString());
        }

        private static NumberFormatValues Format(int format) => format switch
        {
            1 => NumberFormatValues.UpperRoman,
            2 => NumberFormatValues.LowerRoman,
            3 => NumberFormatValues.UpperLetter,
            4 => NumberFormatValues.LowerLetter,
            5 => NumberFormatValues.Ordinal,
            6 => NumberFormatValues.CardinalText,
            7 => NumberFormatValues.OrdinalText,
            14 => NumberFormatValues.DecimalFullWidth,
            18 => NumberFormatValues.DecimalEnclosedCircle,
            22 => NumberFormatValues.DecimalZero,
            23 => NumberFormatValues.Bullet,
            24 => NumberFormatValues.Ganada,
            25 => NumberFormatValues.Chosung,
            255 => NumberFormatValues.None,
            _ => NumberFormatValues.Decimal,
        };
    }
}
