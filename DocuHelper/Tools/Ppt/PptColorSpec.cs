using System;
using System.Globalization;

namespace DocuHelper.Tools.Ppt
{
    public sealed class PptColorSpec
    {
        public static readonly PptColorSpec Auto = new(PptColorKind.Auto, null, null, 0);

        public static readonly PptColorSpec None = new(PptColorKind.None, null, null, 0);

        private PptColorSpec(PptColorKind kind, string hex, string scheme, double percent)
        {
            Kind = kind;
            Hex = hex;
            Scheme = scheme;
            Percent = percent;
        }

        public PptColorKind Kind { get; }

        public string Hex { get; }

        public string Scheme { get; }

        public double Percent { get; }

        public bool IsHex => Kind == PptColorKind.Hex;

        public bool IsScheme => Kind == PptColorKind.Scheme;

        public bool IsAuto => Kind == PptColorKind.Auto;

        public bool IsNone => Kind == PptColorKind.None;

        public bool HasColor => Kind is PptColorKind.Hex or PptColorKind.Scheme;

        public static PptColorSpec FromHex(string hex, double percent = 0) =>
            new(PptColorKind.Hex, hex.ToUpperInvariant(), null, percent);

        public static PptColorSpec FromScheme(string scheme, double percent = 0) =>
            new(PptColorKind.Scheme, null, scheme, percent);

        public override string ToString()
        {
            switch (Kind)
            {
                case PptColorKind.Auto:
                    return "auto";
                case PptColorKind.None:
                    return "none";
            }

            var name = Kind == PptColorKind.Hex ? "#" + Hex : Scheme;
            if (Percent == 0)
                return name;

            var sign = Percent > 0 ? "+" : "-";
            return name + sign + Math.Abs(Percent).ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
