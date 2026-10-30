namespace DocuHelper.Tools.Excel
{
    public static class ExcelAnchor
    {
        public static string Cell(string sheet, CellAddress address) => sheet + "!" + address;

        public static string Sheet(string sheet) => sheet + "!";

        public static bool TryParse(string anchor, out string sheet, out string cellRef)
        {
            sheet = null;
            cellRef = null;
            if (string.IsNullOrEmpty(anchor))
                return false;

            var bang = anchor.LastIndexOf('!');
            if (bang <= 0)
                return false;

            sheet = anchor.Substring(0, bang);
            cellRef = anchor.Substring(bang + 1);
            return true;
        }
    }
}
