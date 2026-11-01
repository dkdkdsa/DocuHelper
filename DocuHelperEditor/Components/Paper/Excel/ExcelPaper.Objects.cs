using System.Linq;
using DocuHelper.Editors;
using DocuHelperEditor.State.Papers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace DocuHelperEditor.Components.Paper.Excel
{
    public partial class ExcelPaper
    {
        private string _selectedObjectSheet;
        private string _selectedObjectId;

        private RenderFragment<Sheet> ObjectOverlay => sheet => builder =>
        {
            builder.OpenComponent<SheetObjectLayer>(0);
            builder.AddAttribute(1, nameof(SheetObjectLayer.SheetName), sheet.Name);
            builder.AddAttribute(2, nameof(SheetObjectLayer.Objects), sheet.Objects);
            builder.AddAttribute(3, nameof(SheetObjectLayer.SelectedId), _selectedObjectSheet == sheet.Name ? _selectedObjectId : null);
            builder.AddAttribute(4, nameof(SheetObjectLayer.OnSelect), EventCallback.Factory.Create<string>(this, id => SelectObject(sheet.Name, id)));
            builder.AddAttribute(5, nameof(SheetObjectLayer.OnChange), EventCallback.Factory.Create<SheetObjectChange>(this, ChangeObject));
            builder.CloseComponent();
        };

        private void SelectObject(string sheet, string id)
        {
            _selectedObjectSheet = string.IsNullOrEmpty(id) ? null : sheet;
            _selectedObjectId = string.IsNullOrEmpty(id) ? null : id;
        }

        private void ChangeObject(SheetObjectChange change)
        {
            if (change.SheetName != CurrentSheet.Name)
                return;

            var item = CurrentSheet.Objects.FirstOrDefault(o => o.Id == change.Id);
            if (item == null || !item.Editable)
                return;

            var sheet = CurrentSheet.Name;
            switch (change.Action)
            {
                case SheetObjectAction.Move:
                    Edit(EditCommand.Create("change_object", new { sheet, @object = item.Name, action = "move", anchor = SheetRef.Make(change.Column, change.Row) }));
                    break;
                case SheetObjectAction.Resize:
                    Edit(EditCommand.Create("change_object", new { sheet, @object = item.Name, action = "resize", columns = change.Columns, rows = change.Rows }));
                    break;
                case SheetObjectAction.Delete:
                    if (Edit(EditCommand.Create("change_object", new { sheet, @object = item.Name, action = "delete" })))
                        SelectObject(sheet, null);
                    break;
            }
        }
    }
}
