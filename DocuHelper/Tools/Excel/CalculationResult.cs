using System.Collections.Generic;

namespace DocuHelper.Tools.Excel
{
    public sealed class CalculationResult
    {
        public Dictionary<CellKey, ExcelValue> Values { get; } = new();

        public HashSet<CellKey> Unknown { get; } = new();

        public bool Aborted { get; set; }

        public int Updated { get; set; }
    }
}
