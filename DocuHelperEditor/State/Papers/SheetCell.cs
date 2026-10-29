using System.Collections.Generic;

namespace DocuHelperEditor.State.Papers
{
    public class SheetCell
    {
        public string Ref { get; set; }

        public string Text { get; set; } = string.Empty;

        public string Formula { get; set; }

        public bool IsNumber { get; set; }

        public List<string> ChangeIds { get; set; } = new();

        public bool IsAsking { get; set; }

        public bool? Bold { get; set; }

        public bool Italic { get; set; }

        public bool Underline { get; set; }

        public SheetAlign Align { get; set; }

        public SheetNumberFormat NumberFormat { get; set; }

        public string EditText => string.IsNullOrEmpty(Formula) ? (Text ?? string.Empty) : Formula;

        public bool HasChange(string changeId) => !string.IsNullOrEmpty(changeId) && ChangeIds != null && ChangeIds.Contains(changeId);

        public bool IsBoldIn(SheetRowKind kind) => Bold ?? (kind != SheetRowKind.Normal);

        public void SetInput(string input)
        {
            var value = input ?? string.Empty;
            if (value.Length > 1 && value[0] == '=')
            {
                Formula = value;
                Text = value;
                IsNumber = false;
                return;
            }

            Formula = null;
            if (SheetNumber.TryParse(value, out var number, out var detected))
            {
                if (detected != SheetNumberFormat.General)
                    NumberFormat = detected;
                else if (NumberFormat == SheetNumberFormat.Percent)
                    number /= 100m;

                IsNumber = true;
                Text = SheetNumber.Format(number, NumberFormat);
                return;
            }

            IsNumber = false;
            Text = value;
        }

        public bool ApplyNumberFormat(SheetNumberFormat format)
        {
            var text = Text ?? string.Empty;
            if (IsNumber && SheetNumber.TryParse(text, out var number, out _))
                text = SheetNumber.Format(number, format);

            if (NumberFormat == format && text == (Text ?? string.Empty))
                return false;

            NumberFormat = format;
            Text = text;
            return true;
        }
    }
}
