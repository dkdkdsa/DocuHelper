using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DocuHelper.Editors;
using DocuHelper.Tools;
using DocuHelperEditor.Services;
using DocuHelperEditor.State;
using DocuHelperEditor.State.Ribbon;
using Microsoft.JSInterop;
using static DocuHelperEditor.State.Ribbon.RibbonMenus;

namespace DocuHelperEditor.Components.Paper.Ppt
{
    public sealed partial class PptRibbonCommands : RibbonRouter
    {
        public const string UndoId = "ppt.trailing.undo";
        public const string RedoId = "ppt.trailing.redo";

        private const string EntryPrefix = "m:";
        private const string QuickEntry = "m:quick";
        private const string AutoColor = "auto";
        private const string NoOptionsReason = "먼저 효과를 골라 주세요.";
        private const string StateUnknownReason = "지금 모양을 아직 읽지 못해서 켜고 끌 수 없어요.";
        private const string SampleReason = "이 화면은 예시라서 고칠 수 없어요.";
        private const string NoSlideReason = "슬라이드가 하나도 없어요. 먼저 새 슬라이드를 넣어 주세요.";
        private const string NoShapeReason = "도형이나 글상자를 먼저 골라 주세요.";
        private const string NoTextReason = "글자를 고칠 글상자를 먼저 골라 주세요.";
        private const string TwoShapesReason = "도형을 둘 이상 골라야 해요.";
        private const string ThreeShapesReason = "도형을 셋 이상 골라야 해요.";
        private const string PrintReason = "인쇄와 PDF 저장은 아직 준비 중이에요.";
        private const double DefaultPointSize = 18;

        private static readonly double[] s_fontSizes = { 8, 9, 10, 10.5, 11, 12, 14, 16, 18, 20, 24, 28, 32, 36, 40, 44, 48, 54, 60, 66, 72, 80, 88, 96 };

        private static readonly HashSet<string> s_context = new(StringComparer.Ordinal)
        {
            "slide", "slides", "shape", "shapes", "paragraphs", "range",
        };

        private static readonly HashSet<string> s_knownStates = new(StringComparer.Ordinal)
        {
            "bold", "italic", "underline", "align:left", "align:center", "align:right", "align:justify",
        };

        private static readonly string[] s_newSlideIds = { "ppt.home.newSlide", "ppt.insert.newSlide" };

        private static readonly string[] s_pending =
        {
            "ppt.insert.symbol",
        };

        private static readonly string[] s_external =
        {
            "ppt.view.slideMaster", "ppt.view.handoutMaster", "ppt.view.notesMaster", "ppt.file.openOriginal",
        };

        private static readonly string[] s_print =
        {
            "ppt.file.pdf", "ppt.file.print", "ppt.file.printHandout",
        };

