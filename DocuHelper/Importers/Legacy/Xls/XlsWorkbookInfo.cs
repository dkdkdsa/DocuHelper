using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DocuHelper.Importers.Legacy.Xls
{
    internal sealed class XlsWorkbookInfo
    {
        private static readonly Regex s_cellLike = new(@"^[A-Za-z]{1,3}[0-9]+$|^[Rr][0-9]*([Cc][0-9]*)?$|^[Cc][0-9]*$", RegexOptions.CultureInvariant);

        public bool Date1904 { get; set; }

        public List<XlsSheetInfo> Sheets { get; } = new();

        public List<XlsSupBook> SupBooks { get; } = new();

        public List<(int SupBook, int First, int Last)> Xtis { get; } = new();

        public List<XlsNameRecord> Names { get; } = new();

        public void ReadExternSheet(XlsRecord record)
        {
            var reader = new XlsDataReader(record);
            var count = reader.ReadUInt16();
            for (var i = 0; i < count && reader.Remaining >= 6; i++)
                Xtis.Add((reader.ReadUInt16(), reader.ReadInt16(), reader.ReadInt16()));
        }

        public void ReadSupBook(XlsRecord record)
        {
            var book = new XlsSupBook();
            if (record.Data.Length >= 4)
                book.IsSelf = record.Data[2] == 0x01 && record.Data[3] == 0x04;

            SupBooks.Add(book);
        }

        public void ReadExternName(XlsRecord record)
        {
            if (SupBooks.Count == 0)
                return;

            var reader = new XlsDataReader(record);
            reader.Skip(6);
            SupBooks[SupBooks.Count - 1].ExternNames.Add(reader.ReadShortString());
        }

        public void AssignOutputNames()
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var output = 0;
            foreach (var sheet in Sheets)
            {
                if (!sheet.IsWorksheet)
                    continue;

                var name = Sanitize(sheet.SourceName);
                var candidate = name;
                for (var n = 2; !used.Add(candidate); n++)
                {
                    var suffix = "(" + n + ")";
                    candidate = name.Substring(0, Math.Min(name.Length, 31 - suffix.Length)) + suffix;
                }

                sheet.Name = candidate;
                sheet.OutputIndex = output++;
            }
        }

        public string SheetPrefix(int xti)
        {
            if (xti < 0 || xti >= Xtis.Count)
                return null;

            var (book, first, last) = Xtis[xti];
            if (book >= SupBooks.Count || !SupBooks[book].IsSelf || first < 0 || last < first || last >= Sheets.Count)
                return null;

            var firstSheet = Sheets[first];
            var lastSheet = Sheets[last];
            if (!firstSheet.IsWorksheet || !lastSheet.IsWorksheet)
                return null;

            var text = first == last ? firstSheet.Name : firstSheet.Name + ":" + lastSheet.Name;
            return NeedsQuotes(firstSheet.Name) || NeedsQuotes(lastSheet.Name) ? "'" + text.Replace("'", "''") + "'!" : text + "!";
        }

        public string ExternName(int xti, int index)
        {
            if (xti < 0 || xti >= Xtis.Count || index < 1)
                return null;

            var book = Xtis[xti].SupBook;
            if (book >= SupBooks.Count)
                return null;

            if (SupBooks[book].IsSelf)
                return index <= Names.Count ? Names[index - 1].DisplayName : null;

            var names = SupBooks[book].ExternNames;
            return index <= names.Count ? names[index - 1] : null;
        }

        private static string Sanitize(string name)
        {
            var builder = new StringBuilder();
            foreach (var c in name ?? string.Empty)
                builder.Append("[]:*?/\\".IndexOf(c) >= 0 || c < ' ' ? '_' : c);

            var text = builder.ToString().Trim('\'');
            if (text.Length > 31)
                text = text.Substring(0, 31);

            return text.Length == 0 ? "Sheet" : text;
        }

        private static bool NeedsQuotes(string name)
        {
            if (name.Length == 0 || s_cellLike.IsMatch(name) || char.IsDigit(name[0]))
                return true;

            foreach (var c in name)
            {
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '.')
                    return true;
            }

            return false;
        }
    }
}
