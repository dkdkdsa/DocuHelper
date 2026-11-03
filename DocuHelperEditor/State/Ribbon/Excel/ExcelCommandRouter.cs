using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DocuHelperEditor.Services;
using Microsoft.JSInterop;

namespace DocuHelperEditor.State.Ribbon.Excel
{
    public sealed partial class ExcelCommandRouter : SpecRouter
    {
        private const char GroupSeparator = '|';

        private static readonly (string Code, bool Ctrl, bool Shift, bool Alt, string Id, string Arg)[] s_shortcuts =
        {
            ("KeyB", true, false, false, "excel.home.bold", null),
            ("KeyI", true, false, false, "excel.home.italic", null),
            ("KeyU", true, false, false, "excel.home.underline", null),
            ("Digit5", true, true, false, "excel.home.percent", null),
            ("Equal", false, false, true, "excel.home.autosum", "SUM"),
            ("KeyL", true, true, false, "excel.data.filter", null),
            ("KeyK", true, false, false, "excel.insert.link", null),
            ("KeyT", true, false, false, "excel.insert.table", null),
            ("KeyE", true, false, false, "excel.data.flashfill", null),
            ("F3", true, false, false, "excel.formula.namemanager", null),
            ("F3", false, true, false, "excel.formula.insertfn", null),
            ("F2", false, true, false, "excel.review.newnote", null),
            ("F7", false, false, false, "excel.review.spelling", null),
            ("F7", false, true, false, "excel.review.thesaurus", null),
            ("Backquote", true, false, false, "excel.formula.showformulas", null),
        };

        private readonly IExcelSurface _surface;
        private readonly IClipboardService _clipboard;
        private readonly IFileDialogService _dialogs;
        private readonly IPrintService _prints;
        private readonly Dictionary<string, Func<string, (string Key, object Value)[]>> _formats = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Func<string, Dictionary<string, object>>> _tools = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Func<string, Task>> _runs = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Func<string>> _prompts = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Func<bool>> _pressed = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Func<string>> _values = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Func<string>> _colors = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Func<CommandSpec, string>> _needs = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Func<CommandSpec, IReadOnlyList<RibbonMenuItem>>> _menus = new(StringComparer.Ordinal);

        public ExcelCommandRouter(WorkspaceStore workspace, AssistantStore assistant, IJSRuntime js, IClipboardService clipboard, IFileDialogService dialogs, IPrintService prints, IExcelSurface surface)
            : base(workspace, assistant, js)
        {
            _surface = surface;
            _clipboard = clipboard;
            _dialogs = dialogs;
            _prints = prints;
            State = new ExcelRibbonState(surface);
            AddHomeHandlers();
            AddInsertHandlers();
            AddLayoutHandlers();
            AddFormulaHandlers();
            AddDataHandlers();
            AddReviewHandlers();
            AddViewHandlers();
            AddFileHandlers();
            Load(ExcelCommandTable.All, new CommandHandlers
            {
                Pressed = _pressed,
                Values = _values,
                Colors = _colors,
                Needs = _needs,
                Menus = _menus,
            });
            RegisterFeatures();
            RegisterDialogs();
        }

        public ExcelRibbonState State { get; }

        public IExcelSurface Surface => _surface;

        public override void Notify(string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
                _surface.Message(text.Trim());
        }

        public static bool TryShortcut(string code, bool ctrl, bool shift, bool alt, out string id, out string arg)
        {
            foreach (var entry in s_shortcuts)
            {
                if (entry.Code == code && entry.Ctrl == ctrl && entry.Shift == shift && entry.Alt == alt)
                {
                    id = entry.Id;
                    arg = entry.Arg;
                    return true;
                }
            }

            id = null;
            arg = null;
            return false;
        }

        protected override void RefreshSelection() => _surface.Refocus();

        protected override void Register(CommandSpec spec)
        {
            if (spec.Action == "alias")
            {
                Alias(spec.Id, spec.Args);
                return;
            }

            var command = Create(spec);
            if (spec.Need != null)
                command.Unless(() => NeedBlocker(spec));

            if (spec.Pressed != null)
                command.PressedWhen(Handlers.Pressed[spec.Pressed]);

            if (spec.Value != null)
                command.ValueFrom(Handlers.Values[spec.Value]);

            if (spec.Color != null)
                command.ColorFrom(Handlers.Colors[spec.Color]);

            if (spec.MenuStyle != null)
            {
                var menu = Handlers.Menus[spec.MenuStyle];
                command.MenuFrom(() => menu(spec));
            }

            if (spec.WhileLocked)
                command.AllowWhileLocked();
        }

        private RibbonCommand Create(CommandSpec spec)
        {
            switch (spec.Class)
            {
                case RibbonCommandClass.Tool:
                    return spec.Tool == "format_range" ? Format(spec.Id, _formats[spec.Action]) : Tool(spec.Id, spec.Tool, _tools[spec.Action]);
                case RibbonCommandClass.Clipboard:
                    return AddClipboard(spec.Id, _runs[spec.Action]);
                case RibbonCommandClass.Assistant:
                    return AddAssistant(spec.Id, _prompts[spec.Action]);
                case RibbonCommandClass.External:
                    return AddExternal(spec.Id);
                case RibbonCommandClass.Disabled:
                    return AddDisabled(spec.Id, spec.Reason);
                case RibbonCommandClass.View:
                    return spec.Action == "dialog" ? Dialog(spec.Id, spec.Tool) : AddView(spec.Id, _runs[spec.Action]);
                default:
                    return CreateFile(spec);
            }
        }

