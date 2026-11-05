using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Dash.Logic;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Elements
{
    public sealed class RepeatElement : Element
    {
        public string Data { get; set; }

        public string Direction { get; set; }

        public int? Max { get; set; }

        public string Empty { get; set; }

        public List<Element> Template { get; set; } = new();

        public override IEnumerable<Element> Nested() => Template ?? new List<Element>();

        public override IEnumerable<string> Uses() => With(new[] { Data });

        public override void Validate(DashCheck check)
        {
            foreach (var name in DashNames.InExpression(When))
                check.RequireName(this, name);

            check.RequireTable(this, Data);
            check.PushItem(Data);
            foreach (var element in Nested())
                element?.Validate(check);

            OnClick?.Validate(check, this);
            check.PopItem();
        }

        public override ElementResult Resolve(DashScope scope)
        {
            var result = scope.Base(this, new RepeatResult());
            result.Direction = Direction ?? "column";
            result.EmptyText = scope.FillText(Empty);

            var table = scope.Table(Data);
            if (table == null)
            {
                result.Problem = $"‘{Data}’ 표를 가져오지 못했어요.";
                return result;
            }

            var count = Max.HasValue ? Math.Min(table.Rows.Count, Math.Max(0, Max.Value)) : table.Rows.Count;
            for (var i = 0; i < count; i++)
            {
                var rowScope = scope.ForItem(table, i);
                var row = rowScope.Base(this, new RepeatRowResult { Row = i, OnClick = OnClick, Item = rowScope.ItemValues() });
                row.Visible = true;
                row.Style = new DashStyle();
                foreach (var element in Nested().Where(item => item != null))
                    row.Children.Add(element.Resolve(rowScope));

                result.Children.Add(row);
            }

            return result;
        }
    }
}
