using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    public sealed class ExcelBook
    {
        private List<ExcelSheet> _sheets;
        private List<string> _strings;
        private HashSet<int> _richStrings;
        private Dictionary<string, int> _stringIndex;
        private ExcelStyles _styles;

        public ExcelBook(SpreadsheetDocument document)
        {
            Document = document;
        }

        public SpreadsheetDocument Document { get; }

        public WorkbookPart WorkbookPart => Document.WorkbookPart;

        public IReadOnlyList<ExcelSheet> Sheets => _sheets ??= LoadSheets();

        public ExcelStyles Styles => _styles ??= new ExcelStyles(WorkbookPart);

        public ExcelSheet FindSheet(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            var key = name.Trim();
            if (key.EndsWith("!", StringComparison.Ordinal))
                key = key.Substring(0, key.Length - 1);

            if (key.Length > 1 && key[0] == '\'' && key[^1] == '\'')
                key = key.Substring(1, key.Length - 2).Replace("''", "'");

            return Sheets.FirstOrDefault(s => s.Name == key)
                ?? Sheets.FirstOrDefault(s => string.Equals(s.Name, key, StringComparison.OrdinalIgnoreCase))
                ?? Sheets.FirstOrDefault(s => string.Equals(s.Name.Trim(), key, StringComparison.OrdinalIgnoreCase));
        }

        public string SheetList() => string.Join(", ", Sheets.Select(s => "‘" + s.Name + "’"));

        public string SharedString(int index)
        {
            var strings = Strings();
            return index >= 0 && index < strings.Count ? strings[index] : string.Empty;
        }

        public ExcelValue ReadValue(Cell cell)
        {
            if (cell == null)
                return ExcelValue.Blank;

            var type = cell.DataType?.InnerText;
            if (type == "inlineStr")
                return cell.InlineString != null ? ExcelValue.FromText(ItemText(cell.InlineString)) : ExcelValue.FromText(cell.CellValue?.Text ?? string.Empty);

            var raw = cell.CellValue?.Text;
            if (raw == null)
                return ExcelValue.Blank;

            switch (type)
            {
                case "s":
                    return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
                        ? ExcelValue.FromText(SharedString(index))
                        : ExcelValue.Blank;
                case "str":
                    return ExcelValue.FromText(raw);
                case "b":
                    return ExcelValue.FromBoolean(raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase));
                case "e":
                    return ExcelValue.FromError(raw);
                case "d":
                    return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date)
                        ? ExcelValue.FromNumber(ExcelNumberFormat.ToSerial(date))
                        : ExcelValue.FromText(raw);
                default:
                    if (raw.Length == 0)
                        return ExcelValue.Blank;

                    return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                        ? ExcelValue.FromNumber(number)
                        : ExcelValue.FromText(raw);
            }
        }

        public ExcelCellStyle StyleOf(Cell cell) => Styles.Describe(cell?.StyleIndex?.Value ?? 0);

        public string DisplayText(ExcelValue value, ExcelCellStyle style) => value.Kind switch
        {
            ExcelValueKind.Number => ExcelNumberFormat.Format(value.Number, style?.FormatCode),
            ExcelValueKind.Boolean => value.Boolean ? "TRUE" : "FALSE",
            ExcelValueKind.Text or ExcelValueKind.Error => value.Text,
            _ => string.Empty,
        };

        internal int AddSharedString(string text)
        {
            var strings = Strings();
            if (_stringIndex == null)
            {
                _stringIndex = new Dictionary<string, int>(StringComparer.Ordinal);
                for (var i = 0; i < strings.Count; i++)
                {
                    if (!_richStrings.Contains(i))
                        _stringIndex.TryAdd(strings[i], i);
                }
            }

            if (_stringIndex.TryGetValue(text, out var existing))
                return existing;

            var part = WorkbookPart.SharedStringTablePart ?? WorkbookPart.AddNewPart<SharedStringTablePart>();
            part.SharedStringTable ??= new SharedStringTable();
            var element = new Text(text);
            if (text.Length > 0 && (char.IsWhiteSpace(text[0]) || char.IsWhiteSpace(text[^1]) || text.Contains('\n')))
                element.Space = SpaceProcessingModeValues.Preserve;

            part.SharedStringTable.Append(new SharedStringItem(element));
            var index = strings.Count;
            strings.Add(text);
            _stringIndex[text] = index;
            part.SharedStringTable.UniqueCount = (uint)strings.Count;
            part.SharedStringTable.Count = null;
            return index;
        }

        internal void SetText(Cell cell, string text)
        {
            cell.CellFormula = null;
            cell.InlineString = null;
            cell.CellValue = new CellValue(AddSharedString(text).ToString(CultureInfo.InvariantCulture));
            cell.DataType = CellValues.SharedString;
        }

        internal static void SetNumber(Cell cell, double number)
        {
            cell.CellFormula = null;
            cell.InlineString = null;
            cell.CellValue = new CellValue(ExcelValue.Invariant(number));
            cell.DataType = null;
        }

        internal static void SetBoolean(Cell cell, bool value)
        {
            cell.CellFormula = null;
            cell.InlineString = null;
            cell.CellValue = new CellValue(value ? "1" : "0");
            cell.DataType = CellValues.Boolean;
        }

        internal static void SetFormula(Cell cell, string formula)
        {
            cell.InlineString = null;
            cell.CellValue = null;
            cell.DataType = null;
            cell.CellFormula = new CellFormula(formula);
        }

        internal void MarkCalculationNeeded()
        {
            var workbook = WorkbookPart.Workbook;
            var calculation = workbook.CalculationProperties;
            if (calculation == null)
            {
                calculation = new CalculationProperties();
                workbook.CalculationProperties = calculation;
            }

            calculation.FullCalculationOnLoad = true;
            var chain = WorkbookPart.CalculationChainPart;
            if (chain != null)
                WorkbookPart.DeletePart(chain);
        }

        internal ExcelSheet AddSheet(string name, ExcelSheet after)
        {
            var workbookPart = WorkbookPart;
            var workbook = workbookPart.Workbook;
            var sheets = workbook.Sheets;
            if (sheets == null)
            {
                sheets = new Sheets();
                workbook.Sheets = sheets;
            }

            var entries = sheets.Elements<Sheet>().ToList();
            var part = workbookPart.AddNewPart<WorksheetPart>();
            part.Worksheet = new Worksheet(new SheetData());
            var sheetId = entries.Select(s => s.SheetId?.Value ?? 0u).DefaultIfEmpty(0u).Max() + 1;
            var entry = new Sheet { Name = name, SheetId = sheetId, Id = workbookPart.GetIdOfPart(part) };

            int position;
            if (after != null)
            {
                after.Entry.InsertAfterSelf(entry);
                position = after.Position + 1;
            }
            else
            {
                sheets.Append(entry);
                position = entries.Count;
            }

            if (position < entries.Count)
                ShiftSheetPositions(position);

            _sheets = null;
            return Sheets.First(s => ReferenceEquals(s.Entry, entry));
        }

        private static string ItemText(OpenXmlElement item)
        {
            var builder = new StringBuilder();
            foreach (var child in item.ChildElements)
            {
                if (child is Text text)
                    builder.Append(text.Text);
                else if (child is Run run)
                    builder.Append(run.Text?.Text);
            }

            return builder.ToString();
        }

        private void ShiftSheetPositions(int position)
        {
            var workbook = WorkbookPart.Workbook;
            foreach (var definedName in workbook.DefinedNames?.Elements<DefinedName>() ?? Enumerable.Empty<DefinedName>())
            {
                if (definedName.LocalSheetId?.Value is uint local && local >= position)
                    definedName.LocalSheetId = local + 1;
            }

            foreach (var view in workbook.BookViews?.Elements<WorkbookView>() ?? Enumerable.Empty<WorkbookView>())
            {
                if (view.ActiveTab?.Value is uint active && active >= position)
                    view.ActiveTab = active + 1;

                if (view.FirstSheet?.Value is uint first && first >= position)
                    view.FirstSheet = first + 1;
            }
        }

        private List<ExcelSheet> LoadSheets()
        {
            var sheets = new List<ExcelSheet>();
            var workbookPart = WorkbookPart;
            var entries = workbookPart?.Workbook?.Sheets?.Elements<Sheet>();
            if (entries == null)
                return sheets;

            var position = 0;
            foreach (var entry in entries)
            {
                var id = entry.Id?.Value;
                if (id != null && workbookPart.TryGetPartById(id, out var part) && part is WorksheetPart worksheetPart && worksheetPart.Worksheet != null)
                    sheets.Add(new ExcelSheet(entry, worksheetPart, position, sheets.Count));

                position++;
            }

            return sheets;
        }

        private List<string> Strings()
        {
            if (_strings != null)
                return _strings;

            _strings = new List<string>();
            _richStrings = new HashSet<int>();
            var table = WorkbookPart?.SharedStringTablePart?.SharedStringTable;
            if (table == null)
                return _strings;

            foreach (var item in table.Elements<SharedStringItem>())
            {
                if (item.Elements<Run>().Any())
                    _richStrings.Add(_strings.Count);

                _strings.Add(ItemText(item));
            }

            return _strings;
        }
    }
}
