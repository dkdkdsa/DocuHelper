using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DocuHelper.Dash;
using DocuHelper.Dash.Elements;
using DocuHelper.Dash.Logic;
using DocuHelper.Dash.Runtime;
using DocuHelper.Editors;
using DocuHelperEditor.State;
using DocuHelperEditor.State.Papers;
using Microsoft.AspNetCore.Components;

namespace DocuHelperEditor.Components.Paper.Dash
{
    public partial class DashInspector
    {
        private const string PageId = "page";
        private const string Ink = "#1C2A48";
        private const string Violet = "#5B3FA0";
        private const string Marker = "#FFE35A";

        private static readonly (string Hex, string Label)[] s_textColors =
        {
            (Ink, "잉크"), ("#4A5570", "회색"), (Violet, "보라"), ("#8A3A0F", "갈색"), ("#FFFFFF", "흰색"),
        };

        private static readonly (string Hex, string Label)[] s_fills =
        {
            ("#FFE35A", "형광펜 노랑"), ("#ECE7F6", "연보라"), ("#F4F6F9", "연한 회색"), ("#E3F1E8", "연두"), (Ink, "잉크"), ("#FFFFFF", "흰색"),
        };

        private static readonly (string Hex, string Label)[] s_lineColors =
        {
            (Ink, "잉크"), ("#7A849B", "회색"), ("#E3E7EE", "연한 회색"), (Violet, "보라"),
        };

        private static readonly (string Hex, string Label)[] s_meterColors =
        {
            (Ink, "잉크"), (Violet, "보라"), ("#8A3A0F", "갈색"),
        };

        private static readonly (string Hex, string Label)[] s_pageColors =
        {
            ("#FFFFFF", "흰색"), ("#F4F6F9", "연한 회색"), ("#ECE7F6", "연보라"),
        };

        private static readonly (string Key, string Text, double Width, double Height)[] s_papers =
        {
            ("a4l", "A4 가로", 1123, 794), ("a4p", "A4 세로", 794, 1123), ("wide", "화면 16:9", 1280, 720),
        };

        private static readonly (string Key, string Text, double Size, int Weight)[] s_presets =
        {
            ("h1", "큰 제목", 32, 700), ("h2", "제목", 22, 700), ("big", "큰 숫자", 44, 700), ("num", "숫자", 28, 700),
            ("lead", "강조 글", 18, 600), ("body", "본문", 16, 400), ("small", "작은 글", 13, 400),
        };

        private static readonly (string Key, string Text, string Path)[] s_aligns =
        {
            ("left", "왼쪽 맞춤", "M21 6H3 M15 12H3 M17 18H3"),
            ("center", "가운데 맞춤", "M21 6H3 M17 12H7 M19 18H5"),
            ("right", "오른쪽 맞춤", "M21 6H3 M21 12H9 M21 18H7"),
        };

        private static readonly (string Key, string Text)[] s_chartKinds = { ("column", "세로 막대"), ("bar", "가로 막대"), ("line", "꺾은선") };

        private static readonly (string Key, string Text)[] s_directions = { ("column", "아래로 한 줄씩"), ("row", "옆으로 한 줄씩") };

        private static readonly (string Key, string Text)[] s_looks = { ("fill", "채움"), ("line", "테두리"), ("ghost", "옅게") };

        private static readonly (string Key, string Text)[] s_acts =
        {
            ("set", "조절값 바꾸기"), ("reset", "모두 처음 값으로"), ("ask", "도우미에게 부탁"), ("open", "엑셀 열기"), ("refresh", "엑셀 다시 읽기"), ("pdf", "PDF로 저장"),
        };

        private static readonly string[] s_staleActionKeys = { "param", "value", "text", "file", "sheet", "params", "send" };

        private readonly string _uid = "dash-inspector-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        private Element _el;
        private DashStyle _style;
        private string _lastId;
        private bool _valuesOpen;
        private bool _revealed;
        private string _revealFail;

        [Parameter]
        public DashPaperModel Model { get; set; }

