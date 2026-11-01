using DocuHelper.Editors;
using DocuHelperEditor.State.Papers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace DocuHelperEditor.Components.Paper.Excel
{
    public partial class ExcelPaper
    {
        private string _selectedObject;

        private RenderFragment<Sheet> ObjectOverlay => sheet => builder =>
        {
            builder.OpenComponent<SheetObjectLayer>(0);
            builder.AddAttribute(1, nameof(SheetObjectLayer.Objects), sheet.Objects);
            builder.AddAttribute(2, nameof(SheetObjectLayer.Selected), _selectedObject);
            builder.AddAttribute(3, nameof(SheetObjectLayer.OnSelect), EventCallback.Factory.Create<string>(this, SelectObject));
            builder.AddAttribute(4, nameof(SheetObjectLayer.OnChange), EventCallback.Factory.Create<SheetObjectChange>(this, ChangeObject));
            builder.CloseComponent();
        };

        private void SelectObject(string name) => _selectedObject = string.IsNullOrEmpty(name) ? null : name;

        private void ChangeObject(SheetObjectChange change)
        {
            var sheet = CurrentSheet.Name;
            switch (change.Action)
            {
                case "move":
                    Edit(EditCommand.Create("change_object", new { sheet, @object = change.Name, action = "move", anchor = SheetRef.Make(change.Column, change.Row) }));
                    break;
                case "resize":
                    Edit(EditCommand.Create("change_object", new { sheet, @object = change.Name, action = "resize", columns = change.Columns, rows = change.Rows }));
                    break;
                case "delete":
                    if (Edit(EditCommand.Create("change_object", new { sheet, @object = change.Name, action = "delete" })))
                        _selectedObject = null;
                    break;
            }
        }
    }
}
