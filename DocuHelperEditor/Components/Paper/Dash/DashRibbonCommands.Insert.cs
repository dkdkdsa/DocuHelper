using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using DocuHelper.Dash.Elements;
using DocuHelper.Editors;
using DocuHelperEditor.State.Ribbon;

namespace DocuHelperEditor.Components.Paper.Dash
{
    public sealed partial class DashRibbonCommands
    {
        public bool InsertControl(string param, (double X, double Y)? at = null)
        {
            var model = Model;
            var type = model == null ? null : DashInserts.ControlFor(model.Document.FindParam(param), model.Runtime);
            return type != null && Insert(DashInserts.ParamControl(type, param), at);
        }

        public bool InsertValue(string name, (double X, double Y)? at = null) => Insert(DashInserts.ValueText(name), at);

        public bool InsertTable(string name, (double X, double Y)? at = null) => Insert(DashInserts.TableChart(name), at);

        private bool Insert(JsonObject item, (double X, double Y)? at = null)
        {
            if (Model == null || Editable() != null)
                return false;

            _select = null;
            if (at == null)
                return Apply(PlanInsert(item));

            item["style"] = new JsonObject { ["x"] = Math.Round(at.Value.X), ["y"] = Math.Round(at.Value.Y) };
            return Apply(PlanInsert(item, false));
        }

        private IReadOnlyList<EditCommand> InsertEdits(CommandSpec spec, string arg)
        {
            var entry = EntryOf(spec, arg);
            if (entry == null && spec.Menu.Count > 0)
                return null;

            return JsonNode.Parse(entry?.Args ?? spec.Args) is JsonObject item ? PlanInsert(item) : null;
        }

        private IReadOnlyList<EditCommand> PlanInsert(JsonObject item, bool inSelection = true)
        {
            var model = Model;
            if (model == null)
                return null;

            var plan = DashInserts.Plan(model, item, inSelection ? InsertParent() : null);
            _select = new[] { plan.Id };
            return plan.Commands;
        }

        private string InsertParent()
        {
            var selected = SelectedElements();
            return selected.Count == 1 && IsContainer(selected[0]) ? selected[0].Id : null;
        }

        private static bool IsContainer(Element element) =>
            element is RowElement or ColumnElement or RepeatElement or (CanvasElement and not PageElement);
    }
}
