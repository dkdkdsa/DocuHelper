using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DocuHelper.Importers.Legacy.Xls
{
    internal static class XlsWorkbookReader
    {
        private const int WorkbookGlobals = 0x0005;
        private const int WorksheetSubstream = 0x0010;
        private const int Biff8 = 0x0600;

        private static readonly Regex s_validName = new(@"^[\p{L}_\\][\p{L}\p{N}_.\\?]*$", RegexOptions.CultureInvariant);
        private static readonly Regex s_cellLike = new(@"^[A-Za-z]{1,3}[0-9]+$|^[Rr][0-9]*([Cc][0-9]*)?$|^[Cc][0-9]*$", RegexOptions.CultureInvariant);

        public static XlsWorkbook Read(byte[] stream)
        {
            var records = XlsRecordReader.Read(stream);
            if (records.Count == 0 || records[0].Id != XlsIds.Bof && !IsOldBof(records[0].Id))
                throw new InvalidDataException("The workbook has no start record.");

            if (records[0].Id != XlsIds.Bof || records[0].Data.Length < 4 || (records[0].Data[1] << 8 | records[0].Data[0]) < Biff8)
                throw new LegacyOfficeException(XlsMessages.TooOld);

            var info = new XlsWorkbookInfo();
            var styles = new XlsStyleTable();
            var strings = new List<string>();
            var finished = false;
            for (var i = 1; i < records.Count && !finished; i++)
            {
                var record = records[i];
                switch (record.Id)
                {
                    case XlsIds.Eof:
                        finished = true;
                        break;
                    case XlsIds.FilePass:
                        throw new LegacyOfficeException(XlsMessages.Password);
                    case XlsIds.Date1904:
                        info.Date1904 = record.Data.Length >= 2 && record.Data[0] == 1;
                        break;
                    case XlsIds.BoundSheet:
                        ReadBoundSheet(record, info);
                        break;
                    case XlsIds.Sst:
                        strings = XlsSharedStrings.Read(record);
                        break;
                    case XlsIds.Font:
                        styles.ReadFont(record);
                        break;
                    case XlsIds.Format:
                        styles.ReadFormat(record);
                        break;
                    case XlsIds.Xf:
                        styles.ReadXf(record);
                        break;
                    case XlsIds.XfExt:
                        styles.ReadXfExt(record);
                        break;
                    case XlsIds.Palette:
                        styles.Palette.Read(record);
                        break;
                    case XlsIds.SupBook:
                        info.ReadSupBook(record);
                        break;
                    case XlsIds.ExternName:
                        info.ReadExternName(record);
                        break;
                    case XlsIds.ExternSheet:
                        info.ReadExternSheet(record);
                        break;
                    case XlsIds.Name:
                        info.Names.Add(XlsNameRecord.Read(record));
                        break;
                }
            }

            if (!finished)
                throw new InvalidDataException("The workbook globals have no end.");

            info.AssignOutputNames();
            var workbook = new XlsWorkbook { Date1904 = info.Date1904, Styles = styles };
            var decompiler = new XlsFormulaDecompiler(info);
            var sheetReader = new XlsSheetReader(strings, decompiler);
            var starts = new Dictionary<int, int>();
            for (var i = 0; i < records.Count; i++)
            {
                if (records[i].Id == XlsIds.Bof)
                    starts[records[i].Position] = i;
            }

            foreach (var sheet in info.Sheets)
            {
                if (!sheet.IsWorksheet)
                    continue;

                if (!starts.TryGetValue(sheet.Position, out var start) || !IsWorksheet(records[start]))
                {
                    sheet.OutputIndex = -1;
                    continue;
                }

                workbook.Sheets.Add(sheetReader.Read(records, start, sheet));
            }

            Renumber(info, workbook);
            ReadNames(info, workbook, decompiler);
            return workbook;
        }

        private static void Renumber(XlsWorkbookInfo info, XlsWorkbook workbook)
        {
            var next = 0;
            var set = new HashSet<XlsSheetInfo>();
            foreach (var sheet in workbook.Sheets)
                set.Add(sheet.Info);

            foreach (var sheet in info.Sheets)
                sheet.OutputIndex = set.Contains(sheet) ? next++ : -1;
        }

        private static bool IsOldBof(int id) => id == 0x0009 || id == 0x0209 || id == 0x0409;

        private static bool IsWorksheet(XlsRecord bof) =>
            bof.Data.Length >= 4 && (bof.Data[3] << 8 | bof.Data[2]) == WorksheetSubstream;

        private static void ReadBoundSheet(XlsRecord record, XlsWorkbookInfo info)
        {
            var reader = new XlsDataReader(record);
            var sheet = new XlsSheetInfo
            {
                Position = (int)reader.ReadUInt32(),
                State = reader.ReadByte() & 0x03,
                Type = reader.ReadByte(),
            };

            sheet.SourceName = reader.ReadShortString();
            info.Sheets.Add(sheet);
        }

        private static void ReadNames(XlsWorkbookInfo info, XlsWorkbook workbook, XlsFormulaDecompiler decompiler)
        {
            var seen = new HashSet<string>();
            foreach (var record in info.Names)
            {
                if (record.IsMacro)
                    continue;

                int? local = null;
                if (record.SheetPosition > 0)
                {
                    var index = record.SheetPosition - 1;
                    if (index >= info.Sheets.Count || info.Sheets[index].OutputIndex < 0)
                        continue;

                    local = info.Sheets[index].OutputIndex;
                }

                string name;
                if (record.IsBuiltIn)
                {
                    if (local == null || (record.BuiltInCode != 6 && record.BuiltInCode != 7))
                        continue;

                    name = record.DisplayName;
                }
                else
                {
                    name = record.Name;
                    if (!s_validName.IsMatch(name) || s_cellLike.IsMatch(name))
                        continue;
                }

                var formula = record.Tokens == null || record.Tokens.Length == 0 ? null : decompiler.Decompile(record.Tokens, null, 0, 0, true);
                if (string.IsNullOrEmpty(formula) || !seen.Add(name.ToUpperInvariant() + "|" + local))
                    continue;

                workbook.Names.Add(new XlsDefinedName { Name = name, Formula = formula, LocalSheet = local, Hidden = record.Hidden });
            }
        }
    }
}
