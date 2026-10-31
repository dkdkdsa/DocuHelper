using System.Linq;
using DocuHelper.Importers.Hwp;

namespace DocuHelper.Agents
{
    public static partial class ClaudePrompt
    {
        internal static string HwpRules => string.Join("\n", new[]
        {
            "Hancom 한글 files (.hwp, .hwpx) open as 한글 files. You read and change them with the same tools as Word files, and they are saved back as 한글 files.",
            HwpLimits.PromptText(),
            "New 한글 files use 함초롬바탕 10pt with 160 % line spacing and A4 margins of 30 mm left and right; do not set fonts or line spacing unless the user asks.",
            "In 한글 files set_document_design theme and colors are not available, and table borders, row heights and set_style are not available (shading, alignment, text format, widths and structure are).",
        }.Where(line => line != null));
    }
}
