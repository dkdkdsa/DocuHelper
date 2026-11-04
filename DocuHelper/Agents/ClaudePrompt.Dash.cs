namespace DocuHelper.Agents
{
    public static partial class ClaudePrompt
    {
        private const string DashNote = """
            Dashboards (.dash): before you add or change dashboard items, read the dashboard page of the guide (read_guide) once in the conversation; it has the build order, the item formats and every rule, and they are not repeated here. Build in a few large add_dash_item calls with items, check tables with preview_dash, and fix what read_dash lists before you finish.
            """;

        internal const string DashRules = """
            A dashboard (.dash) is one page the viewer can play with: elements on the page read values and tables by name, and controls change params. You never compute numbers for it; the app reads the source files and calculates everything again whenever a param changes.
            Build a dashboard in this order: read the source files with their own tools (read_sheet, read_document, read_slides) to learn sheet names and columns; add_dash_item kind source (excel with file, sheet and header row, word for Word or 한글 tables, pptTable, pptChart); kind param (pick, range, number, bool) for what the viewer may change; kind value (table with steps, formula, pick, fixed); then the elements. Check each table with preview_dash before you use it, and call read_dash to see the whole dashboard and its problems.
            Table steps are a closed list: alias (merge spellings of one name), filter (column with param, eq, in or min/max; part "day" filters a date column by day of month), lookup, group (by, sum, count), share, compare (repeat the earlier steps on another source and add its columns with a prefix), sort, limit (with an optional rest row), hide, total, calc (a row formula over column names). A formula value is an Excel formula over names: params, other values, a range param's 이름.시작 and 이름.끝, and table columns as 표이름.열이름, e.g. SUM(기간표.금액) or 매출/목표. Names used in formulas must start with a letter and have no spaces.
            Text elements show values by writing «이름» in the text; the app fills them in, formats them with the value's format (man, won, gun, pct, day:6) and fixes the Korean particle after them, so write «매출»을 or «1등 거래처»예요 naturally. Inside a repeat template use «item.열이름».
            Page children need style x, y, w, h in page pixels (the A4 landscape page is 1123 x 794); keep 48 px margins and line things up. Put related elements in a row or column (style gap, padding) so they flow. Use when (an Excel condition such as 보기="거래처별" or 달성률>=1) to show one of two texts instead of writing negative numbers.
            Controls: slider (number param), rangeSlider (range param), toggle (bool), segmented or select (pick), button with an action (set, reset, ask, open, refresh, pdf). A chart with range and rangeControl gets a period slider right under it and dims the days outside it.
            update_dash_item takes a merge patch: send only the fields that change. add_dash_item and update_dash_item also take items, a list of several items or changes applied in order: build a dashboard in a few large calls (all sources and params, then all values, then all elements), not one call per item. Every write needs label like the other tools.
            """;

        private const string DashGuideStart = """
            Dashboard guide
            A dashboard (.dash) is one A4 landscape page, 1123 x 794 px, that works like a small app. Sources read tables from the files in the folder, params are what the viewer may change, values are tables and numbers computed from them, and elements on the page show values and change params. The app recalculates everything when a param or a source file changes. The viewer's choices are not saved: the file keeps the starting values.
            People switch between three views on the 보기 tab: 보기 (press the buttons and move the sliders), 고치기 (place, move and edit elements like PowerPoint shapes; double-click a text to change it) and 데이터 (each table step by step next to its source rows).

            Steps
            1. Read every source file with its own tool (read_sheet, read_document, read_slides) and note the sheet name, the header row and the exact column names. Dates must be real dates and amounts real numbers.
            2. create_file with a name such as "6월 매출 대시보드.dash"; the dashboard tools arrive on your next step.
            3. add_dash_item with items, a label per call: first the sources and params, then the values, then the elements, so the whole build takes a handful of calls. Check the tables with preview_dash before elements use them, and don't re-read the dashboard after every change.
            4. Call read_dash at the end and fix every problem it lists. In finish, say which files the dashboard reads and what the viewer can change.

            Rules
            """;

        private const string DashGuideItems = """

            Items as written in the file. Pass one as item; "type" picks the kind.
            Sources (kind source):
            {"type":"excel","name":"유월","file":"6월 매출정리.xlsx","sheet":"6월 매출"}, with "header": the sheet row number of the header when it is not the first row, and "range": "A3:F200" to read only part of the sheet. A two-row header with merged group cells (1학기 over 1차(일시), 2차(일시)) is found on its own and read as columns such as 1학기 1차(일시); "headerRows": 1 or 2 overrides that. Read the source as it is; don't add helper sheets or reshape the user's workbook for the dashboard.
            {"type":"word","name":"목표표","file":"거래처 목표.docx","table":1} reads a Word or 한글 table, counted from 1; "noHeader": true when the first row is already data.
            {"type":"pptTable","name":"실적표","file":"상반기 실적.pptx","slide":2,"table":1} and {"type":"pptChart","name":"실적차트","file":"상반기 실적.pptx","slide":3,"chart":1}, counted from 1.
            Params (kind param), each with name and label:
            {"type":"range","name":"기간","label":"기간","min":1,"max":30,"from":1,"to":30,"format":"day:6"} has two handles; formulas use 기간.시작 and 기간.끝.
            {"type":"pick","name":"거래처","label":"거래처","optionsFrom":"거래처목록.거래처","all":"전체","default":"전체"} takes its choices from a table column, or from "options": ["거래처별","품목별"]; all adds a choice that keeps every row.
            {"type":"number","name":"목표","label":"목표","min":10000000,"max":100000000,"step":1000000,"default":50000000,"format":"man"}
            {"type":"bool","name":"비교","label":"5월과 비교","default":true}
            Values (kind value):
            {"type":"table","name":"기간표","source":"유월","steps":[{"type":"alias","column":"거래처","map":{"(주)한빛상사":"한빛상사"}},{"type":"filter","column":"거래처","param":"거래처"},{"type":"filter","column":"날짜","param":"기간","part":"day"}]}
            {"type":"table","name":"거래처별","source":"기간표","steps":[{"type":"group","by":["거래처"],"sum":["금액"],"count":"건수"},{"type":"share","column":"금액","as":"비율"},{"type":"sort","column":"금액","descending":true},{"type":"calc","as":"만원","expression":"ROUND(금액/10000,0)"}]} starts from another table.
            Other steps: {"type":"filter","column":"지역","eq":"서울"}, or "in": ["서울","부산"], or "min" and "max"; {"type":"lookup","from":"목표표","key":"거래처","take":["목표"]} adds columns of another table by a matching column ("foreignKey" when it has another name there); {"type":"compare","source":"오월","prefix":"5월 "} repeats the earlier steps on another source and adds its number columns ("key" and "columns" choose them); {"type":"limit","count":5,"rest":"그 밖"}; {"type":"hide","columns":["비고"]}; {"type":"total","label":"합계","op":"sum"} (sum, avg, count, min or max). Any step takes "off": true to skip it.
            {"type":"formula","name":"매출","expression":"SUM(기간표.금액)","format":"man"} and {"type":"formula","name":"달성률","expression":"매출/목표","format":"pct"}
            {"type":"pick","name":"1등 거래처","table":"거래처별","row":1,"column":"거래처"} takes one cell; {"type":"fixed","name":"기준","number":50000000} or "text".
            Formats: man (5,297만 원), won (52,970,000원), gun (247건), pct (106%; pct:1 for one decimal), num (1,234), day:6 (a day number shown as 6월 8일), date, text.
            Elements (kind element), each with its own style; page children need x, y, w and h:
            {"type":"text","name":"제목","style":{"x":48,"y":34,"w":640,"h":46,"fontSize":30,"fontWeight":700},"text":"6월 매출 대시보드"}
            {"type":"text","style":{"x":48,"y":84,"w":640,"h":26,"fontSize":15,"color":"#4A5570"},"text":"«기간» · 거래처 «거래처»"}
            {"type":"shape","shape":"round","style":{"x":48,"y":128,"w":330,"h":132,"fill":"#F7F8FA","radius":12}} (rect, round, ellipse or pill; it can hold text too)
            {"type":"line","style":{"x":48,"y":118,"w":1027,"h":2,"line":"#D8DCE3"}}
            {"type":"chart","data":"날마다","category":"날짜","values":["금액"],"kind":"column","emphasis":"max","range":"기간","rangeControl":true,"style":{"x":48,"y":284,"w":650,"h":330}} (kind column, bar, line, area or pie; emphasis max, min or last; "labels": true prints the numbers)
            {"type":"table","data":"거래처별","columns":[{"column":"거래처"},{"column":"금액","format":"man","align":"right"}],"max":10,"style":{"x":722,"y":336,"w":353,"h":360}}
            {"type":"meter","value":"달성률","style":{"x":396,"y":200,"w":300,"h":22}} draws a value from 0 to 1 as a bar.
            Controls: {"type":"slider","param":"목표","label":"목표","showValue":true}, {"type":"rangeSlider","param":"기간","label":"기간"}, {"type":"toggle","param":"비교","label":"5월과 비교"}, {"type":"segmented","param":"보기"}, {"type":"select","param":"거래처","label":"거래처"}, {"type":"button","label":"처음으로","look":"ghost","action":{"type":"reset"}} (look fill, line or ghost).
            Actions: {"type":"set","param":"기간","value":[1,30]}, {"type":"reset"} (or with "params": ["기간"]), {"type":"ask","text":"«기간» 매출을 짧게 요약해 줘"} puts the text in the assistant box, {"type":"open","file":"6월 매출정리.xlsx"}, {"type":"refresh"} and {"type":"pdf"}.
            Groups: {"type":"column","style":{"x":48,"y":128,"w":330,"h":132,"fill":"#F7F8FA","radius":12,"padding":18,"gap":8},"children":[...]} stacks its children and {"type":"row","style":{"x":700,"y":38,"w":375,"h":40,"gap":8,"justify":"end"},"children":[...]} lines them up. Give the children with the group or add them later with parent set to the group's id; a child needs only h in a column or w in a row, or "grow": 1 to take the rest.
            {"type":"repeat","data":"거래처별","max":8,"empty":"이 기간에는 판 것이 없어요","onClick":{"type":"set","param":"거래처","value":"item.거래처"},"style":{"x":722,"y":336,"w":353,"h":360,"gap":6},"template":[{"type":"text","style":{"w":104,"h":28},"text":"«item.거래처»"},{"type":"meter","style":{"grow":1,"h":28},"value":"item.비율"}]} draws its template once per row; "direction": "row" lines the rows up side by side and "grid" wraps them into a gallery of cards, each card one column element with a fixed w and h.
            Style keys: x, y, w, h, rotation, direction, gap, padding, align, justify, grow, fontName, fontSize, fontWeight, italic, underline, color, textAlign, mark, fill, line, lineWidth, lineDash, radius, opacity. Colors are hex such as #1C2A48. A new dashboard has no shared styles, so give each element its own style.

            Layout that reads well
            - Title at the top left (fontSize 28 to 30, bold) and a one-line subtitle that names the period and the choices.
            - A row of three or four number cards near the top: a small grey label, a big bold number (fontSize 32 to 36) and one sentence about it.
            - The main chart below on the left, a list, table or second chart on the right, and each control right next to what it changes.
            - 48 px page margins, 16 to 24 px gaps, aligned edges, everything inside 1123 x 794, and about a dozen things on the page at most.
            - Write sentences that read well for any value, and use when to pick one of two sentences (달성률<1 and 달성률>=1) instead of showing negative numbers.
            """;

        internal static string DashGuide => (DashGuideStart + "\n" + DashRules + "\n" + DashGuideItems).Replace("\r\n", "\n");
    }
}