        private static readonly Regex s_hex = new("^[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

        private readonly PptRibbonState _state;
        private readonly IClipboardService _clipboard;
        private readonly IFileDialogService _dialogs;
        private readonly Dictionary<string, PptCommandSpec> _specs = new(StringComparer.Ordinal);
        private readonly Dictionary<string, (Tool Instance, PptToolSchema Schema)> _schemas = new(StringComparer.Ordinal);
        private readonly Dictionary<string, JsonNode> _fields = new(StringComparer.Ordinal);
        private string _pickedName;
        private int _activate = -1;

        public PptRibbonCommands(WorkspaceStore workspace, AssistantStore assistant, IJSRuntime js, IFileDialogService dialogs, PptRibbonState state, IClipboardService clipboard)
            : base(workspace, assistant, js)
        {
            _dialogs = dialogs;
            _state = state;
            _clipboard = clipboard;
            foreach (var spec in PptCommandData.All)
            {
                _specs[spec.Id] = spec;
                Register(spec);
            }

            RegisterBuiltIn();
        }

        public override async Task InvokeAsync(string id, string arg)
        {
            var target = id;
            var spec = id != null && _specs.TryGetValue(id, out var found) ? found : null;
            PptMenuSpec entry = null;
            if (spec != null)
            {
                if (arg == QuickEntry)
                {
                    arg = null;
                }
                else if (arg != null && arg.StartsWith(EntryPrefix, StringComparison.Ordinal)
                    && int.TryParse(arg.AsSpan(EntryPrefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
                    && spec.Menu != null && index >= 0 && index < spec.Menu.Count)
                {
                    target = EntryId(spec, index);
                    entry = spec.Menu[index];
                    arg = null;
                }
            }

            if (IsDisabled(target))
                return;

            _fields.Clear();
            _pickedName = null;
            try
            {
                if (spec != null && !await GatherAsync(spec, entry, target, arg))
                    return;

                await base.InvokeAsync(target, arg);
            }
            catch (ToolInputException e)
            {
                Notify(e.Message);
                RaiseChanged();
            }
            finally
            {
                _fields.Clear();
                _pickedName = null;
                _activate = -1;
            }
        }

        protected override void RefreshSelection()
        {
            if (_activate < 0)
                return;

            var index = _activate;
            _activate = -1;
            _state.ActivateSlide?.Invoke(index);
        }

        private static string EntryId(PptCommandSpec spec, int index) =>
            spec.Id + "#" + index.ToString(CultureInfo.InvariantCulture);

        private void Register(PptCommandSpec spec)
        {
            switch (spec.Kind)
            {
                case PptCommandKind.Tool:
                    Decorate(AddTool(spec.Id, arg => BuildTool(spec, null, arg)).Unless(() => ToolBlocker(spec, null)), spec);
                    break;
                case PptCommandKind.View:
                    Decorate(AddView(spec.Id, _ => ApplyView(spec.Args)).EnabledWhen(() => CanApplyView(spec.Args), PendingReason), spec);
                    break;
                case PptCommandKind.Assistant:
                    Decorate(AddAssistant(spec.Id, () => Phrase(spec.Prompt)).Unless(() => DialogBlocker(spec.Dialog)), spec);
                    break;
                default:
                    AddDisabled(spec.Id, spec.Reason);
                    break;
            }

            if (spec.Menu == null)
                return;

            for (var i = 0; i < spec.Menu.Count; i++)
                RegisterEntry(spec, spec.Menu[i], EntryId(spec, i));
        }

        private void RegisterEntry(PptCommandSpec spec, PptMenuSpec entry, string id)
        {
            switch (KindOf(spec, entry))
            {
                case PptCommandKind.Assistant:
                    AddAssistant(id, () => Phrase(entry.Prompt)).Unless(() => DialogBlocker(entry.Dialog));
                    break;
                case PptCommandKind.Disabled:
                    AddDisabled(id, entry.Reason);
                    break;
                case PptCommandKind.View:
                    AddView(id, _ => ApplyView(entry.Args)).EnabledWhen(() => CanApplyView(entry.Args), PendingReason);
                    break;
                default:
                    AddTool(id, arg => BuildTool(spec, entry, arg)).Unless(() => ToolBlocker(spec, entry));
                    break;
            }
        }

        private static PptCommandKind KindOf(PptCommandSpec spec, PptMenuSpec entry)
        {
            if (entry.Kind != PptCommandKind.Tool)
                return entry.Kind;

            if (!string.IsNullOrEmpty(entry.Prompt))
                return PptCommandKind.Assistant;

            if (entry.Tool != null)
                return PptCommandKind.Tool;

            return spec.Kind == PptCommandKind.View ? PptCommandKind.View : PptCommandKind.Tool;
        }

        private void Decorate(RibbonCommand command, PptCommandSpec spec)
        {
            if (spec.Pressed != null)
                command.PressedWhen(() => PressedOf(spec.Pressed));

            switch (spec.Id)
            {
                case "ppt.home.fontSize":
                    command.ValueFrom(() => _state.Selection.PointSize > 0 ? _state.Selection.PointSize.ToString("0.##", CultureInfo.InvariantCulture) : null);
                    break;
                case "ppt.home.fontColor":
                    command.ColorFrom(() => _state.FontColor);
                    break;
            }

            if (spec.Menu != null && spec.Menu.Count > 0)
                command.MenuFrom(() => MenuOf(spec));
            else if (spec.Dialog != null && ColorFieldOf(spec.Dialog) != null)
                command.MenuFrom(ColorMenu);
        }

        private void RegisterBuiltIn()
        {
            AddUndo(UndoId);
            AddRedo(RedoId);
            AddSave("ppt.file.save");
            AddSaveCopy("ppt.file.saveAs");
            foreach (var id in s_print)
                AddDisabled(id, PrintReason);

            foreach (var id in s_external)
                AddExternal(id);

            RegisterClipboard();
            foreach (var id in s_pending)
                AddDisabled(id, PendingReason);
        }

        private string ToolBlocker(PptCommandSpec spec, PptMenuSpec entry)
        {
            if (File?.Docu == null)
                return SampleReason;

            var plan = PlanOf(spec, entry, false);
            if (!Ready(plan.Tool, plan.Template) || DialogBlocker(entry?.Dialog ?? spec.Dialog) != null)
                return PendingReason;

            if (entry == null && spec.ArgsOff != null && spec.Pressed != null
                && !s_knownStates.Contains(spec.Pressed) && !_state.Renders("state:" + spec.Pressed))
                return StateUnknownReason;

            var reason = NeedBlocker(spec.Need);
            if (reason == null && entry != null)
                reason = NeedBlocker(entry.Need);

            if (reason != null)
                return reason;

            if (entry == null && HasOnlyEffectEntries(spec) && VisibleEntries(spec).Count == 0)
                return NoOptionsReason;

            return null;
        }

        private string DialogBlocker(string dialog)
        {
            if (dialog == null || ColorFieldOf(dialog) != null)
                return null;

            return _state.Dialogs == null ? PendingReason : null;
        }

        private string NeedBlocker(PptNeed need)
        {
            var selection = _state.Selection;
            switch (need)
            {
                case PptNeed.Slide:
                    return selection.HasSlide ? null : NoSlideReason;
                case PptNeed.Shape:
                    return selection.HasShape ? null : NoShapeReason;
                case PptNeed.Text:
                    return selection.HasText ? null : NoTextReason;
                case PptNeed.Shapes2:
                    return selection.ShapeCount >= 2 ? null : TwoShapesReason;
                case PptNeed.Shapes3:
                    return selection.ShapeCount >= 3 ? null : ThreeShapesReason;
                case PptNeed.ClipboardImage:
                    return PendingReason;
                default:
                    return null;
            }
        }

        private (string Tool, JsonObject Template) PlanOf(PptCommandSpec spec, PptMenuSpec entry, bool off)
        {
            var tool = entry?.Tool ?? spec.Tool;
            var template = entry?.Tool != null ? new JsonObject() : PptArgs.ParseObject(spec.Args);
            if (off && entry == null && spec.ArgsOff != null)
                template = PptArgs.Merge(template, PptArgs.ParseObject(spec.ArgsOff));

            if (entry != null)
                template = PptArgs.Merge(template, PptArgs.ParseObject(entry.Args));

            if (Ready(tool, template))
                return (tool, template);

            var legacy = LegacyPlan(spec, entry, off);
            return legacy != null && Ready(legacy.Value.Tool, legacy.Value.Template) ? legacy.Value : (tool, template);
        }

        private static (string Tool, JsonObject Template)? LegacyPlan(PptCommandSpec spec, PptMenuSpec entry, bool off)
        {
            if (entry != null)
                return null;

            switch (spec.Id)
            {
                case "ppt.home.fontGrow":
                    return ("set_font", PptArgs.ParseObject("""{"slide":"{slide}","shape":"{shape}","size":"{sizeUp}"}"""));
                case "ppt.home.fontShrink":
                    return ("set_font", PptArgs.ParseObject("""{"slide":"{slide}","shape":"{shape}","size":"{sizeDown}"}"""));
                case "ppt.home.alignLeft":
                    return ("set_font", AlignArgs("left"));
                case "ppt.home.alignCenter":
                    return ("set_font", AlignArgs(off ? "left" : "center"));
                case "ppt.home.newSlide":
                case "ppt.insert.newSlide":
                    return ("add_slide", PptArgs.ParseObject("""{"after":"{slide}","layout":"title_content"}"""));
                default:
                    return null;
            }
        }

        private static JsonObject AlignArgs(string align) => new()
        {
            ["slide"] = "{slide}",
            ["shape"] = "{shape}",
            ["align"] = align,
        };

        private bool Ready(string tool, JsonObject template)
        {
            var schema = SchemaOf(tool);
            if (schema == null)
                return false;

            foreach (var pair in template)
            {
                if (s_context.Contains(pair.Key))
                    continue;

                if (!schema.Has(pair.Key))
                    return false;

                if (pair.Value is JsonValue value && value.TryGetValue<string>(out var text)
                    && !PptArgs.IsPlaceholder(text) && !schema.Allows(pair.Key, text))
                    return false;
            }

            return true;
        }

        private PptToolSchema SchemaOf(string tool)
        {
            if (string.IsNullOrEmpty(tool) || File == null
                || !Workspace.Workspace.Editors.TryGetValue(File.Type, out var editor)
                || !editor.Tools.TryGetValue(tool, out var instance))
                return null;

            if (!_schemas.TryGetValue(tool, out var cached) || !ReferenceEquals(cached.Instance, instance))
                _schemas[tool] = cached = (instance, PptToolSchema.Parse(instance.InputSchema));

            return cached.Schema;
        }

        private EditCommand BuildTool(PptCommandSpec spec, PptMenuSpec entry, string arg)
        {
            _state.CommitDraft?.Invoke();
            var selection = _state.Selection;
            var off = entry == null && spec.ArgsOff != null && IsOn(spec.Id);
            var plan = PlanOf(spec, entry, off);
            var args = PptArgs.Resolve(plan.Template, name => Lookup(name, arg, selection));
            Coerce(plan.Tool, args);
            if (plan.Template.ContainsKey("size") && !args.ContainsKey("size"))
                return null;

            if (plan.Tool == "add_slide")
                _activate = Math.Clamp(args["after"]?.GetValue<int>() ?? selection.SlideCount, 0, selection.SlideCount);

            return EditCommand.Create(plan.Tool, args);
        }

        private void Coerce(string tool, JsonObject args)
        {
            var schema = SchemaOf(tool);
            if (schema == null)
                return;

            foreach (var key in args.Select(p => p.Key).ToList())
            {
                if (args[key] is not JsonValue value || !value.TryGetValue<string>(out var text))
                    continue;

                var type = schema.TypeOf(key);
                if ((type == "number" || type == "integer")
                    && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                    args[key] = type == "integer" ? JsonValue.Create((int)Math.Round(number)) : JsonValue.Create(number);
            }
        }

        private JsonNode Lookup(string name, string arg, PptRibbonSelection selection)
        {
            switch (name)
            {
                case "slide":
                    return JsonValue.Create(selection.SlideNumber);
                case "slides":
                    return selection.HasSlide ? new JsonArray(JsonValue.Create(selection.SlideNumber)) : null;
                case "shape":
                case "shapes":
                    return selection.HasShape ? JsonValue.Create(selection.ShapeKey) : null;
                case "value":
                    if (_fields.TryGetValue(name, out var chosen))
                        return chosen;

                    return string.IsNullOrEmpty(arg) ? null : JsonValue.Create(arg);
                case "userName":
                    return JsonValue.Create(Environment.UserName);
                case "imageName":
                case "audioName":
                    return _pickedName == null ? null : JsonValue.Create(_pickedName);
                case "sizeUp":
                    return NextSize(selection, 1);
                case "sizeDown":
                    return NextSize(selection, -1);
                case "slideText":
                    return JsonValue.Create(selection.HasSlide ? $"{selection.SlideNumber}번 슬라이드" : "모든 슬라이드");
                case "scopeText":
                    return JsonValue.Create(ScopeText(selection));
                case "selectionText":
                    return JsonValue.Create(string.Empty);
                case "paragraphs":
                case "range":
                    return null;
                default:
                    return _fields.TryGetValue(name, out var field) ? field : null;
            }
        }

        private static JsonNode NextSize(PptRibbonSelection selection, int direction)
        {
            var current = selection.PointSize > 0 ? selection.PointSize : DefaultPointSize;
            var next = direction > 0
                ? s_fontSizes.FirstOrDefault(s => s > current + 0.01)
                : s_fontSizes.LastOrDefault(s => s < current - 0.01);

            return next > 0 ? JsonValue.Create(next) : null;
        }

        private string ScopeText(PptRibbonSelection selection)
        {
            var scope = _fields.TryGetValue("scope", out var node) && node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
            if (scope == "deck")
                return "모든 슬라이드";

            if (scope == "selection" || (scope == null && selection.HasShape))
                return "선택한 글상자";

            return selection.HasSlide ? $"{selection.SlideNumber}번 슬라이드" : "모든 슬라이드";
        }

        private string Phrase(string template) =>
            PptArgs.Fill(template, name => Lookup(name, null, _state.Selection));

        private bool PressedOf(string key)
        {
            if (key.StartsWith("view:", StringComparison.Ordinal))
                return _state.View == key.Substring(5);

            var selection = _state.Selection;
            if (key.StartsWith("align:", StringComparison.Ordinal))
                return selection.HasShape && selection.Align == key.Substring(6);

            if (key.StartsWith("transition:", StringComparison.Ordinal))
                return selection.HasSlide && selection.TransitionEffect == key.Substring(11);

            if (key.StartsWith("animation:", StringComparison.Ordinal))
                return selection.HasShape && selection.AnimationEffect == key.Substring(10);

            switch (key)
            {
                case "bold":
                    return selection.Bold;
                case "italic":
                    return selection.Italic;
                case "underline":
                    return selection.Underline;
                case "strike":
                    return selection.Strike;
                case "bullet":
                    return selection.Bullet == "bullet";
                case "number":
                    return selection.Bullet == "number";
                case "hidden":
                    return selection.Hidden;
                case "useTimings":
                    return selection.UseTimings;
                case "mediaControls":
                    return selection.MediaControls;
                case "advanceOnClick":
                    return selection.AdvanceOnClick;
                default:
                    return _state.Flag(key);
            }
        }

        private bool CanApplyView(string args) => _state.CanApply(PptArgs.ParseObject(args)["set"] as JsonObject);

        private Task ApplyView(string args)
        {
            var template = PptArgs.ParseObject(args)["set"] as JsonObject;
            if (template == null)
                return Task.CompletedTask;

            var selection = _state.Selection;
            _state.Apply(PptArgs.Resolve(template, name => Lookup(name, null, selection)));
            return Task.CompletedTask;
        }

        private async Task<bool> GatherAsync(PptCommandSpec spec, PptMenuSpec entry, string target, string arg)
        {
            var dialog = entry?.Dialog ?? spec.Dialog;
            if (dialog != null)
            {
                var color = ColorFieldOf(dialog);
                if (color != null)
                {
                    var value = PickedColor(arg);
                    if (value == null)
                        return false;

                    _fields[color] = JsonValue.Create(value);
                    if (value != AutoColor)
                        _state.FontColor = value.TrimStart('#');
                }
                else
                {
                    var values = await _state.Dialogs.ShowAsync(target, dialog);
                    if (values == null)
                        return false;

                    foreach (var pair in values)
                        _fields[pair.Key] = JsonSerializer.SerializeToNode(pair.Value);
                }
            }

            if (entry?.Picker != null && !await PickFileAsync(entry.Picker))
                return false;

            return true;
        }

        private async Task<bool> PickFileAsync(string picker)
        {
            var options = PptArgs.ParseObject(picker);
            var extensions = options["extensions"] as JsonArray;
            var audio = extensions != null && extensions.Any(e => e?.GetValue<string>() == ".wav");
            var path = audio ? await _dialogs.PickAudioAsync() : await _dialogs.PickImageAsync();
            if (string.IsNullOrEmpty(path))
                return false;

            var limit = options["maxMegabytes"]?.GetValue<int>() ?? 20;
            var info = new FileInfo(path);
            if (!info.Exists)
                throw new ToolInputException("고른 파일을 찾지 못했어요. 다시 골라 주세요.");

            if (info.Length > limit * 1024L * 1024L)
                throw new ToolInputException($"파일이 너무 커요. {limit}MB 이하로 골라 주세요.");

            byte[] bytes;
            try
            {
                bytes = await System.IO.File.ReadAllBytesAsync(path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                AppLog.Write($"Could not read {path}", e);
                throw new ToolInputException("고른 파일을 읽지 못했어요. 다른 파일을 골라 주세요.");
            }

            _pickedName = Path.GetFileName(path);
            _fields[audio ? "audio" : "image"] = JsonValue.Create(Convert.ToBase64String(bytes));
            return true;
        }

        private IReadOnlyList<RibbonMenuItem> MenuOf(PptCommandSpec spec)
        {
            var items = new List<RibbonMenuItem>();
            if (Array.IndexOf(s_newSlideIds, spec.Id) >= 0)
            {
                var detail = SchemaOf("add_slide")?.Allows("layout", "same_as_current") == true ? "지금 슬라이드와 같은 모양" : "제목과 내용";
                items.Add(Item(QuickEntry, "기본 모양으로 추가", detail: detail, disabled: IsDisabled(spec.Id), reason: DisabledReasonOf(spec.Id)));
                items.Add(Separator());
            }

            var current = ValueOf(spec.Id);
            if (spec.Id == "ppt.home.fontSize")
            {
                items.Add(Input("글자 크기", current ?? string.Empty, "pt"));
                items.Add(Separator());
            }

            var visible = VisibleEntries(spec);
            var grouped = visible.Count > 0 && LongestRun(spec, visible) >= 3;
            var checkable = spec.Id == "ppt.home.fontName" || spec.Id == "ppt.home.fontSize";
            string previous = null;
            foreach (var index in visible)
            {
                var entry = spec.Menu[index];
                if (grouped && entry.Detail != null && entry.Detail != previous)
                    items.Add(Heading(entry.Detail));

                previous = entry.Detail;
                var id = EntryId(spec, index);
                items.Add(Item(
                    EntryPrefix + index.ToString(CultureInfo.InvariantCulture),
                    entry.Label,
                    detail: grouped ? null : entry.Detail,
                    swatch: entry.Swatch,
                    font: spec.Id == "ppt.home.fontName" && entry.Detail == "글꼴" ? entry.Label : null,
                    check: checkable ? entry.Label == current : null,
                    disabled: IsDisabled(id),
                    reason: DisabledReasonOf(id)));
            }

            return items;
        }

        private List<int> VisibleEntries(PptCommandSpec spec)
        {
            var effect = EffectOf(spec);
            var visible = new List<int>();
            for (var i = 0; i < spec.Menu.Count; i++)
            {
                var entry = spec.Menu[i];
                if (entry.Effects != null && effect != null && !entry.Effects.Contains(effect))
                    continue;

                visible.Add(i);
            }

            return visible;
        }

        private string EffectOf(PptCommandSpec spec)
        {
            var selection = _state.Selection;
            if (spec.Id.StartsWith("ppt.trans.", StringComparison.Ordinal))
                return selection.TransitionEffect;

            if (spec.Id.StartsWith("ppt.anim.", StringComparison.Ordinal))
                return selection.AnimationEffect;

            return null;
        }

        private static bool HasOnlyEffectEntries(PptCommandSpec spec) =>
            spec.Menu != null && spec.Menu.Count > 0 && spec.Menu.All(e => e.Effects != null);

        private static int LongestRun(PptCommandSpec spec, List<int> visible)
        {
            var longest = 0;
            var run = 0;
            string previous = null;
            foreach (var index in visible)
            {
                var detail = spec.Menu[index].Detail;
                run = detail != null && detail == previous ? run + 1 : 1;
                previous = detail;
                longest = Math.Max(longest, detail == null ? 0 : run);
            }

            return longest;
        }

        private IReadOnlyList<RibbonMenuItem> ColorMenu() => new[]
        {
            Item(AutoColor, "자동", swatch: "000000"),
            Separator(),
            Palette("색 고르기", RibbonPalette.Standard, _state.FontColor),
        };

        private static string PickedColor(string arg)
        {
            if (string.IsNullOrEmpty(arg))
                return null;

            if (arg == AutoColor)
                return AutoColor;

            return s_hex.IsMatch(arg) ? "#" + arg.ToUpperInvariant() : null;
        }

        private static string ColorFieldOf(string dialog)
        {
            var fields = (JsonNode.Parse(dialog) as JsonObject)?["fields"] as JsonArray;
            if (fields == null || fields.Count != 1 || fields[0] is not JsonObject only)
                return null;

            return only["type"]?.GetValue<string>() == "color" ? only["name"]?.GetValue<string>() : null;
        }
    }
}
