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
        public const string AskUserTool = "ask_user";
        public const string FinishTool = "finish";

        private const int MaxFollowUps = 3;
        private const int MaxDetailLength = 24;

        private const string StepDescription = "Short Korean progress label the user sees while you work, e.g. \"요약표 만들기\". Give calls that belong to the same step the same text.";

        private const string LabelDescription = "Short Korean sentence for the user's change list, ending in 요, e.g. \"거래처마다 금액 합계를 구해요\". One change item per call.";

        private const string ListFilesSchema = """
            {"type":"object","properties":{}}
            """;

        private const string CreateFileSchema = """
            {
              "type": "object",
              "properties": {
                "name": {"type": "string", "description": "New file name ending in .xlsx, .docx or .pptx, e.g. \"7월 회의 보고서.docx\"."},
                "label": {"type": "string", "description": "Short Korean sentence for the change list, e.g. \"7월 회의 보고서를 새로 만들어요\"."},
                "step": {"type": "string", "description": "Short Korean progress label, e.g. \"새 보고서 만들기\"."}
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
                "step": {"type": "string", "description": "Short Korean progress label, e.g. \"같은 거래처인지 여쭤보기\"."}
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
        private readonly List<AgentToolDefinition> _definitions = new();
        private readonly Dictionary<Docu, DocuState> _before = new();
        private readonly HashSet<Docu> _locked = new();
        private readonly List<Docu> _read = new();
        private readonly List<AgentChange> _changes = new();
        private readonly List<string> _untouched = new();
        private readonly List<string> _followUps = new();

        public AgentToolbox(Workspace workspace, AgentRequest request, IReadOnlyList<string> notes)
        {
            _workspace = workspace;
            _request = request;
            _notes = notes;
            BuildDefinitions();
        }

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
            var docu = ResolveDocu(args.GetOptionalString("file"), editor);
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
            if (!_before.ContainsKey(docu))
                _before[docu] = docu.Capture();

            if (_locked.Add(docu))
                docu.SetLocked(true);
        }

        private Docu ResolveDocu(string fileName, DocuEditor editor)
        {
            var docu = _workspace.FindDocu(fileName);
            if (docu == null && string.IsNullOrWhiteSpace(fileName))
            {
                var candidates = _workspace.Docus.Where(d => d.Type == editor.TargetType).ToList();
                if (candidates.Count == 1)
                    docu = candidates[0];
            }

            if (docu == null)
                throw new ToolInputException($"Could not find the file '{fileName}'. Files in the folder: {FileList()}.");

            if (docu.Type != editor.TargetType)
                throw new ToolInputException($"'{docu.Name}' is not a {KindName(editor.TargetType)} file, so this tool cannot open it.");

            return docu;
        }

        private ToolResult ListFiles()
        {
            if (_workspace.Docus.Count == 0)
                return ToolResult.Ok("The working folder has no Excel, Word or PowerPoint files yet.", "파일 없음");

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

            return ToolResult.Ok($"Created '{docu.Name}' in the working folder. It is blank. Fill it with the {KindName(docu.Type)} tools using file=\"{docu.Name}\".", "새 파일");
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

            var step = new AgentStep { Label = StepLabel(args, "여쭤보기"), FileName = question.FileName };
            host.OnStepStarted(step);
            var answer = await host.AskUserAsync(question, cancellationToken);
            step.Detail = Shorten(answer);
            host.OnStepCompleted(step);

            return ToolResult.Ok($"The user answered: {answer}");
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

        private void BuildDefinitions()
        {
            foreach (var editor in _workspace.Editors.Values.OrderBy(e => e.TargetType))
            {
                foreach (var tool in editor.Tools.Values)
                    _definitions.Add(Define(tool.Name, tool.Description, WithCommonFields(tool.InputSchema, !tool.IsReadOnly, KindName(editor.TargetType))));
            }

            _definitions.Add(Define(ListFilesTool, "List the files in the user's working folder with a one-line summary of each.", ListFilesSchema));
            _definitions.Add(Define(CreateFileTool, "Create a new blank Excel, Word or PowerPoint file in the working folder and open it. The blank file is written right away; your edits to it stay unsaved until the user saves.", CreateFileSchema));
            _definitions.Add(Define(AskUserTool, "Ask the user one question and wait for the answer. Use it only when you cannot pick a sensible default.", AskUserSchema));
            _definitions.Add(Define(FinishTool, "Report the result to the user and end the task. Call it once, after all changes are made.", FinishSchema));
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

            if (write)
                properties["label"] = new JsonObject { ["type"] = "string", ["description"] = LabelDescription };

            properties["step"] = new JsonObject { ["type"] = "string", ["description"] = StepDescription };

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
