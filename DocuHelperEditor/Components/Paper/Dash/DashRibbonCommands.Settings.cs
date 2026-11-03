using System.Threading.Tasks;
using DocuHelper.Dash;
using DocuHelper.Editors;
using DocuHelperEditor.State.Ribbon;

namespace DocuHelperEditor.Components.Paper.Dash
{
    public sealed partial class DashRibbonCommands
    {
        private const string SettingsId = "dash.home.settings";

        private async Task SettingsAsync()
        {
            var model = Model;
            var selected = SelectedElements();
            if (model == null || selected.Count != 1 || _state.Dialogs == null)
                return;

            var element = selected[0];
            var form = DashSettings.Form(model, element);
            var spec = new DialogSpec(DashLabels.Of(element) + " 설정", form.Fields, DashSettings.Validate);
            var values = await _state.Dialogs.AskFieldsAsync(SettingsId, spec, form.Defaults);
            if (values == null)
                return;

            var patch = DashSettings.Patch(model, element, form.Fields, values);
            if (patch.Count > 0)
                Apply(EditCommand.Create(DashCanvasEdits.UpdateTool, new { id = element.Id, patch }));
        }
    }
}