        [Parameter]
        public DashViewState State { get; set; }

        [Parameter]
        public DashRibbonCommands Commands { get; set; }

        [Parameter]
        public WorkspaceFile File { get; set; }

        [Parameter]
        public EventCallback OnChanged { get; set; }

        [Parameter]
        public EventCallback<string> OnSelect { get; set; }

        private string HeadId => _uid + "-head";

        private bool Busy => File?.Activity == FileActivity.Editing;

        private DashDocument Document => Model.Document;

        private DashRuntime Runtime => Model.Runtime;

        private bool IsRootChild => _el != null && Document.Root.Children.Contains(_el);

        private bool GeoOff => Busy || _el == null || _el.Locked;

        protected override void OnParametersSet()
        {
            _el = null;
            _style = null;
            if (Model?.Document?.Root == null)
                return;

            _el = (State?.Selected ?? new List<string>())
                .Select(Document.Find)
                .FirstOrDefault(element => element != null && element != Document.Root);
            _style = _el == null ? null : new DashScope(Runtime).StyleOf(_el);
            if (_el?.Id != _lastId)
            {
                _lastId = _el?.Id;
                _valuesOpen = false;
                _revealed = false;
                _revealFail = null;
            }
        }

        private string Title => _el == null ? "대시보드 페이지" : DashLabels.Of(_el);

        private string IconPath => _el == null ? DashIcons.Page : DashIcons.Of(_el.Kind);

        private string Subtitle
        {
            get
            {
                if (_el == null)
                    return "아무것도 고르지 않았어요";

                var text = DashLabels.Kind(_el.Kind);
                if (_el.Locked)
                    text += ", 잠금";

                var data = _el switch
                {
                    ChartElement chart => chart.Data,
                    TableElement table => table.Data,
                    RepeatElement repeat => repeat.Data,
                    _ => null,
                };
                if (!string.IsNullOrWhiteSpace(data))
                    text += ", ‘" + data + "’ 표";

                var param = DashDependents.ParamOf(_el);
                if (!string.IsNullOrWhiteSpace(param))
                    text += ", ‘" + param + "’ 조절값";

                return text;
            }
        }

