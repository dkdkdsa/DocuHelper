using System.Collections.Generic;
using System.Linq;

namespace DocuHelperEditor.State.Papers
{
    public class SheetValidation
    {
        public List<SheetRegion> Ranges { get; set; } = new();

        public bool ItemsResolved { get; set; } = true;

        public SheetValidationType Type { get; set; }

        public string Operator { get; set; }

        public string Formula1 { get; set; }

        public string Formula2 { get; set; }

        public List<string> Items { get; set; } = new();

        public bool AllowBlank { get; set; } = true;

        public bool DropDown { get; set; } = true;

        public bool ShowInput { get; set; }

        public string InputTitle { get; set; }

        public string InputMessage { get; set; }

        public bool ShowError { get; set; } = true;

        public SheetValidationStyle ErrorStyle { get; set; }

        public string ErrorTitle { get; set; }

        public string ErrorMessage { get; set; }

        public bool IsList => Type == SheetValidationType.List;

        public bool HasDropDown => IsList && DropDown && Items.Count > 0;

        public bool HasInputMessage => ShowInput && (!string.IsNullOrEmpty(InputTitle) || !string.IsNullOrEmpty(InputMessage));

        public bool Contains(int row, int column) => Ranges.Any(r => r.Contains(row, column));
    }
}
