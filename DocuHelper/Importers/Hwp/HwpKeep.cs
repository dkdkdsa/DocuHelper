using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.CustomXmlDataProperties;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Importers.Hwp
{
    internal static class HwpKeep
    {
        internal const string Prefix = "_hwpKeep_";
        internal const int BookmarkBase = 500000;

        private const string Namespace = "urn:docuhelper:hwp-keep";
        private const string ItemId = "{6F3B2C1A-9D4E-4A7B-8C5F-2E1D0B9A7C43}";
        private static readonly XNamespace Ns = Namespace;

        internal static void Write(WordprocessingDocument word, IReadOnlyDictionary<string, HwpKeptObject> kept)
        {
            var main = word.MainDocumentPart;
            foreach (var part in main.CustomXmlParts.Where(IsKeep).ToList())
                main.DeletePart(part);

            if (kept == null || kept.Count == 0)
                return;

            var custom = main.AddCustomXmlPart(CustomXmlPartType.CustomXml);
            var root = new XElement(Ns + "keep", kept.Select(pair => new XElement(Ns + "object",
                new XAttribute("id", pair.Key),
                new XElement(Ns + "text", pair.Value.Text ?? string.Empty),
                new XElement(Ns + "xml", pair.Value.Xml))));
            using (var stream = custom.GetStream(FileMode.Create, FileAccess.Write))
                new XDocument(root).Save(stream);

            var properties = custom.AddNewPart<CustomXmlPropertiesPart>();
            properties.DataStoreItem = new DataStoreItem(
                new SchemaReferences(new SchemaReference { Uri = Namespace }))
            {
                ItemId = ItemId,
            };
        }

        internal static Dictionary<string, HwpKeptObject> Read(WordprocessingDocument word)
        {
            var result = new Dictionary<string, HwpKeptObject>(StringComparer.Ordinal);
            foreach (var part in word.MainDocumentPart.CustomXmlParts.Where(IsKeep))
            {
                using var stream = part.GetStream();
                foreach (var item in XDocument.Load(stream).Root.Elements(Ns + "object"))
                {
                    var id = item.Attribute("id")?.Value;
                    var xml = item.Element(Ns + "xml")?.Value;
                    if (id != null && xml != null)
                        result[id] = new HwpKeptObject { Xml = xml, Text = item.Element(Ns + "text")?.Value ?? string.Empty };
                }
            }

            return result;
        }

        internal static string TextOf(IEnumerable<OpenXmlElement> runs) =>
            string.Concat(runs.SelectMany(run => run.Descendants()).Select(node => node switch
            {
                DocumentFormat.OpenXml.Wordprocessing.Text text => text.Text,
                Break => "\n",
                CarriageReturn => "\n",
                TabChar => "\t",
                _ => string.Empty,
            }));

        private static bool IsKeep(CustomXmlPart part)
        {
            using var stream = part.GetStream();
            try
            {
                return XDocument.Load(stream).Root?.Name.NamespaceName == Namespace;
            }
            catch (System.Xml.XmlException)
            {
                return false;
            }
        }
    }
}
