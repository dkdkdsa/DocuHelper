using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using static DocuHelper.Importers.Hwp.HwpxNames;

namespace DocuHelper.Importers.Hwp
{
    internal static class HwpxPackageWriter
    {
        internal const string MimeType = "application/hwp+zip";

    private static readonly XmlWriterSettings s_xml = new()
    {
        Encoding = new UTF8Encoding(false),
        Indent = false,
        OmitXmlDeclaration = false,
    };

        internal static byte[] Pack(XElement header, IReadOnlyList<XElement> sections, IReadOnlyList<HwpxBinary> binaries)
        {
            using var output = new MemoryStream();
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
            {
                var mime = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
                using (var stream = mime.Open())
                {
                    var bytes = Encoding.ASCII.GetBytes(MimeType);
                    stream.Write(bytes, 0, bytes.Length);
                }

                Add(zip, "version.xml", Version());
                Add(zip, "Contents/header.xml", header);
                for (var i = 0; i < sections.Count; i++)
                    Add(zip, $"Contents/section{i}.xml", sections[i]);
                Add(zip, "settings.xml", Settings());
                foreach (var binary in binaries)
                {
                    var entry = zip.CreateEntry(binary.Path, CompressionLevel.Optimal);
                    using var stream = entry.Open();
                    stream.Write(binary.Bytes, 0, binary.Bytes.Length);
                }

                Add(zip, "META-INF/container.xml", Container());
                Add(zip, "META-INF/manifest.xml", new XElement(Odf + "manifest", new XAttribute(XNamespace.Xmlns + "odf", Odf)));
                Add(zip, "Contents/content.hpf", Package(binaries, sections.Count));
            }

            return output.ToArray();
        }

    private static void Add(ZipArchive zip, string path, XElement root)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, s_xml);
        new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root).Save(writer);
    }

    private static XElement Version() => new(Hv + "HCFVersion",
        new XAttribute(XNamespace.Xmlns + "hv", Hv),
        new XAttribute("tagetApplication", "WORDPROCESSOR"),
        new XAttribute("major", "5"),
        new XAttribute("minor", "1"),
        new XAttribute("micro", "0"),
        new XAttribute("buildNumber", "1"),
        new XAttribute("os", "1"),
        new XAttribute("xmlVersion", "1.4"),
        new XAttribute("application", "Hancom Office Hangul"),
        new XAttribute("appVersion", "11, 0, 0, 0"));

    private static XElement Settings() => new(Ha + "HWPApplicationSetting",
        new XAttribute(XNamespace.Xmlns + "ha", Ha),
        new XAttribute(XNamespace.Xmlns + "config", Config),
        new XElement(Ha + "CaretPosition", new XAttribute("listIDRef", "0"), new XAttribute("paraIDRef", "0"), new XAttribute("pos", "0")));

    private static XElement Container() => new(Ocf + "container",
        new XAttribute(XNamespace.Xmlns + "ocf", Ocf),
        new XAttribute(XNamespace.Xmlns + "hpf", Hpf),
        new XElement(Ocf + "rootfiles",
            new XElement(Ocf + "rootfile", new XAttribute("full-path", "Contents/content.hpf"), new XAttribute("media-type", "application/hwpml-package+xml"))));

    private static XElement Package(IReadOnlyList<HwpxBinary> binaries, int sectionCount) => new(Opf + "package",
        Namespaces(),
        new XAttribute("version", ""),
        new XAttribute("unique-identifier", ""),
        new XAttribute("id", ""),
        new XElement(Opf + "metadata",
            new XElement(Opf + "title"),
            new XElement(Opf + "language", "ko")),
        new XElement(Opf + "manifest",
            Item("header", "Contents/header.xml"),
            Enumerable.Range(0, sectionCount).Select(i => Item($"section{i}", $"Contents/section{i}.xml")),
            Item("settings", "settings.xml"),
            binaries.Select(b => new XElement(Opf + "item", new XAttribute("id", b.Id), new XAttribute("href", b.Path), new XAttribute("media-type", b.MediaType), new XAttribute("isEmbeded", "1")))),
        new XElement(Opf + "spine",
            new XElement(Opf + "itemref", new XAttribute("idref", "header"), new XAttribute("linear", "yes")),
            Enumerable.Range(0, sectionCount).Select(i => new XElement(Opf + "itemref", new XAttribute("idref", $"section{i}"), new XAttribute("linear", "yes")))));

    private static XElement Item(string id, string href) =>
        new(Opf + "item", new XAttribute("id", id), new XAttribute("href", href), new XAttribute("media-type", "application/xml"));
    }
}
