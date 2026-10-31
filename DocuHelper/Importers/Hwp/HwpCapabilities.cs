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
            HwpFeature.CharShade => false,
            HwpFeature.CharSpacing => false,

            HwpFeature.ParaRightIndent => false,
            HwpFeature.ParaKeep => false,
            HwpFeature.ParaFixedLine => false,
            HwpFeature.ParaDivide => false,
            HwpFeature.ParaBorder => false,

            HwpFeature.TabStops => false,

            HwpFeature.TableCellFormat => false,

            HwpFeature.HeaderFooter => true,
            HwpFeature.PageNumber => true,
            HwpFeature.PageControl => false,

            HwpFeature.Columns => true,
            HwpFeature.PageBorder => true,

            HwpFeature.Sections => false,

            HwpFeature.Pictures => false,

            HwpFeature.Notes => false,

            HwpFeature.Hyperlinks => false,
            HwpFeature.Bookmarks => false,
            HwpFeature.ClickHere => false,

            HwpFeature.NamedStyles => false,

            HwpFeature.RealNumbering => false,

            HwpFeature.KeepObjects => false,

            _ => false,
        };
    }
}
