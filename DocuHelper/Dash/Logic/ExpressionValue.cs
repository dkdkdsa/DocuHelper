using System.Collections.Generic;

namespace DocuHelper.Dash.Logic
{
    public sealed class ExpressionValue : DashValue
    {
        public string Expression { get; set; }

        public override IEnumerable<string> Uses() => DashNames.InExpression(Expression);

        public override void Validate(DashCheck check)
        {
            base.Validate(check);
            if (!DashNames.TryParse(Expression, out _))
            {
                check.Fail($"‘{Name}’ 값의 식을 읽을 수 없어요.");
                return;
            }

            foreach (var name in Uses())
                check.RequireName(Name, name);
        }
    }
}
