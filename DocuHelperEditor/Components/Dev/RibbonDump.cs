#if DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DocuHelperEditor.State.Ribbon;
using DocuHelperEditor.State.Ribbon.Excel;
using Microsoft.JSInterop;

namespace DocuHelperEditor.Components.Dev
{
    public static class RibbonDump
    {
        private static readonly List<WeakReference<RibbonRouter>> s_routers = new();
        private static readonly JsonSerializerOptions s_options = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        public static void Track(RibbonRouter router)
        {
            lock (s_routers)
                s_routers.Add(new WeakReference<RibbonRouter>(router));
        }

        [JSInvokable]
        public static string DumpRibbons(string dir)
        {
            Directory.CreateDirectory(dir);
            var written = new List<string>();
            List<RibbonRouter> live;
            lock (s_routers)
                live = s_routers.Select(r => r.TryGetTarget(out var t) ? t : null).Where(t => t != null && t.File != null && t.File == t.Workspace.ActiveFile && t is not RibbonPlaygroundHost).ToList();

            foreach (var router in live)
            {
                var name = router.GetType().Name + "-" + router.File.Type;
                System.IO.File.WriteAllText(Path.Combine(dir, name + ".json"), Commands(router).ToJsonString(s_options), new UTF8Encoding(false));
                written.Add(name);
            }

            System.IO.File.WriteAllText(Path.Combine(dir, "dialogs.json"), Dialogs().ToJsonString(s_options), new UTF8Encoding(false));
            written.Add("dialogs");
            return string.Join(",", written);
        }

        [JSInvokable]
        public static string InvokeRibbon(string id, string arg)
        {
            RibbonRouter router;
            lock (s_routers)
                router = s_routers.Select(r => r.TryGetTarget(out var t) ? t : null).LastOrDefault(t => t != null && t.File != null && t.File == t.Workspace.ActiveFile && t is not RibbonPlaygroundHost);

            if (router == null)
                return "no router";

            _ = router.InvokeAsync(id, arg);
            return router.GetType().Name;
        }

        private static JsonObject Commands(RibbonRouter router)
        {
            var root = new JsonObject();
            foreach (var id in router.Ids.OrderBy(i => i, StringComparer.Ordinal))
            {
                var command = router.Find(id);
                var entry = new JsonObject
                {
                    ["class"] = command.Class.ToString(),
                    ["tools"] = new JsonArray(command.Tools.Select(t => (JsonNode)t).ToArray()),
                    ["locked"] = command.IsAllowedWhileLocked,
                    ["blocker"] = router.DisabledReasonOf(id),
                    ["pressed"] = router.IsOn(id),
                    ["value"] = router.ValueOf(id),
                    ["color"] = router.ColorOf(id),
                };

                if (command.Class == RibbonCommandClass.Assistant)
                    entry["prompt"] = Try(() => (JsonNode)command.Prompt?.Invoke());

                var menu = Try(() => router.MenuFor(id));
                if (menu is IReadOnlyList<RibbonMenuItem> items && items.Count > 0)
                    entry["menu"] = new JsonArray(items.Select(i => (JsonNode)MenuItem(i)).ToArray());

                if (command.Build != null)
                {
                    var builds = new JsonObject { ["(null)"] = Build(command, null) };
                    if (menu is IReadOnlyList<RibbonMenuItem> list)
                    {
                        foreach (var value in list.Select(i => i.Value).Where(v => v != null).Distinct())
                            builds[value] = Build(command, value);
                    }

                    entry["build"] = builds;
                }

                root[id] = entry;
            }

            return root;
        }

        private static JsonNode MenuItem(RibbonMenuItem item)
        {
            return new JsonObject
            {
                ["kind"] = item.Kind.ToString(),
                ["value"] = item.Value,
                ["label"] = item.Label,
                ["detail"] = item.Detail,
                ["checked"] = item.Checked,
                ["disabled"] = item.Disabled,
                ["reason"] = item.Reason,
                ["current"] = item.Current,
                ["swatches"] = item.Swatches.Count,
            };
        }

        private static JsonNode Build(RibbonCommand command, string arg)
        {
            try
            {
                var edits = command.Build(arg);
                if (edits == null)
                    return null;

                return new JsonArray(edits.Select(e => (JsonNode)new JsonObject { ["tool"] = e.Name, ["args"] = JsonNode.Parse(e.Arguments.GetRawText()) }).ToArray());
            }
            catch (Exception e)
            {
                return "throws " + e.GetType().Name + ": " + e.Message;
            }
        }

