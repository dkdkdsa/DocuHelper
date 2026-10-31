using System.Collections.Generic;

namespace DocuHelper.Tools.Word
{
    public sealed record WordColorScheme(string Key, string Label, string Dark1, string Light1, string Dark2, string Light2, IReadOnlyList<string> Accents, string Hyperlink, string FollowedHyperlink);
}
