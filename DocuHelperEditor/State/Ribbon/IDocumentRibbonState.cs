namespace DocuHelperEditor.State.Ribbon
{
    public interface IDocumentRibbonState
    {
        IDialogPresenter Dialogs { get; }

        int? SelectedObjectId { get; set; }

        WordFormatPainter FormatPainter { get; set; }
    }
}
