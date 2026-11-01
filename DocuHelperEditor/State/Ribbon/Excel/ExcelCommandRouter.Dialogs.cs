namespace DocuHelperEditor.State.Ribbon.Excel
{
    public sealed partial class ExcelCommandRouter
    {
        private void RegisterDialogs()
        {
            State.RegisterDialog(ExcelDialogIds.ConditionalFormat);
            State.RegisterDialog(ExcelDialogIds.Validation);
            State.RegisterDialog(ExcelDialogIds.ProtectSheet);
            State.RegisterDialog(ExcelDialogIds.Sort);
            State.RegisterDialog(ExcelDialogIds.NameManager);
            State.RegisterDialog(ExcelDialogIds.Paste);
            State.RegisterDialog(ExcelDialogIds.Chart);
            foreach (var id in ExcelNamedDialogs.Ids)
                State.RegisterDialog(id);
        }
    }
}
