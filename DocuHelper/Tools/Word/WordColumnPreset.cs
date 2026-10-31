using System.Collections.Generic;

namespace DocuHelper.Tools.Word
{
    public sealed record WordColumnPreset(string Key, int Count, double? SpaceCm, bool Equal, IReadOnlyList<int> Ratios);
}
