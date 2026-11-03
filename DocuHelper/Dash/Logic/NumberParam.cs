using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Logic
{
    public sealed class NumberParam : DashParam
    {
        public double Min { get; set; }

        public double Max { get; set; }

        public double Step { get; set; } = 1;

        public double Default { get; set; }

        public override void Validate(DashCheck check)
        {
            base.Validate(check);
            if (!(Min <= Default && Default <= Max))
                check.Fail($"‘{Name}’ 숫자의 처음 값은 가장 작은 값과 가장 큰 값 사이에 있어야 해요.");
        }

        public override object InitialValue() => Snap(Default, Min, Max, Step);

        public override object Normalize(object value)
        {
            var number = DashJson.Number(value);
            return number.HasValue ? Snap(number.Value, Min, Max, Step) : InitialValue();
        }
    }
}
