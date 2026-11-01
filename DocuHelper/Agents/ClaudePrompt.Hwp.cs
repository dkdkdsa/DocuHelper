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
            "In 한글 files set_document_design theme and colors are not available. edit_table keeps cell shading, borders, row heights, vertical alignment and the header row, but set_style keeps only header_row; presets, banded or total rows, table alignment, width and autofit are not available.",
            "In 한글 files format_paragraph keeps the right indent, keep with next, keep lines, widow control, exact and at-least line spacing, divide alignment, and paragraph borders and shading. page_break_before, outline_level and suppress_line_numbers are refused.",
            "In 한글 files manage_toc is not available. Tab stops with dot, hyphen or underline leaders in an opened file are kept when it is saved, but no tool adds them.",
            "In 한글 files edit_object inserts pictures, which are saved inline; floating pictures are saved inline too. Shapes and charts are not available.",
            "In 한글 files set_page_control hides the header, footer, page number, border or background of one page, or restarts page numbering at a paragraph.",
            "In 한글 files set_header_footer keeps the text, the page number (plain, dash or brackets; decimal, roman or letter numbers; a start number), the first-page option and odd and even headers. Watermarks, the date, time, file, title, author and page-count fields, page_x_of_y, title_page and first-page-only text are refused.",
        }.Where(line => line != null));
    }
}
