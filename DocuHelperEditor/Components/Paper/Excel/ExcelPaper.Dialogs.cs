using DocuHelper.Editors;

namespace DocuHelperEditor.Components.Paper.Excel
{
    public partial class ExcelPaper
    {
        private void ApplyDialog(EditCommand command)
        {
            if (Edit(command))
                _router.State.CloseDialog();
        }

        private void CloseDialog() => _router.State.CloseDialog();
    }
}