        private static string Show(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        private static bool TryNumber(string text, out double number) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && double.IsFinite(number);

        private static JsonObject Pair(string key, JsonNode value) => new() { [key] = value };

        private static JsonObject Style(string key, JsonNode value) => new() { ["style"] = Pair(key, value) };

        private static JsonNode Num(double value) => JsonValue.Create(Math.Round(value, 2));

        private static JsonNode Str(string value) => value == null ? null : JsonValue.Create(value);

        private static JsonNode Flag(bool value) => JsonValue.Create(value);

        private static bool SameColor(string a, string b) =>
            string.Equals((a ?? string.Empty).Trim().TrimStart('#'), (b ?? string.Empty).Trim().TrimStart('#'), StringComparison.OrdinalIgnoreCase);

        private bool Send(string id, JsonObject patch)
        {
            if (Commands == null || id == null || patch == null || patch.Count == 0)
                return false;

            return Commands.Apply(EditCommand.Create(DashCanvasEdits.UpdateTool, new { id, patch }));
        }

        private bool Edit(JsonObject patch) => _el != null && Send(_el.Id, patch);

        private bool EditParam(DashParam param, JsonObject patch)
        {
            if (Commands == null || param?.Name == null)
                return false;

            return Commands.Apply(EditCommand.Create(DashCanvasEdits.UpdateTool, new { kind = "param", name = param.Name, patch }));
        }

        private void SetName(ChangeEventArgs e)
        {
            var text = (e.Value?.ToString() ?? string.Empty).Trim();
            if (string.Equals(text, _el.Name ?? string.Empty, StringComparison.Ordinal))
                return;

            Edit(Pair("name", text.Length == 0 ? null : Str(text)));
        }

        private double BoxOf(string key) => key switch
        {
            "x" => _style.X ?? 0,
            "y" => _style.Y ?? 0,
            "w" => _style.W ?? DashCss.DefaultWidth,
            _ => _style.H ?? DashCss.DefaultHeight,
        };

        private void SetBox(string key, ChangeEventArgs e)
        {
            if (!TryNumber(e.Value?.ToString(), out var number))
                return;

            if (key is "w" or "h")
                number = Math.Max(2, number);

            if (Math.Abs(number - BoxOf(key)) < 0.05)
                return;

            Edit(Style(key, Num(number)));
        }

        private void Front() => Commands?.Arrange("front", _el.Id);

        private void Back() => Commands?.Arrange("back", _el.Id);

        private void ToggleLock() => Edit(Pair("locked", Flag(!_el.Locked)));

        private void Remove()
        {
            if (Commands == null || _el.Locked)
                return;

            var id = _el.Id;
            if (Commands.Apply(EditCommand.Create(DashCanvasEdits.RemoveTool, new { id })))
                State?.Selected.Remove(id);
        }

        private Element ParentElement => _el == null ? null : Document.Walk().FirstOrDefault(candidate => candidate.Nested().Contains(_el));

        private Task GoParent()
        {
            var parent = ParentElement;
            return parent == null || parent == Document.Root ? Task.CompletedTask : OnSelect.InvokeAsync(parent.Id);
        }

        private string PresetKey
        {
            get
            {
                var size = _style.FontSize ?? 16;
                var weight = _style.FontWeight ?? 400;
                foreach (var preset in s_presets)
                {
                    if (Math.Abs(preset.Size - size) < 0.01 && (preset.Weight >= 600) == (weight >= 600))
                        return preset.Key;
                }

                return string.Empty;
            }
        }

        private void SetPreset(ChangeEventArgs e)
        {
            var key = e.Value?.ToString();
            foreach (var preset in s_presets)
            {
                if (preset.Key == key)
                {
                    Edit(new JsonObject { ["style"] = new JsonObject { ["fontSize"] = Num(preset.Size), ["fontWeight"] = preset.Weight } });
                    return;
                }
            }
        }

        private double FontSize => _style.FontSize ?? 16;

        private void StepSize(int direction)
        {
            var size = FontSize;
            var step = size >= 30 ? 4 : 1;
            Edit(Style("fontSize", Num(Math.Clamp(size + direction * step, 6, 140))));
        }

        private bool Bold => (_style.FontWeight ?? 400) >= 600;

        private void ToggleBold() => Edit(Style("fontWeight", Bold ? 400 : 700));

        private string AlignKey => _style.TextAlign is "center" or "right" ? _style.TextAlign : "left";

        private void SetAlign(string key) => Edit(Style("textAlign", Str(key)));

        private void SetColor(string hex) => Edit(Style("color", Str(hex)));

        private bool Marked => !string.IsNullOrWhiteSpace(_style.Mark);

        private void ToggleMark() => Edit(Style("mark", Marked ? (_el.Classes?.Count > 0 ? Str(string.Empty) : null) : Str(Marker)));

        private IEnumerable<string> Tokens => DashNames.InTemplate((_el as TextElement)?.Text);

        private string TokenText(string name)
        {
            if (DashNames.IsItem(name))
                return "줄마다 달라져요";

            return Runtime.Exists(name) ? Runtime.Display(name) : "없는 값";
        }

        private IEnumerable<(string Name, string Text)> InsertableValues() =>
            Document.Values.Where(value => value != null && value is not StepsTable && !string.IsNullOrWhiteSpace(value.Name))
                .Select(value => (value.Name, Runtime.Display(value.Name)))
                .Concat(Document.Params.Where(param => param != null && !string.IsNullOrWhiteSpace(param.Name))
                    .Select(param => (param.Name, "조절값, " + Runtime.Display(param.Name))));

        private void InsertToken(string name)
        {
            if (_el is not TextElement text)
                return;

            _valuesOpen = false;
            if (State != null && State.EditingId == _el.Id)
                State.EditingId = null;

            Edit(Pair("text", Str((text.Text ?? string.Empty) + "«" + name + "»")));
        }

        private void SetFill(string hex) => Edit(Style("fill", Str(hex)));

        private double Radius => _style.Radius ?? 0;

        private void StepRadius(int direction) => Edit(Style("radius", Num(Math.Clamp(Radius + direction * 2, 0, 60))));

        private bool HasBorder => !string.IsNullOrWhiteSpace(_style.Line);

        private void ToggleBorder(bool on) =>
            Edit(on
                ? new JsonObject { ["style"] = new JsonObject { ["line"] = Ink, ["lineWidth"] = Num(_style.LineWidth ?? 1) } }
                : Style("line", null));

        private void SetStroke(string hex) => Edit(Style("line", Str(hex)));

        private double Weight => _style.LineWidth ?? 1.5;

        private void StepWeight(int direction) => Edit(Style("lineWidth", Num(Math.Clamp(Weight + direction * 0.5, 0.5, 8))));

        private IEnumerable<(string Value, string Label)> TableChoices(string current)
        {
            var list = Document.Values.OfType<StepsTable>().Where(table => !string.IsNullOrWhiteSpace(table.Name)).Select(table => (table.Name, table.Name))
                .Concat(Document.Sources.Where(source => source != null && !string.IsNullOrWhiteSpace(source.Name)).Select(source => (source.Name, source.Name)))
                .ToList();
            if (!string.IsNullOrEmpty(current) && !list.Any(item => item.Item1 == current))
                list.Insert(0, (current, current + " (없는 이름)"));

            return list;
        }

        private void SetData(ChangeEventArgs e) => Edit(Pair("data", Str(e.Value?.ToString())));

        private string ChartKindKey => (_el as ChartElement)?.ChartKind is "bar" or "line" ? ((ChartElement)_el).ChartKind : "column";

        private void SetChartKind(string key) => Edit(Pair("kind", Str(key)));

        private bool TopOnly => string.Equals((_el as ChartElement)?.Emphasis, "max", StringComparison.OrdinalIgnoreCase);

        private void SetEmphasis(bool top) => Edit(Pair("emphasis", top ? Str("max") : null));

        private void ToggleLabels(bool on) => Edit(Pair("labels", Flag(on)));

        private RangeParam FirstRange => Document.Params.OfType<RangeParam>().FirstOrDefault(param => !string.IsNullOrWhiteSpace(param.Name));

        private void ToggleRange(bool on) => Edit(Pair("range", on ? Str(FirstRange?.Name) : null));

        private int RowCount(string data) => Runtime.TryTable(data)?.Rows.Count ?? 6;

        private int? TableMax => (_el as TableElement)?.Max;

        private void StepTable(int direction)
        {
            var table = (TableElement)_el;
            var current = table.Max ?? RowCount(table.Data);
            Edit(Pair("max", Math.Clamp(current + direction, 1, 50)));
        }

        private void SetDirection(string key) => Edit(Pair("direction", Str(key)));

        private void StepRepeat(int direction)
        {
            var repeat = (RepeatElement)_el;
            Edit(Pair("max", Math.Clamp((repeat.Max ?? 5) + direction, 1, 50)));
        }

        private void ToggleTemplate(Element child, bool shown) => Send(child.Id, Pair("hidden", Flag(!shown)));

        private IEnumerable<DashParam> Compatible() => _el switch
        {
            SliderElement => Document.Params.OfType<NumberParam>().Cast<DashParam>(),
            RangeSliderElement => Document.Params.OfType<RangeParam>().Cast<DashParam>(),
            ToggleElement => Document.Params.OfType<BoolParam>().Cast<DashParam>(),
            SegmentedElement or SelectElement => Document.Params.OfType<PickParam>().Cast<DashParam>(),
            _ => Enumerable.Empty<DashParam>(),
        };

        private DashParam BoundParam => Document.FindParam(DashDependents.ParamOf(_el));

        private string LabelOf() => _el switch
        {
            SliderElement slider => slider.Label,
            RangeSliderElement range => range.Label,
            ToggleElement toggle => toggle.Label,
            SegmentedElement segmented => segmented.Label,
            SelectElement dropdown => dropdown.Label,
            _ => null,
        };

        private void ChooseParam(DashParam param)
        {
            var old = DashDependents.ParamOf(_el);
            if (string.Equals(old, param.Name, StringComparison.Ordinal))
                return;

            var patch = Pair("param", Str(param.Name));
            if (string.Equals(LabelOf() ?? string.Empty, old ?? string.Empty, StringComparison.Ordinal))
                patch["label"] = Str(param.Name);

            if (!string.IsNullOrEmpty(old) && _el.Name != null && _el.Name.StartsWith(old + " ", StringComparison.Ordinal))
                patch["name"] = Str(param.Name + _el.Name.Substring(old.Length));

            if (_el is SegmentedElement && param is PickParam pick)
            {
                var width = Math.Max(_style.W ?? DashCss.DefaultWidth, 88 * Runtime.OptionsOf(pick).Count);
                var left = Math.Max(0, Math.Min(_style.X ?? 0, Model.PageWidth - width));
                patch["style"] = new JsonObject { ["w"] = Num(width), ["x"] = Num(left) };
            }

            Edit(patch);
        }

        private void SetLabel(ChangeEventArgs e) => Edit(Pair("label", Str(e.Value?.ToString() ?? string.Empty)));

        private void SetNumber(NumberParam param, string key, ChangeEventArgs e)
        {
            if (!TryNumber(e.Value?.ToString(), out var number))
                return;

            var min = param.Min;
            var max = param.Max;
            var step = param.Step;
            var start = param.Default;
            switch (key)
            {
                case "min":
                    min = number;
                    break;
                case "max":
                    max = number;
                    break;
                case "step":
                    step = number;
                    break;
                default:
                    start = number;
                    break;
            }

            if (!(max > min))
            {
                Commands?.Notify("가장 크게는 가장 작게보다 커야 해요.");
                return;
            }

            if (!(step > 0))
            {
                Commands?.Notify("한 칸은 0보다 커야 해요.");
                return;
            }

            start = Math.Clamp(start, min, max);
            EditParam(param, new JsonObject { ["min"] = Num(min), ["max"] = Num(max), ["step"] = Num(step), ["default"] = Num(start) });
        }

        private void SetRange(RangeParam param, bool startSide, ChangeEventArgs e)
        {
            if (!TryNumber(e.Value?.ToString(), out var number))
                return;

            var from = param.From;
            var to = param.To;
            if (startSide)
            {
                from = Math.Clamp(number, param.Min, param.Max);
                to = Math.Max(to, from);
            }
            else
            {
                to = Math.Clamp(number, param.Min, param.Max);
                from = Math.Min(from, to);
            }

            EditParam(param, new JsonObject { ["from"] = Num(from), ["to"] = Num(to) });
        }

        private void WholeRange(RangeParam param) =>
            EditParam(param, new JsonObject { ["from"] = Num(param.Min), ["to"] = Num(param.Max) });

        private void SetDefaultBool(BoolParam param, bool on) => EditParam(param, Pair("default", Flag(on)));

        private void SetDefaultPick(PickParam param, string option) => EditParam(param, Pair("default", Str(option)));

        private void SetLook(string key) => Edit(Pair("look", Str(key)));

        private void ChooseShape(string type)
        {
            if (_el == null || string.Equals(_el.Kind, type, StringComparison.Ordinal) || Commands == null)
                return;

            var parent = ParentElement;
            if (parent == null || JsonSerializer.SerializeToNode<Element>(_el, DashFile.Options) is not JsonObject item)
                return;

            item["type"] = type;
            var param = BoundParam as PickParam;
            var count = param == null ? 0 : Runtime.OptionsOf(param).Count;
            var style = item["style"] as JsonObject ?? new JsonObject();
            if (type == "select")
            {
                style["w"] = 216;
                if (item["label"] == null && param != null)
                    item["label"] = param.Name;
            }
            else if (count > 0)
            {
                var width = Math.Max(style["w"]?.GetValue<double>() ?? DashCss.DefaultWidth, 88 * count);
                style["w"] = Math.Round(width, 2);
                style["x"] = Math.Round(Math.Max(0, Math.Min(style["x"]?.GetValue<double>() ?? 0, Model.PageWidth - width)), 2);
            }

            item["style"] = style;
            var id = _el.Id;
            var index = parent.Nested().ToList().IndexOf(_el);
            var commands = new List<EditCommand> { EditCommand.Create(DashCanvasEdits.RemoveTool, new { id }) };
            commands.Add(parent == Document.Root
                ? EditCommand.Create(DashCanvasEdits.AddTool, new { item, index })
                : EditCommand.Create(DashCanvasEdits.AddTool, new { item, parent = parent.Id, index }));
            if (Commands.Apply(commands) && State != null)
            {
                State.Selected.Clear();
                State.Selected.Add(id);
            }
        }

        private IEnumerable<(string Value, string Label)> NumericValues(string current)
        {
            var list = Document.Values.Where(value => value != null && value is not StepsTable && !string.IsNullOrWhiteSpace(value.Name))
                .Select(value => (value.Name, value.Name))
                .ToList();
            if (!string.IsNullOrEmpty(current) && !list.Any(item => item.Item1 == current))
                list.Insert(0, (current, current + " (없는 이름)"));

            return list;
        }

        private void SetMeterValue(string name) => Edit(Pair("value", Str(name)));

        private void SetMeterColor(string hex) => Edit(Pair("color", Str(hex)));

        private string MeterNote
        {
            get
            {
                var meter = (MeterElement)_el;
                if (string.IsNullOrWhiteSpace(meter.Value) || !Runtime.Exists(meter.Value))
                    return "‘" + meter.Value + "’ 값이 없어요.";

                var ratio = DashCells.Number(Runtime.Value(meter.Value));
                return ratio.HasValue
                    ? "지금 " + DashFormats.Display(Math.Max(0, ratio.Value), "pct") + "만큼 찼어요. 100%가 되면 가득 차요."
                    : "채울 값이 아직 숫자가 아니에요.";
            }
        }

        private string ActKey => (_el as ButtonElement)?.Action?.Kind;

        private DashAction Act => (_el as ButtonElement)?.Action;

        private void PickAct(string key)
        {
            if (key == ActKey)
                return;

            var action = new JsonObject { ["type"] = key };
            if (key == "set")
            {
                var param = Document.Params.OfType<RangeParam>().Cast<DashParam>().Concat(Document.Params).FirstOrDefault(item => item != null && !string.IsNullOrWhiteSpace(item.Name));
                if (param == null)
                {
                    Commands?.Notify("먼저 데이터에서 바꿀 조절값을 만들어 주세요.");
                    return;
                }

                var first = Presets(param).FirstOrDefault().Value;
                if (first == null)
                {
                    Commands?.Notify("이 조절값에는 고를 값이 아직 없어요.");
                    return;
                }

                action["param"] = param.Name;
                action["value"] = first;
            }
            else if (key == "ask")
            {
                action["text"] = "«" + (Document.Params.FirstOrDefault()?.Name ?? "값") + "» 매출을 세 줄로 요약해 줘";
            }
            else if (key == "open")
            {
                var file = Document.Sources.OfType<ExcelSheetSource>().Select(source => source.File).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
                if (file == null)
                {
                    Commands?.Notify("열 수 있는 엑셀 파일이 아직 없어요.");
                    return;
                }

                action["file"] = file;
            }

            foreach (var stale in s_staleActionKeys)
            {
                if (!action.ContainsKey(stale))
                    action[stale] = null;
            }

            Edit(Pair("action", action));
        }

        private List<(JsonNode Value, string Text)> Presets(DashParam param)
        {
            var list = new List<(JsonNode, string)>();
            switch (param)
            {
                case BoolParam:
                    list.Add((Flag(true), "켜기"));
                    list.Add((Flag(false), "끄기"));
                    break;
                case NumberParam number:
                    foreach (var value in new[] { number.Min, number.Default, number.Max }.Distinct())
                        list.Add((Num(value), param.Display(value)));

                    break;
                case RangeParam range:
                    list.Add((new JsonArray(Num(range.Min), Num(range.Max)), "전체 기간"));
                    if (range.From > range.Min || range.To < range.Max)
                        list.Add((new JsonArray(Num(range.From), Num(range.To)), param.Display(new DashRange(range.From, range.To))));

                    break;
                case PickParam pick:
                    foreach (var option in Runtime.OptionsOf(pick))
                        list.Add((Str(option), option));

                    break;
            }

            return list;
        }

        private void PickActParam(DashParam param)
        {
            if (Act is SetAction set && set.Param == param.Name)
                return;

            var first = Presets(param).FirstOrDefault().Value;
            if (first != null)
                Edit(Pair("action", new JsonObject { ["type"] = "set", ["param"] = param.Name, ["value"] = first }));
        }

        private void PickActValue(JsonNode value) => Edit(Pair("action", Pair("value", value.DeepClone())));

        private void SetAskText(ChangeEventArgs e) => Edit(Pair("action", Pair("text", Str(e.Value?.ToString() ?? string.Empty))));

        private string ActNote
        {
            get
            {
                switch (Act)
                {
                    case SetAction set:
                        var param = Document.FindParam(set.Param);
                        if (param == null)
                            return "바꿀 조절값을 골라 주세요.";

                        var shown = Presets(param).Where(item => JsonNode.DeepEquals(item.Value, set.Value)).Select(item => item.Text).FirstOrDefault() ?? set.Value?.ToJsonString().Trim('"');
                        return "누르면 ‘" + param.Name + "’" + KoreanParticles.Attach(param.Name, "이") + " " + shown + " 상태로 바뀌어요. 다른 조절값은 그대로예요.";
                    case ResetAction:
                        return "누르면 조절값이 모두 파일을 열 때 값으로 돌아가요.";
                    case AskAction:
                        return "누르면 아래 말을 도우미 입력 칸에 적어 줘요. 답은 도우미 창에 와요.";
                    case OpenAction open:
                        return "누르면 ‘" + open.File + "’ 파일을 열어요.";
                    case RefreshAction:
                        return "누르면 엑셀 파일을 다시 읽어서 숫자를 새로 셈해요.";
                    case PdfAction:
                        return "누르면 지금 보이는 그대로 PDF 파일을 만들어요.";
                    default:
                        return "누르면 할 일을 골라 주세요.";
                }
            }
        }

        private string AskPreview => new DashScope(Runtime).FillText((Act as AskAction)?.Text ?? string.Empty);

        private sealed record WhenChoice(string Text, string Expression, string Note);

        private List<WhenChoice> WhenChoices()
        {
            var list = new List<WhenChoice> { new("항상", null, "언제나 보여요. 값에 따라 숨기려면 도우미에게 ‘달성률이 100%를 넘으면 보여 줘’처럼 부탁해도 돼요.") };
            foreach (var param in Document.Params.Where(item => item != null && !string.IsNullOrWhiteSpace(item.Name)))
            {
                var name = param.Name;
                var quoted = "‘" + name + "’" + KoreanParticles.Attach(name, "을");
                if (param is BoolParam)
                {
                    list.Add(new(name + " 켰을 때", name, quoted + " 켰을 때만 보여요."));
                    list.Add(new(name + " 껐을 때", "NOT(" + name + ")", quoted + " 껐을 때만 보여요."));
                }
                else if (param is PickParam pick)
                {
                    var options = Runtime.OptionsOf(pick);
                    if (options.Count is > 0 and <= 4)
                    {
                        foreach (var option in options)
                            list.Add(new(option + "일 때", name + "=\"" + option + "\"", "‘" + name + "’" + KoreanParticles.Attach(name, "이") + " ‘" + option + "’일 때만 보여요."));
                    }
                }
            }

            return list;
        }

        private static string Normal(string expression) =>
            (expression ?? string.Empty).Trim().TrimStart('=').Replace(" ", string.Empty, StringComparison.Ordinal);

        private bool IsWhen(WhenChoice choice) => Normal(choice.Expression) == Normal(_el.When);

        private void SetWhen(WhenChoice choice)
        {
            _revealed = false;
            _revealFail = null;
            if (IsWhen(choice))
                return;

            Edit(Pair("when", Str(choice.Expression)));
        }

        private bool HiddenByCondition => _el != null && !_el.Hidden && !string.IsNullOrWhiteSpace(_el.When) && !Runtime.IsVisible(_el);

        private string WhenNote(List<WhenChoice> choices)
        {
            if (string.IsNullOrWhiteSpace(_el.When))
                return choices[0].Note;

            return choices.FirstOrDefault(IsWhen)?.Note ?? "다음 식이 맞을 때만 보여요: " + _el.When;
        }

        private string NoticeText
        {
            get
            {
                var note = WhenNote(WhenChoices());
                if (_revealed)
                    return "조건에 맞게 조절값을 바꿔서 보고 있어요. " + note;

                return string.IsNullOrEmpty(_revealFail)
                    ? "지금 조절값으로는 페이지에 안 보여요. " + note
                    : _revealFail + " " + note;
            }
        }

        private Task Reveal()
        {
            if (_el == null)
                return Task.CompletedTask;

            _revealFail = null;
            var names = DashNames.InExpression(_el.When).ToHashSet(StringComparer.Ordinal);
            foreach (var param in Document.Params.Where(item => item != null && names.Contains(item.Name)))
            {
                var original = Runtime.State.Get(param.Name);
                foreach (var candidate in Candidates(param))
                {
                    Model.SetParam(param.Name, candidate);
                    if (Runtime.IsVisible(_el))
                    {
                        _revealed = true;
                        return OnChanged.InvokeAsync();
                    }
                }

                Model.SetParam(param.Name, original);
            }

            _revealFail = "조절값을 바꿔 봐도 지금 엑셀로는 이 조건이 맞지 않아요.";
            return OnChanged.InvokeAsync();
        }

        private IEnumerable<object> Candidates(DashParam param) => param switch
        {
            BoolParam => new object[] { true, false },
            PickParam pick => Runtime.OptionsOf(pick).Cast<object>().ToList(),
            NumberParam number => new object[] { number.Min, number.Default, number.Max }.Distinct().ToList(),
            _ => Array.Empty<object>(),
        };

        private Task RestoreReveal()
        {
            _revealed = false;
            Model.ResetParams();
            return OnChanged.InvokeAsync();
        }

        private string PaperKey
        {
            get
            {
                var root = Document.Root;
                var match = s_papers.FirstOrDefault(paper => paper.Key == root.Paper);
                return match.Key ?? string.Empty;
            }
        }

        private void SetPaper(string key)
        {
            foreach (var paper in s_papers)
            {
                if (paper.Key == key && key != Document.Root.Paper)
                {
                    Send(PageId, new JsonObject { ["paper"] = key, ["width"] = paper.Width, ["height"] = paper.Height });
                    return;
                }
            }
        }

        private void SetPageFill(string hex) => Send(PageId, Style("fill", Str(hex)));

        private void ToggleMagnet(bool on)
        {
            if (State != null)
                State.Magnet = on;

            _ = OnChanged.InvokeAsync();
        }

        private Task OpenCalculation()
        {
            var data = (_el as ChartElement)?.Data;
            if (State == null || string.IsNullOrWhiteSpace(data))
                return Task.CompletedTask;

            State.Mode = DashViewState.DataMode;
            State.DataTable = data;
            State.Selected.Clear();
            return OnChanged.InvokeAsync();
        }

        private IReadOnlyList<Element> FollowingElements() =>
            BoundParam == null ? Array.Empty<Element>() : DashDependents.ElementsOf(Document, BoundParam.Name);

        private IReadOnlyList<DashValue> FollowingValues() =>
            BoundParam == null ? Array.Empty<DashValue>() : DashDependents.ValuesOf(Document, BoundParam.Name);
    }
}
