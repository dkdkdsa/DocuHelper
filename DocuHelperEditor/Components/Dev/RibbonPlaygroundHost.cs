using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DocuHelper.Docus;
using DocuHelperEditor.Services;
using DocuHelperEditor.State;
using DocuHelperEditor.State.Papers;
using DocuHelperEditor.State.Ribbon;
using Microsoft.JSInterop;
using static DocuHelperEditor.State.Ribbon.RibbonMenus;

namespace DocuHelperEditor.Components.Dev
{
    public sealed class RibbonPlaygroundHost : RibbonRouter
    {
        public const string RouterPrefix = "rt.";

        private const string SamplePng = "iVBORw0KGgoAAAANSUhEUgAAAAYAAAAECAYAAACtBE5DAAAAG0lEQVR4nGM4YKn9X0bL4z86zYBNEEQzkKwDAJCYLEHzThdqAAAAAElFTkSuQmCC";

        private static readonly string[] s_align = { "aleft", "acenter", "aright", "ajust" };
        private static readonly string[] s_views = { "v-read", "v-print", "v-web", "v-normal", "v-break", "v-layout", "v-sorter" };
        private static readonly string[] s_toggles = { "bold", "italic", "under", "strike", "bullets", "numbers", "ruler", "nav", "gridlines", "fxbar", "headings", "codes", "paracodes", "lines", "t-click", "notes", "guides", "freeze", "outline", "vmid", "vtop", "vbot" };
        private static readonly string[] s_fonts = { "맑은 고딕", "굴림", "돋움", "바탕", "궁서", "Arial", "Calibri", "Times New Roman", "Consolas" };
        private static readonly string[] s_sizes = { "8", "9", "10", "11", "12", "14", "16", "18", "20", "24", "28", "36" };

        private readonly HashSet<string> _on;
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>
        {
            ["font"] = "맑은 고딕",
            ["size"] = "11",
        };

        private readonly Dictionary<string, string> _colors = new Dictionary<string, string>
        {
            ["fcolor"] = "C0392B",
            ["hilite"] = "FFE35A",
            ["fill"] = "FFE35A",
        };

        private readonly IClipboardService _clipboard;
        private readonly IFileDialogService _dialogs;
        private readonly IPrintService _prints;
        private bool _flipped;

        public RibbonPlaygroundHost(DocuType format, WorkspaceStore workspace, AssistantStore assistant, IJSRuntime js, IClipboardService clipboard, IFileDialogService dialogs, IPrintService prints)
            : base(workspace, assistant, js)
        {
            _clipboard = clipboard;
            _dialogs = dialogs;
            _prints = prints;

            switch (format)
            {
                case DocuType.Excel:
                    _on = new HashSet<string> { "v-normal", "vmid", "ruler", "fxbar", "gridlines", "headings" };
                    break;
                case DocuType.Ppt:
                    _on = new HashSet<string> { "bullets", "aleft", "t-none", "v-normal", "t-click", "notes" };
                    _values["size"] = "24";
                    _colors["fcolor"] = "B5462A";
                    break;
                case DocuType.Hwp:
                    _on = new HashSet<string> { "ajust", "portrait", "outline", "ruler", "pane-pages", "lines" };
                    _values["hwp-style"] = "바탕글";
                    _values["hwp-font"] = "함초롬바탕";
                    _values["hwp-size"] = "10.0 pt";
                    _colors["fcolor"] = "1C2A48";
                    break;
                default:
                    _on = new HashSet<string> { "aleft", "st-normal", "v-print", "v-vert", "nav" };
                    break;
            }

            RegisterRouterCommands();
        }

        public string Last { get; private set; }

        public string LastNotice { get; private set; }

        public string Pasted { get; private set; }

        public string Picked { get; private set; }

        public bool DisableSave { get; set; }

        public bool NothingSelected { get; set; }

        public bool IsEditing => File?.Docu?.IsLocked == true;

        public void SetEditing(bool editing)
        {
            if (File?.Docu != null)
                File.Docu.SetLocked(editing);
        }

        public override void Notify(string text)
        {
            LastNotice = text;
            base.Notify(text);
        }

        public override bool IsOn(string id) => IsRouted(id) ? base.IsOn(id) : id != null && _on.Contains(id);

        public override bool IsDisabled(string id)
        {
            if (IsRouted(id))
                return base.IsDisabled(id);

            if (DisableSave && id == "save")
                return true;

            return NothingSelected && (id == "cut" || id == "copy" || id == "brush");
        }

        public override string DisabledReasonOf(string id) => IsRouted(id) ? base.DisabledReasonOf(id) : null;

        public override string ValueOf(string id)
        {
            if (IsRouted(id))
                return base.ValueOf(id);

            return id != null && _values.TryGetValue(id, out var value) ? value : null;
        }

        public override string ColorOf(string id)
        {
            if (IsRouted(id))
                return base.ColorOf(id);

            return id != null && _colors.TryGetValue(id, out var color) ? color : null;
        }

