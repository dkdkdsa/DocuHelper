using System.Collections.Generic;

namespace DocuHelper.Importers.Legacy.Xls
{
    internal sealed class XlsSupBook
    {
        public bool IsSelf { get; set; }

        public List<string> ExternNames { get; } = new();
    }
}
