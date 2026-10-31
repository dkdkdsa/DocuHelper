using System;
using System.Threading.Tasks;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Ribbon.Excel
{
    public interface IExcelSurface
    {
        ExcelPaperModel Model { get; }

        Sheet Sheet { get; }

        SheetRegion Selection { get; }

        string ActiveRef { get; }

        SheetCell ActiveCell { get; }

        SheetRowKind ActiveRowKind { get; }

        bool IsEditing { get; }

        bool IsMergedSelection { get; }

        bool WholeRows { get; }

        bool WholeColumns { get; }

        bool ShowFormulaBar { get; set; }

        bool HasClip { get; }

        string ClipSheet { get; }

        string ClipRef { get; }

        bool ClipIsCut { get; }

        bool IsPainting { get; }

        bool Prepare();

        void Refocus();

        void Message(string text);

        void Confirm(string text, string button, Action run);

        void Select(SheetRegion region);

        string NextSheetName();

        Task RunCommandAsync(string command);

        Task CopyAsync(bool cut);

        Task PasteAsync();

        void SortTable(bool descending);

        void AutoSum(string function);

        void ToggleMerge();

        void OpenFind(bool replace);

        void FocusNameBox();

        void BeginFormula(string text);

        void BeginSheetRename(int index);

        bool SelectNext(string kind);

        void ArmPainter();

        void AfterPaste(bool transposed);

        void DisarmPainter();
    }
}
