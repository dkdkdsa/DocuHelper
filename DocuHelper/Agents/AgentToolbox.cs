using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using DocuHelper.Docus;
using DocuHelper.Editors;
using DocuHelper.Tools;

namespace DocuHelper.Agents
{
    public class AgentToolbox
    {
        public const string ListFilesTool = "list_files";
        public const string CreateFileTool = "create_file";
        public const string ReadGuideTool = "read_guide";
        public const string AskUserTool = "ask_user";
        public const string FinishTool = "finish";

        private const int MaxFollowUps = 3;
        private const int MaxDetailLength = 24;

        private const string ListFilesSchema = """
            {"type":"object","properties":{}}
            """;

        private const string CreateFileSchema = """
            {
              "type": "object",
              "properties": {
                "name": {"type": "string", "description": "New file name ending in .xlsx, .docx, .hwpx, .pptx or .dash, e.g. \"7월 회의 보고서.docx\". Use .hwpx for a Hancom 한글 document and .dash for a dashboard (현황판) built from other files."},
                "label": {"type": "string"},
                "step": {"type": "string"}
              },
              "required": ["name"]
            }
            """;

        private const string AskUserSchema = """
            {
              "type": "object",
              "properties": {
                "question": {"type": "string", "description": "One short, plain Korean question."},
                "kind": {"type": "string", "enum": ["choice", "which_file", "big_change", "conflict"], "description": "choice: pick one way to do it. which_file: more than one file could be meant. big_change: confirm before changing more than 20 places. conflict: you want to overwrite something the user changed by hand."},
                "options": {
                  "type": "array",
                  "description": "2 or 3 answers, best first. The user can always type their own answer instead.",
                  "items": {
                    "type": "object",
                    "properties": {
                      "label": {"type": "string", "description": "The answer in the user's words, short."},
                      "detail": {"type": "string", "description": "Optional half-line explanation."},
                      "file": {"type": "string", "description": "which_file only: the file this answer picks."}
                    },
                    "required": ["label"]
                  }
                },
                "hint": {"type": "string", "description": "Optional one-line note under the question."},
                "file": {"type": "string", "description": "The file the question is about, if any."},
                "cells": {"type": "array", "description": "Excel only: the cells the question is about, such as B2 or '거래처 목록'!B2:B9. They get a pencil circle on the sheet. At most 10.", "items": {"type": "string"}},
                "user_value": {"type": "string", "description": "conflict only: what the user typed by hand."},
                "suggested_value": {"type": "string", "description": "conflict only: what you would put instead."},
                "impacts": {
                  "type": "array",
                  "description": "big_change only: how many places would change in each file.",
                  "items": {
                    "type": "object",
                    "properties": {
                      "file": {"type": "string"},
                      "count": {"type": "integer"}
                    },
                    "required": ["file", "count"]
                  }
                },
                "step": {"type": "string"}
              },
              "required": ["question", "kind", "options"]
            }
            """;

        private const string FinishSchema = """
            {
              "type": "object",
              "properties": {
                "summary": {"type": "string", "description": "What you did, in one or two short, friendly Korean sentences. Mention anything the user should check."},
                "untouched": {"type": "array", "items": {"type": "string"}, "description": "Optional: what you deliberately left as it was, as short Korean sentences, e.g. \"‘6월 매출’ 시트는 그대로 둬요\"."},
                "follow_ups": {"type": "array", "items": {"type": "string"}, "description": "Optional: up to 3 short next requests, written the way the user would type them, e.g. \"평균 금액도 내 줘\"."}
              },
              "required": ["summary"]
            }
            """;

        private readonly Workspace _workspace;
        private readonly AgentRequest _request;
        private readonly IReadOnlyList<string> _notes;
        private readonly Dictionary<Docu, DocuState> _before = new();
        private readonly HashSet<Docu> _locked = new();
        private Docu _lastWritten;
        private readonly List<Docu> _read = new();
        private readonly List<AgentChange> _changes = new();
        private readonly List<string> _untouched = new();
        private readonly List<string> _followUps = new();

