using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace DocuHelper.Importers.Hwp
{
    internal static class HwpxXml
    {
        public const int MaxSwitchDepth = 4;

        private static readonly string[] s_knownCases = ["HwpUnitChar", "paragraph", "ooxmlchart"];

        private static readonly HashSet<string> s_content = new(StringComparer.Ordinal)
        {
            "tbl", "pic", "rect", "ellipse", "arc", "polygon", "curve", "line", "connectLine", "container",
            "ole", "equation", "chart", "textart", "video", "autoNum", "pageNum",
        };

        public static IEnumerable<XElement> Children(XElement element) => Flatten(element, 0);

        public static IEnumerable<XElement> Children(XElement element, string name) =>
            Children(element).Where(e => e.Name.LocalName == name);

        public static XElement Child(XElement element, string name) => Children(element, name).FirstOrDefault();

        public static XElement Branch(XElement choice)
        {
            XElement fallback = null;
            foreach (var option in choice.Elements())
            {
                if (option.Name.LocalName == "case" && IsKnown(Attribute(option, "required-namespace")))
                    return option;

                if (option.Name.LocalName == "default")
                    fallback ??= option;
            }

            return fallback;
        }

        public static string Attribute(XElement element, string name)
        {
            if (element == null)
                return null;

            var attribute = element.Attribute(name);
            if (attribute != null)
                return attribute.Value;

            foreach (var candidate in element.Attributes())
            {
                if (candidate.Name.LocalName == name)
                    return candidate.Value;
            }

            return null;
        }

        public static int Int(XElement element, string name, int fallback)
        {
            var text = Attribute(element, name);
            if (text == null || !long.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                return fallback;

            return value > int.MaxValue || value < int.MinValue ? fallback : (int)value;
        }

        public static bool Flag(XElement element, string name)
        {
            var text = Attribute(element, name);
            return text == "1" || string.Equals(text, "true", StringComparison.OrdinalIgnoreCase);
        }

        public static bool HasContent(XElement element) =>
            element != null && element.Descendants().Any(e =>
                s_content.Contains(e.Name.LocalName) || (e.Name.LocalName == "t" && !string.IsNullOrWhiteSpace(e.Value)));

        private static IEnumerable<XElement> Flatten(XElement element, int depth)
        {
            if (element == null)
                yield break;

            foreach (var child in element.Elements())
            {
                if (child.Name.LocalName != "switch")
                {
                    yield return child;
                    continue;
                }

                if (depth >= MaxSwitchDepth)
                    continue;

                foreach (var nested in Flatten(Branch(child), depth + 1))
                    yield return nested;
            }
        }

        private static bool IsKnown(string space) =>
            !string.IsNullOrEmpty(space) && s_knownCases.Any(k => space.EndsWith(k, StringComparison.Ordinal));
    }
}
