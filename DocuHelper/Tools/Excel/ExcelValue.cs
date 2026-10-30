using System;
using System.Globalization;

namespace DocuHelper.Tools.Excel
{
    public sealed class ExcelValue
    {
        public const string DivideByZero = "#DIV/0!";
        public const string NotAvailable = "#N/A";
        public const string BadName = "#NAME?";
        public const string NullError = "#NULL!";
        public const string BadNumber = "#NUM!";
        public const string BadReference = "#REF!";
        public const string BadValue = "#VALUE!";

        public static readonly ExcelValue Blank = new(ExcelValueKind.Blank, 0, null);
        public static readonly ExcelValue True = new(ExcelValueKind.Boolean, 1, null);
        public static readonly ExcelValue False = new(ExcelValueKind.Boolean, 0, null);
        public static readonly ExcelValue Zero = new(ExcelValueKind.Number, 0, null);

        private ExcelValue(ExcelValueKind kind, double number, string text)
        {
            Kind = kind;
            Number = number;
            Text = text;
        }

        public ExcelValueKind Kind { get; }

        public double Number { get; }

        public string Text { get; }

        public bool IsBlank => Kind == ExcelValueKind.Blank;

        public bool IsNumber => Kind == ExcelValueKind.Number;

        public bool IsText => Kind == ExcelValueKind.Text;

        public bool IsBoolean => Kind == ExcelValueKind.Boolean;

        public bool IsError => Kind == ExcelValueKind.Error;

        public bool Boolean => Number != 0;

        public static ExcelValue FromNumber(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return FromError(BadNumber);

            return new ExcelValue(ExcelValueKind.Number, Round15(value), null);
        }

        public static ExcelValue FromText(string text) => new(ExcelValueKind.Text, 0, text ?? string.Empty);

        public static ExcelValue FromBoolean(bool value) => value ? True : False;

        public static ExcelValue FromError(string code) => new(ExcelValueKind.Error, 0, code);

        public static double Round15(double value)
        {
            if (value == 0 || double.IsNaN(value) || double.IsInfinity(value))
                return value == 0 ? 0 : value;

            return double.Parse(value.ToString("G15", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        public static string Invariant(double value) => Round15(value).ToString(CultureInfo.InvariantCulture);

        public bool SameAs(ExcelValue other)
        {
            if (other == null || Kind != other.Kind)
                return false;

            return Kind switch
            {
                ExcelValueKind.Number => Round15(Number) == Round15(other.Number),
                ExcelValueKind.Boolean => Boolean == other.Boolean,
                ExcelValueKind.Text or ExcelValueKind.Error => string.Equals(Text, other.Text, StringComparison.Ordinal),
                _ => true,
            };
        }

        public string ToRawText() => Kind switch
        {
            ExcelValueKind.Number => Invariant(Number),
            ExcelValueKind.Boolean => Boolean ? "TRUE" : "FALSE",
            ExcelValueKind.Text or ExcelValueKind.Error => Text,
            _ => string.Empty,
        };

        public override string ToString() => ToRawText();
    }
}
