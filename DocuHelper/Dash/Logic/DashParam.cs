using System;
using System.Text.Json.Serialization;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Logic
{
    public abstract class DashParam
    {
        [JsonPropertyOrder(-10)]
        public string Name { get; set; }

        [JsonPropertyOrder(-9)]
        public string Label { get; set; }

        [JsonPropertyOrder(-8)]
        public string Format { get; set; }

        [JsonIgnore]
        public string Kind => DashKinds.ParamName(GetType());

        public virtual void Validate(DashCheck check)
        {
            if (string.IsNullOrWhiteSpace(Name))
                check.Fail("이름이 없는 조절값이 있어요.");
        }

        public abstract object InitialValue();

        public abstract object Normalize(object value);

        public virtual string Display(object value) => DashFormats.Display(value, Format);

        protected static double Snap(double value, double min, double max, double step)
        {
            var result = value;
            if (step > 0)
                result = min + Math.Round((result - min) / step) * step;

            if (max > min)
                result = Math.Clamp(result, min, max);

            return Math.Round(result, 10);
        }
    }
}
