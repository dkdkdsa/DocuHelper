namespace DocuHelper.Dash.Logic
{
    public sealed class RangeParam : DashParam
    {
        public double Min { get; set; }

        public double Max { get; set; }

        public double Step { get; set; } = 1;

        public double From { get; set; }

        public double To { get; set; }

        public override void Validate(DashCheck check)
        {
            base.Validate(check);
            if (!(Min <= From && From <= To && To <= Max))
                check.Fail($"‘{Name}’ 범위의 처음 값은 가장 작은 값과 가장 큰 값 사이에 있어야 해요.");
        }
    }
}