        private static JsonNode Try(Func<object> read)
        {
            try
            {
                var value = read();
                return value is JsonNode node ? node : value == null ? null : JsonValue.Create(value.ToString());
            }
            catch (Exception e)
            {
                return "throws " + e.GetType().Name;
            }
        }

        private static IReadOnlyList<RibbonMenuItem> Try(Func<IReadOnlyList<RibbonMenuItem>> read)
        {
            try
            {
                return read();
            }
            catch
            {
                return null;
            }
        }

        private static JsonObject Dialogs()
        {
            var root = new JsonObject();
            foreach (var (key, dialog) in NamedDialogs.All)
                root["named:" + key] = Named(dialog.Title, dialog.Fields, dialog.Validate, dialog.Build);

            var context = new ExcelDialogContext("Sheet1", "A1:C5", "B2", "메모", "글자");
            foreach (var id in ExcelNamedDialogs.Ids.OrderBy(i => i, StringComparer.Ordinal))
            {
                var dialog = ExcelNamedDialogs.Create(id, context);
                var node = Named(dialog.Title, dialog.Fields, dialog.Validate, dialog.Build);
                node["tool"] = dialog.Tool;
                root["excel:" + id] = node;
            }

            foreach (var (id, dialog) in SpecDialogs("DocuHelperEditor.State.Ribbon.WordCommandTable").Concat(SpecDialogs("DocuHelperEditor.State.Ribbon.HwpCommandTable")))
                root["spec:" + id] = Named(dialog.Title, dialog.Fields, dialog.Validate, null);

            foreach (var (id, dialog) in PptDialogs())
                root["ppt:" + id] = Named(dialog.Title, dialog.Fields, null, null);

            return root;
        }

        private static JsonObject Named(string title, IReadOnlyList<DialogField> fields, Func<IReadOnlyDictionary<string, JsonNode>, string> validate, Func<IReadOnlyDictionary<string, JsonNode>, JsonObject> build)
        {
            var values = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
            foreach (var field in fields)
            {
                if (field.Default == null)
                    continue;

                try
                {
                    values[field.Name] = JsonNode.Parse(field.Default);
                }
                catch (JsonException)
                {
                    values[field.Name] = JsonValue.Create(field.Default);
                }
            }

            var node = new JsonObject
            {
                ["title"] = title,
                ["fields"] = new JsonArray(fields.Select(f => (JsonNode)new JsonObject
                {
                    ["name"] = f.Name,
                    ["label"] = f.Label,
                    ["type"] = f.Type,
                    ["default"] = f.Default,
                    ["options"] = f.Options == null ? null : new JsonArray(f.Options.Select(o => (JsonNode)o).ToArray()),
                    ["labels"] = f.Labels == null ? null : new JsonArray(f.Labels.Select(o => (JsonNode)o).ToArray()),
                }).ToArray()),
            };

            if (validate != null)
                node["validate"] = Try(() => (object)validate(values));

            if (build != null)
                node["build"] = Try(() => (JsonNode)build(values)?.DeepClone());

            return node;
        }

        private static IEnumerable<(string, DialogSpec)> SpecDialogs(string tableType)
        {
            var type = typeof(DialogSpec).Assembly.GetType(tableType);
            var all = (IEnumerable)type.GetProperty("All", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static).GetValue(null);
            foreach (var spec in all)
            {
                var dialog = spec.GetType().GetProperty("Dialog").GetValue(spec) as DialogSpec;
                if (dialog != null)
                    yield return ((string)spec.GetType().GetProperty("Id").GetValue(spec), dialog);
            }
        }

        private static IEnumerable<(string, DialogSpec)> PptDialogs()
        {
            foreach (var spec in DocuHelperEditor.Components.Paper.Ppt.PptCommandData.All)
            {
                if (spec.Dialog != null)
                    yield return (spec.Id, spec.Dialog);

                var index = 0;
                foreach (var entry in spec.Menu)
                {
                    if (entry.Dialog != null)
                        yield return (spec.Id + "#" + index, entry.Dialog);

                    index++;
                }
            }
        }
    }
}
#endif
