using System.Collections.Generic;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Elements
{
    public abstract class ContainerElement : Element
    {
        public List<Element> Children { get; set; } = new();

        public override IEnumerable<Element> Nested() => Children ?? new List<Element>();

        public override ElementResult Resolve(DashScope scope)
        {
            var result = scope.Base(this, new ElementResult());
            foreach (var child in Nested())
                result.Children.Add(child.Resolve(scope));

            return result;
        }
    }
}