        private IReadOnlyList<DocuType> _types = Array.Empty<DocuType>();
        private IReadOnlyList<AgentToolDefinition> _definitions;

        public AgentToolbox(Workspace workspace, AgentRequest request, IReadOnlyList<string> notes, IEnumerable<DocuType> keepTypes = null)
        {
            _workspace = workspace;
            _request = request;
            _notes = notes;
            _types = (keepTypes ?? Enumerable.Empty<DocuType>()).Where(workspace.Editors.ContainsKey).Distinct().OrderBy(type => type).ToList();
            Reveal();
        }

        public IReadOnlyList<DocuType> Types => _types;

        public IReadOnlyList<AgentToolDefinition> Definitions => _definitions;

        public IReadOnlyDictionary<Docu, DocuState> Before => _before;

        public IReadOnlyList<AgentChange> Changes => _changes;

        public bool IsFinished { get; private set; }

        public string Summary { get; private set; }

        public string DescribeContext()
        {
            var text = new StringBuilder();
            text.AppendLine("<workspace>");
            text.AppendLine($"Today: {DateTime.Now.ToString("yyyy-MM-dd dddd", CultureInfo.InvariantCulture)}");
            text.AppendLine($"Folder: {Path.GetFileName(_workspace.FolderPath)}");

            if (_workspace.Docus.Count == 0)
            {
                text.AppendLine("Files: none yet. You can make one with create_file.");
            }
            else
            {
                text.AppendLine("Files:");
                foreach (var docu in _workspace.Docus)
                    text.AppendLine($"- {docu.Name} ({KindName(docu.Type)}{(docu.IsDirty ? ", has unsaved edits" : string.Empty)}): {Describe(docu)}");
            }

            if (!string.IsNullOrWhiteSpace(_request.ActiveFile))
                text.AppendLine($"The user is looking at: {_request.ActiveFile}");

            if (!string.IsNullOrWhiteSpace(_request.Selection))
                text.AppendLine($"The user has selected: {_request.Selection}");

            if (!_request.AskBigChanges)
                text.AppendLine("The user turned off big-change checks: don't ask big_change questions, just make the changes.");

            if (_request.Attachments.Count > 0)
                text.AppendLine($"Attached to this message: {string.Join(", ", _request.Attachments.Select(a => a.Name))}");

            if (_notes.Count > 0)
            {
                text.AppendLine("Since your last turn:");
                foreach (var note in _notes)
                    text.AppendLine($"- {note}");
            }

            text.Append("</workspace>");
            return text.ToString();
        }

