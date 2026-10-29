using System.Collections.Generic;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Samples
{
    public static class ExcelSamples
    {
        private const string NewSheetChange = "xl-new-sheet";
        private const string SumsChange = "xl-sums";
        private const string TotalChange = "xl-total";
        private const string SortChange = "xl-sort";
        private const string SalesSheetName = "6월 매출";
        private const string SharedClient = "한빛상사";
        private const string SharedClientCorp = "(주)한빛상사";
        private const int NoteColumn = 6;

        public static ExcelPaperModel SalesWorkbook(bool withSummary, bool asking)
        {
            var model = new ExcelPaperModel();
            model.Sheets.Add(SalesSheet(asking, false));
            model.Sheets.Add(ClientSheet());
            if (withSummary)
            {
                model.Sheets.Add(SummarySheet());
                model.ActiveSheetIndex = model.Sheets.Count - 1;
            }

            return model;
        }

        public static ExcelPaperModel SalesWorkbookWithNotes()
        {
            var model = new ExcelPaperModel();
            model.Sheets.Add(SalesSheet(true, true));
            model.Sheets.Add(ClientSheet());
            return model;
        }

        public static List<ChangeItem> SummaryChanges(string fileId)
        {
            return new List<ChangeItem>
            {
                new ChangeItem { Id = NewSheetChange, FileId = fileId, Label = "‘거래처별 요약’ 시트를 새로 만들어요", Location = "새 시트", Icon = "file-plus" },
                new ChangeItem { Id = SumsChange, FileId = fileId, Label = "거래처마다 건수와 금액 합계를 구해요", Location = "B2:C7", Icon = "sigma" },
                new ChangeItem { Id = TotalChange, FileId = fileId, Label = "맨 아래 줄에 전체 합계를 넣어요", Location = "8행", Icon = "total" },
                new ChangeItem { Id = SortChange, FileId = fileId, Label = "쉼표를 넣고 큰 금액부터 정렬해요", Location = "A, C열", Icon = "sort" },
            };
        }

        public static ExcelPaperModel PurchasePlan()
        {
            var sheet = NewSheet("발주 계획", 72, 132, 124, 56, 84, 96, null);
            var head = AddRow(sheet, SheetRowKind.Header, null, "월", "품목", "거래처", "수량", "단가", "금액");
            AlignRight(head, 3, 4, 5);
            Data(sheet, "7월", "A4 복사용지", SharedClient, "40", "24,000", "960,000");
            Data(sheet, "8월", "토너 카트리지", SharedClient, "6", "89,000", "534,000");
            Data(sheet, "9월", "볼펜(흑) 12입", "대성문구", "60", "6,500", "390,000");
            Data(sheet, "10월", "커피믹스", "그린유통", "20", "21,500", "430,000");
            Data(sheet, "11월", "파일 바인더", "서울오피스", "40", "3,200", "128,000");
            Data(sheet, "12월", "복합기 토너", "동부물산", "4", "92,000", "368,000");
            var total = AddRow(sheet, SheetRowKind.Total, null, "합계", string.Empty, string.Empty, string.Empty, string.Empty, "2,810,000");
            total.Cells[5].Formula = "=SUM(F2:F7)";
            sheet.SelectedRef = "A1";

            var model = new ExcelPaperModel();
            model.Sheets.Add(sheet);
            return model;
        }

        private static Sheet SalesSheet(bool asking, bool withNotes)
        {
            var sheet = withNotes
                ? NewSheet(SalesSheetName, 100, 124, 132, 56, 84, 96, 150, 96, 96, 96, 96, null)
                : NewSheet(SalesSheetName, 100, 124, 132, 56, 84, 96, null);
            var head = AddRow(sheet, SheetRowKind.Header, null, "날짜", "거래처", "품목", "수량", "단가", "금액");
            AlignRight(head, 3, 4, 5);
            Data(sheet, "2026-06-02", SharedClient, "A4 복사용지", "20", "24,000", "480,000");
            Data(sheet, "2026-06-02", "대성문구", "볼펜(흑) 12입", "50", "6,500", "325,000");
            Data(sheet, "2026-06-03", SharedClientCorp, "토너 카트리지", "4", "89,000", "356,000");
            Data(sheet, "2026-06-05", "서울오피스", "파일 바인더", "30", "3,200", "96,000");
            Data(sheet, "2026-06-05", SharedClient, "A4 복사용지", "15", "24,000", "360,000");
            Data(sheet, "2026-06-07", "미래사무기기", "라벨 프린터", "2", "135,000", "270,000");
            Data(sheet, "2026-06-09", "대성문구", "포스트잇", "40", "2,800", "112,000");
            Data(sheet, "2026-06-10", "그린유통", "생수 2L (6입)", "25", "7,900", "197,500");
            Data(sheet, "2026-06-12", "서울오피스", "사무용 의자", "3", "189,000", "567,000");
            Data(sheet, "2026-06-12", SharedClientCorp, "A4 복사용지", "10", "24,000", "240,000");
            Data(sheet, "2026-06-14", "그린유통", "커피믹스", "12", "21,500", "258,000");
            Data(sheet, "2026-06-15", "동부물산", "복합기 토너", "3", "92,000", "276,000");
            Data(sheet, "2026-06-16", SharedClient, "클리어 파일", "60", "1,200", "72,000");
            Data(sheet, "2026-06-17", "미래사무기기", "문서 세단기", "1", "245,000", "245,000");
            Data(sheet, "2026-06-18", "대성문구", "스테이플러", "20", "4,500", "90,000");
            Data(sheet, "2026-06-19", "동부물산", "복사용지 B4", "10", "27,000", "270,000");
            Data(sheet, "2026-06-20", "서울오피스", "화이트보드", "2", "78,000", "156,000");
            Data(sheet, "2026-06-22", "그린유통", "종이컵 1,000개", "4", "18,000", "72,000");
            Data(sheet, "2026-06-23", "대성문구", "형광펜 5색", "30", "3,800", "114,000");
            sheet.SelectedRef = "B2";

            if (withNotes)
            {
                Note(sheet, 0, "비고");
                Note(sheet, 2, "월말 정산");
                Note(sheet, 4, "반품 2개 빼고");
                Note(sheet, 9, "의자 1개 교환");
                Note(sheet, 12, "다음 달 입금");
                sheet.SelectedRef = SheetRef.Make(NoteColumn, 4);
            }

            if (asking)
            {
                foreach (var row in sheet.Rows)
                {
                    if (row.Kind != SheetRowKind.Normal || row.Cells.Count < 2)
                        continue;

                    var client = row.Cells[1].Text;
                    if (client == SharedClient || client == SharedClientCorp)
                        row.Cells[1].IsAsking = true;
                }
            }

            return sheet;
        }

        private static Sheet ClientSheet()
        {
            var sheet = NewSheet("거래처 목록", 150, 96, 96, 96, 96, null);
            AddRow(sheet, SheetRowKind.Header, null, "거래처", "구분", "지역");
            Data(sheet, SharedClient, "도매", "서울");
            Data(sheet, "대성문구", "소매", "경기");
            Data(sheet, "서울오피스", "도매", "서울");
            Data(sheet, "미래사무기기", "도매", "인천");
            Data(sheet, "그린유통", "소매", "경기");
            Data(sheet, "동부물산", "도매", "부산");
            sheet.SelectedRef = "A2";
            return sheet;
        }

        private static Sheet SummarySheet()
        {
            var sheet = NewSheet("거래처별 요약", 150, 104, 140, 96, 96, null);
            sheet.IsNew = true;
            sheet.ChangeId = NewSheetChange;

            var head = AddRow(sheet, SheetRowKind.Header, NewSheetChange, "거래처", "거래 건수", "금액 합계");
            AlignRight(head, 1, 2);
            Mark(head, NewSheetChange, 0, 1, 2);

            AddSummaryRow(sheet, SharedClient, "58", "14,820,000", "*" + SharedClient);
            AddSummaryRow(sheet, "서울오피스", "41", "11,356,000", "서울오피스");
            AddSummaryRow(sheet, "대성문구", "52", "6,948,500", "대성문구");
            AddSummaryRow(sheet, "미래사무기기", "17", "5,610,000", "미래사무기기");
            AddSummaryRow(sheet, "그린유통", "46", "4,327,500", "그린유통");
            AddSummaryRow(sheet, "동부물산", "33", "3,912,000", "동부물산");

            var total = AddRow(sheet, SheetRowKind.Total, TotalChange, "전체 합계", "247", "46,974,000");
            total.Cells[1].Formula = "=SUM(B2:B7)";
            total.Cells[2].Formula = "=SUM(C2:C7)";
            Mark(total, TotalChange, 0, 1, 2);

            sheet.ChangeAnchors[NewSheetChange] = "A1";
            sheet.ChangeAnchors[SumsChange] = "C2";
            sheet.ChangeAnchors[TotalChange] = "C8";
            sheet.ChangeAnchors[SortChange] = "C2";
            sheet.SelectedRef = "C2";
            return sheet;
        }

        private static void AddSummaryRow(Sheet sheet, string client, string count, string amount, string criteria)
        {
            var row = AddRow(sheet, SheetRowKind.Normal, SumsChange, client, count, amount);
            row.Cells[1].Formula = "=COUNTIF('" + SalesSheetName + "'!B:B,\"" + criteria + "\")";
            row.Cells[2].Formula = "=SUMIF('" + SalesSheetName + "'!B:B,\"" + criteria + "\",'" + SalesSheetName + "'!F:F)";
            Mark(row, SortChange, 0, 2);
            Mark(row, SumsChange, 1, 2);
        }

        private static Sheet NewSheet(string name, params int?[] widths)
        {
            var sheet = new Sheet { Name = name };
            for (var i = 0; i < widths.Length; i++)
                sheet.Columns.Add(new SheetColumn { Letter = SheetRef.ColumnName(i), Width = widths[i] });

            return sheet;
        }

        private static SheetRow Data(Sheet sheet, params string[] texts) => AddRow(sheet, SheetRowKind.Normal, null, texts);

        private static SheetRow AddRow(Sheet sheet, SheetRowKind kind, string changeId, params string[] texts)
        {
            var rowIndex = sheet.Rows.Count;
            var row = new SheetRow { Kind = kind, ChangeId = changeId };
            for (var i = 0; i < texts.Length; i++)
                row.Cells.Add(MakeCell(texts[i], i, rowIndex));

            sheet.Rows.Add(row);
            return row;
        }

        private static SheetCell MakeCell(string text, int column, int row)
        {
            var value = text ?? string.Empty;
            var cell = new SheetCell { Ref = SheetRef.Make(column, row), Text = value };
            if (SheetNumber.TryParse(value, out _, out var format))
            {
                cell.IsNumber = true;
                cell.NumberFormat = format;
            }

            return cell;
        }

        private static void Note(Sheet sheet, int row, string text)
        {
            sheet.EnsureCell(row, NoteColumn).Text = text;
        }

        private static void AlignRight(SheetRow row, params int[] columns)
        {
            foreach (var column in columns)
            {
                if (column < row.Cells.Count)
                    row.Cells[column].Align = SheetAlign.Right;
            }
        }

        private static void Mark(SheetRow row, string changeId, params int[] columns)
        {
            foreach (var column in columns)
            {
                if (column < row.Cells.Count && !row.Cells[column].ChangeIds.Contains(changeId))
                    row.Cells[column].ChangeIds.Add(changeId);
            }
        }
    }
}
