using System;
using System.Collections.Generic;
using DocumentFormat.OpenXml;

namespace DocuHelper.Tools.Ppt
{
    public static class PptSchemaOrder
    {
        public static readonly IReadOnlyList<string> RunProperties = new[]
        {
            "ln",
            "noFill|solidFill|gradFill|blipFill|pattFill|grpFill",
            "effectLst|effectDag",
            "highlight",
            "uLnTx|uLn",
            "uFillTx|uFill",
            "latin",
            "ea",
            "cs",
            "sym",
            "hlinkClick",
            "hlinkMouseOver",
            "rtl",
            "extLst",
        };

        public static readonly IReadOnlyList<string> ParagraphProperties = new[]
        {
            "lnSpc",
            "spcBef",
            "spcAft",
            "buClrTx|buClr",
            "buSzTx|buSzPct|buSzPts",
            "buFontTx|buFont",
            "buNone|buAutoNum|buChar|buBlip",
            "tabLst",
            "defRPr",
            "extLst",
        };

        public static readonly IReadOnlyList<string> BodyProperties = new[]
        {
            "prstTxWarp",
            "noAutofit|normAutofit|spAutoFit",
            "scene3d",
            "sp3d|flatTx",
            "extLst",
        };

        public static readonly IReadOnlyList<string> ShapeProperties = new[]
        {
            "xfrm",
            "custGeom|prstGeom",
            "noFill|solidFill|gradFill|blipFill|pattFill|grpFill",
            "ln",
            "effectLst|effectDag",
            "scene3d",
            "sp3d",
            "extLst",
        };

        public static readonly IReadOnlyList<string> LineProperties = new[]
        {
            "noFill|solidFill|gradFill|pattFill",
            "prstDash|custDash",
            "round|bevel|miter",
            "headEnd",
            "tailEnd",
            "extLst",
        };

        public static readonly IReadOnlyList<string> TableCellProperties = new[]
        {
            "lnL",
            "lnR",
            "lnT",
            "lnB",
            "lnTlToBr",
            "lnBlToTr",
            "cell3D",
            "noFill|solidFill|gradFill|blipFill|pattFill|grpFill",
            "headers",
            "extLst",
        };

        public static readonly IReadOnlyList<string> Slide = new[]
        {
            "cSld",
            "clrMapOvr",
            "transition|AlternateContent",
            "timing",
            "extLst",
        };

        public static readonly IReadOnlyList<string> CommonSlideData = new[]
        {
            "bg",
            "spTree",
            "custDataLst",
            "controls",
            "extLst",
        };

        public static readonly IReadOnlyList<string> Presentation = new[]
        {
            "sldMasterIdLst",
            "notesMasterIdLst",
            "handoutMasterIdLst",
            "sldIdLst",
            "sldSz",
            "notesSz",
            "smartTags",
            "embeddedFontLst",
            "custShowLst",
            "photoAlbum",
            "custDataLst",
            "kinsoku",
            "defaultTextStyle",
            "modifyVerifier",
            "extLst",
        };

        public static int RankOf(IReadOnlyList<string> order, string localName)
        {
            for (var i = 0; i < order.Count; i++)
            {
                foreach (var name in order[i].Split('|'))
                {
                    if (string.Equals(name, localName, StringComparison.Ordinal))
                        return i;
                }
            }

            return -1;
        }

        public static void Insert(OpenXmlCompositeElement parent, OpenXmlElement child, IReadOnlyList<string> order)
        {
            var rank = RankOf(order, child.LocalName);
            if (rank < 0)
            {
                AppendBeforeExtensions(parent, child);
                return;
            }

            foreach (var existing in parent.ChildElements)
            {
                var existingRank = RankOf(order, existing.LocalName);
                if (existingRank > rank)
                {
                    existing.InsertBeforeSelf(child);
                    return;
                }
            }

            parent.AppendChild(child);
        }

        public static void Replace(OpenXmlCompositeElement parent, OpenXmlElement child, IReadOnlyList<string> order)
        {
            Remove(parent, child.LocalName, order);
            Insert(parent, child, order);
        }

        public static int Remove(OpenXmlCompositeElement parent, string localName, IReadOnlyList<string> order)
        {
            var rank = RankOf(order, localName);
            var removed = 0;
            foreach (var existing in new List<OpenXmlElement>(parent.ChildElements))
            {
                var matches = rank >= 0
                    ? RankOf(order, existing.LocalName) == rank
                    : string.Equals(existing.LocalName, localName, StringComparison.Ordinal);

                if (!matches)
                    continue;

                existing.Remove();
                removed++;
            }

            return removed;
        }

        private static void AppendBeforeExtensions(OpenXmlCompositeElement parent, OpenXmlElement child)
        {
            foreach (var existing in parent.ChildElements)
            {
                if (string.Equals(existing.LocalName, "extLst", StringComparison.Ordinal))
                {
                    existing.InsertBeforeSelf(child);
                    return;
                }
            }

            parent.AppendChild(child);
        }
    }
}