        public async Task<ToolResult> ExecuteAsync(string name, JsonElement input, IAgentHost host, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var args = new ToolInput(input);
            try
            {
                switch (name)
                {
                    case FinishTool:
                        return Finish(args);
                    case AskUserTool:
                        return await AskUserAsync(args, host, cancellationToken);
                    case ListFilesTool:
                        return Track(host, args, "작업 폴더 살펴보기", null, ListFiles);
                    case CreateFileTool:
                        return Track(host, args, $"{args.GetString("name")} 만들기", null, () => CreateFile(args));
                    case ReadGuideTool:
                        return Track(host, args, "설명서 읽기", null, () => ReadGuide(args));
                    default:
                        return RunFormatTool(name, input, args, host);
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                return ToolResult.Fail(e.Message);
            }
        }

        public AgentResult BuildResult(string text)
        {
            var result = new AgentResult
            {
                Text = string.IsNullOrWhiteSpace(Summary) ? text?.Trim() : Summary.Trim(),
                Changes = _changes.Where(c => _workspace.Docus.Contains(c.Docu)).ToList(),
                ReadOnlyFiles = _read.Where(d => !_before.ContainsKey(d) && _workspace.Docus.Contains(d)).Select(d => d.Name).ToList(),
            };
            result.Untouched.AddRange(_untouched);
            result.FollowUps.AddRange(_followUps);
            return result;
        }

        public void Rollback()
        {
            foreach (var pair in _before)
            {
                if (pair.Value == null)
                    _workspace.DiscardCreated(pair.Key);
                else if (_workspace.Docus.Contains(pair.Key))
                    pair.Key.Restore(pair.Value);
            }

            _before.Clear();
            _changes.Clear();
        }

        public void Release()
        {
            foreach (var docu in _locked)
            {
                if (_workspace.Docus.Contains(docu))
                    docu.SetLocked(false);
            }

            _locked.Clear();
        }

        private ToolResult RunFormatTool(string name, JsonElement input, ToolInput args, IAgentHost host)
        {
            var editor = _workspace.Editors.Values.FirstOrDefault(e => e.Tools.ContainsKey(name));
            if (editor == null)
                return ToolResult.Fail($"There is no tool named '{name}'.");

            var tool = editor.Tools[name];
            var docu = ResolveDocu(args.GetOptionalString("file"), name, editor);
            editor = _workspace.GetEditor(docu);
            if (tool.IsReadOnly)
                return Track(host, args, $"{docu.Name} 읽기", docu.Name, () => Read(editor, docu, name, input));

            var fallback = args.GetOptionalString("label") ?? $"{docu.Name} 고치기";
            return Track(host, args, fallback, docu.Name, () => Write(editor, docu, name, input, args));
        }

        private ToolResult Read(DocuEditor editor, Docu docu, string name, JsonElement input)
        {
            var result = editor.Execute(docu, name, input);
            if (!result.IsError && !_read.Contains(docu))
                _read.Add(docu);

            return result;
        }

        private ToolResult Write(DocuEditor editor, Docu docu, string name, JsonElement input, ToolInput args)
        {
            Prepare(docu);
            var result = editor.Execute(docu, name, input);
            if (result.IsError)
                return result;

            var change = result.Change;
            if (change != null)
            {
                var label = args.GetOptionalString("label");
                if (!string.IsNullOrWhiteSpace(label))
                    change.Label = label.Trim();

                if (string.IsNullOrWhiteSpace(change.Label))
                    change.Label = "내용을 고쳤어요";

                docu.AddChange(change);
                _changes.Add(new AgentChange { Docu = docu, Change = change });
            }

            docu.MarkDirty();
            return result;
        }

        private void Prepare(Docu docu)
        {
            _lastWritten = docu;
            if (!_before.ContainsKey(docu))
                _before[docu] = docu.Capture();

            if (_locked.Add(docu))
                docu.SetLocked(true);
        }

        private Docu ResolveDocu(string fileName, string toolName, DocuEditor editor)
        {
            var docu = _workspace.FindDocu(fileName);
            if (docu == null && string.IsNullOrWhiteSpace(fileName))
            {
                var candidates = _workspace.Docus.Where(d => Handles(d, toolName)).ToList();
                if (candidates.Count == 1)
                    docu = candidates[0];
            }

            if (docu == null)
                throw new ToolInputException($"Could not find the file '{fileName}'. Files in the folder: {FileList()}.");

            if (!Handles(docu, toolName))
                throw new ToolInputException($"'{docu.Name}' is not a {KindName(editor.TargetType)} file, so this tool cannot open it.");

            return docu;
        }

        private bool Handles(Docu docu, string toolName) =>
            _workspace.Editors.TryGetValue(docu.Type, out var editor) && editor.Tools.ContainsKey(toolName);

        private ToolResult ListFiles()
        {
            Reveal();
            if (_workspace.Docus.Count == 0)
                return ToolResult.Ok("The working folder has no Excel, Word, 한글 or PowerPoint files yet.", "파일 없음");

            var text = new StringBuilder();
            foreach (var docu in _workspace.Docus)
                text.AppendLine($"- {docu.Name} ({KindName(docu.Type)}): {Describe(docu)}");

            var others = _workspace.ListFolderFiles()
                .Where(path => _workspace.FindEditor(path) == null)
                .Select(Path.GetFileName)
                .ToList();
            if (others.Count > 0)
                text.AppendLine($"Other files you cannot open: {string.Join(", ", others)}");

            return ToolResult.Ok(text.ToString().TrimEnd(), $"{_workspace.Docus.Count}개");
        }

        private ToolResult CreateFile(ToolInput args)
        {
            var docu = _workspace.CreateDocu(args.GetString("name"));
            _before[docu] = null;
            _locked.Add(docu);
            docu.SetLocked(true);

            var change = new DocuChange
            {
                Label = args.GetOptionalString("label") ?? $"‘{docu.Name}’ 파일을 새로 만들어요",
                Location = "새 파일",
                Icon = "file-plus",
            };
            docu.AddChange(change);
            _changes.Add(new AgentChange { Docu = docu, Change = change });
            docu.MarkDirty();
            Reveal();

            return ToolResult.Ok($"Created '{docu.Name}' in the working folder. It is blank. Fill it with the {KindName(docu.Type)} tools using file=\"{docu.Name}\".", "새 파일");
        }

        private ToolResult ReadGuide(ToolInput args)
        {
            var topic = args.GetString("topic");
            var page = _workspace.Guide.Find(topic)
                ?? throw new ToolInputException($"There is no guide page '{topic}'. Pages: {string.Join(", ", _workspace.Guide.Pages.Select(p => p.Topic))}.");

            return ToolResult.Ok(page.Text, page.Title);
        }

        private async Task<ToolResult> AskUserAsync(ToolInput args, IAgentHost host, CancellationToken cancellationToken)
        {
            var question = new AgentQuestion
            {
                Kind = ParseKind(args.GetOptionalString("kind")),
                Question = args.GetString("question"),
                Hint = args.GetOptionalString("hint"),
                FileName = _workspace.FindDocu(args.GetOptionalString("file"))?.Name,
                UserValue = args.GetOptionalString("user_value"),
                SuggestedValue = args.GetOptionalString("suggested_value"),
            };
            question.Choices.AddRange(ParseChoices(args));
            question.Impacts.AddRange(ParseImpacts(args));
            question.Cells.AddRange(AskCells.Parse(args));
            question.FileName ??= _lastWritten?.Name;

            var mark = _workspace.RunHandEditCount;
            var step = new AgentStep { Label = StepLabel(args, "여쭤보기"), FileName = question.FileName };
            host.OnStepStarted(step);
            var answer = await AskWhileUnlocked(question, host, cancellationToken);
            step.Detail = Shorten(answer);
            host.OnStepCompleted(step);

            var text = $"The user answered: {answer}";
            var changed = _workspace.HandEditsSince(mark);
            if (changed.Count > 0)
            {
                text += "\nWhile you waited, the user changed these by hand:\n" + string.Join("\n", changed) + "\nRead those places again before you write over them.";
            }

            return ToolResult.Ok(text);
        }

        private async Task<string> AskWhileUnlocked(AgentQuestion question, IAgentHost host, CancellationToken cancellationToken)
        {
            var unlocked = new List<Docu>();
            try
            {
                foreach (var docu in _locked.Where(d => _workspace.Docus.Contains(d)).ToList())
                {
                    docu.SetLocked(false);
                    unlocked.Add(docu);
                }

                return await host.AskUserAsync(question, cancellationToken);
            }
            finally
            {
                foreach (var docu in unlocked)
                {
                    if (_workspace.Docus.Contains(docu))
                        docu.SetLocked(true);
                }
            }
        }

        private ToolResult Finish(ToolInput args)
        {
            Summary = args.GetOptionalString("summary");

            _untouched.Clear();
            _untouched.AddRange(args.GetStringList("untouched", false).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()));

            _followUps.Clear();
            _followUps.AddRange(args.GetStringList("follow_ups", false).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Take(MaxFollowUps));

            IsFinished = true;
            return ToolResult.Ok("Shown to the user.");
        }

