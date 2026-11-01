using System.Collections.Generic;

namespace DocuHelper.Importers.Legacy.Xls
{
    internal static class XlsSharedStrings
    {
        public static List<string> Read(XlsRecord record)
        {
            var strings = new List<string>();
            var reader = new XlsDataReader(record);
            reader.Skip(8);
            while (reader.Remaining >= 3)
                strings.Add(reader.ReadLongString());

            return strings;
        }
    }
}
