using DocuHelper.Dash.Elements;

namespace DocuHelper.Dash.Runtime
{
    public sealed class DashScope
    {
        public DashScope(DashDocument document)
        {
            Document = document;
        }

        public DashDocument Document { get; }

        public string Suffix { get; init; }

        public T Base<T>(Element element, T result) where T : ElementResult
        {
            result.Source = element;
            result.Key = Suffix == null ? element.Id : element.Id + "#" + Suffix;
            result.Style = StyleOf(element);
            result.Visible = !element.Hidden && IsVisible(element);
            return result;
        }

        public DashStyle StyleOf(Element element)
        {
            var style = new DashStyle();
            if (element.Classes != null && Document?.Styles != null)
            {
                foreach (var name in element.Classes)
                {
                    if (name != null && Document.Styles.TryGetValue(name, out var rule))
                        style = style.Merge(rule);
                }
            }

            return style.Merge(element.Style);
        }

        public bool IsVisible(Element element) => true;
    }
}
