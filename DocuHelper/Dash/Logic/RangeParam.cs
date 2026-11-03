using System.Linq;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Logic
{
    public sealed class RangeParam : DashParam
    {
        public const string StartSuffix = ".시작";
        public const string EndSuffix = ".끝";

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

        public override object InitialValue() => new DashRange(Snap(From, Min, Max, Step), Snap(To, Min, Max, Step));

        public override object Normalize(object value)
        {
            if (value is DashRange range)
                return new DashRange(Snap(range.From, Min, Max, Step), Snap(range.To, Min, Max, Step));

            var items = DashJson.List(value);
            var numbers = items?.Select(DashJson.Number).ToList();
            if (numbers == null || numbers.Count != 2 || !numbers[0].HasValue || !numbers[1].HasValue)
                return InitialValue();

            return new DashRange(Snap(numbers[0].Value, Min, Max, Step), Snap(numbers[1].Value, Min, Max, Step));
        }

        public override string Display(object value) =>
            value is DashRange range ? DashFormats.DisplayRange(range.From, range.To, Format) : DashFormats.Display(value, Format);
    }
}
