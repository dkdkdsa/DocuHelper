using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public sealed class WordTransaction
    {
        private readonly List<(OpenXmlCompositeElement Root, List<OpenXmlElement> Children)> _saved = new();

        private WordTransaction()
        {
        }

        public static WordTransaction Begin(IEnumerable<OpenXmlElement> roots)
        {
            var transaction = new WordTransaction();
            foreach (var root in roots.Select(Narrow).OfType<OpenXmlCompositeElement>().Distinct())
                transaction._saved.Add((root, root.ChildElements.Select(c => c.CloneNode(true)).ToList()));

            return transaction;
        }

        public void Rollback()
        {
            foreach (var (root, children) in _saved)
            {
                root.RemoveAllChildren();
                foreach (var child in children)
                    root.AppendChild(child.CloneNode(true));
            }
        }

        private static OpenXmlElement Narrow(OpenXmlElement root) =>
            root is Document document && document.Body != null ? document.Body : root;

        public static OpenXmlElement RootOf(OpenXmlElement element) =>
            element.Ancestors<OpenXmlPartRootElement>().FirstOrDefault();
    }
}
