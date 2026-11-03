using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using DocuHelper.Dash.Elements;
using DocuHelper.Editors;
using DocuHelperEditor.State.Ribbon;

namespace DocuHelperEditor.Components.Paper.Dash
{
    public sealed partial class DashRibbonCommands
    {
        private IReadOnlyList<EditCommand> ArrangeEdits(CommandSpec spec, string arg)
        {
            var model = Model;
            var entry = EntryOf(spec, arg);
            var to = entry != null && JsonNode.Parse(entry.Args) is JsonObject args ? args["to"]?.GetValue<string>() : null;
            if (model == null || to == null)
                return null;

            var commands = new List<EditCommand>();
            var picked = new HashSet<Element>(SelectedElements());
            var ascending = to is "front" or "backward";
            foreach (var parent in picked.Select(element => ParentOf(model.Document, element)).Where(parent => parent != null).Distinct())
            {
                var order = parent.Nested().Where(child => child != null).ToList();
                var chosen = order.Where(picked.Contains).ToList();
                for (var step = 0; step < chosen.Count; step++)
                {
                    var at = ascending ? step : chosen.Count - 1 - step;
                    var element = chosen[at];
                    var from = order.IndexOf(element);
                    var target = TargetOf(to, from, order.Count);
                    if (target == from || Settled(to, order, chosen, picked, at, from, target))
                        continue;

                    order.RemoveAt(from);
                    order.Insert(target, element);
                    commands.Add(EditCommand.Create(DashCanvasEdits.MoveTool, new { id = element.Id, index = target }));
                }
            }

            return commands;
        }

        private static int TargetOf(string to, int from, int count) => to switch
        {
            "front" => count - 1,
            "forward" => Math.Min(from + 1, count - 1),
            "backward" => Math.Max(from - 1, 0),
            _ => 0,
        };

        private static bool Settled(string to, List<Element> order, List<Element> chosen, HashSet<Element> picked, int at, int from, int target) => to switch
        {
            "front" => order.Skip(from).SequenceEqual(chosen.Skip(at)),
            "back" => order.Take(from + 1).SequenceEqual(chosen.Take(at + 1)),
            _ => picked.Contains(order[target]),
        };

        private IReadOnlyList<EditCommand> DeleteEdits() =>
            Outermost(SelectedElements())
                .Where(element => !element.Locked)
                .Select(element => EditCommand.Create(DashCanvasEdits.RemoveTool, new { id = element.Id }))
                .ToList();
    }
}
