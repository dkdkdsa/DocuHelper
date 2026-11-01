using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Wordprocessing;
using static DocuHelper.Importers.Hwp.HwpxNames;

namespace DocuHelper.Importers.Hwp
{
    internal sealed partial class HwpxWriteState
    {
        private const int MinHwpLevels = 7;
        private const int MaxHwpLevels = 10;
        private const string DefaultBullet = "•";

        private readonly Dictionary<int, HwpxList> _lists = new();

        private (string Heading, int Id, int Level)? ListOf(DocumentFormat.OpenXml.Wordprocessing.Paragraph paragraph, out Indentation indentation)
        {
            indentation = null;
            if (_numbering.Of(paragraph, out var numId, out var level) == null)
                return null;

            var definition = _numbering.FindLevel(numId, level);
            var format = definition?.NumberingFormat?.Val?.Value;
            if (definition == null || format == NumberFormatValues.None)
                return null;

            indentation = definition.PreviousParagraphProperties?.Indentation;
            var bullet = format == NumberFormatValues.Bullet;
            if (!_lists.TryGetValue(numId, out var list))
            {
                list = new HwpxList
                {
                    Id = _lists.Values.Count(l => l.Bullet == bullet) + 1,
                    Bullet = bullet,
                    Levels = LevelsOf(numId),
                };
                _lists[numId] = list;
            }

            return (bullet ? "BULLET" : "NUMBER", list.Id, Math.Max(0, level));
        }

        private Level[] LevelsOf(int numId)
        {
            var levels = new List<Level>();
            for (var index = 0; index < MaxHwpLevels; index++)
            {
                var level = _numbering.FindLevel(numId, index);
                if (level == null)
                    break;

                levels.Add(level);
            }

            return levels.ToArray();
        }

        private XElement NumberingsElement()
        {
            var numbers = _lists.Values.Where(l => !l.Bullet).OrderBy(l => l.Id).ToList();
            var element = new XElement(Hh + "numberings", new XAttribute("itemCnt", Number(numbers.Count)));
            foreach (var list in numbers)
                element.Add(NumberingProperty(list));

            return element;
        }

        private XElement BulletsElement()
        {
            var bullets = _lists.Values.Where(l => l.Bullet).OrderBy(l => l.Id).ToList();
            var element = new XElement(Hh + "bullets", new XAttribute("itemCnt", Number(bullets.Count)));
            foreach (var list in bullets)
            {
                element.Add(new XElement(Hh + "bullet",
                    new XAttribute("id", Number(list.Id)),
                    new XAttribute("char", BulletChar(list)),
                    new XAttribute("useImage", "0")));
            }

            return element;
        }

        private static XElement NumberingProperty(HwpxList list)
        {
            var element = new XElement(Hh + "numbering", new XAttribute("id", Number(list.Id)), new XAttribute("start", "0"));
            var count = Math.Max(MinHwpLevels, list.Levels.Length);
            for (var index = 0; index < count; index++)
            {
                var level = index < list.Levels.Length ? list.Levels[index] : null;
                element.Add(new XElement(Hh + "paraHead",
                    new XAttribute("start", Number(StartOf(level))),
                    new XAttribute("level", Number(index + 1)),
                    new XAttribute("align", "LEFT"),
                    new XAttribute("useInstWidth", "1"),
                    new XAttribute("autoIndent", "1"),
                    new XAttribute("widthAdjust", "0"),
                    new XAttribute("textOffsetType", "PERCENT"),
                    new XAttribute("textOffset", "50"),
                    new XAttribute("numFormat", FormatName(level)),
                    new XAttribute("charPrIDRef", "4294967295"),
                    new XAttribute("checkable", "0"),
                    HwpLevelText(level, index)));
            }

            return element;
        }

        private static string BulletChar(HwpxList list)
        {
            var text = list.Levels.Length > 0 ? list.Levels[0].LevelText?.Val?.Value : null;
            return string.IsNullOrEmpty(text) ? DefaultBullet : text;
        }

        private static int StartOf(Level level) => level?.StartNumberingValue?.Val?.Value ?? 1;

        private static string FormatName(Level level)
        {
            var format = level?.NumberingFormat?.Val?.Value;
            if (format == NumberFormatValues.DecimalEnclosedCircle)
                return "CIRCLED_DIGIT";

            if (format == NumberFormatValues.UpperRoman)
                return "ROMAN_CAPITAL";

            if (format == NumberFormatValues.LowerRoman)
                return "ROMAN_SMALL";

            if (format == NumberFormatValues.UpperLetter)
                return "LATIN_CAPITAL";

            if (format == NumberFormatValues.LowerLetter)
                return "LATIN_SMALL";

            if (format == NumberFormatValues.Ganada)
                return "HANGUL_SYLLABLE";

            if (format == NumberFormatValues.Chosung)
                return "HANGUL_JAMO";

            if (format == NumberFormatValues.KoreanDigital)
                return "HANGUL_PHONETIC";

            if (format == NumberFormatValues.Bullet)
                return "BULLET";

            return "DIGIT";
        }

        private static string HwpLevelText(Level level, int index)
        {
            var text = level?.LevelText?.Val?.Value;
            if (string.IsNullOrEmpty(text))
                return "^" + Number(index + 1) + ".";

            var builder = new StringBuilder(text.Length);
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '%' && i + 1 < text.Length && text[i + 1] >= '1' && text[i + 1] <= '9')
                {
                    builder.Append('^').Append(text[i + 1]);
                    i++;
                    continue;
                }

                builder.Append(text[i]);
            }

            return builder.ToString();
        }

        private sealed class HwpxList
        {
            public int Id { get; init; }

            public bool Bullet { get; init; }

            public Level[] Levels { get; init; }
        }
    }
}
