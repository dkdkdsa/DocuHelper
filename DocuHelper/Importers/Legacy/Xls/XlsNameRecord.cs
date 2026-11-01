using System;

namespace DocuHelper.Importers.Legacy.Xls
{
    internal sealed class XlsNameRecord
    {
        private static readonly string[] s_builtIn =
        {
            "Consolidate_Area", "Auto_Open", "Auto_Close", "Extract", "Database", "Criteria", "Print_Area", "Print_Titles",
            "Recorder", "Data_Form", "Auto_Activate", "Auto_Deactivate", "Sheet_Title", "_FilterDatabase",
        };

        public string Name { get; set; }

        public int BuiltInCode { get; set; } = -1;

        public bool Hidden { get; set; }

        public bool IsMacro { get; set; }

        public int SheetPosition { get; set; }

        public byte[] Tokens { get; set; }

        public bool IsBuiltIn => BuiltInCode >= 0;

        public string DisplayName => IsBuiltIn ? "_xlnm." + BuiltInName : Name;

        public string BuiltInName => BuiltInCode >= 0 && BuiltInCode < s_builtIn.Length ? s_builtIn[BuiltInCode] : Name;

        public static XlsNameRecord Read(XlsRecord record)
        {
            var reader = new XlsDataReader(record);
            var flags = reader.ReadUInt16();
            reader.Skip(1);
            var length = reader.ReadByte();
            var tokenLength = reader.ReadUInt16();
            reader.Skip(2);
            var sheet = reader.ReadUInt16();
            reader.Skip(4);
            var name = string.Empty;
            if (length > 0)
            {
                var wide = (reader.ReadByte() & 1) != 0;
                name = reader.ReadChars(length, wide);
            }

            var tokens = reader.ReadBytes(Math.Min(tokenLength, reader.Remaining));
            var result = new XlsNameRecord
            {
                Name = name,
                Hidden = (flags & 0x0001) != 0,
                IsMacro = (flags & 0x0002) != 0 || (flags & 0x0004) != 0,
                SheetPosition = sheet,
                Tokens = tokens,
            };

            if ((flags & 0x0020) != 0 && name.Length == 1)
                result.BuiltInCode = name[0];

            return result;
        }
    }
}
