using System.Linq;
using DocuHelper.Tools.Excel;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Mapping
{
    public partial class ExcelPaperMapper
    {
        static partial void MapView(ExcelFeatureContext context)
        {
            var view = context.Source.Worksheet?.SheetViews?.Elements<DocumentFormat.OpenXml.Spreadsheet.SheetView>().FirstOrDefault();
            var value = view?.View?.Value;
            var mode = value == DocumentFormat.OpenXml.Spreadsheet.SheetViewValues.PageLayout
                ? SheetViewMode.PageLayout
                : value == DocumentFormat.OpenXml.Spreadsheet.SheetViewValues.PageBreakPreview
                    ? SheetViewMode.PageBreak
                    : SheetViewMode.Normal;
            var zoom = view?.ZoomScale?.Value ?? 100;
            context.Model.View.Seed(context.View.Name, mode, (int)zoom);
            context.View.RowLimit = context.Model.View.RowLimitOf(context.View.Name);
        }
    }
}
