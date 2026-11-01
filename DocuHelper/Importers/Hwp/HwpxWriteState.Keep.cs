using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using static DocuHelper.Importers.Hwp.HwpxNames;

namespace DocuHelper.Importers.Hwp
{
    internal sealed partial class HwpxWriteState
    {
        private readonly Dictionary<string, HwpKeptObject> _kept;

        private XElement KeptObject(BookmarkStart start, DocumentFormat.OpenXml.Wordprocessing.Paragraph paragraph, HashSet<OpenXmlElement> suppressed)
        {
            var name = start.Name?.Value ?? string.Empty;
            if (!name.StartsWith(HwpKeep.Prefix, StringComparison.Ordinal) || !_kept.TryGetValue(name.Substring(HwpKeep.Prefix.Length), out var record))
                return null;

            var all = paragraph.Descendants().ToList();
            var from = all.FindIndex(element => ReferenceEquals(element, start));
            var to = all.FindIndex(element => element is BookmarkEnd end && end.Id?.Value == start.Id?.Value);
            if (from < 0 || to <= from)
                return null;

            var inside = all.Skip(from + 1).Take(to - from - 1).OfType<Run>().ToList();
            if (HwpKeep.TextOf(inside) != record.Text || !TryParseKept(record.Xml, out var element))
                return null;

            foreach (var run in inside)
                suppressed.Add(run);

            return new XElement(Hp + "run", new XAttribute("charPrIDRef", Number(CharId(CharKeyOf(null, paragraph)))), element);
        }

        private static bool TryParseKept(string xml, out XElement element)
        {
            try
            {
                element = XElement.Parse(xml, LoadOptions.PreserveWhitespace);
            }
            catch (XmlException)
            {
                element = null;
                return false;
            }

            foreach (var declaration in element.DescendantsAndSelf().SelectMany(e => e.Attributes().Where(a => a.IsNamespaceDeclaration)).ToList())
                declaration.Remove();

            return true;
        }
    }
}
