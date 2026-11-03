using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using DocuHelper.Dash;
using DocuHelper.Dash.Logic;
using DocuHelper.Dash.Runtime;
using DocuHelper.Editors;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.Components.Paper.Dash
{
    internal static class DashInserts
    {
        private const int MaxSegments = 4;
        private const string FallbackColumn = "이름";

        private static readonly Dictionary<string, string> s_paramKinds = new(StringComparer.Ordinal)
        {
            ["slider"] = "number",
            ["rangeSlider"] = "range",
            ["toggle"] = "bool",
            ["segmented"] = "pick",
            ["select"] = "pick",
        };

        public static (IReadOnlyList<EditCommand> Commands, string Id) Plan(DashPaperModel model, JsonObject item, string parent)
        {
            var commands = new List<EditCommand>();
            Bind(model, item, commands);
            var id = NewId(IdsOf(model.Document), DashRaw.Text(item, "type"));
            item["id"] = id;
            commands.Add(EditCommand.Create(DashCanvasEdits.AddTool, parent == null ? new { item } : (object)new { item, parent }));
            return (commands, id);
        }

        public static HashSet<string> IdsOf(DashDocument document) =>
            new(document.Walk().Select(element => element.Id), StringComparer.Ordinal);

        public static string NewId(ISet<string> taken, string prefix)
        {
            var stem = string.IsNullOrWhiteSpace(prefix) ? "e" : prefix;
            for (var n = 1; ; n++)
            {
                var id = stem + n.ToString(CultureInfo.InvariantCulture);
                if (taken.Add(id))
                    return id;
            }
        }

        public static string ControlFor(DashParam param, DashRuntime runtime) => param switch
        {
            NumberParam => "slider",
            RangeParam => "rangeSlider",
            BoolParam => "toggle",
            PickParam pick => runtime.OptionsOf(pick).Count <= MaxSegments ? "segmented" : "select",
            _ => null,
        };

        public static JsonObject ParamControl(string type, string param) => new() { ["type"] = type, ["param"] = param };

        public static JsonObject ValueText(string name) => new() { ["type"] = "text", ["text"] = "«" + name + "»" };

        public static JsonObject TableChart(string table) => new() { ["type"] = "chart", ["kind"] = "column", ["data"] = table };

        private static void Bind(DashPaperModel model, JsonObject item, List<EditCommand> commands)
        {
            var document = model.Document;
            switch (DashRaw.Text(item, "type"))
            {
                case "chart":
                case "table":
                    Fill(item, "data", FirstTable(document));
                    break;
                case "repeat":
                    var table = FirstTable(document);
                    Fill(item, "data", table);
                    if (!item.ContainsKey("template"))
                        item["template"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = "«item." + FirstColumn(model, table) + "»" });

                    break;
                case "meter":
                    Fill(item, "value", FirstFormula(document));
                    break;
                case "slider":
                case "rangeSlider":
                case "toggle":
                case "segmented":
                case "select":
                    BindParam(document, item, commands);
                    break;
            }
        }

        private static void Fill(JsonObject item, string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(value) && !item.ContainsKey(key))
                item[key] = value;
        }

        private static string FirstTable(DashDocument document) =>
            document.Values.OfType<StepsTable>().Select(table => table.Name).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
            ?? document.Sources.Where(source => source != null).Select(source => source.Name).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));

        private static string FirstFormula(DashDocument document) =>
            document.Values.OfType<ExpressionValue>().Select(value => value.Name).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));

        private static string FirstColumn(DashPaperModel model, string table)
        {
            var column = string.IsNullOrWhiteSpace(table)
                ? null
                : model.Runtime.TryTable(table)?.Columns.FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
            return column ?? FallbackColumn;
        }

        private static void BindParam(DashDocument document, JsonObject item, List<EditCommand> commands)
        {
            var name = DashRaw.Text(item, "param");
            string label;
            if (name != null)
            {
                label = document.FindParam(name)?.Label;
            }
            else
            {
                var kind = s_paramKinds[DashRaw.Text(item, "type")];
                var found = document.Params.FirstOrDefault(param => param != null && param.Kind == kind && !string.IsNullOrWhiteSpace(param.Name));
                if (found != null)
                {
                    name = found.Name;
                    label = found.Label;
                }
                else
                {
                    var created = NewParam(document, kind);
                    commands.Add(EditCommand.Create(DashCanvasEdits.AddTool, new { kind = "param", item = created }));
                    name = DashRaw.Text(created, "name");
                    label = DashRaw.Text(created, "label");
                }

                item["param"] = name;
            }

            if (!item.ContainsKey("label"))
                item["label"] = string.IsNullOrWhiteSpace(label) ? name : label;
        }

        private static JsonObject NewParam(DashDocument document, string kind)
        {
            switch (kind)
            {
                case "number":
                    return new JsonObject
                    {
                        ["type"] = "number",
                        ["name"] = FreeName(document, "숫자"),
                        ["label"] = "숫자",
                        ["min"] = 0,
                        ["max"] = 100,
                        ["step"] = 1,
                        ["default"] = 50,
                    };
                case "range":
                    return new JsonObject
                    {
                        ["type"] = "range",
                        ["name"] = FreeName(document, "기간"),
                        ["label"] = "기간",
                        ["min"] = 1,
                        ["max"] = 31,
                        ["from"] = 1,
                        ["to"] = 31,
                        ["format"] = "day",
                    };
                case "bool":
                    return new JsonObject
                    {
                        ["type"] = "bool",
                        ["name"] = FreeName(document, "켜기"),
                        ["label"] = "켜기",
                        ["default"] = false,
                    };
                default:
                    return new JsonObject
                    {
                        ["type"] = "pick",
                        ["name"] = FreeName(document, "고르기"),
                        ["label"] = "고르기",
                        ["options"] = new JsonArray("가", "나", "다"),
                        ["default"] = "가",
                    };
            }
        }

        private static string FreeName(DashDocument document, string stem)
        {
            var taken = new HashSet<string>(
                document.Params.Where(param => param != null).Select(param => param.Name)
                    .Concat(document.Values.Where(value => value != null).Select(value => value.Name)),
                StringComparer.Ordinal);
            for (var n = 1; ; n++)
            {
                var name = stem + n.ToString(CultureInfo.InvariantCulture);
                if (!taken.Contains(name))
                    return name;
            }
        }
    }
}
