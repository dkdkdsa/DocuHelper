using System.Collections.Generic;

namespace DocuHelper.Tools.Excel
{
    public sealed class FormulaNode
    {
        public FormulaNode(FormulaNodeKind kind)
        {
            Kind = kind;
        }

        public FormulaNodeKind Kind { get; }

        public double Number { get; set; }

        public string Value { get; set; }

        public FormulaReference Reference { get; set; }

        public List<FormulaNode> Children { get; } = new();

        public IEnumerable<FormulaNode> Descendants()
        {
            yield return this;
            foreach (var child in Children)
            {
                foreach (var node in child.Descendants())
                    yield return node;
            }
        }
    }
}
