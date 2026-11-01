using DocuHelper.Editors;
using DocuHelperEditor.State.Ribbon.Excel;

namespace DocuHelperEditor.Components.Paper.Excel
{
    public partial class ExcelPaper
    {
        private void ApplyDialog(EditCommand command)
        {
            if (Edit(command))
                _router.State.CloseDialog();
        }

        private ExcelDialogContext DialogContext => new(CurrentSheet.Name, SelectionText, SelectedRef);

        private string CommitForm(EditCommand command) => Edit(command) ? null : _editError ?? "적용하지 못했어요.";

        private void CloseDialog() => _router.State.CloseDialog();
    }
}
