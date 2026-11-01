using System;
using System.Collections.Generic;
using System.Linq;

namespace DocuHelper.Importers.Hwp
{
    public static class HwpCapabilities
    {
        private static readonly HwpFeature[] s_all = Enum.GetValues<HwpFeature>();

        public static IReadOnlyList<HwpFeature> All => s_all;

        public static IEnumerable<HwpFeature> Missing => s_all.Where(f => !Has(f));

        public static bool Has(HwpFeature feature) => feature switch
        {
            HwpFeature.CharShade => true,
            HwpFeature.CharSpacing => true,

            HwpFeature.ParaRightIndent => true,
            HwpFeature.ParaKeep => true,
            HwpFeature.ParaFixedLine => true,
            HwpFeature.ParaDivide => true,
            HwpFeature.ParaBorder => true,

            HwpFeature.TabStops => true,
            HwpFeature.StaticToc => false,

            HwpFeature.TableCellFormat => true,

            HwpFeature.HeaderFooter => true,
            HwpFeature.PageNumber => true,
            HwpFeature.PageControl => true,

            HwpFeature.Columns => true,
            HwpFeature.PageBorder => true,

            HwpFeature.Sections => true,

            HwpFeature.Pictures => true,

            HwpFeature.Notes => true,

            HwpFeature.Hyperlinks => true,
            HwpFeature.Bookmarks => true,
            HwpFeature.ClickHere => true,

            HwpFeature.NamedStyles => true,

            HwpFeature.RealNumbering => true,

            HwpFeature.KeepObjects => false,

            _ => false,
        };
    }
}
