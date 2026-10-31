using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordStyleWriter
    {
        public const int MaxNameLength = 80;
        public const int MaxChain = 16;

        public static Style Find(WordScan scan, string text)
        {
            var key = (text ?? string.Empty).Trim();
            if (key.Length == 0)
                throw new ToolInputException("스타일 이름이 비어 있어요.");

            var found = scan.Styles.Find(key) ?? scan.Styles.FindByName(key);
            if (found != null)
                return found;

            var builtin = WordBuiltinStyles.Find(key);
            if (builtin == null)
                return null;

            var byId = scan.Styles.Find(builtin.Id);
            return (byId != null && WordStyles.Normalize(byId.StyleName?.Val?.Value) == WordStyles.Normalize(builtin.Name) ? byId : null)
                ?? scan.Styles.FindByName(builtin.Name)
                ?? scan.Styles.FindByName(builtin.Korean);
        }

        public static bool IsBuiltin(Style style)
        {
            if (style.CustomStyle?.Value == true)
                return false;

            if (style.Default?.Value == true)
                return true;

            return WordBuiltinStyles.Find(style.StyleName?.Val?.Value) != null || WordBuiltinStyles.Find(style.StyleId?.Value) != null;
        }

        public static Style Define(WordScan scan, WordStyleRequest request, int defaultTab)
        {
            var name = CheckName(request.Name);
            var existing = Find(scan, name);
            if (existing != null)
                throw new ToolInputException($"‘{name}’ 스타일이 이미 있어요. 모양을 바꾸려면 action을 modify로 해 주세요.");

            var builtin = WordBuiltinStyles.Find(name);
            var character = request.Character == true;
            if (builtin != null)
            {
                if ((builtin.Type == StyleValues.Character) != character)
                    throw new ToolInputException($"‘{name}’은(는) {(builtin.Type == StyleValues.Character ? "글자" : "문단")} 스타일이에요. type을 맞춰 주세요.");

                var created = scan.Styles.Find(WordBuiltinStyles.Ensure(scan.Styles, name));
                Modify(scan, created, request, defaultTab);
                return created;
            }

            if (character && request.Paragraph != null && request.Paragraph.HasAny)
                throw new ToolInputException("글자 스타일에는 문단 속성(paragraph)을 쓸 수 없어요. character만 넣어 주세요.");

            if (character && request.Next != null)
                throw new ToolInputException("글자 스타일에는 next_style을 쓸 수 없어요.");

            var basedOn = request.BasedOn != null ? Related(scan, request.BasedOn, character, "based_on") : character ? DefaultCharacter(scan) : scan.Styles.NormalId;
            var next = request.Next != null ? Related(scan, request.Next, false, "next_style") : null;
            if (basedOn != null && scan.Styles.Chain(basedOn).Count() >= MaxChain - 1)
                throw new ToolInputException("스타일 상속이 너무 깊어져요. based_on을 더 위쪽 스타일로 정해 주세요.");

            var style = new Style { Type = character ? StyleValues.Character : StyleValues.Paragraph, CustomStyle = true };
            style.AppendChild(new StyleName { Val = name });
            if (basedOn != null)
                style.AppendChild(new BasedOn { Val = basedOn });

            if (next != null)
                style.AppendChild(new NextParagraphStyle { Val = next });

            style.AppendChild(new PrimaryStyle());
            style.StyleId = scan.Styles.NewId(IdOf(name, character));

            scan.Styles.Add(style);
            Fill(scan, style, request, basedOn, defaultTab);
            return style;
        }

        public static void Modify(WordScan scan, Style style, WordStyleRequest request, int defaultTab)
        {
            var character = style.Type?.Value == StyleValues.Character;
            if (style.Type?.Value != StyleValues.Paragraph && !character)
                throw new ToolInputException("문단 스타일과 글자 스타일만 고칠 수 있어요.");

            if (request.Character != null && request.Character.Value != character)
                throw new ToolInputException($"‘{style.StyleName?.Val?.Value}’은(는) {(character ? "글자" : "문단")} 스타일이라 종류를 바꿀 수 없어요.");

            if (character && request.Paragraph != null && request.Paragraph.HasAny)
                throw new ToolInputException("글자 스타일에는 문단 속성(paragraph)을 쓸 수 없어요. character만 넣어 주세요.");

            if (character && request.Next != null)
                throw new ToolInputException("글자 스타일에는 next_style을 쓸 수 없어요.");

            if (character && request.CopyFrom != null)
                throw new ToolInputException("글자 스타일은 copy_from으로 고칠 수 없어요. character로 속성을 넣어 주세요.");

            var basedOn = style.BasedOn?.Val?.Value;
            if (request.BasedOn != null)
            {
                var candidate = Related(scan, request.BasedOn, character, "based_on");
                CheckChain(scan, style, candidate);
                basedOn = candidate;
            }

            var next = request.Next != null ? Related(scan, request.Next, false, "next_style") : null;

            if (request.BasedOn != null)
                style.BasedOn = new BasedOn { Val = basedOn };

            if (next != null)
                style.NextParagraphStyle = new NextParagraphStyle { Val = next };

            Fill(scan, style, request, basedOn, defaultTab);
        }

        public static IReadOnlyList<Paragraph> Delete(WordScan scan, Style style, out int repointed)
        {
            var id = style.StyleId.Value;
            var character = style.Type?.Value == StyleValues.Character;
            if (style.Type?.Value != StyleValues.Paragraph && !character)
                throw new ToolInputException($"‘{style.StyleName?.Val?.Value}’은(는) 문단 스타일이나 글자 스타일이 아니라서 지울 수 없어요.");

            if (IsBuiltin(style))
                throw new ToolInputException($"‘{style.StyleName?.Val?.Value}’은(는) 워드에 기본으로 들어 있는 스타일이라 지울 수 없어요. 모양을 바꾸려면 action을 modify로 해 주세요.");

            var numbering = scan.Numbering.Root;
            if (numbering != null && numbering.Descendants<ParagraphStyleIdInLevel>().Any(p => p.Val?.Value == id))
                throw new ToolInputException($"‘{style.StyleName?.Val?.Value}’은(는) 번호 목록의 수준에 연결돼 있어서 지울 수 없어요.");

            var fallback = style.BasedOn?.Val?.Value;
            if (fallback != null && scan.Styles.Find(fallback) == null)
                fallback = null;

            if (character && fallback == DefaultCharacter(scan))
                fallback = null;

            if (!character && fallback == scan.Styles.NormalId)
                fallback = null;

            var users = new List<Paragraph>();
            repointed = 0;
            foreach (var root in Roots(scan.Document))
            {
                if (character)
                {
                    foreach (var reference in root.Descendants<RunStyle>().Where(r => r.Val?.Value == id).ToList())
                    {
                        if (reference.Ancestors<Paragraph>().FirstOrDefault() is { } owner && !users.Contains(owner))
                            users.Add(owner);

                        Repoint(reference, fallback);
                        repointed++;
                    }
                }
                else
                {
                    foreach (var reference in root.Descendants<ParagraphStyleId>().Where(r => r.Val?.Value == id).ToList())
                    {
                        if (reference.Ancestors<Paragraph>().FirstOrDefault() is { } owner && !users.Contains(owner))
                            users.Add(owner);

                        Repoint(reference, fallback);
                        repointed++;
                    }
                }
            }

            foreach (var other in scan.Styles.Root.Elements<Style>())
            {
                if (other.BasedOn?.Val?.Value == id)
                {
                    if (fallback != null)
                        other.BasedOn.Val = fallback;
                    else
                        other.BasedOn = null;
                }

                if (other.NextParagraphStyle?.Val?.Value == id)
                    other.NextParagraphStyle = null;

                if (other.LinkedStyle?.Val?.Value == id)
                    other.LinkedStyle = null;
            }

            scan.Styles.Remove(style);
            return users;
        }

        public static string Display(Style style) => style.StyleName?.Val?.Value ?? style.StyleId?.Value;

        public static IReadOnlyList<WordBlock> Users(WordScan scan, Style style, int limit)
        {
            var character = style.Type?.Value == StyleValues.Character;
            var found = new List<WordBlock>();
            foreach (var block in scan.Blocks)
            {
                var uses = block.IsTable
                    ? block.Table.Descendants<Paragraph>().Any(p => Uses(scan, p, style, character))
                    : Uses(scan, block.Paragraph, style, character);
                if (!uses)
                    continue;

                found.Add(block);
                if (found.Count >= limit)
                    break;
            }

            return found;
        }

        private static bool Uses(WordScan scan, Paragraph paragraph, Style style, bool character)
        {
            if (!character)
                return scan.Styles.ParagraphChain(paragraph).Contains(style);

            if (scan.Styles.ParagraphChain(paragraph).Any(s => ReferenceEquals(s, style)))
                return false;

            return WordText.Runs(paragraph).Any(r => scan.Styles.Chain(r.RunProperties?.RunStyle?.Val?.Value).Contains(style));
        }

        private static void Fill(WordScan scan, Style style, WordStyleRequest request, string basedOn, int defaultTab)
        {
            if (request.Paragraph != null && request.Paragraph.HasAny)
            {
                var properties = style.StyleParagraphProperties ?? new StyleParagraphProperties();
                WordParagraphFormat.Apply(properties, request.Paragraph, WordInheritedParagraph.ForStyle(scan, basedOn), false, defaultTab);
                Attach(style, properties);
            }

            if (request.CopyFrom != null)
                Copy(style, request.CopyFrom);

            if (request.Run != null && request.Run.HasAny)
            {
                var properties = style.StyleRunProperties ?? new StyleRunProperties();
                WordCharacterFormat.Apply(properties, request.Run, WordInheritedRun.ForStyle(scan, basedOn));
                Attach(style, properties);
            }
        }

        private static void Copy(Style style, Paragraph source)
        {
            var direct = source.ParagraphProperties;
            if (direct != null)
            {
                var properties = style.StyleParagraphProperties ?? new StyleParagraphProperties();
                foreach (var child in direct.ChildElements.ToList())
                {
                    if (child is ParagraphStyleId || child is NumberingProperties || child is SectionProperties || child is ParagraphMarkRunProperties || child is ParagraphPropertiesChange || child is FrameProperties || child is Tabs)
                        continue;

                    var clone = child.CloneNode(true);
                    foreach (var old in properties.ChildElements.Where(e => e.GetType() == clone.GetType()).ToList())
                        old.Remove();

                    properties.AddChild(clone);
                }

                Attach(style, properties);
            }

            var run = WordText.TemplateProperties(source);
            if (run != null)
            {
                var properties = style.StyleRunProperties ?? new StyleRunProperties();
                WordCharacterFormat.Merge(properties, run);
                Attach(style, properties);
            }
        }

        private static void Attach(Style style, StyleParagraphProperties properties)
        {
            if (properties.Parent != null)
            {
                if (!properties.HasChildren)
                    properties.Remove();

                return;
            }

            if (properties.HasChildren)
                style.StyleParagraphProperties = properties;
        }

        private static void Attach(Style style, StyleRunProperties properties)
        {
            if (properties.Parent != null)
            {
                if (!properties.HasChildren)
                    properties.Remove();

                return;
            }

            if (properties.HasChildren)
                style.StyleRunProperties = properties;
        }

        private static string Related(WordScan scan, string text, bool character, string label)
        {
            var existing = Find(scan, text);
            if (existing == null)
            {
                var builtin = WordBuiltinStyles.Find(text);
                if (builtin != null && (builtin.Type == StyleValues.Character) != character)
                    throw new ToolInputException($"'{label}'의 ‘{text}’은(는) {(character ? "문단" : "글자")} 스타일이라 쓸 수 없어요.");

                existing = scan.Styles.Find(WordBuiltinStyles.Ensure(scan.Styles, text));
            }

            var isCharacter = existing.Type?.Value == StyleValues.Character;
            if (existing.Type?.Value != StyleValues.Paragraph && !isCharacter)
                throw new ToolInputException($"'{label}'의 ‘{text}’은(는) 문단·글자 스타일이 아니에요.");

            if (isCharacter != character)
                throw new ToolInputException($"'{label}'의 ‘{text}’은(는) {(isCharacter ? "글자" : "문단")} 스타일이라 쓸 수 없어요. {(character ? "글자" : "문단")} 스타일을 넣어 주세요.");

            return existing.StyleId.Value;
        }

        private static void CheckChain(WordScan scan, Style style, string candidate)
        {
            var depth = 0;
            foreach (var parent in scan.Styles.Chain(candidate))
            {
                depth++;
                if (ReferenceEquals(parent, style))
                    throw new ToolInputException($"‘{Display(style)}’이(가) 자기 자신에게서 이어받게 돼요. based_on을 다른 스타일로 정해 주세요.");
            }

            var below = Descendants(scan, style);
            if (depth + below >= MaxChain)
                throw new ToolInputException("스타일 상속이 너무 깊어져요. based_on을 더 위쪽 스타일로 정해 주세요.");
        }

        private static int Descendants(WordScan scan, Style style)
        {
            var deepest = 0;
            var queue = new Queue<(Style Style, int Depth)>();
            queue.Enqueue((style, 0));
            var seen = new HashSet<Style> { style };
            while (queue.Count > 0)
            {
                var (current, depth) = queue.Dequeue();
                deepest = Math.Max(deepest, depth);
                foreach (var child in scan.Styles.Root.Elements<Style>().Where(s => s.BasedOn?.Val?.Value == current.StyleId?.Value))
                {
                    if (seen.Add(child))
                        queue.Enqueue((child, depth + 1));
                }
            }

            return deepest;
        }

        private static void Repoint(ParagraphStyleId reference, string fallback)
        {
            if (fallback != null)
            {
                reference.Val = fallback;
                return;
            }

            var properties = reference.Parent;
            reference.Remove();
            if (properties is ParagraphProperties pPr && !pPr.HasChildren && pPr.Parent is Paragraph paragraph)
                paragraph.ParagraphProperties = null;
        }

        private static void Repoint(RunStyle reference, string fallback)
        {
            if (fallback != null)
            {
                reference.Val = fallback;
                return;
            }

            var properties = reference.Parent;
            reference.Remove();
            if (properties is RunProperties rPr && !rPr.HasChildren && rPr.Parent is Run run)
                run.RunProperties = null;
        }

        private static string DefaultCharacter(WordScan scan) => scan.Styles.Find("DefaultParagraphFont") != null ? "DefaultParagraphFont" : null;

        private static string CheckName(string name)
        {
            var text = (name ?? string.Empty).Trim();
            if (text.Length == 0)
                throw new ToolInputException("스타일 이름이 비어 있어요.");

            if (text.Length > MaxNameLength || text.Any(char.IsControl))
                throw new ToolInputException($"스타일 이름은 글자 {MaxNameLength}자 이하의 한 줄이어야 해요.");

            return text;
        }

        private static string IdOf(string name, bool character)
        {
            var letters = new string(name.Where(c => c < 128 && char.IsLetterOrDigit(c)).ToArray());
            if (letters.Length == 0 || char.IsDigit(letters[0]))
                letters = (character ? "Char" : "Style") + letters;

            return letters;
        }

        private static IEnumerable<OpenXmlElement> Roots(WordprocessingDocument document)
        {
            var main = document.MainDocumentPart;
            yield return main.Document;
            foreach (var part in main.HeaderParts)
            {
                if (part.Header != null)
                    yield return part.Header;
            }

            foreach (var part in main.FooterParts)
            {
                if (part.Footer != null)
                    yield return part.Footer;
            }

            if (main.FootnotesPart?.Footnotes != null)
                yield return main.FootnotesPart.Footnotes;

            if (main.EndnotesPart?.Endnotes != null)
                yield return main.EndnotesPart.Endnotes;

            if (main.WordprocessingCommentsPart?.Comments != null)
                yield return main.WordprocessingCommentsPart.Comments;
        }
    }
}
