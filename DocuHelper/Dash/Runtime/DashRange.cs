using System.Globalization;

namespace DocuHelper.Dash.Runtime
{
    public sealed class DashRange
    {
        public DashRange(double from, double to)
        {
            From = from <= to ? from : to;
            To = from <= to ? to : from;
        }

        public double From { get; }

        public double To { get; }

        public bool Contains(double value) => value >= From - 1e-9 && value <= To + 1e-9;

        public override bool Equals(object obj) => obj is DashRange other && other.From == From && other.To == To;

        public override int GetHashCode() => From.GetHashCode() * 31 + To.GetHashCode();

        public override string ToString() => From.ToString(CultureInfo.InvariantCulture) + "~" + To.ToString(CultureInfo.InvariantCulture);
    }
}
