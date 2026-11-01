using System;
using DocuHelper.Tools.Excel;
using DocuHelperEditor.Services;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Mapping
{
    public partial class ExcelPaperMapper
    {
        private static void MapFeatures(ExcelFeatureContext context)
        {
            Isolate(() => MapView(context), nameof(MapView));
            Isolate(() => MapTables(context), nameof(MapTables));
            Isolate(() => MapConditional(context), nameof(MapConditional));
            Isolate(() => MapValidation(context), nameof(MapValidation));
            Isolate(() => MapNotes(context), nameof(MapNotes));
            Isolate(() => MapFilter(context), nameof(MapFilter));
            Isolate(() => MapOutline(context), nameof(MapOutline));
            Isolate(() => MapObjects(context), nameof(MapObjects));
            Isolate(() => MapPage(context), nameof(MapPage));
            Isolate(() => MapProtection(context), nameof(MapProtection));
        }

        private static void Isolate(Action map, string feature)
        {
            try
            {
                map();
            }
            catch (Exception e)
            {
                AppLog.Write("Excel 기능 매핑에 실패했어요: " + feature, e);
            }
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
