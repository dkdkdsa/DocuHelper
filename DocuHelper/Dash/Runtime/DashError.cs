namespace DocuHelper.Dash.Runtime
{
    public sealed class DashError
    {
        public DashError(string code)
        {
            Code = code;
        }

        public string Code { get; }

        public override string ToString() => Code;
    }
}
