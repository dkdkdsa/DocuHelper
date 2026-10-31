using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using A = DocumentFormat.OpenXml.Drawing;

namespace DocuHelper.Tools.Ppt
{
    public static class PptListStyleMerge
    {
        private const int Levels = 9;

        public static void Bake(A.ListStyle target, IReadOnlyList<OpenXmlElement> chain)
        {
            var merged = Flatten(chain);
            target.RemoveAllChildren();
            foreach (var level in merged)
                target.AppendChild(level);
        }

        public static List<A.TextParagraphPropertiesType> Flatten(IReadOnlyList<OpenXmlElement> chain)
        {
            var result = new List<A.TextParagraphPropertiesType>();
            for (var level = 1; level <= Levels; level++)
            {
                var props = PropertiesFor(chain, level);
                if (props.Count == 0)
                    continue;

                var merged = Create(level);
                foreach (var source in Enumerable.Reverse(props))
                    Overlay(merged, source);

                if (merged.HasAttributes || merged.HasChildren)
                    result.Add(merged);
            }

            return result;
        }

        private static List<A.TextParagraphPropertiesType> PropertiesFor(IReadOnlyList<OpenXmlElement> chain, int level)
        {
            var name = "lvl" + level + "pPr";
            var found = new List<A.TextParagraphPropertiesType>();
            foreach (var container in chain)
            {
                A.TextParagraphPropertiesType match = null;
                A.TextParagraphPropertiesType fallback = null;
                foreach (var child in container.ChildElements)
                {
                    if (child is not A.TextParagraphPropertiesType props)
                        continue;

                    if (props.LocalName == name)
                        match = props;
                    else if (props.LocalName == "defPPr")
                        fallback = props;
                }

                if (match != null)
                    found.Add(match);

                if (fallback != null)
                    found.Add(fallback);
            }

            return found;
        }

        private static A.TextParagraphPropertiesType Create(int level) => level switch
        {
            1 => new A.Level1ParagraphProperties(),
            2 => new A.Level2ParagraphProperties(),
            3 => new A.Level3ParagraphProperties(),
            4 => new A.Level4ParagraphProperties(),
            5 => new A.Level5ParagraphProperties(),
            6 => new A.Level6ParagraphProperties(),
            7 => new A.Level7ParagraphProperties(),
            8 => new A.Level8ParagraphProperties(),
            _ => new A.Level9ParagraphProperties(),
        };

        private static void Overlay(A.TextParagraphPropertiesType target, A.TextParagraphPropertiesType source)
        {
            foreach (var attribute in source.GetAttributes())
            {
                if (attribute.LocalName != "lvl")
                    target.SetAttribute(attribute);
            }

            foreach (var child in source.ChildElements)
            {
                if (child.LocalName == "extLst")
                    continue;

                if (child is A.DefaultRunProperties defaults)
                {
                    var run = target.GetFirstChild<A.DefaultRunProperties>();
                    if (run == null)
                    {
                        run = new A.DefaultRunProperties();
                        PptSchemaOrder.Insert(target, run, PptSchemaOrder.ParagraphProperties);
                    }

                    OverlayRun(run, defaults);
                    continue;
                }

                PptSchemaOrder.Replace(target, child.CloneNode(true), PptSchemaOrder.ParagraphProperties);
            }
        }

        private static void OverlayRun(A.TextCharacterPropertiesType target, A.TextCharacterPropertiesType source)
        {
            foreach (var attribute in source.GetAttributes())
                target.SetAttribute(attribute);

            foreach (var child in source.ChildElements)
            {
                if (child.LocalName != "extLst")
                    PptSchemaOrder.Replace(target, child.CloneNode(true), PptSchemaOrder.RunProperties);
            }
        }
    }
}
