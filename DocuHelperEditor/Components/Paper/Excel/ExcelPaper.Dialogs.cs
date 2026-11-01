using System;
using System.Linq;
using DocuHelper.Editors;
using DocuHelperEditor.Services;
using DocuHelperEditor.State.Ribbon.Excel;

namespace DocuHelperEditor.Components.Paper.Excel
{
    public partial class ExcelPaper
    {
        private const string DialogReturnSelector = "[role='tablist'] [aria-selected='true']";

        private const string DialogApplyFailed = "적용하지 못했어요. 입력한 값과 시트 상태를 확인해 주세요.";

        private ExcelDialogContext DialogContext => new(CurrentSheet.Name, SelectionText, SelectedRef);

        private string DialogReturn => DialogReturnSelector;

        private string ApplyDialog(EditCommand command)
        {
            try
            {
                var result = Workspace.ApplyEdit(File, command);
                if (!result.IsError)
                    return null;

                AppLog.Write("Excel dialog command was rejected: " + result.Content);
                return IsKorean(result.Content) ? "적용하지 못했어요. " + result.Content : DialogApplyFailed;
            }
            catch (Exception e)
            {
                AppLog.Write("Excel dialog command failed", e);
                return DialogApplyFailed;
            }
        }

        private void CloseDialog() => _router.State.CloseDialog();

        private static bool IsKorean(string text) => !string.IsNullOrEmpty(text) && text.Any(ch => ch >= '가' && ch <= '힣');
    }
}
