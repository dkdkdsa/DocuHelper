using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DocuHelper.Docus;
using DocuHelper.Editors;
using DocuHelper.Tools.Word;
using DocuHelperEditor.Services;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed partial class WordCommandRouter
    {
        private static readonly (string Name, string Label)[] s_properties =
        {
            ("title", "제목"),
            ("subject", "주제"),
            ("author", "작성자"),
            ("keywords", "검색 키워드"),
            ("comments", "설명"),
        };

        private void ProvideFileCommands()
        {
            Provide("showInfo", _ => ShowDocumentInfoAsync());
            Provide("showWordCount", _ => ShowWordCountAsync());
            Provide("exportPdf", _ => ExportPdfAsync());
            Provide("print", _ => PrintAsync());
        }

        private Task ShowWordCountAsync()
        {
            if (File?.Docu is not WordDocu word || Model == null)
            {
                Notify(NoFileReason);
                return Task.CompletedTask;
            }

            var counts = WordStats.Count(word);
            Notify($"‘{File.Name}’에는 {counts.Words:N0}단어, {counts.Characters:N0}글자가 있어요. {counts.Pages}쪽이에요.");
            return Task.CompletedTask;
        }

        private async Task ExportPdfAsync()
        {
            if (File?.Docu is not WordDocu)
            {
                Notify(NoFileReason);
                return;
            }

            var path = await _files.PickSaveFileAsync(Path.GetFileNameWithoutExtension(File.Name) + ".pdf");
            if (string.IsNullOrEmpty(path))
                return;

            ShowPrintResult(await _prints.ExportPdfAsync(path));
        }

        private async Task PrintAsync()
        {
            if (File?.Docu is not WordDocu)
            {
                Notify(NoFileReason);
                return;
            }

            ShowPrintResult(await _prints.PrintAsync());
        }

        private void ShowPrintResult(PrintResult result)
        {
            if (result.Status == PrintStatus.Cancelled)
                return;

            Notify(result.Message ?? (result.IsDone ? "인쇄했어요." : "끝내지 못했어요."));
        }

        private async Task ShowDocumentInfoAsync()
        {
            if (File?.Docu is not WordDocu word || Model == null)
            {
                Notify("문서 정보를 보여 줄 수 없어요.");
                return;
            }

            var counts = WordStats.Count(word);
            var author = WordDocumentProperties.Get(word, "author");
            Notify($"‘{File.Name}’은 {counts.Pages}쪽, {counts.Words:N0}단어, {counts.Characters:N0}글자예요. 작성자는 {(string.IsNullOrWhiteSpace(author) ? "없어요" : author)}이고, 저장 형식은 {Path.GetExtension(File.Name)}이에요.");
            if (State.Dialogs == null)
                return;

            var fields = s_properties
                .Select(p => new DialogField(p.Name, p.Label, "text", JsonSerializer.Serialize(WordDocumentProperties.Get(word, p.Name) ?? string.Empty), Array.Empty<string>(), Array.Empty<string>()))
                .ToArray();
            var values = await State.Dialogs.AskFieldsAsync("word.file.info", new DialogSpec("문서 정보", fields), null);
            if (values == null)
                return;

            var properties = new JsonObject();
            foreach (var (name, _) in s_properties)
                properties[name] = JsonValue.Create(values[name]?.GetValue<string>() ?? string.Empty);

            Apply(new[] { EditCommand.Create("set_document_settings", new JsonObject { ["properties"] = properties }) });
        }
    }
}
