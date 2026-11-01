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
            "In 한글 files manage_toc inserts, rebuilds and removes a table of contents built from heading styles, with dot, hyphen or underline leaders. Its page numbers are estimates, and the user should refresh the fields in 한글 for exact numbers. Figure and table lists and indexes are not available there.",
            "In 한글 files hyperlinks, bookmarks, footnotes and endnotes are kept, and insert_reference adds them; cross references and captions are not available there. Click-here fields are kept, and edit_form_field lists, inserts, fills, edits and deletes them.",
            "In 한글 files paragraph style names are kept (바탕글, 개요 1 to 7, 본문, 캡션, 쪽 번호, 머리말, 각주, 미주 and styles the file defines). A style's formatting is saved with each paragraph, so changing a style does not change text that already uses it.",
            "In 한글 files set_list makes bulleted and numbered lists that are saved as 한글 numbering, so the numbers renumber when items move. Numbers typed into outline headings (개요 1 to 7) are plain text and do not renumber.",
            "In 한글 files edit_object inserts pictures, which are saved inline; floating pictures are saved inline too. Shapes and charts are not available.",
            "In 한글 files equations, text art, ruby text and overlapped characters from an opened file are saved unchanged. If you change the text of a text art, ruby or overlap, that object is saved as the new text. Shapes, charts and embedded objects are not kept: their words stay, and the drawing is dropped on save.",
            "In 한글 files set_page_control hides the header, footer, page number, border or background of one page, or restarts page numbering at a paragraph.",
            "In 한글 files each section keeps its own page size, orientation, margins, columns, page borders, header, footer and page number start. set_page_setup can change one section (scope section, or from_here to start a new section), and insert_page adds next-page section breaks and column breaks. Continuous, even-page and odd-page section breaks are refused. A section break starts on a new page, and the page colour is kept for the whole document only.",
            "In 한글 files set_header_footer keeps the text, the page number (plain, dash or brackets; decimal, roman or letter numbers; a start number), the first-page option and odd and even headers. Watermarks, the date, time, file, title, author and page-count fields, page_x_of_y, title_page and first-page-only text are refused.",
        }.Where(line => line != null));
    }
}
