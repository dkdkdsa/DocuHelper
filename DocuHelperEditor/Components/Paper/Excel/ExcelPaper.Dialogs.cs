using System;
using System.Linq;
using DocuHelper.Editors;
using DocuHelperEditor.Services;
using DocuHelperEditor.State.Papers;
using DocuHelperEditor.State.Ribbon.Excel;

namespace DocuHelperEditor.Components.Paper.Excel
{
    public partial class ExcelPaper
    {
        private const string DialogReturnSelector = "[role='tablist'] [aria-selected='true']";

        private const string DialogApplyFailed = "적용하지 못했어요. 입력한 값과 시트 상태를 확인해 주세요.";

        private ExcelDialogContext DialogContext
        {
            get
            {
                SheetRef.TryParse(SelectedRef, out var column, out var row);
                var cell = CurrentSheet.FindCell(row, column);
                return new ExcelDialogContext(CurrentSheet.Name, SelectionText, SelectedRef, cell?.Note?.Text, cell?.EditText ?? string.Empty);
            }
        }

        private string DialogReturn => DialogReturnSelector;

        private string ApplyDialog(EditCommand command)
        {
            try
            {
                var result = Workspace.ApplyEdit(File, command);
                if (!result.IsError)
                    return null;

                AppLog.Write("Excel dialog command was rejected: " + result.Content);
                return StartsWithHangul(result.Content) ? "적용하지 못했어요. " + result.Content : DialogApplyFailed;
            }
            catch (Exception e)
            {
                AppLog.Write("Excel dialog command failed", e);
                return DialogApplyFailed;
            }
        }

        private string ApplyPaste(EditCommand command)
        {
            var problem = ApplyDialog(command);
            if (problem == null && _clip is { IsCut: true })
                _clip = null;

            return problem;
        }

        private void CloseDialog() => _router.State.CloseDialog();

        private static bool StartsWithHangul(string text) =>
            !string.IsNullOrEmpty(text) && text[0] >= '가' && text[0] <= '힣';
    }
}
