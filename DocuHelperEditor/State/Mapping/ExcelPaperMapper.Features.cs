using DocuHelper.Tools.Excel;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Mapping
{
    public partial class ExcelPaperMapper
    {
        private static void MapFeatures(ExcelFeatureContext context)
        {
            MapView(context);
            MapTables(context);
            MapConditional(context);
            MapValidation(context);
            MapNotes(context);
            MapFilter(context);
            MapOutline(context);
            MapObjects(context);
            MapPage(context);
            MapProtection(context);
        }

        static partial void MapWorkbook(ExcelBook book, ExcelPaperModel model);

        static partial void MapView(ExcelFeatureContext context);

        static partial void MapTables(ExcelFeatureContext context);

        static partial void MapConditional(ExcelFeatureContext context);

        static partial void MapValidation(ExcelFeatureContext context);

        static partial void MapNotes(ExcelFeatureContext context);

        static partial void MapFilter(ExcelFeatureContext context);

        static partial void MapOutline(ExcelFeatureContext context);

        static partial void MapObjects(ExcelFeatureContext context);

        static partial void MapPage(ExcelFeatureContext context);

        static partial void MapProtection(ExcelFeatureContext context);
    }
}
