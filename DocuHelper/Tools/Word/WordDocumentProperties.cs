using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Ep = DocumentFormat.OpenXml.ExtendedProperties;

namespace DocuHelper.Tools.Word
{
    public static class WordDocumentProperties
    {
        public const int MaxLength = 1000;

        public static readonly IReadOnlyList<string> Names = new[] { "title", "subject", "author", "keywords", "comments", "category", "company" };

        private static readonly string[] s_order =
        {
            "writeProtection", "view", "zoom", "removePersonalInformation", "removeDateAndTime", "doNotDisplayPageBoundaries",
            "displayBackgroundShape", "printPostScriptOverText", "printFractionalCharacterWidth", "printFormsData", "embedTrueTypeFonts",
            "embedSystemFonts", "saveSubsetFonts", "saveFormsData", "mirrorMargins", "alignBordersAndEdges", "bordersDoNotSurroundHeader",
            "bordersDoNotSurroundFooter", "gutterAtTop", "hideSpellingErrors", "hideGrammaticalErrors", "activeWritingStyle", "proofState",
            "formsDesign", "attachedTemplate", "linkStyles", "stylePaneFormatFilter", "stylePaneSortMethod", "documentType", "mailMerge",
            "revisionView", "trackRevisions", "doNotTrackMoves", "doNotTrackFormatting", "documentProtection", "autoFormatOverride",
            "styleLockTheme", "styleLockQFSet", "defaultTabStop", "autoHyphenation", "consecutiveHyphenLimit", "hyphenationZone",
            "doNotHyphenateCaps", "showEnvelope", "summaryLength", "clickAndTypeStyle", "defaultTableStyle", "evenAndOddHeaders",
            "bookFoldRevPrinting", "bookFoldPrinting", "bookFoldPrintingSheets", "drawingGridHorizontalSpacing", "drawingGridVerticalSpacing",
            "displayHorizontalDrawingGridEvery", "displayVerticalDrawingGridEvery", "doNotUseMarginsForDrawingGridOrigin",
            "drawingGridHorizontalOrigin", "drawingGridVerticalOrigin", "doNotShadeFormData", "noPunctuationKerning", "characterSpacingControl",
            "printTwoOnOne", "strictFirstAndLastChars", "noLineBreaksAfter", "noLineBreaksBefore", "savePreviewPicture",
            "doNotValidateAgainstSchema", "saveInvalidXml", "ignoreMixedContent", "alwaysShowPlaceholderText", "doNotDemarcateInvalidXml",
            "saveXmlDataOnly", "useXSLTWhenSaving", "saveThroughXslt", "showXMLTags", "alwaysMergeEmptyNamespace", "updateFields",
            "hdrShapeDefaults", "footnotePr", "endnotePr", "compat", "docVars", "rsids", "mathPr", "attachedSchema", "themeFontLang",
            "clrSchemeMapping", "doNotIncludeSubdocsInStats", "doNotAutoCompressPictures", "forceUpgrade", "captions", "readModeInkLockDown",
            "smartTagType", "schemaLibrary", "shapeDefaults", "doNotEmbedSmartTags", "decimalSymbol", "listSeparator",
        };

        private const string CorePart = "docProps/core.xml";
        private const string CoreContentType = "application/vnd.openxmlformats-package.core-properties+xml";
        private const string CoreRelationship = "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties";

        private static readonly XNamespace s_cp = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
        private static readonly XNamespace s_dc = "http://purl.org/dc/elements/1.1/";
        private static readonly XNamespace s_terms = "http://purl.org/dc/terms/";
        private static readonly XNamespace s_xsi = "http://www.w3.org/2001/XMLSchema-instance";

        private static readonly Regex s_language = new("^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8}){0,3}$", RegexOptions.Compiled);

        public static string Clean(string name, string value)
        {
            var text = WordText.Clean(value).Replace('\n', ' ').Trim();
            if (text.Length > MaxLength)
                throw new ToolInputException($"문서 정보 ‘{name}’은 {MaxLength}자까지 쓸 수 있어요. 지금은 {text.Length}자예요.");

            return text;
        }