        private static ToolResult Track(IAgentHost host, ToolInput args, string fallbackLabel, string fileName, Func<ToolResult> run)
        {
            var step = new AgentStep { Label = StepLabel(args, fallbackLabel), FileName = fileName };
            host.OnStepStarted(step);

            ToolResult result = null;
            try
            {
                result = run();
                return result;
            }
            finally
            {
                step.Detail = result == null || result.IsError ? null : Shorten(result.Detail);
                host.OnStepCompleted(step);
            }
        }

        private static List<AgentChoice> ParseChoices(ToolInput args)
        {
            var choices = new List<AgentChoice>();
            if (!args.Has("options"))
                return choices;

            var options = args.Get("options");
            if (options.ValueKind != JsonValueKind.Array)
                throw new ToolInputException("'options' must be a list.");

            foreach (var option in options.EnumerateArray())
            {
                if (option.ValueKind == JsonValueKind.Object)
                {
                    var item = new ToolInput(option);
                    choices.Add(new AgentChoice
                    {
                        Label = item.GetString("label"),
                        Detail = item.GetOptionalString("detail"),
                        FileName = item.GetOptionalString("file"),
                    });
                    continue;
                }

                var label = ToolInput.AsText(option);
                if (!string.IsNullOrWhiteSpace(label))
                    choices.Add(new AgentChoice { Label = label });
            }

            return choices;
        }

