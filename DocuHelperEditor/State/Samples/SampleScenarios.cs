using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Docus;

namespace DocuHelperEditor.State.Samples
{
    public static class SampleScenarios
    {
        public const string Default = "excel-result";

        private const string FolderName = "6월 결산";
        private const string FolderPath = @"문서\문서도우미\6월 결산";
        private const string SalesId = "sales";
        private const string ReportId = "report";
        private const string DeckId = "deck";
        private const string PlanId = "plan";

        public const string Real = "real";

        public static string Current { get; private set; }

        public static IReadOnlyList<string> Names { get; } = new[]
        {
            Real,
            "start",
            "excel-ask",
            "excel-result",
            "excel-working",
            "collapsed",
            "rail-asking",
            "rail-done",
            "word",
            "ppt",
            "history",
            "toast",
            "unsaved",
            "error-connection",
            "error-key",
            "popup-file",
            "popup-big",
            "popup-conflict",
            "drop",
            "conversations",
            "setup",
        };

        public static void Leave() => Current = null;

        public static void Apply(string name, WorkspaceStore workspace, AssistantStore assistant, SettingsStore settings)
        {
            Current = name;
            settings.Load("sk-ant-0000000000000000", KeyCheckState.Ok, @"문서\문서도우미");
            workspace.SetFolderCollapsed(false);
            workspace.SetSavedAll(false);
            workspace.LoadHistory(Array.Empty<SaveHistoryEntry>());
            workspace.ClearDrop();
            assistant.SetPanelCollapsed(false);
            assistant.CloseConversationList();

            switch (name)
            {
                case "start":
                case "setup":
                    workspace.Load("내 작업 폴더", @"문서\문서도우미\내 작업 폴더", Array.Empty<WorkspaceFile>(), Array.Empty<string>(), null);
                    assistant.Load(Array.Empty<ChatEntry>(), AssistantStatus.Idle, null);
                    break;
                case "excel-ask":
                    ExcelAsk(workspace, assistant, AskSamples.SameClient(SalesId));
                    break;
                case "excel-working":
                    ExcelWorking(workspace, assistant);
                    break;
                case "collapsed":
                    {
                        ExcelAsk(workspace, assistant, AskSamples.SameClient(SalesId));
                        var sales = workspace.FindFile(SalesId);
                        sales.Paper = ExcelSamples.SalesWorkbookWithNotes();
                        sales.IsDirty = true;
                        workspace.SetSelection("6월 매출 G5");
                        workspace.SetFolderCollapsed(true);
                        assistant.SetPanelCollapsed(true);
                        break;
                    }
                case "rail-asking":
                    ExcelAsk(workspace, assistant, AskSamples.SameClient(SalesId));
                    assistant.SetPanelCollapsed(true);
                    break;
                case "rail-done":
                    ExcelResult(workspace, assistant);
                    assistant.SetPanelCollapsed(true);
                    assistant.MarkResultUnseen();
                    break;
                case "word":
                    Word(workspace, assistant);
                    break;
                case "ppt":
                    Ppt(workspace, assistant);
                    break;
                case "history":
                    ExcelResult(workspace, assistant);
                    workspace.LoadHistory(History());
                    workspace.ToggleHistory();
                    break;
                case "toast":
                    ExcelSaved(workspace, assistant);
                    workspace.LoadHistory(History());
                    workspace.ShowToast();
                    break;
                case "unsaved":
                    ExcelResult(workspace, assistant);
                    workspace.RequestClose(SalesId);
                    break;
                case "error-connection":
                    ExcelPlain(workspace);
                    assistant.Load(ChatSamples.ConnectionError(), AssistantStatus.Error, null);
                    break;
                case "error-key":
                    ExcelPlain(workspace);
                    assistant.Load(ChatSamples.KeyError(), AssistantStatus.Error, null);
                    break;
                case "popup-file":
                    ExcelResult(workspace, assistant);
                    assistant.Load(assistant.Entries.ToList(), AssistantStatus.Asking, AskSamples.WhichFile());
                    break;
                case "popup-big":
                    ExcelAsk(workspace, assistant, AskSamples.BigChange());
                    break;
                case "popup-conflict":
                    ExcelResult(workspace, assistant);
                    assistant.Load(assistant.Entries.ToList(), AssistantStatus.Asking, AskSamples.Conflict(SalesId));
                    break;
                case "drop":
                    ExcelResult(workspace, assistant);
                    workspace.ShowDrop(new[]
                    {
                        new DropCandidate { Name = "6월 매출정리.xlsx", Type = DocuType.Excel, CanOpen = true },
                        new DropCandidate
                        {
                            Name = "회의록.hwp",
                            CanOpen = false,
                            Reason = "한글 파일은 아직 못 열어요. 한글에서 ‘다른 이름으로 저장’으로 .docx를 골라 저장한 다음 넣어 주세요.",
                        },
                    });
                    break;
                case "conversations":
                    ExcelResult(workspace, assistant);
                    assistant.ToggleConversationList();
                    break;
                default:
                    ExcelResult(workspace, assistant);
                    break;
            }
        }

