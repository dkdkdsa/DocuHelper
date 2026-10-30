using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace DocuHelper.Importers.Hwp
{
    internal sealed class HwpxPackage : IDisposable
    {
        private const string ContainerPath = "META-INF/container.xml";
        private const string ManifestPath = "META-INF/manifest.xml";
        private const string DefaultRoot = "Contents/content.hpf";
        private const string DefaultHeader = "Contents/header.xml";

        private static readonly XmlReaderSettings s_settings = new()
        {
            DtdProcessing = DtdProcessing.Ignore,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
        };

        private readonly ZipArchive _archive;
        private readonly Dictionary<string, ZipArchiveEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _items = new(StringComparer.Ordinal);
        private readonly List<string> _spine = new();
        private string _folder = string.Empty;
        private bool _indexed;

        public HwpxPackage(byte[] content)
        {
            _archive = new ZipArchive(new MemoryStream(content, false), ZipArchiveMode.Read);
            foreach (var entry in _archive.Entries)
                _entries.TryAdd(Normalize(entry.FullName), entry);
        }

        public bool IsEncrypted()
        {
            if (_archive.Entries.Any(e => e.IsEncrypted))
                return true;

            var manifest = Load(ManifestPath);
            return manifest != null && manifest.Descendants().Any(e => e.Name.LocalName == "encryption-data");
        }

        public XElement Header() => Load(HeaderPath()) ?? throw new InvalidDataException("header.xml is missing.");

        public IEnumerable<XElement> Sections()
        {
            Index();
            var header = HeaderPath();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { header };
            var found = false;
            foreach (var id in _spine)
            {
                if (!_items.TryGetValue(id, out var href) || !href.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    continue;

                var path = Resolve(href) ?? throw new InvalidDataException(href + " is missing.");
                if (!seen.Add(path))
                    continue;

                var root = Load(path);
                if (root.Name.LocalName != "sec")
                    continue;

                found = true;
                yield return root;
            }

            if (found)
                yield break;

            for (var i = 0; ; i++)
            {
                var root = Load("Contents/section" + i.ToString(CultureInfo.InvariantCulture) + ".xml");
                if (root == null)
                    yield break;

                yield return root;
            }
        }

        public XElement Item(string id)
        {
            Index();
            return _items.TryGetValue(id, out var href) ? Load(Resolve(href)) : null;
        }

        public void Dispose() => _archive.Dispose();

        private string HeaderPath()
        {
            Index();
            foreach (var (id, href) in _items)
            {
                if (id != "header" && !href.EndsWith("header.xml", StringComparison.OrdinalIgnoreCase))
                    continue;

                var path = Resolve(href);
                if (path != null)
                    return path;
            }

            return DefaultHeader;
        }

        private void Index()
        {
            if (_indexed)
                return;

            _indexed = true;
            var rootPath = RootPath();
            var root = Load(rootPath);
            if (root == null)
                return;

            var slash = rootPath.LastIndexOf('/');
            _folder = slash < 0 ? string.Empty : rootPath.Substring(0, slash + 1);
            foreach (var element in root.Descendants())
            {
                switch (element.Name.LocalName)
                {
                    case "item":
                        var id = HwpxXml.Attribute(element, "id");
                        var href = HwpxXml.Attribute(element, "href");
                        if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(href))
                            _items.TryAdd(id, href);
                        break;
                    case "itemref":
                        var reference = HwpxXml.Attribute(element, "idref");
                        if (!string.IsNullOrEmpty(reference))
                            _spine.Add(reference);
                        break;
                }
            }
        }

        private string RootPath()
        {
            var container = Load(ContainerPath);
            var files = container?.Descendants().Where(e => e.Name.LocalName == "rootfile").ToList() ?? new List<XElement>();
            var root = files.FirstOrDefault(e => HwpxXml.Attribute(e, "media-type") == "application/hwpml-package+xml")
                ?? files.FirstOrDefault(e => (HwpxXml.Attribute(e, "full-path") ?? string.Empty).EndsWith(".hpf", StringComparison.OrdinalIgnoreCase));
            var path = HwpxXml.Attribute(root, "full-path");
            return string.IsNullOrEmpty(path) || !_entries.ContainsKey(Normalize(path)) ? DefaultRoot : Normalize(path);
        }

        private string Resolve(string href)
        {
            var path = Normalize(Uri.UnescapeDataString(href));
            if (_entries.ContainsKey(path))
                return path;

            var relative = Normalize(_folder + path);
            return _entries.ContainsKey(relative) ? relative : null;
        }

        private XElement Load(string path)
        {
            if (path == null || !_entries.TryGetValue(Normalize(path), out var entry))
                return null;

            using var stream = entry.Open();
            using var reader = XmlReader.Create(stream, s_settings);
            return XDocument.Load(reader, LoadOptions.PreserveWhitespace).Root;
        }

        private static string Normalize(string path)
        {
            var value = path.Replace('\\', '/').TrimStart('/');
            return value.StartsWith("./", StringComparison.Ordinal) ? value.Substring(2) : value;
        }
    }
}
