using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DocuHelperEditor.State.Ribbon.Excel
{
    public sealed partial class ExcelCommandRouter
    {
        private static readonly string[] s_logical = { "IF", "IFS", "IFERROR", "IFNA", "AND", "OR", "NOT", "XOR", "SWITCH" };

        private static readonly string[] s_text = { "LEFT", "RIGHT", "MID", "LEN", "TRIM", "FIND", "SEARCH", "SUBSTITUTE", "CONCAT", "TEXTJOIN", "TEXT", "UPPER", "LOWER", "PROPER", "REPT", "VALUE", "EXACT" };

        private static readonly string[] s_dateTime = { "TODAY", "NOW", "DATE", "YEAR", "MONTH", "DAY", "WEEKDAY", "EDATE", "EOMONTH", "DATEDIF", "NETWORKDAYS", "WORKDAY" };

        private static readonly string[] s_lookup = { "VLOOKUP", "HLOOKUP", "XLOOKUP", "INDEX", "MATCH" };

        private static readonly string[] s_math = { "SUM", "SUMIF", "SUMIFS", "SUMPRODUCT", "PRODUCT", "ROUND", "ROUNDUP", "ROUNDDOWN", "INT", "TRUNC", "MOD", "ABS", "POWER", "SQRT" };

        private static readonly string[] s_statistics = { "AVERAGE", "AVERAGEIF", "AVERAGEIFS", "COUNT", "COUNTA", "COUNTIF", "COUNTIFS", "MAX", "MAXIFS", "MIN", "MINIFS", "MEDIAN", "RANK", "LARGE", "SMALL" };

        private static readonly string[] s_information = { "ISNUMBER", "ISTEXT", "ISBLANK", "ISERROR", "ISERR", "ISNA", "NA" };

        private void RegisterFormula()
        {
            AddView("excel.formula.insertfn", _ => BeginFormula(string.Empty)).Unless(Editable);
            Alias("excel.formula.autosum", "excel.home.autosum");
            Functions("excel.formula.logical", s_logical);
            Functions("excel.formula.text", s_text);
            Functions("excel.formula.datetime", s_dateTime);
            Functions("excel.formula.lookup", s_lookup);
            Functions("excel.formula.math", s_math);
            AddView("excel.formula.more", arg => BeginFormula(arg))
                .Unless(Editable)
                .MenuFrom(MoreFunctionsMenu);
            Dialog("excel.formula.namemanager", "define_name");
            SheetOption("excel.formula.showformulas", "set_sheet_view", "show_formulas", () => State.Sheet.ShowFormulas);
            AddView("excel.formula.errorcheck", arg => CheckErrors(arg))
                .Unless(Editable)
                .MenuFrom(() => new[]
                {
                    Entry("errors", "오류 검사…", "#REF!, #DIV/0! 같은 오류가 난 칸을 차례로 보여 줘요"),
                    RibbonMenus.Separator(),
                    Entry("circular", "순환 참조", "이 앱은 순환 참조를 막아서 대개 없어요"),
                });
            Tool("excel.formula.calcoptions", "set_workbook_options", arg => CalculationArgs(arg))
                .MenuFrom(() => new[]
                {
                    Entry("auto", "자동"),
                    Entry("auto_except_tables", "데이터 표만 수동"),
                    Entry("manual", "수동"),
                    RibbonMenus.Separator(),
                    Entry("now:workbook", "지금 계산", "F9"),
                    Entry("now:sheet", "시트 계산", "Shift+F9"),
                });
        }

        private void Functions(string id, string[] names)
        {
            AddView(id, arg => BeginFormula(arg))
                .Unless(Editable)
                .MenuFrom(() =>
                {
                    var items = names.Select(n => Entry(n, n)).ToList();
                    items.Add(RibbonMenus.Separator());
                    items.Add(Entry(string.Empty, "함수 삽입…", "수식 입력줄에서 함수를 골라 적어요"));
                    return items;
                });
        }

        private IReadOnlyList<RibbonMenuItem> MoreFunctionsMenu()
        {
            var items = new List<RibbonMenuItem> { RibbonMenus.Heading("통계") };
            items.AddRange(s_statistics.Select(n => Entry(n, n)));
            items.Add(RibbonMenus.Heading("정보"));
            items.AddRange(s_information.Select(n => Entry(n, n)));
            return items;
        }

        private Task BeginFormula(string name)
        {
            _surface.BeginFormula(string.IsNullOrEmpty(name) ? "=" : "=" + name + "(");
            return Task.CompletedTask;
        }

        private Task CheckErrors(string arg)
        {
            if (arg == "circular")
                _surface.Message("이 시트에는 순환 참조가 없어요. 이 앱은 자기 자신을 가리키는 수식을 막아요.");
            else
                _surface.SelectNext("error");

            return Task.CompletedTask;
        }

        private Dictionary<string, object> CalculationArgs(string arg)
        {
            if (string.IsNullOrEmpty(arg))
                return null;

            return arg.StartsWith("now:", System.StringComparison.Ordinal)
                ? Workbook(("calculate_now", arg.Substring(4)), ("sheet", State.SheetName))
                : Workbook(("calculation", arg));
        }
    }
}
