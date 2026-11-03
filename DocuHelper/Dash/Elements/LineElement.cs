using System.Text.Json.Serialization;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Elements
{
    public sealed class LineElement : Element
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool ArrowStart { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool ArrowEnd { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Flip { get; set; }

        public override ElementResult Resolve(DashScope scope)
        {
            var result = scope.Base(this, new LineResult());
            result.ArrowStart = ArrowStart;
            result.ArrowEnd = ArrowEnd;
            result.Flip = Flip;
            return result;
        }
    }
}