        public override IReadOnlyList<RibbonMenuItem> MenuFor(string id)
        {
            if (IsRouted(id))
                return base.MenuFor(id);

            switch (id)
            {
                case "paste":
                    return new[]
                    {
                        Item("paste", "붙여넣기", key: "Ctrl+V"),
                        Item("paste-values", "값만 붙여넣기", detail: "서식은 빼고 값만 넣어요"),
                        Item("paste-format", "서식만 붙여넣기"),
                    };
                case "font":
                case "hwp-font":
                    return s_fonts.Select(f => Item(f, f, font: f, check: f == ValueOf(id))).ToArray();
                case "size":
                case "hwp-size":
                    return new[] { Input("글자 크기", ValueOf(id)?.Replace(" pt", string.Empty), "pt"), Separator() }
                        .Concat(s_sizes.Select(s => Item(s, s + " pt", check: s == ValueOf(id))))
                        .ToArray();
                case "fcolor":
                case "fill":
                case "hilite":
                    return new[]
                    {
                        Item("auto", id == "fcolor" ? "자동 (검정)" : "색 없음", swatch: id == "fcolor" ? "000000" : null),
                        Separator(),
                        Palette("색 고르기", RibbonPalette.Standard, ColorOf(id)),
                    };
                case "search":
                    return new[]
                    {
                        Item("find", "찾기", key: "Ctrl+F"),
                        Item("goto", "이동", key: "Ctrl+G"),
                    };
                case "table":
                    return new[]
                    {
                        Heading("표 크기"),
                        Item("2x2", "2 x 2 표"),
                        Item("3x3", "3 x 3 표"),
                        Item("4x4", "4 x 4 표"),
                        Separator(),
                        Item("custom", "행과 열 직접 정하기", detail: "넣을 행 수와 열 수를 적어요"),
                    };
                default:
                    return null;
            }
        }

        public override async Task InvokeAsync(string id, string arg)
        {
            Last = arg == null ? id : id + " = " + arg;
            if (IsRouted(id))
            {
                await base.InvokeAsync(id, arg);
                return;
            }

            if (arg != null && _values.ContainsKey(id))
                _values[id] = id == "hwp-size" && !arg.EndsWith("pt") ? arg + " pt" : arg;
            else if (arg != null && _colors.ContainsKey(id))
                _colors[id] = arg;
            else if (s_align.Contains(id))
                Select(s_align, id);
            else if (s_views.Contains(id))
                Select(s_views, id);
            else if (s_toggles.Contains(id))
                Flip(id);

            RaiseChanged();
        }

        private static bool IsRouted(string id) => id != null && id.StartsWith(RouterPrefix, StringComparison.Ordinal);

        private void Flip(string id)
        {
            if (!_on.Add(id))
                _on.Remove(id);
        }

        private void Select(string[] group, string id)
        {
            foreach (var other in group)
                _on.Remove(other);

            _on.Add(id);
        }

        private string FirstSheetName() => (File?.Paper as ExcelPaperModel)?.Sheets.FirstOrDefault()?.Name ?? "Sheet1";

        private void Report(PrintResult result)
        {
            if (!result.IsDone)
                Notify(result.Message ?? "끝나지 않았어요.");
        }

        private void RegisterRouterCommands()
        {
            AddTool("rt.tool.write", "write_cells", _ => new { sheet = FirstSheetName(), start = "Z50", values = new object[][] { new object[] { "라우터 시험" } } });
            AddTool("rt.tool.refuse", "write_cells", _ => new { sheet = FirstSheetName(), start = "Z51", values = new object[][] { new object[] { null } } });
            AddTool("rt.tool.later", "tool_that_does_not_exist_yet", _ => new { });
            AddTool("rt.tool.bad", _ => throw new DocuHelper.Tools.ToolInputException("글자 크기는 숫자로 적어 주세요."));

            AddView("rt.view.flip", _ => _flipped = !_flipped).PressedWhen(() => _flipped);

            AddClipboard("rt.clip.copy", _ => _clipboard.SetTextAsync("라우터 복사 시험"));
            AddClipboard("rt.clip.html", _ => _clipboard.SetHtmlAsync("<p><b>라우터</b> 복사 시험</p>", "라우터 복사 시험"));
            AddClipboard("rt.clip.image", _ => _clipboard.SetImageAsync(Convert.FromBase64String(SamplePng)));
            AddClipboard("rt.clip.paste", async _ =>
            {
                var content = await _clipboard.ReadAsync();
                Pasted = $"글 {content.Text?.Length ?? 0}자, HTML {content.Html?.Length ?? 0}자, 그림 {content.Image?.Length ?? 0}바이트";
            });

            AddAssistant("rt.ai.spell", "이 파일의 맞춤법과 띄어쓰기를 살펴봐 줘. 고칠 곳은 먼저 알려 주고, 내가 괜찮다고 하면 고쳐 줘.");

            AddExternal("rt.ext.open");

            AddSave("rt.file.save");
            AddUndo("rt.file.undo");
            AddRedo("rt.file.redo");
            AddFile("rt.file.print", async _ => Report(await _prints.PrintAsync("<p>시험</p>")));
            AddFile("rt.file.pdf", async _ => Report(await _prints.ExportPdfAsync("<p>시험</p>", Path.Combine(Path.GetTempPath(), "router-test.pdf"))));
            AddFile("rt.file.locked", _ => Task.CompletedTask).AllowWhileLocked();

            AddDisabled("rt.off.always", "이 단추는 일부러 꺼 두었어요. 마우스를 올리면 이유가 보여요.");
            AddView("rt.off.select", _ => Picked = "고른 것 있음").EnabledWhen(() => !NothingSelected, "먼저 고를 것을 골라 주세요.");
            AddView("rt.menu.reason", arg => Picked = arg).MenuFrom(() => new[]
            {
                Item("open", "열 수 있는 항목"),
                Item("saved", "저장해야 쓰는 항목", disabled: true, reason: "파일을 저장한 뒤에 쓸 수 있어요."),
                Item("plain", "설명만 있는 항목", detail: "이 줄이 그대로 이유가 돼요", disabled: true),
                Item("quiet", "이유 없는 꺼진 항목", disabled: true),
            });

            AddFile("rt.dialog.image", async _ => Picked = await _dialogs.PickImageAsync() ?? "고르지 않음");
            AddFile("rt.dialog.audio", async _ => Picked = await _dialogs.PickAudioAsync() ?? "고르지 않음");
        }
    }
}
