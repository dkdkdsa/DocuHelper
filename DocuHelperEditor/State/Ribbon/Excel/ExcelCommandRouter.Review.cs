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

        private void AddReviewHandlers()
        {
            _prompts.Add("spelling", () => SpellingPrompt);
            _prompts.Add("thesaurus", () => ThesaurusPrompt);
            _prompts.Add("translate", () => TranslatePrompt);

            _runs.Add("accessibility", arg => PrefillAsync(AccessibilityText(arg)));
            _runs.Add("prevNote", _ => StepNote(-1));
            _runs.Add("nextNote", _ => StepNote(1));
            _runs.Add("protectSheet", _ => ProtectSheet());
            _runs.Add("protectBook", _ => ProtectBook());

            _tools.Add("showNote", _ => Sheetwide(("cell", State.ActiveRef), ("action", State.Cell?.Note?.Visible == true ? "hide" : "show")));
            _tools.Add("deleteNote", _ => Sheetwide(("cell", State.ActiveRef), ("action", "delete")));

            _pressed.Add("noteVisible", () => State.Cell?.Note?.Visible == true);
            _pressed.Add("sheetProtected", () => State.Sheet.IsProtected);
            _pressed.Add("bookProtected", () => State.Model.StructureLocked);

            _needs.Add("cellHasWord", _ => !string.IsNullOrWhiteSpace(State.Cell?.Text) ? null : "단어가 들어 있는 칸을 골라 주세요.");
            _needs.Add("assistantFree", _ => AssistantBlocker());
            _needs.Add("selectionHasText", _ => State.SelectionHasText ? null : "글자가 들어 있는 칸을 골라 주세요.");
            _needs.Add("hasNote", _ => State.Cell?.Note != null ? null : "메모가 있는 칸을 골라 주세요.");
            _needs.Add("hasNotes", _ => State.HasNotes ? null : "이 시트에는 메모가 없어요.");
            _needs.Add("sheetProtectable", _ => State.Sheet.IsProtected || State.HasDialog(ExcelDialogIds.ProtectSheet) ? null : PendingReason);
            _needs.Add("bookProtectable", _ => State.Model.StructureLocked || State.HasDialog(ExcelDialogIds.ProtectBook) ? null : PendingReason);
            _needs.Add("sheetUnprotected", _ => !State.Sheet.IsProtected ? null : "시트 보호를 풀어야 범위를 정할 수 있어요.");

            _menus.Add("accessibility", _ => new[]
            {
                Entry("access", "접근성 검사"),
                Entry("alt", "대체 텍스트가 없는 그림과 차트 찾기"),
                Entry("merged", "병합한 칸과 빈 열·행 찾기"),
            });
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
            foreach (var (r, stored) in State.Sheet.Rows.Entries)
            {
                var cells = stored.Cells;
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
                State.OpenDialog(ExcelDialogIds.ProtectSheet);

            return Task.CompletedTask;
        }

        private Task ProtectBook()
        {
            if (!_surface.Prepare())
                return Task.CompletedTask;

            if (State.Model.StructureLocked)
                Apply(EditCommand.Create("protect", Workbook(("scope", "workbook"), ("action", "unprotect"), ("structure", true))));
            else
                State.OpenDialog(ExcelDialogIds.ProtectBook);

            return Task.CompletedTask;
        }
    }
}
