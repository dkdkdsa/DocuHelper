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
using DocuHelperEditor.Services;
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
            Provide("word.mail.merge.editlist", _ => OpenRecipientsAsync());
            Provide("word.mail.finish.merge", _ => FinishMergeAsync());
        }

        private IReadOnlyList<RibbonMenuItem> FieldMenu(CommandSpec spec)
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
            return new MenuEntry(new CommandMenuSpec { Label = arg.Substring(FieldPrefix.Length), Args = args.ToJsonString() }, args);
        }

        private Task OpenRecipientsAsync()
        {
            var name = State.Recipients?.FileName;
            if (name == null)
            {
                Notify("받는 사람 목록을 먼저 골라 주세요.");
                return Task.CompletedTask;
            }

            var file = Workspace.Files.FirstOrDefault(f => f.Name == name);
            if (file == null)
            {
                Notify("받는 사람 파일이 작업 폴더에 없어요. 받는 사람을 다시 골라 주세요.");
                return Task.CompletedTask;
            }

            Workspace.Open(file.Id);
            return Task.CompletedTask;
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
                new DialogField("file", "엑셀 파일", "choice", JsonSerializer.Serialize(sheets[0]), sheets, sheets),
                new DialogField("sheet", "시트 이름(비우면 첫 시트)", "text", "\"\"", Array.Empty<string>(), Array.Empty<string>()),
            };
            var values = await State.Dialogs.AskFieldsAsync("word.mail.recipients", new DialogSpec("받는 사람 목록", fields), null);
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
            catch (Exception e)
            {
                AppLog.Write("Word mail recipients read failed", e);
                recipients = null;
            }

            if (recipients == null)
            {
                Notify("엑셀 파일을 읽지 못했어요. 첫 줄에 필드 이름이 있는지 확인해 주세요.");
                return;
            }

            State.Recipients = recipients;
            var limit = recipients.Rows.Count >= WordMailRecipients.MaxRecipients ? $" 최대 {WordMailRecipients.MaxRecipients}명까지만 불러왔어요." : string.Empty;
            Notify($"받는 사람 {recipients.Rows.Count}명을 불러왔어요. 필드: {string.Join(", ", recipients.Fields)}.{limit}");
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

                    foreach (var paragraph in part.RootElement.Descendants<Paragraph>().ToList())
                    {
                        ReplaceFields(paragraph, values);
                        UnlinkMergeFields(paragraph);
                    }
                }

                document.Save();
            }

            return stream.ToArray();
        }

        private static void ReplaceFields(Paragraph paragraph, IReadOnlyDictionary<string, string> values)
        {
            var texts = paragraph.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>().ToList();
            var searchFrom = 0;
            while (true)
            {
                var joined = string.Concat(texts.Select(t => t.Text ?? string.Empty));
                var field = FirstField(joined, values, searchFrom);
                if (field == null)
                    return;

                ReplaceRange(texts, field.Value.Start, field.Value.Start + field.Value.Length, field.Value.Value);
                searchFrom = field.Value.Start + field.Value.Value.Length;
            }
        }

        private static void UnlinkMergeFields(Paragraph paragraph)
        {
            var fields = new Stack<(bool Merge, List<DocumentFormat.OpenXml.Wordprocessing.Run> Runs)>();
            var unlink = new List<DocumentFormat.OpenXml.Wordprocessing.Run>();
            foreach (var run in paragraph.Descendants<DocumentFormat.OpenXml.Wordprocessing.Run>().ToList())
            {
                var kind = run.GetFirstChild<FieldChar>()?.FieldCharType?.Value;
                var code = run.GetFirstChild<FieldCode>();
                if (kind == FieldCharValues.Begin)
                {
                    fields.Push((false, new List<DocumentFormat.OpenXml.Wordprocessing.Run> { run }));
                    continue;
                }

                if (fields.Count == 0)
                    continue;

                if (code != null)
                {
                    var top = fields.Pop();
                    top.Runs.Add(run);
                    fields.Push((top.Merge || (code.Text ?? string.Empty).Trim().StartsWith("MERGEFIELD", StringComparison.OrdinalIgnoreCase), top.Runs));
                    continue;
                }

                if (kind == FieldCharValues.Separate)
                {
                    fields.Peek().Runs.Add(run);
                    continue;
                }

                if (kind == FieldCharValues.End)
                {
                    var done = fields.Pop();
                    done.Runs.Add(run);
                    if (done.Merge)
                        unlink.AddRange(done.Runs);
                }
            }

            foreach (var run in unlink)
                run.Remove();
        }

        private static (int Start, int Length, string Value)? FirstField(string text, IReadOnlyDictionary<string, string> values, int from)
        {
            (int Start, int Length, string Value)? first = null;
            foreach (var pair in values)
            {
                var token = "«" + pair.Key + "»";
                var index = text.IndexOf(token, from, StringComparison.Ordinal);
                if (index >= 0 && (first == null || index < first.Value.Start))
                    first = (index, token.Length, pair.Value);
            }

            return first;
        }

        private static void ReplaceRange(IReadOnlyList<DocumentFormat.OpenXml.Wordprocessing.Text> texts, int start, int end, string value)
        {
            var position = 0;
            var placed = false;
            foreach (var text in texts)
            {
                var content = text.Text ?? string.Empty;
                var from = position;
                var to = position + content.Length;
                position = to;
                if (to <= start || from >= end)
                    continue;

                var head = content.Substring(0, Math.Max(start, from) - from);
                var tail = content.Substring(Math.Min(end, to) - from);
                text.Text = head + (placed ? string.Empty : value) + tail;
                text.Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve;
                placed = true;
            }
        }
    }
}
