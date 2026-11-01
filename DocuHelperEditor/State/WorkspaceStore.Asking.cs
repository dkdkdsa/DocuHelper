using System.Collections.Generic;
using System.Linq;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State
{
    public partial class WorkspaceStore
    {
        private readonly List<string> _askingCells = new();
        private readonly List<SheetCell> _markedCells = new();
        private string _markedFileId;

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

            RaiseNotice($"도우미 작업을 되돌렸어요. 기다리는 동안 직접 고친 {lost.Count}곳은 다시 확인해 주세요: {string.Join(", ", lost)}");
        }

        public bool IsRunTouchingFiles => _workspace.IsRunTouchingFiles;

        public void ReportMergedHandEdits()
        {
            var names = _workspace.TakeMergedHandEdits();
            if (names.Count == 0)
                return;

            RaiseNotice($"‘{string.Join("’, ‘", names)}’에서 도우미 작업 뒤에 직접 고친 내용이 있어요. 이것을 되돌리면 그 뒤의 도우미 작업도 함께 돌아가요.");
        }

        private void ApplyAskingCells(WorkspaceFile file)
        {
            if (_markedFileId == file.Id)
            {
                foreach (var cell in _markedCells)
                    cell.IsAsking = false;

                _markedCells.Clear();
                _markedFileId = null;
            }

            if (file.Id != AskingFileId || file.Paper is not ExcelPaperModel workbook)
                return;

            foreach (var reference in _askingCells)
            {
                foreach (var cell in AskingCellRefs.Find(reference, workbook))
                {
                    cell.IsAsking = true;
                    _markedCells.Add(cell);
                }
            }

            _markedFileId = file.Id;
        }
    }
}
