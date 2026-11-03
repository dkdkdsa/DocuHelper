using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DocuHelper.Dash;
using DocuHelper.Dash.Logic;
using DocuHelper.Editors;
using DocuHelperEditor.State.Papers;
using DocuHelperEditor.State.Ribbon;

namespace DocuHelperEditor.Components.Paper.Dash
{
    public sealed partial class DashRibbonCommands
    {
        private const string NoFileOfKindReason = "작업 폴더에 맞는 파일이 없어요.";
        private const string NoOriginReason = "먼저 원본을 연결해 주세요.";
        private const string SourceKind = "source";
        private const string ParamKind = "param";
        private const string ValueKind = "value";

        private string FilesBlocker(string type) =>
            Model == null ? NoModelReason : DashSourceFiles.Of(Workspace, type).Count > 0 ? null : NoFileOfKindReason;

        private string OriginBlocker()
        {
            var document = Model?.Document;
            if (document == null)
                return NoModelReason;

            var any = document.Sources.Any(source => source != null && !string.IsNullOrWhiteSpace(source.Name))
                || document.Values.OfType<StepsTable>().Any(table => !string.IsNullOrWhiteSpace(table.Name));
            return any ? null : NoOriginReason;
        }

        private async Task DataAsync(CommandSpec spec, string arg)
        {
            var model = Model;
            if (model == null || _state.Dialogs == null)
                return;

            switch (spec.Action)
            {
                case "source":
                    await AddSourceAsync(model, spec, arg);
                    break;
                case "param":
                    await AddParamAsync(model, spec, arg);
                    break;
                case "table":
                    await AddTableAsync(model, spec);
                    break;
                case "formula":
                    await AddFormulaAsync(model, spec);
                    break;
            }
        }

        private async Task AddSourceAsync(DashPaperModel model, CommandSpec spec, string arg)
        {
            var type = TypeOf(spec, arg);
            if (type == null)
                return;

            var files = DashSourceFiles.Of(Workspace, type);
            if (files.Count == 0)
            {
                Notify(NoFileOfKindReason);
                return;
            }

            var name = await AskAddAsync(spec.Id, SourceKind, DashDataForms.Source(model.Document, type, files));
            if (name != null)
                Show(name);
        }

        private async Task AddParamAsync(DashPaperModel model, CommandSpec spec, string arg)
        {
            var type = TypeOf(spec, arg);
            if (type == null)
                return;

            var name = await AskAddAsync(spec.Id, ParamKind, DashDataForms.Param(model.Document, type));
            if (name != null)
                Show(name);
        }

        private async Task AddTableAsync(DashPaperModel model, CommandSpec spec)
        {
            var name = await AskAddAsync(spec.Id, ValueKind, DashDataForms.Table(model.Document, _state.DataTable));
            if (name == null)
                return;

            Show(name);
            _state.Mode = DashViewState.DataMode;
            _state.Selected.Clear();
            _state.EditingId = null;
        }

        private async Task AddFormulaAsync(DashPaperModel model, CommandSpec spec)
        {
            var name = await AskAddAsync(spec.Id, ValueKind, DashDataForms.Formula(model.Document));
            if (name != null)
                Show(name);
        }

        private async Task<string> AskAddAsync(string id, string kind, DashDataForm form)
        {
            var dialog = new DialogSpec(form.Title, form.Fields, form.Validate);
            var values = await _state.Dialogs.AskFieldsAsync(id, dialog, form.Defaults);
            if (values == null)
                return null;

            var item = form.Build(values);
            return Apply(EditCommand.Create(DashCanvasEdits.AddTool, new { kind, item })) ? DashRaw.Text(item, "name") : null;
        }

        private void Show(string name)
        {
            _state.DataTable = name;
            _state.DataStep = -1;
        }

        private static string TypeOf(CommandSpec spec, string arg)
        {
            var entry = EntryOf(spec, arg);
            return entry != null && JsonNode.Parse(entry.Args) is JsonObject args ? DashRaw.Text(args, "type") : null;
        }
    }
}
