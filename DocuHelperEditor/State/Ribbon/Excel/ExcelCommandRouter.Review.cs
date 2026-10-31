using System.Collections.Generic;
using System.Threading.Tasks;
using DocuHelper.Editors;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Ribbon.Excel
{
    public sealed partial class ExcelCommandRouter
    {
        private const string SpellingPrompt = "이 시트에서 글자가 들어 있는 칸의 맞춤법과 띄어쓰기를 살펴봐 줘. 고칠 곳은 어느 칸을 어떻게 고칠지 먼저 알려 주고, 내가 괜찮다고 하면 고쳐 줘.";
        private const string ThesaurusPrompt = "고른 칸의 낱말과 뜻이 비슷한 다른 낱말을 다섯 개 알려 줘. 칸은 고치지 말고 알려 주기만 해 줘.";
        private const string TranslatePrompt = "고른 칸의 글을 영어로 번역해서 오른쪽 빈 열에 넣어 줘. 원래 칸은 그대로 두고, 다른 언어가 좋으면 먼저 물어봐 줘.";
        private const string AccessibilityPrompt = "이 통합 문서를 접근성 기준으로 살펴봐 줘. 머리글 줄이 있는지, 병합한 칸이나 빈 열·행이 표를 끊는지, 대체 텍스트가 없는 그림이나 차트가 있는지, 색으로만 구분한 곳이 있는지 알려 주고 고칠 방법도 같이 알려 줘.";
        private const string AlternativeTextPrompt = "대체 텍스트가 없는 그림과 차트를 모두 찾아서 알려 주고, 각각 어떤 설명을 넣으면 좋을지 제안해 줘. 내가 괜찮다고 하면 넣어 줘.";
        private const string MergedPrompt = "병합한 칸과 비어 있는 열·행 때문에 표가 끊기는 곳을 모두 찾아서 알려 주고, 고치는 방법도 같이 알려 줘.";

        private void RegisterReview()
        {
            AddAssistant("excel.review.spelling", SpellingPrompt);
            AddAssistant("excel.review.thesaurus", ThesaurusPrompt)
                .EnabledWhen(() => !string.IsNullOrWhiteSpace(State.Cell?.Text), "단어가 들어 있는 칸을 골라 주세요.");
            AddFile("excel.review.accessibility", arg => PrefillAsync(AccessibilityText(arg)))
                .Unless(AssistantBlocker)
                .MenuFrom(() => new[]
                {
                    Entry("access", "접근성 검사"),
                    Entry("alt", "대체 텍스트가 없는 그림과 차트 찾기"),
                    Entry("merged", "병합한 칸과 빈 열·행 찾기"),
                });
            AddAssistant("excel.review.translate", TranslatePrompt)
                .EnabledWhen(() => State.SelectionHasText, "글자가 들어 있는 칸을 골라 주세요.");
            Dialog("excel.review.newnote", "edit_comment");
            Tool("excel.review.shownote", "edit_comment", _ => Sheetwide(("cell", State.ActiveRef), ("action", State.Cell?.Note?.Visible == true ? "hide" : "show")))
                .PressedWhen(() => State.Cell?.Note?.Visible == true)
                .EnabledWhen(() => State.Cell?.Note != null, "메모가 있는 칸을 골라 주세요.");
            Tool("excel.review.deletenote", "edit_comment", _ => Sheetwide(("cell", State.ActiveRef), ("action", "delete")))
                .EnabledWhen(() => State.Cell?.Note != null, "메모가 있는 칸을 골라 주세요.");
            AddView("excel.review.prevnote", _ => StepNote(-1)).EnabledWhen(() => State.HasNotes, "이 시트에는 메모가 없어요.");
            AddView("excel.review.nextnote", _ => StepNote(1)).EnabledWhen(() => State.HasNotes, "이 시트에는 메모가 없어요.");
            AddFile("excel.review.protectsheet", _ => ProtectSheet())
                .Needs("protect")
                .PressedWhen(() => State.Sheet.IsProtected)
                .EnabledWhen(() => State.Sheet.IsProtected || State.HasDialog("excel.review.protectsheet"), PendingReason);
            AddFile("excel.review.protectbook", _ => ProtectBook())
                .Needs("protect")
                .PressedWhen(() => State.Model.StructureLocked)
                .EnabledWhen(() => State.Model.StructureLocked || State.HasDialog("excel.review.protectbook"), PendingReason);
            Dialog("excel.review.allowranges", "protect")
                .EnabledWhen(() => !State.Sheet.IsProtected, "시트 보호를 풀어야 범위를 정할 수 있어요.");
        }

        private static string AccessibilityText(string arg) => arg switch
        {
            "alt" => AlternativeTextPrompt,
            "merged" => MergedPrompt,
            _ => AccessibilityPrompt,
        };

        private Task StepNote(int step)
        {
            var notes = new List<(int Row, int Column)>();
            for (var r = 0; r < State.Sheet.Rows.Count; r++)
            {
                var cells = State.Sheet.Rows[r].Cells;
                for (var c = 0; c < cells.Count; c++)
                {
                    if (cells[c]?.Note != null)
                        notes.Add((r, c));
                }
            }

            if (notes.Count == 0)
                return Task.CompletedTask;

            var (row, column) = State.Active;
            var index = step > 0
                ? notes.FindIndex(n => n.Row > row || (n.Row == row && n.Column > column))
                : notes.FindLastIndex(n => n.Row < row || (n.Row == row && n.Column < column));
            if (index < 0)
                index = step > 0 ? 0 : notes.Count - 1;

            var target = notes[index];
            _surface.Select(new SheetRegion(target.Row, target.Column, target.Row, target.Column));
            return Task.CompletedTask;
        }

        private Task ProtectSheet()
        {
            if (!_surface.Prepare())
                return Task.CompletedTask;

            if (State.Sheet.IsProtected)
                Apply(EditCommand.Create("protect", Workbook(("scope", "sheet"), ("sheet", State.SheetName), ("action", "unprotect"))));
            else
                State.OpenDialog("excel.review.protectsheet");

            return Task.CompletedTask;
        }

        private Task ProtectBook()
        {
            if (!_surface.Prepare())
                return Task.CompletedTask;

            if (State.Model.StructureLocked)
                Apply(EditCommand.Create("protect", Workbook(("scope", "workbook"), ("action", "unprotect"), ("structure", true))));
            else
                State.OpenDialog("excel.review.protectbook");

            return Task.CompletedTask;
        }
    }
}
