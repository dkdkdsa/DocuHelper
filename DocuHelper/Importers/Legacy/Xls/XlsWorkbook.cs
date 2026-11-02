using System.Collections.Generic;

namespace DocuHelper.Importers.Legacy.Xls
{
    internal sealed class XlsWorkbook
    {
        public bool Date1904 { get; set; }

        public XlsStyleTable Styles { get; set; }

        public List<XlsSheet> Sheets { get; } = new();

        public List<XlsDefinedName> Names { get; } = new();
    }
}