        private static List<WorkspaceFile> Files(bool summary, bool asking, bool reportEdited, bool deckEdited)
        {
            return new List<WorkspaceFile>
            {
                new WorkspaceFile
                {
                    Id = SalesId,
                    Name = "6월 매출정리.xlsx",
                    Path = FolderPath + @"\6월 매출정리.xlsx",
                    Type = DocuType.Excel,
                    Paper = ExcelSamples.SalesWorkbook(summary, asking),
                },
                new WorkspaceFile
                {
                    Id = ReportId,
                    Name = "7월 회의 보고서.docx",
                    Path = FolderPath + @"\7월 회의 보고서.docx",
                    Type = DocuType.Word,
                    Paper = WordSamples.MeetingReport(reportEdited),
                },
                new WorkspaceFile
                {
                    Id = DeckId,
                    Name = "상반기 실적 발표.pptx",
                    Path = FolderPath + @"\상반기 실적 발표.pptx",
                    Type = DocuType.Ppt,
                    Paper = PptSamples.ResultsDeck(deckEdited),
                },
                new WorkspaceFile
                {
                    Id = PlanId,
                    Name = "하반기 발주 계획.xlsx",
                    Path = FolderPath + @"\하반기 발주 계획.xlsx",
                    Type = DocuType.Excel,
                    Paper = ExcelSamples.PurchasePlan(),
                },
            };
        }

        private static void ExcelPlain(WorkspaceStore workspace)
        {
            workspace.Load(FolderName, FolderPath, Files(false, false, false, false), new[] { SalesId, ReportId }, SalesId);
        }

        private static void ExcelAsk(WorkspaceStore workspace, AssistantStore assistant, AskUserRequest question)
        {
            var files = Files(false, true, false, false);
            files[0].Activity = FileActivity.Asking;
            workspace.Load(FolderName, FolderPath, files, new[] { SalesId, ReportId }, SalesId);
            workspace.SetSelection("6월 매출 B2");
            assistant.Load(ChatSamples.ExcelAsking(), AssistantStatus.Asking, question);
        }

        private static void ExcelWorking(WorkspaceStore workspace, AssistantStore assistant)
        {
            var files = Files(false, false, false, false);
            files[0].Activity = FileActivity.Editing;
            workspace.Load(FolderName, FolderPath, files, new[] { SalesId, ReportId }, SalesId);
            assistant.Load(ChatSamples.ExcelWorking(), AssistantStatus.Working, null);
        }

        private static void ExcelResult(WorkspaceStore workspace, AssistantStore assistant)
        {
            var files = Files(true, false, false, false);
            var changes = ExcelSamples.SummaryChanges(SalesId);
            files[0].Changes = changes;
            files[0].IsDirty = true;
            workspace.Load(FolderName, FolderPath, files, new[] { SalesId, ReportId }, SalesId);
            if (changes.Count > 1)
                workspace.SelectChange(changes[1].Id);

            assistant.Load(ChatSamples.ExcelResult(changes, SalesId), AssistantStatus.Done, null);
        }

        private static void ExcelSaved(WorkspaceStore workspace, AssistantStore assistant)
        {
            var files = Files(true, false, false, false);
            workspace.Load(FolderName, FolderPath, files, new[] { SalesId, ReportId }, SalesId);
            workspace.SetSavedAll(true);
            var entries = ChatSamples.ExcelResult(ExcelSamples.SummaryChanges(SalesId), SalesId);
            foreach (var entry in entries.Where(e => e.Result != null))
            {
                entry.Result.IsSaved = true;
                entry.Result.SavedAt = "오후 3:22";
            }

            assistant.Load(entries, AssistantStatus.Done, null);
        }

        private static void Word(WorkspaceStore workspace, AssistantStore assistant)
        {
            var files = Files(true, false, true, false);
            var changes = WordSamples.ReportChanges(ReportId);
            files[1].Changes = changes;
            files[1].IsDirty = true;
            workspace.Load(FolderName, FolderPath, files, new[] { SalesId, ReportId }, ReportId);
            if (changes.Count > 0)
                workspace.SelectChange(changes[0].Id);

            workspace.SetSelection("1쪽, 1. 회의 개요");
            assistant.Load(ChatSamples.WordResult(changes, ReportId, "6월 매출정리.xlsx"), AssistantStatus.Done, null);
        }

        private static void Ppt(WorkspaceStore workspace, AssistantStore assistant)
        {
            var files = Files(true, false, false, true);
            var changes = PptSamples.DeckChanges(DeckId);
            files[2].Changes = changes;
            files[2].IsDirty = true;
            workspace.Load(FolderName, FolderPath, files, new[] { SalesId, ReportId, DeckId }, DeckId);
            if (changes.Count > 0)
                workspace.SelectChange(changes[0].Id);

            workspace.SetSelection("4번 슬라이드 글상자");
            assistant.Load(ChatSamples.PptResult(changes, DeckId), AssistantStatus.Done, null);
        }

        private static IEnumerable<SaveHistoryEntry> History()
        {
            return new[]
            {
                new SaveHistoryEntry { Id = "h3", FileId = SalesId, When = "오후 3:22", What = "저장함, 도우미가 4곳 고침", IsCurrent = true },
                new SaveHistoryEntry { Id = "h2", FileId = SalesId, When = "오후 2:10", What = "저장함, 직접 고침" },
                new SaveHistoryEntry { Id = "h1", FileId = SalesId, When = "오후 1:05", What = "처음 넣은 상태" },
            };
        }
    }
}
