using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Dash.Logic;

namespace DocuHelper.Dash.Elements
{
    public sealed class RepeatElement : Element
    {
        public string Data { get; set; }

        public string Direction { get; set; }

        public int? Max { get; set; }

        public string Empty { get; set; }

        public DashAction OnClick { get; set; }

        public List<Element> Template { get; set; } = new();

        public override IEnumerable<Element> Nested() => Template ?? new List<Element>();

        public override IEnumerable<string> Uses() => With(new[] { Data }.Concat(OnClick?.Uses() ?? Array.Empty<string>()));

        public override void Validate(DashCheck check)
        {
            base.Validate(check);
            check.RequireTable(this, Data);
            check.PushItem(Data);
            foreach (var element in Nested())
                element?.Validate(check);

            OnClick?.Validate(check, this);
            check.PopItem();
        }
    }
}
