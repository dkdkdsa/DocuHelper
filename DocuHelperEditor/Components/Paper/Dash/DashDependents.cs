using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Dash;
using DocuHelper.Dash.Elements;
using DocuHelper.Dash.Logic;

namespace DocuHelperEditor.Components.Paper.Dash
{
    internal static class DashDependents
    {
        public static string ParamOf(Element element) => element switch
        {
            SliderElement slider => slider.Param,
            RangeSliderElement range => range.Param,
            ToggleElement toggle => toggle.Param,
            SegmentedElement segmented => segmented.Param,
            SelectElement dropdown => dropdown.Param,
            _ => null,
        };

        public static HashSet<string> NamesOf(DashDocument document, string name)
        {
            var names = new HashSet<string>(StringComparer.Ordinal) { name };
            var grew = true;
            while (grew)
            {
                grew = false;
                foreach (var value in document.Values.Where(value => value != null && !string.IsNullOrWhiteSpace(value.Name)))
                {
                    if (!names.Contains(value.Name) && Uses(value.Uses(), names))
                    {
                        names.Add(value.Name);
                        grew = true;
                    }
                }

                foreach (var param in document.Params.OfType<PickParam>().Where(param => !string.IsNullOrEmpty(param.OptionsFrom)))
                {
                    if (!names.Contains(param.Name) && Uses(new[] { param.OptionsFrom }, names))
                    {
                        names.Add(param.Name);
                        grew = true;
                    }
                }
            }

            return names;
        }

        public static IReadOnlyList<Element> ElementsOf(DashDocument document, string name)
        {
            if (document?.Root == null || string.IsNullOrWhiteSpace(name))
                return Array.Empty<Element>();

            var names = NamesOf(document, name);
            return document.Walk()
                .Where(element => element != document.Root
                    && element is not ButtonElement
                    && !string.Equals(ParamOf(element), name, StringComparison.Ordinal)
                    && Uses(element.Uses(), names))
                .ToList();
        }

        public static IReadOnlyList<DashValue> ValuesOf(DashDocument document, string name)
        {
            if (document == null || string.IsNullOrWhiteSpace(name))
                return Array.Empty<DashValue>();

            var names = NamesOf(document, name);
            return document.Values
                .Where(value => value != null && !string.Equals(value.Name, name, StringComparison.Ordinal) && names.Contains(value.Name))
                .ToList();
        }

        public static IReadOnlyCollection<string> IdsOf(DashDocument document, IReadOnlyList<string> selected)
        {
            var first = (selected ?? Array.Empty<string>()).Select(document.Find).FirstOrDefault(element => element != null && element != document.Root);
            var name = first == null ? null : ParamOf(first);
            return name == null ? Array.Empty<string>() : ElementsOf(document, name).Select(element => element.Id).Where(id => id != null).ToList();
        }

        private static bool Uses(IEnumerable<string> used, HashSet<string> names) =>
            (used ?? Enumerable.Empty<string>()).Any(item => item != null && names.Any(name => string.Equals(item, name, StringComparison.Ordinal) || item.StartsWith(name + ".", StringComparison.Ordinal)));
    }
}
