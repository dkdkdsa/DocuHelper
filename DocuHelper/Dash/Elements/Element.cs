using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Elements
{
    public abstract class Element
    {
        [JsonPropertyOrder(-10)]
        public string Id { get; set; }

        [JsonPropertyOrder(-9)]
        public string Name { get; set; }

        [JsonPropertyOrder(-8)]
        public List<string> Classes { get; set; }

        [JsonPropertyOrder(-7)]
        public DashStyle Style { get; set; }

        [JsonPropertyOrder(-6)]
        public string When { get; set; }

        [JsonPropertyOrder(-5)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Hidden { get; set; }

        [JsonPropertyOrder(-4)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Locked { get; set; }

        [JsonIgnore]
        public string Kind => DashKinds.ElementName(GetType());

        public virtual IEnumerable<Element> Nested() => Array.Empty<Element>();

        public virtual IEnumerable<string> Uses()
        {
            yield break;
        }

        public virtual void Validate(DashCheck check)
        {
        }

        public virtual ElementResult Resolve(DashScope scope) => scope.Base(this, new ElementResult());
    }
}
