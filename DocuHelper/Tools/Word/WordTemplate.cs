using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordTemplate
    {
        private const string Font = "맑은 고딕";
        private const string NormalId = "Normal";

        public static byte[] Blank()
        {
            using var stream = new MemoryStream();
            using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
            {
                var main = document.AddMainDocumentPart();
                main.Document = NewDocument();

                var styles = main.AddNewPart<StyleDefinitionsPart>();
                styles.Styles = NewStyles();

                var numbering = main.AddNewPart<NumberingDefinitionsPart>();
                numbering.Numbering = new Numbering(
                    WordNumbering.NewAbstract(0, WordNumbering.BulletLevels()),
                    WordNumbering.NewAbstract(1, WordNumbering.NumberLevels()),
                    WordNumbering.NewInstance(1, 0, false),
                    WordNumbering.NewInstance(2, 1, false));

                var settings = main.AddNewPart<DocumentSettingsPart>();
                settings.Settings = new Settings(
                    new Zoom { Percent = "100" },
                    new DefaultTabStop { Val = 800 },
                    new CharacterSpacingControl { Val = CharacterSpacingValues.DoNotCompress },
                    new Compatibility(new CompatibilitySetting
                    {
                        Name = CompatSettingNameValues.CompatibilityMode,
                        Uri = "http://schemas.microsoft.com/office/word",
                        Val = "15",
                    }));
            }

            return stream.ToArray();
        }

        private static Document NewDocument()
        {
            var document = new Document();
            document.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
            document.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
            document.AddNamespaceDeclaration("w14", "http://schemas.microsoft.com/office/word/2010/wordml");
            document.MCAttributes = new MarkupCompatibilityAttributes { Ignorable = "w14" };

            var body = new Body(
                new Paragraph { ParagraphId = "00000001" },
                new SectionProperties(
                    new PageSize { Width = 11906U, Height = 16838U },
                    new PageMargin { Top = 1701, Right = 1440U, Bottom = 1440, Left = 1440U, Header = 851U, Footer = 992U, Gutter = 0U },
                    new Columns { Space = "425" },
                    new DocGrid { LinePitch = 360 }));
            document.AppendChild(body);
            return document;
        }

        private static Styles NewStyles()
        {
            var defaults = new DocDefaults(
                new RunPropertiesDefault(new RunPropertiesBaseStyle(
                    new RunFonts { Ascii = Font, HighAnsi = Font, EastAsia = Font, ComplexScript = Font },
                    new Kern { Val = 2U },
                    new FontSize { Val = "20" },
                    new FontSizeComplexScript { Val = "22" },
                    new Languages { Val = "en-US", EastAsia = "ko-KR", Bidi = "ar-SA" })),
                new ParagraphPropertiesDefault(new ParagraphPropertiesBaseStyle(
                    new SpacingBetweenLines { After = "160", Line = "259", LineRule = LineSpacingRuleValues.Auto })));

            var normal = new Style(
                new StyleName { Val = "Normal" },
                new PrimaryStyle(),
                new StyleParagraphProperties(
                    new WidowControl { Val = false },
                    new WordWrap { Val = false },
                    new AutoSpaceDE { Val = false },
                    new AutoSpaceDN { Val = false },
                    new Justification { Val = JustificationValues.Both }))
            {
                Type = StyleValues.Paragraph,
                StyleId = NormalId,
                Default = true,
            };

            var paragraphFont = new Style(
                new StyleName { Val = "Default Paragraph Font" },
                new UIPriority { Val = 1 },
                new SemiHidden(),
                new UnhideWhenUsed())
            {
                Type = StyleValues.Character,
                StyleId = "DefaultParagraphFont",
                Default = true,
            };

            var normalTable = new Style(
                new StyleName { Val = "Normal Table" },
                new UIPriority { Val = 99 },
                new SemiHidden(),
                new UnhideWhenUsed(),
                new StyleTableProperties(
                    new TableIndentation { Width = 0, Type = TableWidthUnitValues.Dxa },
                    new TableCellMarginDefault(
                        new TopMargin { Width = "0", Type = TableWidthUnitValues.Dxa },
                        new TableCellLeftMargin { Width = 108, Type = TableWidthValues.Dxa },
                        new BottomMargin { Width = "0", Type = TableWidthUnitValues.Dxa },
                        new TableCellRightMargin { Width = 108, Type = TableWidthValues.Dxa })))
            {
                Type = StyleValues.Table,
                StyleId = "TableNormal",
                Default = true,
            };

            var noList = new Style(
                new StyleName { Val = "No List" },
                new UIPriority { Val = 99 },
                new SemiHidden(),
                new UnhideWhenUsed())
            {
                Type = StyleValues.Numbering,
                StyleId = "NoList",
                Default = true,
            };

            return new Styles(
                defaults,
                normal,
                paragraphFont,
                normalTable,
                noList,
                WordStyles.Define(WordKind.Title, "Title", NormalId),
                WordStyles.Define(WordKind.Heading1, "Heading1", NormalId),
                WordStyles.Define(WordKind.Heading2, "Heading2", NormalId));
        }
    }
}
