using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using DocuHelper.Dash;
using DocuHelper.Dash.Elements;
using DocuHelper.Dash.Runtime;
using DocuHelper.Editors;
using DocuHelperEditor.State.Ribbon;

namespace DocuHelperEditor.Components.Paper.Dash
{
    public sealed partial class DashRibbonCommands
    {
        private const string DefaultFont = "Gothic A1";
        private const string DefaultInk = "1C2A48";
        private const double DefaultSize = 16;
        private const double MinSize = 6;
        private const double SizeStep = 2;
        private const int BoldFrom = 600;

        private Dictionary<string, Func<bool>> FormatPressed() => new(StringComparer.Ordinal)
        {
            ["bold"] = () => (FirstStyle()?.FontWeight ?? 0) >= BoldFrom,
            ["italic"] = () => FirstStyle()?.Italic == true,
            ["underline"] = () => FirstStyle()?.Underline == true,
            ["align:left"] = () => AlignOf() == "left",
            ["align:center"] = () => AlignOf() == "center",
            ["align:right"] = () => AlignOf() == "right",
            ["mark"] = () => !string.IsNullOrWhiteSpace(FirstStyle()?.Mark),
        };

        private Dictionary<string, Func<string>> ValueHandlers() => new(StringComparer.Ordinal)
        {
            ["fontName"] = () => FirstStyle()?.FontName is { } name && !string.IsNullOrWhiteSpace(name) ? name.Trim() : DefaultFont,
            ["fontSize"] = () => SizeText(FirstStyle()?.FontSize ?? DefaultSize),
        };

        private Dictionary<string, Func<string>> ColorHandlers() => new(StringComparer.Ordinal)
        {
            ["fontColor"] = () => Hex(FirstStyle()?.Color) is { Length: > 0 } hex ? hex : DefaultInk,
        };

        private static string SizeText(double size) => size.ToString("0.##", CultureInfo.InvariantCulture);

        private static string Hex(string color) => color?.Trim().TrimStart('#').ToUpperInvariant();

        private DashStyle StyleOf(Element element) => new DashScope(Model.Runtime).StyleOf(element);

        private DashStyle InheritedStyle(Element element) => new DashScope(Model.Runtime).StyleOf(new TextElement { Classes = element.Classes });

        private DashStyle FirstStyle()
        {
            var first = SelectedElements().FirstOrDefault();
            return Model == null || first == null ? null : StyleOf(first);
        }

        private string AlignOf()
        {
            var style = FirstStyle();
            return style == null ? null : style.TextAlign is "center" or "right" ? style.TextAlign : "left";
        }

        private IReadOnlyList<RibbonMenuItem> MenuOf(CommandSpec spec)
        {
            var current = spec.Value != null ? Handlers.Values[spec.Value]() : spec.Color != null ? Handlers.Colors[spec.Color]() : null;
            var items = new List<RibbonMenuItem>();
            for (var i = 0; i < spec.Menu.Count; i++)
            {
                var entry = spec.Menu[i];
                bool? check = current == null
                    ? null
                    : spec.Color != null
                        ? RibbonColor.SameColor(current, entry.Swatch)
                        : string.Equals(current, entry.Label, StringComparison.Ordinal);
                var blocker = NeedBlocker(entry.Need, spec);
                items.Add(RibbonMenus.Item(
                    EntryPrefix + i.ToString(CultureInfo.InvariantCulture),
                    entry.Label,
                    detail: entry.Detail,
                    swatch: entry.Swatch,
                    font: spec.Value == "fontName" ? entry.Label : null,
                    check: check,
                    disabled: blocker != null,
                    reason: blocker));
            }

            return items;
        }

        private IReadOnlyList<EditCommand> StyleEdits(CommandSpec spec, string arg)
        {
            var elements = SelectedElements();
            var entry = EntryOf(spec, arg);
            if (elements.Count == 0 || (entry == null && spec.Menu.Count > 0))
                return null;

            var json = entry != null ? entry.Args : spec.ArgsOff != null && IsPressed(spec) ? spec.ArgsOff : spec.Args;
            if (JsonNode.Parse(json) is not JsonObject patch)
                return null;

            return StyleCommands(elements, _ => patch);
        }

        private IReadOnlyList<EditCommand> SizeEdits(double step)
        {
            var elements = SelectedElements();
            if (elements.Count == 0)
                return null;

            return StyleCommands(elements, element => new JsonObject
            {
                ["fontSize"] = JsonValue.Create(Math.Max(MinSize, (StyleOf(element).FontSize ?? DefaultSize) + step)),
            });
        }

        private List<EditCommand> StyleCommands(IEnumerable<Element> elements, Func<Element, JsonObject> patchFor)
        {
            var commands = new List<EditCommand>();
            foreach (var element in elements)
            {
                var patch = Resolved(element, patchFor(element));
                if (patch.Count > 0 && !Applied(element, patch))
                    commands.Add(EditCommand.Create(DashCanvasEdits.UpdateTool, new { id = element.Id, patch = new { style = patch } }));
            }

            return commands;
        }

        private JsonObject Resolved(Element element, JsonObject patch)
        {
            var inherited = StyleJson(InheritedStyle(element));
            var result = new JsonObject();
            foreach (var pair in patch)
            {
                if (pair.Value != null)
                    result[pair.Key] = pair.Value.DeepClone();
                else if (inherited.ContainsKey(pair.Key) && NeutralOf(pair.Key) is { } neutral)
                    result[pair.Key] = neutral;
                else
                    result[pair.Key] = null;
            }

            return result;
        }

        private static bool Applied(Element element, JsonObject patch)
        {
            var own = StyleJson(element.Style);
            foreach (var pair in patch)
            {
                own.TryGetPropertyValue(pair.Key, out var current);
                if (!JsonNode.DeepEquals(current, pair.Value))
                    return false;
            }

            return true;
        }

        private static JsonNode NeutralOf(string key) => key switch
        {
            "fontWeight" => JsonValue.Create(400),
            "italic" => JsonValue.Create(false),
            "underline" => JsonValue.Create(false),
            "mark" => JsonValue.Create(string.Empty),
            _ => null,
        };

        private static JsonObject StyleJson(DashStyle style) =>
            style == null ? new JsonObject() : JsonSerializer.SerializeToNode(style, DashFile.Options) as JsonObject ?? new JsonObject();
    }
}
