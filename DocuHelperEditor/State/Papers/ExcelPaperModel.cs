using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DocuHelperEditor.State.Papers
{
    public class ExcelPaperModel : PaperModel
    {
        private const string SheetNamePrefix = "시트";

        public List<Sheet> Sheets { get; set; } = new();

        public int ActiveSheetIndex { get; set; }

        public string RevealedChangeId { get; set; }

        public int RevealedPickVersion { get; set; }

        public Sheet ActiveSheet => Sheets.Count == 0 ? null : Sheets[Math.Clamp(ActiveSheetIndex, 0, Sheets.Count - 1)];

        public string SelectedCellRef
        {
            get => ActiveSheet?.SelectedRef;
            set
            {
                var sheet = ActiveSheet;
                if (sheet != null)
                    sheet.SelectedRef = value;
            }
        }

        public SheetCell SelectedCell => ActiveSheet?.FindCell(SelectedCellRef);

        public override bool IsBlank => Sheets.All(s => s.Rows.All(r => r.Cells.All(c => string.IsNullOrEmpty(c.Text))));

        public bool HasAskingCells => Sheets.Any(s => s.Rows.Any(r => r.Cells.Any(c => c.IsAsking)));

        public static ExcelPaperModel CreateEmpty()
        {
            var model = new ExcelPaperModel();
            model.Sheets.Add(Sheet.CreateEmpty(SheetName(1)));
            return model;
        }

        public Sheet EnsureSheet()
        {
            if (Sheets.Count == 0)
                Sheets.Add(Sheet.CreateEmpty(SheetName(1)));

            return ActiveSheet;
        }

        public Sheet AddSheet()
        {
            var number = Sheets.Count + 1;
            while (Sheets.Any(s => s.Name == SheetName(number)))
                number++;

            var sheet = Sheet.CreateEmpty(SheetName(number));
            sheet.IsNew = true;
            Sheets.Add(sheet);
            ActiveSheetIndex = Sheets.Count - 1;
            return sheet;
        }

        public bool TryLocateChange(string changeId, out int sheetIndex, out string cellRef)
        {
            sheetIndex = -1;
            cellRef = null;
            if (string.IsNullOrEmpty(changeId))
                return false;

            for (var i = 0; i < Sheets.Count; i++)
            {
                var sheet = Sheets[i];
                if (sheet.ChangeAnchors.TryGetValue(changeId, out var anchor) && !string.IsNullOrEmpty(anchor))
                {
                    sheetIndex = i;
                    cellRef = anchor;
                    return true;
                }

                var cell = sheet.Rows.SelectMany(r => r.Cells).FirstOrDefault(c => c.HasChange(changeId));
                if (cell != null)
                {
                    sheetIndex = i;
                    cellRef = cell.Ref;
                    return true;
                }

                var rowIndex = sheet.Rows.FindIndex(r => r.ChangeId == changeId);
                if (rowIndex >= 0)
                {
                    sheetIndex = i;
                    cellRef = SheetRef.Make(0, rowIndex);
                    return true;
                }

                if (sheet.ChangeId == changeId)
                {
                    sheetIndex = i;
                    cellRef = sheet.SelectedRef;
                    return true;
                }
            }

            return false;
        }

        private static string SheetName(int number) => SheetNamePrefix + number.ToString(CultureInfo.InvariantCulture);
    }
}
