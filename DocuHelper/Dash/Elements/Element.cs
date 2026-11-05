using System;
using System.Collections.Generic;
using System.Linq;
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

        [JsonPropertyOrder(-3)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Logic.DashAction OnClick { get; set; }

        [JsonIgnore]
        public string Kind => DashKinds.ElementName(GetType());

        public virtual IEnumerable<Element> Nested() => Array.Empty<Element>();

        public virtual IEnumerable<string> Uses() => With(null);

        public virtual void Validate(DashCheck check)
        {
            foreach (var name in DashNames.InExpression(When))
                check.RequireName(this, name);

            OnClick?.Validate(check, this);
        }

        public virtual ElementResult Resolve(DashScope scope) => scope.Base(this, new ElementResult());

        protected IEnumerable<string> With(IEnumerable<string> own) => DashNames.InExpression(When)
            .Concat(own ?? Array.Empty<string>())
            .Concat(OnClick?.Uses() ?? Array.Empty<string>())
            .Where(name => !string.IsNullOrEmpty(name))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
