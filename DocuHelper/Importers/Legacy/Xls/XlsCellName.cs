using System.Text;

namespace DocuHelper.Importers.Legacy.Xls
{
    internal static class XlsCellName
    {
        public static string Column(int column)
        {
            var builder = new StringBuilder();
            for (var n = column + 1; n > 0; n = (n - 1) / 26)
                builder.Insert(0, (char)('A' + (n - 1) % 26));

            return builder.ToString();
        }

        public static string Cell(int row, int column) => Column(column) + (row + 1);
    }
}