        private static List<AgentImpact> ParseImpacts(ToolInput args)
        {
            var impacts = new List<AgentImpact>();
            if (!args.Has("impacts"))
                return impacts;

            var items = args.Get("impacts");
            if (items.ValueKind != JsonValueKind.Array)
                throw new ToolInputException("'impacts' must be a list.");

            foreach (var element in items.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                    continue;

                var item = new ToolInput(element);
                impacts.Add(new AgentImpact { FileName = item.GetString("file"), Count = item.GetInt("count") });
            }

            return impacts;
        }

        private static AgentQuestionKind ParseKind(string kind) => kind switch
        {
            "which_file" => AgentQuestionKind.WhichFile,
            "big_change" => AgentQuestionKind.BigChange,
            "conflict" => AgentQuestionKind.Conflict,
            _ => AgentQuestionKind.Choice,
        };

        private static string StepLabel(ToolInput args, string fallback)
        {
            var step = args.GetOptionalString("step");
            return string.IsNullOrWhiteSpace(step) ? fallback : step.Trim();
        }

        private static string Shorten(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var value = text.Trim();
            return value.Length <= MaxDetailLength ? value : value.Substring(0, MaxDetailLength).TrimEnd() + "…";
        }

        private static string KindName(DocuType type) => type switch
        {
            DocuType.Excel => "Excel",
            DocuType.Word => "Word",
            DocuType.Hwp => "한글",
            DocuType.Dash => "dashboard (현황판)",
            _ => "PowerPoint",
        };

        private string Describe(Docu docu)
        {
            try
            {
                return _workspace.GetEditor(docu).Describe(docu);
            }
            catch (Exception)
            {
                return "could not be read";
            }
        }

        private string FileList() => _workspace.Docus.Count == 0 ? "none" : string.Join(", ", _workspace.Docus.Select(d => d.Name));

        private void Reveal()
        {
            var types = FolderTypes()
                .Where(_workspace.Editors.ContainsKey)
                .Concat(_types)
                .Distinct()
                .OrderBy(type => type)
                .ToList();
            if (_definitions != null && types.Count == _types.Count)
                return;

            _types = types;
            _definitions = BuildDefinitions();
        }

        private IEnumerable<DocuType> FolderTypes()
        {
            foreach (var docu in _workspace.Docus)
                yield return docu.Type;

            foreach (var path in FolderFiles())
            {
                var editor = _workspace.FindEditor(path);
                if (editor != null)
                    yield return editor.TargetType;
            }
        }