        public static string CleanLanguage(string value)
        {
            var text = (value ?? string.Empty).Trim();
            if (!s_language.IsMatch(text))
                throw new ToolInputException($"‘{text}’은 언어 코드로 쓸 수 없어요. ko-KR, en-US처럼 넣어 주세요.");

            return text;
        }

        public static string Get(WordDocu docu, string name)
        {
            var core = docu.Document.PackageProperties;
            return name switch
            {
                "title" => core.Title,
                "subject" => core.Subject,
                "author" => core.Creator,
                "keywords" => core.Keywords,
                "comments" => core.Description,
                "category" => core.Category,
                "company" => docu.Document.ExtendedFilePropertiesPart?.Properties?.Company?.Text,
                _ => null,
            };
        }

        public static void Set(WordDocu docu, string name, string value)
        {
            var text = value.Length == 0 ? null : value;
            var core = docu.Document.PackageProperties;
            switch (name)
            {
                case "title":
                    core.Title = text;
                    break;
                case "subject":
                    core.Subject = text;
                    break;
                case "author":
                    core.Creator = text;
                    break;
                case "keywords":
                    core.Keywords = text;
                    break;
                case "comments":
                    core.Description = text;
                    break;
                case "category":
                    core.Category = text;
                    break;
                case "company":
                {
                    var part = docu.Document.ExtendedFilePropertiesPart ?? docu.Document.AddExtendedFilePropertiesPart();
                    part.Properties ??= new Ep.Properties();
                    part.Properties.Company = text == null ? null : new Ep.Company(text);
                    break;
                }
            }
        }

        public static byte[] WithCoreProperties(byte[] bytes, WordprocessingDocument document)
        {
            var core = document?.PackageProperties;
            if (core == null || bytes == null)
                return bytes;

            var values = new (string Tag, string Value)[]
            {
                ("dc:title", core.Title),
                ("dc:subject", core.Subject),
                ("dc:creator", core.Creator),
                ("cp:keywords", core.Keywords),
                ("dc:description", core.Description),
                ("cp:category", core.Category),
            }.Where(v => !string.IsNullOrEmpty(v.Value)).ToList();
            if (values.Count == 0)
                return bytes;

            using var stream = new MemoryStream();
            stream.Write(bytes, 0, bytes.Length);
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Update, true))
            {
                if (zip.GetEntry(CorePart) != null)
                    return bytes;

                var now = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
                var xml = new XDocument(
                    new XElement(
                        s_cp + "coreProperties",
                        new XAttribute(XNamespace.Xmlns + "cp", s_cp.NamespaceName),
                        new XAttribute(XNamespace.Xmlns + "dc", s_dc.NamespaceName),
                        new XAttribute(XNamespace.Xmlns + "dcterms", s_terms.NamespaceName),
                        new XAttribute(XNamespace.Xmlns + "xsi", s_xsi.NamespaceName),
                        values.Select(v => new XElement((v.Tag.StartsWith("dc:", StringComparison.Ordinal) ? s_dc : s_cp) + v.Tag.Substring(v.Tag.IndexOf(':') + 1), v.Value)),
                        new XElement(s_terms + "created", new XAttribute(s_xsi + "type", "dcterms:W3CDTF"), now),
                        new XElement(s_terms + "modified", new XAttribute(s_xsi + "type", "dcterms:W3CDTF"), now)));
                Write(zip, CorePart, xml);

                Edit(zip, "[Content_Types].xml", types =>
                {
                    var ns = types.Root.Name.Namespace;
                    types.Root.Add(new XElement(ns + "Override", new XAttribute("PartName", "/" + CorePart), new XAttribute("ContentType", CoreContentType)));
                });
                Edit(zip, "_rels/.rels", rels =>
                {
                    var ns = rels.Root.Name.Namespace;
                    rels.Root.Add(new XElement(ns + "Relationship", new XAttribute("Id", "rIdCore1"), new XAttribute("Type", CoreRelationship), new XAttribute("Target", CorePart)));
                });
            }

