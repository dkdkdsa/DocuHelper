using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordBuiltinStyles
    {
        private static readonly StyleValues P = StyleValues.Paragraph;
        private static readonly StyleValues C = StyleValues.Character;

        private static readonly List<WordBuiltin> s_all = Build();
        private static readonly Dictionary<string, WordBuiltin> s_lookup = Lookup();

        public static IReadOnlyList<WordBuiltin> All => s_all;

        public static string Alias(string koreanOrEnglish) => Find(koreanOrEnglish)?.Display;

        public static WordBuiltin Find(string nameOrId)
        {
            var key = WordStyles.Normalize(nameOrId);
            return key.Length > 0 && s_lookup.TryGetValue(key, out var builtin) ? builtin : null;
        }

        public static string Ensure(WordStyles styles, string nameOrId)
        {
            var text = (nameOrId ?? string.Empty).Trim();
            if (text.Length == 0)
                throw new ToolInputException("스타일 이름이 비어 있어요.");

            var exact = styles.Find(text) ?? styles.FindByName(text);
            if (exact != null)
                return exact.StyleId.Value;

            var builtin = Find(text);
            if (builtin == null)
                throw Missing(styles, text);

            var byId = styles.Find(builtin.Id);
            var existing = (byId != null && WordStyles.Normalize(byId.StyleName?.Val?.Value) == WordStyles.Normalize(builtin.Name) ? byId : null)
                ?? styles.FindByName(builtin.Name)
                ?? styles.FindByName(builtin.Korean);
            if (existing != null)
                return existing.StyleId.Value;

            return Create(styles, builtin);
        }

        private static string Create(WordStyles styles, WordBuiltin builtin)
        {
            switch (builtin.Id)
            {
                case "Normal":
                    return styles.NormalId ?? Add(styles, Make(builtin, null, null, new StyleParagraphProperties(), null));
                case "Title":
                    return styles.EnsureStyle(WordKind.Title);
                case "Heading1":
                    return styles.EnsureStyle(WordKind.Heading1);
                case "Heading2":
                    return styles.EnsureStyle(WordKind.Heading2);
            }

            var normal = styles.NormalId;
            var para = new StyleParagraphProperties();
            var run = new StyleRunProperties();
            string basedOn = builtin.Type == P ? normal : null;
            string next = builtin.Type == P ? normal : null;
            StyleTableProperties table = null;

            var heading = HeadingLevel(builtin.Id);
            if (heading >= 3)
            {
                para.Append(new KeepNext(), new KeepLines(), new SpacingBetweenLines { Before = "240", After = "80" }, new OutlineLevel { Val = heading - 1 });
                var size = heading switch { 3 => "24", 4 => "22", 5 => "21", _ => "20" };
                run.Append(new Bold(), new BoldComplexScript(), new FontSize { Val = size }, new FontSizeComplexScript { Val = size });
            }
            else if (builtin.Id.StartsWith("TOC", StringComparison.Ordinal) && builtin.Id.Length == 4 && char.IsDigit(builtin.Id[3]))
            {
                var level = builtin.Id[3] - '0';
                para.Append(new SpacingBetweenLines { After = "100" }, new Indentation { Left = (220 * (level - 1)).ToString(CultureInfo.InvariantCulture) });
            }
            else if (builtin.Id.StartsWith("Index", StringComparison.Ordinal) && builtin.Id.Length == 6 && char.IsDigit(builtin.Id[5]))
            {
                var level = builtin.Id[5] - '0';
                para.Append(new SpacingBetweenLines { After = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto }, new Indentation { Left = (220 * level).ToString(CultureInfo.InvariantCulture), Hanging = "220" });
            }
            else
            {
                switch (builtin.Id)
                {
                    case "Subtitle":
                        para.Append(new SpacingBetweenLines { After = "160" });
                        run.Append(new Color { Val = "595959" }, new Spacing { Val = 15 }, new FontSize { Val = "28" }, new FontSizeComplexScript { Val = "28" });
                        break;
                    case "NoSpacing":
                        para.Append(new SpacingBetweenLines { After = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto });
                        break;
                    case "Quote":
                        para.Append(new SpacingBetweenLines { Before = "200", After = "160" }, new Indentation { Left = "864", Right = "864" }, new Justification { Val = JustificationValues.Center });
                        run.Append(new Italic(), new ItalicComplexScript(), new Color { Val = "404040" });
                        break;
                    case "IntenseQuote":
                        para.Append(
                            new ParagraphBorders(
                                new TopBorder { Val = BorderValues.Single, Size = 4U, Space = 10U, Color = "4472C4" },
                                new BottomBorder { Val = BorderValues.Single, Size = 4U, Space = 10U, Color = "4472C4" }),
                            new SpacingBetweenLines { Before = "360", After = "360" },
                            new Indentation { Left = "864", Right = "864" },
                            new Justification { Val = JustificationValues.Center });
                        run.Append(new Italic(), new ItalicComplexScript(), new Color { Val = "4472C4" });
                        break;
                    case "ListParagraph":
                        para.Append(new Indentation { Left = "720" }, new ContextualSpacing());
                        break;
                    case "Caption":
                        para.Append(new SpacingBetweenLines { After = "200", Line = "240", LineRule = LineSpacingRuleValues.Auto });
                        run.Append(new Italic(), new ItalicComplexScript(), new Color { Val = "44546A" }, new FontSize { Val = "18" }, new FontSizeComplexScript { Val = "18" });
                        break;
                    case "Header":
                    case "Footer":
                        para.Append(
                            new Tabs(new TabStop { Val = TabStopValues.Center, Position = 4513 }, new TabStop { Val = TabStopValues.Right, Position = 9026 }),
                            new SpacingBetweenLines { After = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto });
                        break;
                    case "FootnoteText":
                    case "EndnoteText":
                        para.Append(new SpacingBetweenLines { After = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto });
                        run.Append(new FontSize { Val = "18" }, new FontSizeComplexScript { Val = "18" });
                        break;
                    case "CommentText":
                        para.Append(new SpacingBetweenLines { Line = "240", LineRule = LineSpacingRuleValues.Auto });
                        run.Append(new FontSize { Val = "20" }, new FontSizeComplexScript { Val = "20" });
                        break;
                    case "CommentSubject":
                        basedOn = Ensure(styles, "CommentText");
                        next = normal;
                        run.Append(new Bold(), new BoldComplexScript());
                        break;
                    case "BalloonText":
                        run.Append(new RunFonts { Ascii = "Segoe UI", HighAnsi = "Segoe UI", ComplexScript = "Segoe UI" }, new FontSize { Val = "18" }, new FontSizeComplexScript { Val = "18" });
                        break;
                    case "TOCHeading":
                        basedOn = Ensure(styles, "Heading1");
                        para.Append(new OutlineLevel { Val = 9 });
                        break;
                    case "TableofFigures":
                        para.Append(new SpacingBetweenLines { After = "0" });
                        break;
                    case "FootnoteReference":
                    case "EndnoteReference":
                        run.Append(new VerticalTextAlignment { Val = VerticalPositionValues.Superscript });
                        break;
                    case "CommentReference":
                        run.Append(new FontSize { Val = "16" }, new FontSizeComplexScript { Val = "16" });
                        break;
                    case "Hyperlink":
                        run.Append(new Color { Val = "0563C1" }, new Underline { Val = UnderlineValues.Single });
                        break;
                    case "Strong":
                        run.Append(new Bold(), new BoldComplexScript());
                        break;
                    case "Emphasis":
                        run.Append(new Italic(), new ItalicComplexScript());
                        break;
                    case "SubtleEmphasis":
                        run.Append(new Italic(), new ItalicComplexScript(), new Color { Val = "404040" });
                        break;
                    case "IntenseEmphasis":
                        run.Append(new Italic(), new ItalicComplexScript(), new Color { Val = "4472C4" });
                        break;
                    case "PlaceholderText":
                        run.Append(new Color { Val = "808080" });
                        break;
                    case "TableGrid":
                        basedOn = styles.Find("TableNormal") != null ? "TableNormal" : null;
                        next = null;
                        para.Append(new SpacingBetweenLines { After = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto });
                        table = new StyleTableProperties(
                            new TableStyleRowBandSize { Val = 1 },
                            new TableStyleColumnBandSize { Val = 1 },
                            new TableBorders(
                                new TopBorder { Val = BorderValues.Single, Size = 4U, Space = 0U, Color = "auto" },
                                new LeftBorder { Val = BorderValues.Single, Size = 4U, Space = 0U, Color = "auto" },
                                new BottomBorder { Val = BorderValues.Single, Size = 4U, Space = 0U, Color = "auto" },
                                new RightBorder { Val = BorderValues.Single, Size = 4U, Space = 0U, Color = "auto" },
                                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4U, Space = 0U, Color = "auto" },
                                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4U, Space = 0U, Color = "auto" }));
                        break;
                }
            }

            if (builtin.Type == C)
                basedOn = styles.Find("DefaultParagraphFont") != null ? "DefaultParagraphFont" : null;

            return Add(styles, Make(builtin, basedOn, next, para, run, table));
        }

        private static string Add(WordStyles styles, Style style)
        {
            style.StyleId = styles.NewId(style.StyleId.Value);
            return styles.Add(style).StyleId.Value;
        }

        private static Style Make(WordBuiltin builtin, string basedOn, string next, StyleParagraphProperties para, StyleRunProperties run, StyleTableProperties table = null)
        {
            var style = new Style { Type = builtin.Type, StyleId = builtin.Id };
            style.AppendChild(new StyleName { Val = builtin.Name });
            if (!string.IsNullOrEmpty(basedOn))
                style.AppendChild(new BasedOn { Val = basedOn });

            if (!string.IsNullOrEmpty(next) && builtin.Type == P)
                style.AppendChild(new NextParagraphStyle { Val = next });

            style.AppendChild(new UIPriority { Val = builtin.Priority });
            if (builtin.Priority >= 35 && !builtin.Quick)
                style.AppendChild(new UnhideWhenUsed());

            if (builtin.Quick)
                style.AppendChild(new PrimaryStyle());

            if (para != null && para.HasChildren && builtin.Type != C)
                style.AppendChild(para);

            if (run != null && run.HasChildren)
                style.AppendChild(run);

            if (table != null)
                style.AppendChild(table);

            if (builtin.Id == "Normal")
                style.Default = true;

            return style;
        }

        private static int HeadingLevel(string id) =>
            id.Length == 8 && id.StartsWith("Heading", StringComparison.Ordinal) && char.IsDigit(id[7]) ? id[7] - '0' : 0;

        private static ToolInputException Missing(WordStyles styles, string text)
        {
            var key = WordStyles.Normalize(text);
            var names = new List<string>();
            foreach (var style in styles.Root?.Elements<Style>() ?? Enumerable.Empty<Style>())
            {
                if (style.Type?.Value == P && style.StyleName?.Val?.Value != null)
                    names.Add(style.StyleName.Val.Value);
            }

            var candidates = names.Concat(s_all.Where(b => b.Type == P).Select(b => b.Display)).Concat(s_all.Where(b => b.Type == P).Select(b => b.Korean)).Distinct().ToList();
            var near = candidates
                .Select(n => (Name: n, Distance: Distance(WordStyles.Normalize(n), key)))
                .Where(x => x.Distance <= Math.Max(2, key.Length / 2))
                .OrderBy(x => x.Distance)
                .Take(4)
                .Select(x => x.Name)
                .ToList();

            if (near.Count > 0)
                return new ToolInputException($"‘{text}’ 스타일을 찾지 못했어요. 비슷한 이름: {string.Join(", ", near)}");

            return new ToolInputException($"‘{text}’ 스타일을 찾지 못했어요. 문서에 있는 스타일: {string.Join(", ", names.Take(8))}");
        }

        private static int Distance(string a, string b)
        {
            var previous = Enumerable.Range(0, b.Length + 1).ToArray();
            for (var i = 1; i <= a.Length; i++)
            {
                var current = new int[b.Length + 1];
                current[0] = i;
                for (var j = 1; j <= b.Length; j++)
                    current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));

                previous = current;
            }

            return previous[b.Length];
        }

        private static Dictionary<string, WordBuiltin> Lookup()
        {
            var map = new Dictionary<string, WordBuiltin>(StringComparer.Ordinal);
            foreach (var builtin in s_all)
            {
                foreach (var key in new[] { builtin.Id, builtin.Name, builtin.Display, builtin.Korean })
                    map.TryAdd(WordStyles.Normalize(key), builtin);
            }

            return map;
        }

        private static List<WordBuiltin> Build()
        {
            var list = new List<WordBuiltin>
            {
                new("Normal", "Normal", "Normal", "표준", P, 0, true),
                new("Title", "Title", "Title", "제목", P, 10, true),
                new("Subtitle", "Subtitle", "Subtitle", "부제", P, 11, true),
            };

            for (var level = 1; level <= 9; level++)
            {
                var n = level.ToString(CultureInfo.InvariantCulture);
                list.Add(new WordBuiltin("Heading" + n, "heading " + n, "Heading " + n, "제목 " + n, P, 9, true));
            }

            list.AddRange(new[]
            {
                new WordBuiltin("NoSpacing", "No Spacing", "No Spacing", "간격 없음", P, 1, true),
                new WordBuiltin("Quote", "Quote", "Quote", "인용", P, 29, true),
                new WordBuiltin("IntenseQuote", "Intense Quote", "Intense Quote", "강한 인용", P, 30, true),
                new WordBuiltin("ListParagraph", "List Paragraph", "List Paragraph", "목록 단락", P, 34, true),
                new WordBuiltin("Caption", "caption", "Caption", "캡션", P, 35, true),
                new WordBuiltin("Header", "header", "Header", "머리글", P, 99, false),
                new WordBuiltin("Footer", "footer", "Footer", "바닥글", P, 99, false),
                new WordBuiltin("FootnoteText", "footnote text", "Footnote Text", "각주 텍스트", P, 99, false),
                new WordBuiltin("EndnoteText", "endnote text", "Endnote Text", "미주 텍스트", P, 99, false),
                new WordBuiltin("CommentText", "annotation text", "Comment Text", "메모 텍스트", P, 99, false),
                new WordBuiltin("CommentSubject", "annotation subject", "Comment Subject", "메모 주제", P, 99, false),
                new WordBuiltin("BalloonText", "Balloon Text", "Balloon Text", "풍선 도움말 텍스트", P, 99, false),
                new WordBuiltin("TOCHeading", "TOC Heading", "TOC Heading", "목차 제목", P, 39, false),
                new WordBuiltin("TableofFigures", "table of figures", "Table of Figures", "그림 목차", P, 99, false),
                new WordBuiltin("FootnoteReference", "footnote reference", "Footnote Reference", "각주 참조", C, 99, false),
                new WordBuiltin("EndnoteReference", "endnote reference", "Endnote Reference", "미주 참조", C, 99, false),
                new WordBuiltin("CommentReference", "annotation reference", "Comment Reference", "메모 참조", C, 99, false),
                new WordBuiltin("Hyperlink", "Hyperlink", "Hyperlink", "하이퍼링크", C, 99, false),
                new WordBuiltin("Strong", "Strong", "Strong", "굵게", C, 22, true),
                new WordBuiltin("Emphasis", "Emphasis", "Emphasis", "강조", C, 20, true),
                new WordBuiltin("SubtleEmphasis", "Subtle Emphasis", "Subtle Emphasis", "약한 강조", C, 19, true),
                new WordBuiltin("IntenseEmphasis", "Intense Emphasis", "Intense Emphasis", "강한 강조", C, 21, true),
                new WordBuiltin("PlaceholderText", "Placeholder Text", "Placeholder Text", "자리 표시자 텍스트", C, 99, false),
                new WordBuiltin("TableGrid", "Table Grid", "Table Grid", "표 구분선", StyleValues.Table, 39, false),
            });

            for (var level = 1; level <= 9; level++)
            {
                var n = level.ToString(CultureInfo.InvariantCulture);
                list.Add(new WordBuiltin("TOC" + n, "toc " + n, "TOC " + n, "목차 " + n, P, 39, false));
            }

            for (var level = 1; level <= 3; level++)
            {
                var n = level.ToString(CultureInfo.InvariantCulture);
                list.Add(new WordBuiltin("Index" + n, "index " + n, "Index " + n, "색인 " + n, P, 99, false));
            }

            return list;
        }
    }
}