        private IReadOnlyList<string> FolderFiles()
        {
            try
            {
                return _workspace.ListFolderFiles();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return Array.Empty<string>();
            }
        }

        private List<AgentToolDefinition> BuildDefinitions()
        {
            var definitions = new List<AgentToolDefinition>();
            var defined = new HashSet<string>();
            foreach (var editor in _workspace.Editors.Values.Where(e => _types.Contains(e.TargetType)).OrderBy(e => e.TargetType))
            {
                var kind = KindName(editor.TargetType);
                if (editor.TargetType == DocuType.Word && _types.Contains(DocuType.Hwp))
                    kind = "Word or 한글";

                foreach (var tool in editor.Tools.Values)
                {
                    if (defined.Add(tool.Name))
                        definitions.Add(Define(tool.Name, tool.Description, WithCommonFields(tool.InputSchema, !tool.IsReadOnly, kind)));
                }
            }

            definitions.Add(Define(ListFilesTool, "List the files in the user's working folder with a one-line summary of each.", ListFilesSchema));
            definitions.Add(Define(CreateFileTool, "Create a new blank Excel, Word, 한글, PowerPoint or dashboard (.dash) file in the working folder and open it. The blank file is written right away; your edits to it stay unsaved until the user saves. If you have no editing tools for that kind of file yet, you get them on your next step.", CreateFileSchema));
            if (_workspace.Guide.Pages.Count > 0)
                definitions.Add(Define(ReadGuideTool, "Read one page of the app's guide: how its screens, menus and buttons work, and how to build a dashboard. Use it when the user asks how to do something in the app or where a button is, and before you build a dashboard. It changes nothing.", GuideSchema()));

            definitions.Add(Define(AskUserTool, "Ask the user one question and wait for the answer. Use it only when you cannot pick a sensible default.", AskUserSchema));
            definitions.Add(Define(FinishTool, "Report the result to the user and end the task. Call it once, after all changes are made.", FinishSchema));
            return definitions;
        }

        private JsonElement GuideSchema()
        {
            var pages = _workspace.Guide.Pages;
            var schema = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["topic"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["enum"] = new JsonArray(pages.Select(page => (JsonNode)page.Topic).ToArray()),
                        ["description"] = string.Join("\n", pages.Select(page => $"{page.Topic}: {page.Summary}")),
                    },
                    ["step"] = new JsonObject { ["type"] = "string" },
                },
                ["required"] = new JsonArray("topic"),
            };

            return JsonSerializer.SerializeToElement(schema);
        }

        private static AgentToolDefinition Define(string name, string description, string schema) =>
            Define(name, description, JsonSerializer.SerializeToElement(JsonNode.Parse(schema)));

        private static AgentToolDefinition Define(string name, string description, JsonElement schema) => new()
        {
            Name = name,
            Description = description,
            InputSchema = schema,
        };

        private static JsonElement WithCommonFields(string schema, bool write, string kind)
        {
            var source = string.IsNullOrWhiteSpace(schema) ? new JsonObject() : JsonNode.Parse(schema)?.AsObject() ?? new JsonObject();

            var properties = new JsonObject
            {
                ["file"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = $"Name of the {kind} file in the working folder.",
                },
            };

            if (source["properties"] is JsonObject own)
            {
                foreach (var pair in own)
                    properties[pair.Key] = pair.Value?.DeepClone();
            }

            // label and step are explained once in ClaudePrompt.WorkRules, not in every tool.
            if (write)
                properties["label"] = new JsonObject { ["type"] = "string" };

            properties["step"] = new JsonObject { ["type"] = "string" };

            var required = new JsonArray { "file" };
            if (source["required"] is JsonArray ownRequired)
            {
                foreach (var item in ownRequired)
                    required.Add(item?.DeepClone());
            }

            if (write)
                required.Add("label");

            var result = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["required"] = required,
            };
            return JsonSerializer.SerializeToElement(result);
        }
    }
}
