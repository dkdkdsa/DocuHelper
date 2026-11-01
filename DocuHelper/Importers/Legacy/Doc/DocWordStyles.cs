using System.Linq;
using DocuHelper.Tools.Word;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal static class DocWordStyles
    {
        private const string NormalId = "Normal";
        private const int DefaultSize = 20;

        public static void Write(WordprocessingDocument word, DocDocument doc, DocStyleMap map)
        {
            var root = word.MainDocumentPart?.StyleDefinitionsPart?.Styles;
            if (root == null)
                return;

            ResetDefaults(root);
            var normal = doc.Styles.All.FirstOrDefault(s => s.Kind == 1 && s.Sti == 0);
            ApplyNormal(root, doc, normal);

            var helper = new WordStyles(word);
            for (var i = 0; i < map.Used.Count; i++)
            {
                var style = doc.Styles.Find(map.Used[i]);
                var character = style.Kind == 2;
                var id = map.IdOf(style.Index, character);
                if (root.Elements<Style>().Any(s => s.StyleId?.Value == id))
                    continue;

                if (map.IsBuiltin(id))
                    WordBuiltinStyles.Ensure(helper, id);
                else
                    root.AppendChild(Custom(doc, map, style, id, character));
            }
        }

        private static void ResetDefaults(Styles root)
        {
            var spacing = root.DocDefaults?.ParagraphPropertiesDefault?.ParagraphPropertiesBaseStyle?.GetFirstChild<SpacingBetweenLines>();
            if (spacing == null)
                return;

            spacing.After = "0";
            spacing.Line = "240";
            spacing.LineRule = LineSpacingRuleValues.Auto;
        }

        private static void ApplyNormal(Styles root, DocDocument doc, DocStyle source)
        {
            var normal = root.Elements<Style>().FirstOrDefault(s => s.StyleId?.Value == NormalId);
            if (normal == null)
                return;

            var paragraph = normal.StyleParagraphProperties ??= new StyleParagraphProperties();
            paragraph.RemoveAllChildren<Justification>();
            var pap = source == null ? new DocPap() : doc.Styles.ResolveParagraph(source.Index);
            var subset = new DocPap
            {
                Left = pap.Left,
                Right = pap.Right,
                First = pap.First,
                Before = pap.Before,
                After = pap.After,
                BeforeAuto = pap.BeforeAuto,
                AfterAuto = pap.AfterAuto,
                Line = pap.Line,
                LineMultiple = pap.LineMultiple,
                Justification = pap.Justification ?? 0,
            };

            DocWordParagraphs.Fill(paragraph, subset, null, 0);

            var chp = source == null ? new DocChp() : doc.Styles.ResolveCharacters(source.Index).Clone();
            chp.FontAscii ??= Valid(doc.Styles.DefaultAscii);
            chp.FontEastAsia ??= Valid(doc.Styles.DefaultEastAsia);
            chp.FontOther ??= Valid(doc.Styles.DefaultOther);
            chp.Size ??= DefaultSize;
            chp.StyleId = null;
            var run = normal.StyleRunProperties ??= new StyleRunProperties();
            run.RemoveAllChildren();
            DocWordRuns.Fill(run, chp, doc.Fonts);
        }

        private static int? Valid(int index) => index >= 0 && index != 0xFFFF ? index : null;

        private static Style Custom(DocDocument doc, DocStyleMap map, DocStyle source, string id, bool character)
        {
            var style = new Style
            {
                Type = character ? StyleValues.Character : StyleValues.Paragraph,
                StyleId = id,
                CustomStyle = true,
            };

            style.AppendChild(new StyleName { Val = string.IsNullOrWhiteSpace(source.Name) ? id : source.Name });
            var parent = source.Base != DocStyle.NoBase && source.Base != source.Index ? map.IdOf(source.Base, character) : null;
            if (parent == null && !character)
                parent = NormalId;

            if (parent != null)
                style.AppendChild(new BasedOn { Val = parent });

            style.AppendChild(new UIPriority { Val = 99 });
            if (character)
            {
                var run = new StyleRunProperties();
                DocWordRuns.Fill(run, WithoutStyle(source.Chp), doc.Fonts);
                if (run.HasChildren)
                    style.AppendChild(run);

                return style;
            }

            var paragraph = new StyleParagraphProperties();
            DocWordParagraphs.Fill(paragraph, source.Pap, null, 0);
            if (paragraph.HasChildren)
                style.AppendChild(paragraph);

            var characters = new StyleRunProperties();
            DocWordRuns.Fill(characters, WithoutStyle(source.Chp), doc.Fonts);
            if (characters.HasChildren)
                style.AppendChild(characters);

            return style;
        }

        private static DocChp WithoutStyle(DocChp chp)
        {
            var copy = chp.Clone();
            copy.StyleId = null;
            return copy;
        }
    }
}
