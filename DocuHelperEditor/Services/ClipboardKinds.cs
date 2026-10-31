using System;

namespace DocuHelperEditor.Services
{
    [Flags]
    public enum ClipboardKinds
    {
        None = 0,
        Text = 1,
        Html = 2,
        Image = 4,
        All = Text | Html | Image,
    }
}
