using System;

namespace DocuHelper.Dash.Runtime
{
    public sealed class DashException : Exception
    {
        public DashException(string message)
            : base(message)
        {
        }
    }
}