            return stream.ToArray();
        }

        public static void Protect(WordDocu docu, WordProtectionMode mode, bool enforce)
        {
            var settings = WordParts.EnsureSettings(docu);
            settings.RemoveAllChildren<DocumentProtection>();
            if (mode == WordProtectionMode.None)
                return;

            var edit = mode switch
            {
                WordProtectionMode.ReadOnly => DocumentProtectionValues.ReadOnly,
                WordProtectionMode.CommentsOnly => DocumentProtectionValues.Comments,
                _ => DocumentProtectionValues.TrackedChanges,
            };

            Place(settings, new DocumentProtection { Edit = edit, Enforcement = enforce });
            if (mode == WordProtectionMode.TrackedChangesOnly && enforce && !WordRevisions.Tracking(docu))
            {
                settings.RemoveAllChildren<TrackRevisions>();
                Place(settings, new TrackRevisions());
            }
        }

        public static void SetLanguage(WordDocu docu, string language)
        {
            var eastAsian = language.StartsWith("ko", StringComparison.OrdinalIgnoreCase)
                || language.StartsWith("ja", StringComparison.OrdinalIgnoreCase)
                || language.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

            var settings = WordParts.EnsureSettings(docu);
            var theme = settings.GetFirstChild<ThemeFontLanguages>();
            var themeValue = (ThemeFontLanguages)(theme?.CloneNode(true) ?? new ThemeFontLanguages());
            themeValue.Val = language;
            if (eastAsian)
                themeValue.EastAsia = language;

            settings.RemoveAllChildren<ThemeFontLanguages>();
            Place(settings, themeValue);

            var styles = WordParts.EnsureStyles(docu).Styles;
            var defaults = styles.GetFirstChild<DocDefaults>();
            if (defaults == null)
            {
                defaults = new DocDefaults();
                styles.InsertAt(defaults, 0);
            }

            var runDefaults = defaults.GetFirstChild<RunPropertiesDefault>();
            if (runDefaults == null)
            {
                runDefaults = new RunPropertiesDefault();
                defaults.InsertAt(runDefaults, 0);
            }

            var properties = runDefaults.GetFirstChild<RunPropertiesBaseStyle>();
            if (properties == null)
            {
                properties = new RunPropertiesBaseStyle();
                runDefaults.AppendChild(properties);
            }

            var languages = properties.GetFirstChild<Languages>();
            if (languages == null)
            {
                languages = new Languages();
                var later = properties.ChildElements.FirstOrDefault(c => c.LocalName == "eastAsianLayout" || c.LocalName == "specVanish" || c.LocalName == "oMath");
                if (later != null)
                    later.InsertBeforeSelf(languages);
                else
                    properties.AppendChild(languages);
            }

            languages.Val = language;
            if (eastAsian)
                languages.EastAsia = language;
        }

        public static void SetTabStop(WordDocu docu, double centimeters)
        {
            var settings = WordParts.EnsureSettings(docu);
            settings.RemoveAllChildren<DefaultTabStop>();
            Place(settings, new DefaultTabStop { Val = (short)WordUnits.CmToTwips(centimeters) });
        }

        public static void SetUpdateFields(WordDocu docu, bool on)
        {
            var settings = WordParts.EnsureSettings(docu);
            settings.RemoveAllChildren<UpdateFieldsOnOpen>();
            if (on)
                Place(settings, new UpdateFieldsOnOpen { Val = true });
        }

        public static void Place(Settings settings, OpenXmlElement element)
        {
            var rank = Rank(element);
            var later = settings.ChildElements.FirstOrDefault(c => Rank(c) > rank);
            if (later != null)
                later.InsertBeforeSelf(element);
            else
                settings.AppendChild(element);
        }

        private static void Write(ZipArchive zip, string name, XDocument xml)
        {
            zip.GetEntry(name)?.Delete();
            var entry = zip.CreateEntry(name);
            using var output = entry.Open();
            using var writer = new StreamWriter(output, new UTF8Encoding(false));
            writer.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" + xml.ToString(SaveOptions.DisableFormatting));
        }

        private static void Edit(ZipArchive zip, string name, Action<XDocument> change)
        {
            var entry = zip.GetEntry(name);
            if (entry == null)
                return;

            XDocument xml;
            using (var input = entry.Open())
                xml = XDocument.Load(input);

            change(xml);
            Write(zip, name, xml);
        }

        private static int Rank(OpenXmlElement element)
        {
            var index = Array.IndexOf(s_order, element.LocalName);
            return index < 0 ? s_order.Length : index;
        }
    }
}
