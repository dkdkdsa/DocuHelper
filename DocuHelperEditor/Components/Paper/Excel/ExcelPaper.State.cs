using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DocuHelperEditor.State.Ribbon.Excel;
using DocuHelper.Editors;
using DocuHelper.Tools.Excel;
using DocuHelperEditor.Services;
using DocuHelperEditor.State;
using DocuHelperEditor.State.Papers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace DocuHelperEditor.Components.Paper.Excel
{
    public partial class ExcelPaper
    {
        private const string SheetNamePrefix = "시트";
        private const string PointAfter = "=(,+-*/^&<>:;";

        private readonly string _uid = "xl" + Guid.NewGuid().ToString("N").Substring(0, 8);
        private ExcelPaperModel _fallback;
        private WorkspaceFile _file;
        private string _editSheet;
        private string _editRef;
        private string _editError;
        private bool _editInCell;
        private bool _editInBar;
        private bool _editTyped;
        private string _draft = string.Empty;
        private string _sinkSeed = string.Empty;
        private int _editVersion;
        private bool _focusSink;
        private string _scrollTarget;
        private bool _dragging;
        private bool _pointing;
        private int _pointRow;
        private int _pointColumn;
        private int _pointStart = -1;
        private string _pointText;
        private SheetRegion? _pointRegion;
        private SheetRegion? _fillPreview;
        private Clip _clip;
        private bool _findOpen;
        private bool _findReplace;
        private string _findQuery = string.Empty;
        private string _replaceWith = string.Empty;
        private string _findNote;
        private int _findFocus;
        private int _matchIndex = -1;
        private readonly List<(int Row, int Column)> _matches = new();
        private SheetTabs _tabs;
        private SheetMenuRequest _menu;
        private IReadOnlyList<SheetMenuItem> _menuItems = Array.Empty<SheetMenuItem>();
        private Confirm _confirm;

        [Parameter]
        public WorkspaceFile File { get; set; }

        private string SinkId => _uid + "-cell";

        private string RefId => _uid + "-ref";

        private string ContentId => _uid + "-fx";

        private string FindId => _uid + "-find";

        private ExcelPaperModel Model
        {
            get
            {
                var model = File?.Paper as ExcelPaperModel;
                if (model == null)
                {
                    _fallback ??= ExcelPaperModel.CreateEmpty();
                    model = _fallback;
                }

                model.EnsureSheet();
                return model;
            }
        }

        private int ActiveIndex => Math.Clamp(Model.ActiveSheetIndex, 0, Model.Sheets.Count - 1);

        private Sheet CurrentSheet => Model.Sheets[ActiveIndex];

        private int MaxRow => CurrentSheet.DisplayRowCount - 1;

        private int MaxColumn => Math.Max(0, CurrentSheet.Columns.Count - 1);

        private string SelectedRef => Clamp(CurrentSheet.SelectedRef) ?? SheetRef.Make(0, 0);

        private string ExtentRef
        {
            get
            {
                var extent = Clamp(CurrentSheet.ExtentRef);
                return extent == null || extent == SelectedRef ? null : extent;
            }
        }

        private SheetRegion Selection
        {
            get
            {
                SheetRef.TryParse(SelectedRef, out var column, out var row);
                var region = SheetRef.TryParse(ExtentRef, out var extentColumn, out var extentRow)
                    ? SheetRegion.Span(row, column, extentRow, extentColumn)
                    : new SheetRegion(row, column, row, column);
                var whole = (region.Left == 0 && region.Right >= MaxColumn) || (region.Top == 0 && region.Bottom >= MaxRow);
                return whole ? region : CurrentSheet.Expand(region);
            }
        }

        private bool WholeRows => Selection.Left == 0 && Selection.Right >= MaxColumn;

        private bool WholeColumns => Selection.Top == 0 && Selection.Bottom >= MaxRow;

        private bool IsMergedSelection => CurrentSheet.Merges.Any(m => m.Intersects(Selection));

        private string MenuLabel => _menu?.Kind switch
        {
            "tab" => "시트 메뉴",
            "row" => "행 메뉴",
            "column" => "열 메뉴",
            _ => "칸 메뉴",
        };

        private string SelectionText => Selection.ToRef();

        private SheetCell SelectedCell => CurrentSheet.FindCell(SelectedRef);

        private SheetRowKind SelectedRowKind
        {
            get
            {
                if (!SheetRef.TryParse(SelectedRef, out _, out var row))
                    return SheetRowKind.Normal;

                return CurrentSheet.FindRow(row)?.Kind ?? SheetRowKind.Normal;
            }
        }

        private bool IsLocked => File?.Activity == FileActivity.Editing;

        private bool IsAskingFile => File?.Activity == FileActivity.Asking;

        private bool ShowAskLegend => IsAskingFile && Model.HasAskingCells;

        private bool IsEditing => _editRef != null;

        private string BarText => IsEditing ? _draft : (SelectedCell?.EditText ?? string.Empty);

        private SheetRegion? ClipRegion => _clip != null && _clip.Sheet == CurrentSheet.Name ? _clip.Region : null;

        private string FindStatus
        {
            get
            {
                if (string.IsNullOrEmpty(_findQuery))
                    return string.Empty;

                if (_matches.Count == 0)
                    return "찾는 내용이 없어요";

                return _matchIndex < 0 ? $"{_matches.Count}개 있어요" : $"{_matches.Count}개 중 {_matchIndex + 1}번째";
            }
        }

        private string HighlightChangeId
        {
            get
            {
                var id = Workspace.SelectedChangeId;
                if (string.IsNullOrEmpty(id) || File?.Changes == null || Workspace.ActiveFile != File)
                    return null;

                return File.Changes.Any(c => c != null && c.Id == id) ? id : null;
            }
        }

        private HashSet<string> LiveChangeIds => File?.Changes == null
            ? new HashSet<string>()
            : new HashSet<string>(File.Changes.Where(c => c != null && !string.IsNullOrEmpty(c.Id)).Select(c => c.Id));

        protected override void OnParametersSet()
        {
            if (_router != null)
                _router.File = File;

            if (File == _file)
                return;

            _file = File;
            _fallback = null;
            _editError = null;
            _clip = null;
            _painter = null;
            _fillPreview = null;
            _findOpen = false;
            _matches.Clear();
            ResetEdit();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (_root.Id != null)
            {
                _wheel ??= new WheelZoom(JS, ZoomStepAsync);
                await _wheel.AttachAsync(_root);
            }

            if (_focusSink)
            {
                _focusSink = false;
                await RunJsAsync("docuHelper.focus", "#" + SinkId);
            }

            if (_scrollTarget != null)
            {
                var target = _scrollTarget;
                _scrollTarget = null;
                await RunJsAsync("docuHelper.scrollIntoView", target);
            }

            if (IsLocked && IsEditing)
            {
                ResetEdit();
                StateHasChanged();
                return;
            }

            if (RevealSelectedChange())
                return;

            SyncSelectionLabel();
        }

        private string Clamp(string cellRef)
        {
            if (!SheetRef.TryParse(cellRef, out var column, out var row))
                return null;

            return SheetRef.Make(Math.Clamp(column, 0, MaxColumn), Math.Clamp(row, 0, MaxRow));
        }

        private bool RevealSelectedChange()
        {
            var changeId = HighlightChangeId;
            var model = Model;
            if (changeId == null)
            {
                model.RevealedChangeId = null;
                return false;
            }

            if (changeId == model.RevealedChangeId && Workspace.ChangePickVersion == model.RevealedPickVersion)
                return false;

            model.RevealedChangeId = changeId;
            model.RevealedPickVersion = Workspace.ChangePickVersion;
            if (!model.TryLocateChange(changeId, out var sheetIndex, out var cellRef))
                return false;

            if (IsEditing)
            {
                CommitEdit();
                model = Model;
                if (!model.TryLocateChange(changeId, out sheetIndex, out cellRef))
                    return false;
            }

            model.ActiveSheetIndex = sheetIndex;
            var sheet = model.Sheets[sheetIndex];
            if (!string.IsNullOrEmpty(cellRef))
            {
                sheet.SelectedRef = cellRef;
                sheet.ExtentRef = null;
            }

            _scrollTarget = "[data-change=\"" + changeId + "\"]";
            Workspace.SetSelection(LabelFor(sheet, SelectedRef));
            return true;
        }

        private void SyncSelectionLabel()
        {
            if (File == null || Workspace.ActiveFile != File || !string.IsNullOrEmpty(Workspace.SelectionLabel))
                return;

            Workspace.SetSelection(LabelFor(CurrentSheet, SelectionText));
        }

        private void SyncLabel() => Workspace.SetSelection(LabelFor(CurrentSheet, SelectionText));

        private static string LabelFor(Sheet sheet, string cellRef) => sheet.Name + " " + cellRef;
    }
}
