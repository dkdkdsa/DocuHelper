using System.Collections.Generic;
using DocuHelper.Docus;

namespace DocuHelperEditor.State.Samples
{
    public static class ChatSamples
    {
        private const string SalesName = "6월 매출정리.xlsx";
        private const string ReportName = "7월 회의 보고서.docx";
        private const string DeckName = "상반기 실적 발표.pptx";
        private const string SalesRequest = "거래처별로 합계 내 줘";

        public static List<ChatEntry> ExcelAsking()
        {
            return new List<ChatEntry>
            {
                TimeEntry("오늘 오후 3:20"),
                UserEntry(SalesRequest, FileChip(SalesName, DocuType.Excel)),
                new ChatEntry
                {
                    Kind = ChatEntryKind.Assistant,
                    Text = "네, 파일을 살펴볼게요.",
                    Progress = new TaskProgress
                    {
                        Steps = new List<TaskStep>
                        {
                            Step(TaskStepState.Done, "6월 매출정리.xlsx 열어서 읽기", "248줄"),
                            Step(TaskStepState.Done, "거래처 이름 살펴보기", "비슷한 이름 1쌍"),
                            Step(TaskStepState.Waiting, "같은 거래처인지 여쭤보기", "답을 기다려요"),
                            Step(TaskStepState.Todo, "요약표 만들기", null),
                            Step(TaskStepState.Todo, "계산식 넣고 정리하기", null),
                        },
                    },
                },
            };
        }

        public static List<ChatEntry> ExcelResult(List<ChangeItem> changes, string fileId)
        {
            return new List<ChatEntry>
            {
                TimeEntry("오늘 오후 3:20"),
                UserEntry(SalesRequest, FileChip(SalesName, DocuType.Excel)),
                new ChatEntry
                {
                    Kind = ChatEntryKind.Assistant,
                    Text = "네, 파일을 살펴볼게요.",
                    AskedQuestion = "‘한빛상사’와 ‘(주)한빛상사’를 같은 거래처로 볼까요?",
                },
                UserEntry("네, 같은 곳이에요"),
                new ChatEntry
                {
                    Kind = ChatEntryKind.Assistant,
                    Text = "‘거래처별 요약’ 시트를 새로 만들었어요. 항목을 누르면 표에 형광펜으로 보여 드려요.",
                    Progress = Finished(
                        "18초",
                        false,
                        Step(TaskStepState.Done, "6월 매출정리.xlsx 열어서 읽기", "248줄"),
                        Step(TaskStepState.Done, "거래처 이름 살펴보기", "비슷한 이름 1쌍"),
                        Step(TaskStepState.Done, "같은 거래처인지 여쭤보기", "같은 곳"),
                        Step(TaskStepState.Done, "요약표 만들기", "거래처 6곳"),
                        Step(TaskStepState.Done, "계산식 넣고 정리하기", "4곳")),
                    Result = new AssistantResult
                    {
                        Groups = new List<ChangeGroup> { Group(fileId, SalesName, DocuType.Excel, changes) },
                        Untouched = new List<string> { "‘6월 매출’, ‘거래처 목록’ 시트는 그대로 둬요" },
                        FollowUps = new List<string> { "평균 금액도 내 줘", "막대그래프도 그려 줘" },
                    },
                },
            };
        }

        public static List<ChatEntry> ExcelWorking()
        {
            return new List<ChatEntry>
            {
                TimeEntry("오늘 오후 3:20"),
                UserEntry(SalesRequest, FileChip(SalesName, DocuType.Excel)),
                new ChatEntry
                {
                    Kind = ChatEntryKind.Assistant,
                    Progress = new TaskProgress
                    {
                        CurrentDetail = "요약표 만드는 중",
                        Steps = new List<TaskStep>
                        {
                            Step(TaskStepState.Done, "파일 읽기", null),
                            Step(TaskStepState.Done, "거래처 이름 살펴보기", null),
                            Step(TaskStepState.Doing, "요약표 만드는 중", null),
                            Step(TaskStepState.Todo, "계산식 넣기", null),
                            Step(TaskStepState.Todo, "정리하기", null),
                        },
                    },
                },
            };
        }

