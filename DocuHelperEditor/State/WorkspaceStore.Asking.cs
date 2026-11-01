using System.Collections.Generic;
using System.Linq;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State
{
    public partial class WorkspaceStore
    {
        private readonly List<string> _askingCells = new();

        public IReadOnlyList<string> AskingCells => _askingCells;

        public void SetAsking(string fileId, IEnumerable<string> cells)
        {
            _askingCells.Clear();
            _askingCells.AddRange(cells ?? Enumerable.Empty<string>());
            SetAskingFile(fileId);
        }

        public void ReportLostHandEdits()
        {
            var lost = _workspace.TakeLostHandEdits();
            if (lost.Count == 0)
                return;

            RaiseNotice($"‘{string.Join("’, ‘", lost)}’에서 직접 고친 일부는 다시 넣지 못했어요. 파일을 확인해 주세요.");
        }

        private void ApplyAskingCells(WorkspaceFile file)
        {
            if (file.Paper is not ExcelPaperModel workbook)
                return;

            foreach (var sheet in workbook.Sheets)
                foreach (var row in sheet.Rows)
                    foreach (var cell in row.Cells)
                        cell.IsAsking = false;

            if (file.Id != AskingFileId)
                return;

            foreach (var reference in _askingCells)
                foreach (var cell in AskingCellRefs.Find(reference, workbook))
                    cell.IsAsking = true;
        }
    }
}
