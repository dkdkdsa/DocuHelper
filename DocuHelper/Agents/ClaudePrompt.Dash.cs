namespace DocuHelper.Agents
{
    public static partial class ClaudePrompt
    {
        internal const string DashRules = """
            A dashboard (.dash) is one page the viewer can play with: elements on the page read values and tables by name, and controls change params. You never compute numbers for it; the app reads the source files and calculates everything again whenever a param changes.
            Build a dashboard in this order: read the source files with their own tools (read_sheet, read_document, read_slides) to learn sheet names and columns; add_dash_item kind source (excel with file, sheet and header row, word for Word or 한글 tables, pptTable, pptChart); kind param (pick, range, number, bool) for what the viewer may change; kind value (table with steps, formula, pick, fixed); then the elements. Check each table with preview_dash before you use it, and call read_dash to see the whole dashboard and its problems.
            Table steps are a closed list: alias (merge spellings of one name), filter (column with param, eq, in or min/max; part "day" filters a date column by day of month), lookup, group (by, sum, count), share, compare (repeat the earlier steps on another source and add its columns with a prefix), sort, limit (with an optional rest row), hide, total, calc (a row formula over column names). A formula value is an Excel formula over names: params, other values, a range param's 이름.시작 and 이름.끝, and table columns as 표이름.열이름, e.g. SUM(기간표.금액) or 매출/목표. Names used in formulas must start with a letter and have no spaces.
            Text elements show values by writing «이름» in the text; the app fills them in, formats them with the value's format (man, won, gun, pct, day:6) and fixes the Korean particle after them, so write «매출»을 or «1등 거래처»예요 naturally. Inside a repeat template use «item.열이름».
            Page children need style x, y, w, h in page pixels (the A4 landscape page is 1123 x 794); keep 48 px margins and line things up. Put related elements in a row or column (style gap, padding) so they flow. Use when (an Excel condition such as 보기="거래처별" or 달성률>=1) to show one of two texts instead of writing negative numbers.
            Controls: slider (number param), rangeSlider (range param), toggle (bool), segmented or select (pick), button with an action (set, reset, ask, open, refresh, pdf). A chart with range and rangeControl gets a period slider right under it and dims the days outside it.
            update_dash_item takes a merge patch: send only the fields that change. Every write needs label like the other tools.
            """;
    }
}