        private RibbonCommand CreateFile(CommandSpec spec)
        {
            switch (spec.Action)
            {
                case "undo":
                case "redo":
                case "save":
                case "saveAs":
                    return RegisterFile(spec);
                case "group":
                    return Group(spec.Id, spec.Menu.Select(m => (Child: (string)JsonNode.Parse(m.Args)["command"], Title: m.Label)).ToArray());
                default:
                    return AddFile(spec.Id, _runs[spec.Action]).Needs(spec.Tool);
            }
        }

        private static Func<string, Task> Run(Action<string> run)
        {
            return arg =>
            {
                run(arg);
                return Task.CompletedTask;
            };
        }

        private bool ToolReady(string tool)
        {
            return File != null
                && Workspace.Workspace.Editors.TryGetValue(File.Type, out var editor)
                && editor.Tools.ContainsKey(tool);
        }

        private bool ToolHas(string tool, string parameter)
        {
            return File != null
                && Workspace.Workspace.Editors.TryGetValue(File.Type, out var editor)
                && editor.Tools.TryGetValue(tool, out var found)
                && found.InputSchema.Contains("\"" + parameter + "\"", StringComparison.Ordinal);
        }

        private Dictionary<string, object> Sheetwide(params (string Key, object Value)[] extra)
        {
            var args = new Dictionary<string, object> { ["sheet"] = State.SheetName };
            foreach (var (key, value) in extra)
                args[key] = value;

            return args;
        }

        private Dictionary<string, object> Cells(params (string Key, object Value)[] extra)
        {
            var args = Sheetwide();
            args["range"] = State.Range;
            foreach (var (key, value) in extra)
                args[key] = value;

            return args;
        }

        private static Dictionary<string, object> Workbook(params (string Key, object Value)[] extra)
        {
            var args = new Dictionary<string, object>();
            foreach (var (key, value) in extra)
                args[key] = value;

            return args;
        }

        private RibbonCommand Format(string id, Func<string, (string Key, object Value)[]> props)
        {
            return AddTool(id, "format_range", arg =>
            {
                if (!_surface.Prepare())
                    return null;

                var changes = props(arg);
                return changes == null ? null : Cells(changes);
            });
        }

        private RibbonCommand Tool(string id, string tool, Func<string, Dictionary<string, object>> args)
        {
            return AddTool(id, tool, arg => _surface.Prepare() ? args(arg) : null);
        }

        private RibbonCommand Dialog(string id, string tool)
        {
            return AddView(id, _ => State.OpenDialog(id))
                .Needs(tool)
                .EnabledWhen(() => State.HasDialog(id), PendingReason)
                .Unless(Editable);
        }

        private string Feature(string feature) => State.HasFeature(feature) ? null : PendingReason;

        private string Editable() => File == null ? NoFileReason : IsLocked ? LockedReason : null;

        private RibbonCommand Group(string id, params (string Child, string Title)[] children)
        {
            return AddFile(id, arg => RunChild(arg))
                .MenuFrom(() =>
                {
                    var items = new List<RibbonMenuItem>();
                    foreach (var (child, title) in children)
                    {
                        var entries = MenuFor(child);
                        if (entries == null || entries.Count == 0)
                        {
                            items.Add(Entry(child, title, check: Find(child)?.PressedPredicate != null ? IsOn(child) : (bool?)null, blocked: DisabledReasonOf(child)));
                            continue;
                        }

                        if (items.Count > 0)
                            items.Add(RibbonMenus.Separator());

                        items.Add(RibbonMenus.Heading(title));
                        foreach (var entry in entries.Where(e => e.Kind == RibbonMenuKind.Item || e.Kind == RibbonMenuKind.Separator))
                            items.Add(entry.Kind == RibbonMenuKind.Item ? entry with { Value = child + GroupSeparator + entry.Value } : entry);
                    }

                    return items;
                });
        }

        private Task RunChild(string arg)
        {
            if (string.IsNullOrEmpty(arg))
                return Task.CompletedTask;

            var bar = arg.IndexOf(GroupSeparator);
            return bar < 0 ? InvokeAsync(arg, null) : InvokeAsync(arg.Substring(0, bar), arg.Substring(bar + 1));
        }

        private RibbonMenuItem Entry(string value, string label, string detail = null, string key = null, bool? check = null, string font = null, string swatch = null, string needs = null, string blocked = null)
        {
            var reason = blocked;
            if (reason == null && needs != null && !ToolReady(needs))
                reason = PendingReason;

            return RibbonMenus.Item(value, label, detail: detail, key: key, check: check, font: font, swatch: swatch, disabled: reason != null, reason: reason);
        }

        private RibbonMenuItem DialogEntry(string value, string label, string dialog, string detail = null, string needs = null)
        {
            var reason = State.HasDialog(dialog) ? null : PendingReason;
            return Entry(value, label, detail: detail, needs: needs, blocked: reason);
        }
    }
}