        public static List<ChatEntry> WordResult(List<ChangeItem> changes, string fileId, string sourceFileName)
        {
            var source = string.IsNullOrEmpty(sourceFileName) ? SalesName : sourceFileName;
            return new List<ChatEntry>
            {
                TimeEntry("오늘 오후 4:05"),
                UserEntry("6월 매출 정리한 거, 이 보고서 2번에 표로 넣어 줘", FileChip(ReportName, DocuType.Word)),
                new ChatEntry
                {
                    Kind = ChatEntryKind.Assistant,
                    Text = $"2번 항목 아래에 설명 한 줄과 표를 넣었어요. 숫자는 ‘{source}’에서 가져왔어요.",
                    Progress = Finished(
                        "12초",
                        true,
                        Step(TaskStepState.Done, "보고서 읽기", "3쪽"),
                        Step(TaskStepState.Done, "작업 폴더에서 6월 매출 파일 찾기", "1개"),
                        Step(TaskStepState.Done, "‘거래처별 요약’ 시트 읽기", "거래처 6곳"),
                        Step(TaskStepState.Done, "2번 항목에 표와 설명 넣기", "2곳")),
                    Result = new AssistantResult
                    {
                        Groups = new List<ChangeGroup> { Group(fileId, ReportName, DocuType.Word, changes) },
                        ReadOnlyFiles = new List<string> { source },
                        FollowUps = new List<string> { "표 밑에 그래프도 넣어 줘", "맞춤법도 봐 줘" },
                    },
                },
            };
        }

        public static List<ChatEntry> PptResult(List<ChangeItem> changes, string fileId)
        {
            return new List<ChatEntry>
            {
                TimeEntry("오늘 오후 4:30"),
                UserEntry("4번 슬라이드 글이 너무 많아. 줄이고 나머지는 발표 메모로 옮겨 줘", FileChip(DeckName + ", 4번 슬라이드", DocuType.Ppt)),
                new ChatEntry
                {
                    Kind = ChatEntryKind.Assistant,
                    Text = "4번 슬라이드 글을 6줄에서 3줄로 줄이고, 뺀 내용은 발표 메모로 옮겼어요. 글자 크기는 그대로예요.",
                    Progress = Finished(
                        "9초",
                        false,
                        Step(TaskStepState.Done, "상반기 실적 발표.pptx 열어서 읽기", "슬라이드 6장"),
                        Step(TaskStepState.Done, "4번 슬라이드 글 줄이기", "6줄에서 3줄"),
                        Step(TaskStepState.Done, "뺀 내용을 발표 메모로 옮기기", "3줄")),
                    Result = new AssistantResult
                    {
                        Groups = new List<ChangeGroup> { Group(fileId, DeckName, DocuType.Ppt, changes) },
                        Untouched = new List<string> { "그래프와 다른 슬라이드는 그대로 둬요" },
                        FollowUps = new List<string> { "5번 슬라이드도 줄여 줘", "제목 글꼴 다 맞춰 줘" },
                    },
                },
            };
        }

        public static List<ChatEntry> ConnectionError()
        {
            return new List<ChatEntry>
            {
                TimeEntry("오늘 오후 3:20"),
                UserEntry(SalesRequest),
                new ChatEntry
                {
                    Kind = ChatEntryKind.Assistant,
                    Error = new AssistantError
                    {
                        Kind = AssistantErrorKind.Connection,
                        Title = "인터넷 연결이 잠깐 끊겼어요",
                        Message = "지금까지 한 건 그대로 있어요.",
                        RetryInSeconds = 8,
                        Attempt = 2,
                    },
                },
            };
        }

        public static List<ChatEntry> KeyError()
        {
            return new List<ChatEntry>
            {
                TimeEntry("오늘 오후 3:20"),
                UserEntry(SalesRequest),
                new ChatEntry
                {
                    Kind = ChatEntryKind.Assistant,
                    Error = new AssistantError
                    {
                        Kind = AssistantErrorKind.Key,
                        Title = "도우미 열쇠가 맞지 않아요",
                        Message = "열쇠가 바뀌었거나 이번 달 사용량이 다 됐을 수 있어요. 설정에서 열쇠를 다시 넣거나, 앱을 깔아 준 분께 알려 주세요.",
                    },
                },
            };
        }

        private static ChatEntry TimeEntry(string text)
        {
            return new ChatEntry { Kind = ChatEntryKind.Time, Text = text };
        }

        private static ChatEntry UserEntry(string text, params ContextChipModel[] chips)
        {
            return new ChatEntry { Kind = ChatEntryKind.User, Text = text, Chips = new List<ContextChipModel>(chips) };
        }


        private static ContextChipModel FileChip(string text, DocuType type)
        {
            return new ContextChipModel { Kind = ContextChipKind.File, Text = text, FileType = type };
        }

        private static TaskStep Step(TaskStepState state, string label, string detail)
        {
            return new TaskStep { State = state, Label = label, Detail = detail };
        }

        private static TaskProgress Finished(string elapsed, bool expanded, params TaskStep[] steps)
        {
            return new TaskProgress
            {
                Steps = new List<TaskStep>(steps),
                IsFinished = true,
                IsExpanded = expanded,
                Elapsed = elapsed,
            };
        }

        private static ChangeGroup Group(string fileId, string fileName, DocuType type, List<ChangeItem> changes)
        {
            return new ChangeGroup
            {
                FileId = fileId,
                FileName = fileName,
                FileType = type,
                Changes = changes ?? new List<ChangeItem>(),
            };
        }
    }
}
