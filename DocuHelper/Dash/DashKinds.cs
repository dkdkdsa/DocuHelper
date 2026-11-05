using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Dash.Elements;
using DocuHelper.Dash.Logic;

namespace DocuHelper.Dash
{
    public static class DashKinds
    {
        public static IReadOnlyDictionary<string, Type> Elements { get; } = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["page"] = typeof(PageElement),
            ["canvas"] = typeof(CanvasElement),
            ["row"] = typeof(RowElement),
            ["column"] = typeof(ColumnElement),
            ["scroll"] = typeof(ScrollElement),
            ["popup"] = typeof(PopupElement),
            ["repeat"] = typeof(RepeatElement),
            ["text"] = typeof(TextElement),
            ["shape"] = typeof(ShapeElement),
            ["line"] = typeof(LineElement),
            ["chart"] = typeof(ChartElement),
            ["table"] = typeof(TableElement),
            ["meter"] = typeof(MeterElement),
            ["slider"] = typeof(SliderElement),
            ["rangeSlider"] = typeof(RangeSliderElement),
            ["toggle"] = typeof(ToggleElement),
            ["segmented"] = typeof(SegmentedElement),
            ["select"] = typeof(SelectElement),
            ["button"] = typeof(ButtonElement),
        };

        public static IReadOnlyDictionary<string, Type> Sources { get; } = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["excel"] = typeof(ExcelSheetSource),
            ["word"] = typeof(WordTableSource),
            ["pptTable"] = typeof(PptTableSource),
            ["pptChart"] = typeof(PptChartSource),
        };

        public static IReadOnlyDictionary<string, Type> Params { get; } = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["pick"] = typeof(PickParam),
            ["range"] = typeof(RangeParam),
            ["number"] = typeof(NumberParam),
            ["bool"] = typeof(BoolParam),
        };

        public static IReadOnlyDictionary<string, Type> Values { get; } = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["formula"] = typeof(ExpressionValue),
            ["table"] = typeof(StepsTable),
            ["pick"] = typeof(PickValue),
            ["fixed"] = typeof(FixedValue),
        };

        public static IReadOnlyDictionary<string, Type> Steps { get; } = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["alias"] = typeof(AliasStep),
            ["filter"] = typeof(FilterStep),
            ["lookup"] = typeof(LookupStep),
            ["group"] = typeof(GroupStep),
            ["share"] = typeof(ShareStep),
            ["compare"] = typeof(CompareStep),
            ["sort"] = typeof(SortStep),
            ["limit"] = typeof(LimitStep),
            ["hide"] = typeof(HideStep),
            ["total"] = typeof(TotalStep),
            ["calc"] = typeof(CalcStep),
        };

        public static IReadOnlyDictionary<string, Type> Actions { get; } = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["set"] = typeof(SetAction),
            ["reset"] = typeof(ResetAction),
            ["ask"] = typeof(AskAction),
            ["open"] = typeof(OpenAction),
            ["refresh"] = typeof(RefreshAction),
            ["pdf"] = typeof(PdfAction),
            ["popup"] = typeof(PopupAction),
        };

        public static string ElementName(Type type) => NameOf(Elements, type);

        public static string SourceName(Type type) => NameOf(Sources, type);

        public static string ParamName(Type type) => NameOf(Params, type);

        public static string ValueName(Type type) => NameOf(Values, type);

        public static string StepName(Type type) => NameOf(Steps, type);

        public static string ActionName(Type type) => NameOf(Actions, type);

        public static string NameOf(IReadOnlyDictionary<string, Type> kinds, Type type) =>
            kinds.FirstOrDefault(pair => pair.Value == type).Key;
    }
}
