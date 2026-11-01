using System.Collections.Generic;
using System.Linq;
using System.Text;
using DocuHelper.Tools.Word;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Importers.Hwp
{
    internal static class HwpWordNumbering
    {
        private const int MaxWordLevels = 9;

        internal static void Write(WordprocessingDocument word, IReadOnlyDictionary<int, HwpNumberingDef> numberings)
        {
            if (numberings == null || numberings.Count == 0)
                return;

            var main = word.MainDocumentPart;
            var part = main.NumberingDefinitionsPart ?? main.AddNewPart<NumberingDefinitionsPart>();
            var root = part.Numbering ??= new Numbering();
            foreach (var element in root.Elements<AbstractNum>().ToList())
                element.Remove();

            foreach (var element in root.Elements<NumberingInstance>().ToList())
                element.Remove();

            var ordered = numberings.OrderBy(p => p.Key).ToList();
            foreach (var pair in ordered)
                root.AppendChild(WordNumbering.NewAbstract(pair.Key, LevelsOf(pair.Value)));

            foreach (var pair in ordered)
                root.AppendChild(WordNumbering.NewInstance(pair.Key, pair.Key, false));
        }

        private static Level[] LevelsOf(HwpNumberingDef definition)
        {
            var sources = definition.Levels.Take(MaxWordLevels).ToList();
            if (sources.Count == 0)
                sources.Add(new HwpNumberingLevelDef { Text = "^1." });

            return sources.Select((source, index) => LevelOf(index, source)).ToArray();
        }

        private static Level LevelOf(int index, HwpNumberingLevelDef source)
        {
            var level = new Level { LevelIndex = index };
            level.AppendChild(new StartNumberingValue { Val = source.Start });
            level.AppendChild(new NumberingFormat { Val = FormatOf(source.Format) });
            level.AppendChild(new LevelText { Val = WordText(source.Text) });
            level.AppendChild(new LevelJustification { Val = LevelJustificationValues.Left });
            return level;
        }

        private static NumberFormatValues FormatOf(string format) => format switch
        {
            "CIRCLED_DIGIT" or "DECAGON_CIRCLE" => NumberFormatValues.DecimalEnclosedCircle,
            "ROMAN_CAPITAL" => NumberFormatValues.UpperRoman,
            "ROMAN_SMALL" => NumberFormatValues.LowerRoman,
            "LATIN_CAPITAL" => NumberFormatValues.UpperLetter,
            "LATIN_SMALL" => NumberFormatValues.LowerLetter,
            "HANGUL_SYLLABLE" => NumberFormatValues.Ganada,
            "HANGUL_JAMO" => NumberFormatValues.Chosung,
            "HANGUL_PHONETIC" => NumberFormatValues.KoreanDigital,
            "BULLET" => NumberFormatValues.Bullet,
            _ => NumberFormatValues.Decimal,
        };

        private static string WordText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var builder = new StringBuilder(text.Length);
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '^' && i + 1 < text.Length && text[i + 1] >= '1' && text[i + 1] <= '9')
                {
                    builder.Append('%').Append(text[i + 1]);
                    i++;
                    continue;
                }

                builder.Append(text[i]);
            }

            return builder.ToString();
        }
    }
}
