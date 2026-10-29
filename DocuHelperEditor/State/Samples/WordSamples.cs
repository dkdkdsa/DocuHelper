using System.Collections.Generic;
using DocuHelper.Docus;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Samples
{
    public static class WordSamples
    {
        public const string SentenceChangeId = "word-report-sentence";
        public const string TableChangeId = "word-report-table";
        public const string SourceFileId = "sales";
        public const string SourceFileName = "6월 매출정리.xlsx";
        public const string SourceSheetName = "거래처별 요약";
        public const string SourceRange = "A1:C8";

        public static WordPaperModel MeetingReport(bool withInsertedTable) => MeetingReport(withInsertedTable, SourceFileId);

        public static WordPaperModel MeetingReport(bool withInsertedTable, string sourceFileId)
        {
            var blocks = new List<WordBlockModel>
            {
                new WordBlockModel { Id = "report-title", Kind = WordBlockKind.Heading1, Text = "7월 정기 회의 보고서", Alignment = WordAlignment.Center },
                new WordBlockModel { Id = "report-byline", Kind = WordBlockKind.Byline, Text = "작성일 2026. 7. 3.\n작성 영업지원팀", Alignment = WordAlignment.Center },
                new WordBlockModel { Id = "report-overview", Kind = WordBlockKind.Heading2, Text = "1. 회의 개요" },
                new WordBlockModel { Id = "report-when", Kind = WordBlockKind.Body, Text = "일시: 7월 3일(금) 오전 10시" },
                new WordBlockModel { Id = "report-where", Kind = WordBlockKind.Body, Text = "장소: 3층 회의실" },
                new WordBlockModel { Id = "report-who", Kind = WordBlockKind.Body, Text = "참석: 영업지원팀 6명" },
                new WordBlockModel { Id = "report-sales", Kind = WordBlockKind.Heading2, Text = "2. 6월 매출 실적" },
            };

            if (withInsertedTable)
            {
                blocks.Add(new WordBlockModel
                {
                    Id = "report-sales-summary",
                    Kind = WordBlockKind.Body,
                    Text = "6월 매출은 모두 46,974,000원이고, 한빛상사가 14,820,000원으로 가장 많았습니다.",
                    ChangeId = SentenceChangeId,
                });
                blocks.Add(new WordBlockModel
                {
                    Id = "report-sales-table",
                    Kind = WordBlockKind.Table,
                    Table = SalesTable(),
                    ChangeId = TableChangeId,
                    Source = new WordSourceNoteModel
                    {
                        FileId = sourceFileId,
                        FileName = SourceFileName,
                        FileType = DocuType.Excel,
                        SheetName = SourceSheetName,
                        Range = SourceRange,
                        Text = "이 표는 ‘6월 매출정리.xlsx’의 ‘거래처별 요약’ 시트에서 가져왔어요.",
                    },
                });
            }

            blocks.Add(new WordBlockModel { Id = "report-plan", Kind = WordBlockKind.Heading2, Text = "3. 7월 계획" });
            blocks.Add(new WordBlockModel { Id = "report-plan-close", Kind = WordBlockKind.Body, ListStyle = WordListKind.Bullet, Text = "거래처별 월말 정산 일정 공유" });
            blocks.Add(new WordBlockModel { Id = "report-plan-visit", Kind = WordBlockKind.Body, ListStyle = WordListKind.Bullet, Text = "새 거래처 두 곳 방문 일정 정하기" });

            return new WordPaperModel
            {
                Blocks = blocks,
                SelectedBlockId = "report-who",
            };
        }

        public static List<ChangeItem> ReportChanges(string fileId)
        {
            return new List<ChangeItem>
            {
                new ChangeItem { Id = SentenceChangeId, FileId = fileId, Label = "2번 항목에 설명 한 줄을 넣어요", Location = "1쪽", Icon = "lines" },
                new ChangeItem { Id = TableChangeId, FileId = fileId, Label = "거래처별 매출 표를 넣어요", Location = "표 1개", Icon = "table" },
            };
        }

        private static WordTableModel SalesTable()
        {
            return new WordTableModel
            {
                Caption = "거래처별 6월 매출",
                HasHeader = true,
                HasFooter = true,
                NumericColumns = new List<int> { 1, 2 },
                Rows = new List<List<string>>
                {
                    new List<string> { "거래처", "건수", "금액(원)" },
                    new List<string> { "한빛상사", "58", "14,820,000" },
                    new List<string> { "서울오피스", "41", "11,356,000" },
                    new List<string> { "대성문구", "52", "6,948,500" },
                    new List<string> { "미래사무기기", "17", "5,610,000" },
                    new List<string> { "그린유통", "46", "4,327,500" },
                    new List<string> { "동부물산", "33", "3,912,000" },
                    new List<string> { "합계", "247", "46,974,000" },
                },
            };
        }
    }
}
