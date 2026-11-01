using System.Collections.Generic;
using System.Linq;
using DocuHelper.Tools;

namespace DocuHelper.Agents
{
    public static class AskCells
    {
        public const int MaxCells = 10;

        public static IEnumerable<string> Parse(ToolInput args) => args.GetStringList("cells", false)
            .Where(cell => !string.IsNullOrWhiteSpace(cell))
            .Select(cell => cell.Trim())
            .Take(MaxCells);
    }
}
