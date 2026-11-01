using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace DocuHelper.Importers.Hwp
{
    internal sealed partial class HwpxBody
    {
        private static readonly HashSet<string> s_keepable = new(StringComparer.Ordinal)
        {
            "equation", "textart", "dutmal", "compose",
        };

        private static readonly HashSet<string> s_referenceNodes = new(StringComparer.Ordinal)
        {
            "img", "ctrl", "run", "p", "subList", "tbl", "pic",
        };

        private bool TryKeep(XElement element, HwpxCharShape shape, HwpxRunBuilder runs)
        {
            if (!s_keepable.Contains(element.Name.LocalName) || HasReferences(element))
                return false;

            if (_deleting > 0)
                return true;

            var key = NextKey();
            runs.OpenKeep(key);
            var start = runs.Runs.Count;
            Emit(KeptText(element), shape, runs);
            var copy = new XElement(element);
            ResetReferences(copy);
            _document.Kept[key] = new HwpKeptObject
            {
                Xml = copy.ToString(SaveOptions.DisableFormatting),
                Text = string.Concat(runs.Runs.Skip(start).Select(run => run.Text ?? string.Empty)),
            };
            runs.CloseKeep(key);
            return true;
        }

        private string NextKey()
        {
            var number = _document.Kept.Count + 1;
            while (_document.Kept.ContainsKey(number.ToString(CultureInfo.InvariantCulture)))
                number++;

            return number.ToString(CultureInfo.InvariantCulture);
        }

        private static string KeptText(XElement element) => element.Name.LocalName switch
        {
            "equation" => string.Empty,
            "textart" => ArtText(element),
            "dutmal" => Dutmal(element),
            _ => HwpxXml.Attribute(element, "composeText") ?? string.Empty,
        };

        private static bool HasReferences(XElement element) =>
            element.DescendantsAndSelf().Any(node => s_referenceNodes.Contains(node.Name.LocalName)
                || node.Attributes().Any(attribute => attribute.Name.LocalName.EndsWith("Ref", StringComparison.Ordinal) && !IsStyleReference(node, attribute)));

        private static bool IsStyleReference(XElement node, XAttribute attribute) =>
            (node.Name.LocalName == "dutmal" && attribute.Name.LocalName == "styleIDRef")
            || (node.Name.LocalName == "charPr" && attribute.Name.LocalName == "prIDRef");

        private static void ResetReferences(XElement element)
        {
            foreach (var node in element.DescendantsAndSelf().ToList())
            {
                foreach (var attribute in node.Attributes().Where(item => IsStyleReference(node, item)).ToList())
                    attribute.Value = "0";
            }
        }
    }
}
