using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DocuHelper.Docus;
using DocuHelper.Editors;
using DocuHelper.Tools.Word;
using DocuHelperEditor.State.Papers;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using static DocuHelperEditor.State.Ribbon.RibbonMenus;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed partial class WordCommandRouter
    {
        private const string FieldPrefix = "field:";

        private void ProvideMailCommands()
        {
            Provide("word.mail.merge.recipients", _ => ChooseRecipientsAsync());
            Provide("word.mail.merge.recipients.existing", _ => ChooseRecipientsAsync());
            Provide("word.mail.finish.merge", _ => FinishMergeAsync());
        }

        private IReadOnlyList<RibbonMenuItem> FieldMenu(WordCommandSpec spec)
        {
            var items = new List<RibbonMenuItem>();
            if (State.Recipients == null)
                items.Add(Item("field:none", "받는 사람 목록을 먼저 골라 주세요", disabled: true, reason: "받는 사람 선택을 먼저 해 주세요."));
            else
            {
                items.AddRange(State.Recipients.Fields.Select(name => Item(FieldPrefix + name, name)));
                items.Add(Separator());
            }

            return items.Concat(Entries(spec, false)).ToList();
        }

        private MenuEntry FieldEntryOf(string arg)
        {
            if (arg == null || !arg.StartsWith(FieldPrefix, StringComparison.Ordinal))
                return null;

            var args = new JsonObject { ["name"] = arg.Substring(FieldPrefix.Length) };
            return new MenuEntry(new WordMenuSpec(arg.Substring(FieldPrefix.Length), args.ToJsonString(), null), args);
        }

        private async Task ChooseRecipientsAsync()
        {
            var sheets = Workspace.Workspace.ListFolderFiles()
                .Select(Path.GetFileName)
                .Where(name => name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (sheets.Length == 0)
            {
                Notify("작업 폴더에 엑셀(.xlsx) 파일이 없어요. 받는 사람 목록을 엑셀 파일로 넣은 뒤 다시 골라 주세요.");
                return;
            }

            if (State.Dialogs == null)
                return;

            var fields = new[]
            {
                new WordDialogField("file", "엑셀 파일", "choice", JsonSerializer.Serialize(sheets[0]), sheets, sheets),
                new WordDialogField("sheet", "시트 이름(비우면 첫 시트)", "text", "\"\"", Array.Empty<string>(), Array.Empty<string>()),
            };
            var values = await State.Dialogs.AskFieldsAsync("word.mail.recipients", new WordDialogSpec("받는 사람 목록", fields), null);
            if (values == null)
                return;

            var file = values["file"]?.GetValue<string>();
            var sheet = values["sheet"]?.GetValue<string>();
            var path = Path.Combine(Workspace.Workspace.FolderPath, file ?? string.Empty);
            WordMailRecipients recipients;
            try
            {
                recipients = WordMailRecipients.Read(path, sheet);
            }
            catch (Exception)
            {
                recipients = null;
            }

            if (recipients == null)
            {
                Notify("엑셀 파일을 읽지 못했어요. 첫 줄에 필드 이름이 있는지 확인해 주세요.");
                return;
            }

            State.Recipients = recipients;
            Notify($"받는 사람 {recipients.Rows.Count}명을 불러왔어요. 필드: {string.Join(", ", recipients.Fields)}");
            State.Touch();
            Workspace.Notify();
        }

        private async Task FinishMergeAsync()
        {
            if (File?.Docu is not WordDocu word || State.Recipients == null || State.Recipients.Rows.Count == 0)
            {
                Notify("받는 사람 목록을 먼저 골라 주세요.");
                return;
            }

            var bytes = word.ToBytes();
            var name = Path.GetFileNameWithoutExtension(File.Name);
            var rows = State.Recipients.Rows;
            for (var index = 0; index < rows.Count; index++)
            {
                var merged = MergedCopy(bytes, rows[index]);
                Workspace.Workspace.AddFile($"{name} {index + 1}.docx", merged, "편지 병합");
            }

            Notify($"받는 사람 {rows.Count}명의 병합 문서를 작업 폴더에 만들었어요.");
            await Task.CompletedTask;
            Workspace.Notify();
        }

        private static byte[] MergedCopy(byte[] bytes, IReadOnlyDictionary<string, string> values)
        {
            using var stream = new MemoryStream();
            stream.Write(bytes, 0, bytes.Length);
            stream.Position = 0;
            using (var document = WordprocessingDocument.Open(stream, true))
            {
                foreach (var part in WordDrawings.Stories(document))
                {
                    if (part.RootElement == null)
                        continue;

                    foreach (var text in part.RootElement.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>())
                    {
                        var replaced = text.Text;
                        foreach (var pair in values)
                            replaced = replaced.Replace("«" + pair.Key + "»", pair.Value, StringComparison.Ordinal);

                        if (replaced != text.Text)
                            text.Text = replaced;
                    }
                }

                document.Save();
            }

            return stream.ToArray();
        }
    }
}
