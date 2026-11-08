using System.Linq;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Elements
{
    public sealed class PopupElement : ContainerElement
    {
        public string Title { get; set; }

        public override void Validate(DashCheck check)
        {
            foreach (var name in DashNames.InExpression(When))
                check.RequireName(this, name);

            OnClick?.Validate(check, this);
            check.PushItem(null);
            foreach (var child in Nested())
                child?.Validate(check);

            check.PopItem();
        }

        public override ElementResult Resolve(DashScope scope)
        {
            var result = scope.Base(this, new PopupResult());
            var open = scope.Runtime.State.IsOpen(Id);
            result.Open = open;
            result.Visible = result.Visible && open;
            var inner = scope.ForValues(scope.Runtime.State.PopupItem(Id));
            result.Title = inner.FillText(Title);
            foreach (var child in Nested().Where(child => child != null))
                result.Children.Add(child.Resolve(inner));

            return result;
        }
    }
}
