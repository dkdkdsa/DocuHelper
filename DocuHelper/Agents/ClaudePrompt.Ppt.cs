namespace DocuHelper.Agents
{
    public static partial class ClaudePrompt
    {
        internal const string PptRules = """
            Keep slide text short enough to fit; check_overflow helps.
            Call read_slides before slide-numbered edits: slide numbers shift after add_slide and after arrange_slides duplicate, delete or move, so use the numbers in each result. Restyle a slide with set_slide_layout (a standard kind or an exact layout name from read_slides); arrange_slides move uses slide numbers as they are before the move, and to is the slide to land after (0 = first).
            Use format_slide_paragraphs for bullets, numbering, indent level, spacing, alignment and vertical anchor or autofit, and never type bullet characters into set_slide_text lines. set_font and set_slide_link accept a range (paragraph, start, end: 0-based characters, end exclusive) to touch part of one paragraph; table shapes take set_font on all cells at once.
            Shape tools take shape ids from read_slides and cm from the slide's top-left corner; a shape inside a group must be ungrouped (arrange_slide_shapes ungroup) before it is moved or arranged on its own. add_slide_diagram draws grouped plain shapes, not SmartArt, with 2-8 short items (hierarchy 2-7, matrix exactly 4): leave x, y, w and h out to fill the empty content placeholder, and use add_slide_shape with place master for logos or bars that belong on every slide. format_slide_shape cannot change a shape's geometry type, flip or crop (delete and re-add instead); text inside shapes is changed with set_font or format_slide_paragraphs.
            add_slide_picture takes the picture as attachment (the app injects the bytes), path (a file in the deck folder) or image_base64, plus a short Korean alt_text; leave x, y, w and h out to fill the free area or the empty content placeholder, give only w or only h to keep proportions, and replace swaps the picture of an existing picture shape.
            edit_slide_table create takes data (first row is the header) or rows and columns; edit later with set_cells, insert_rows, insert_columns, delete_rows, delete_columns, merge, unmerge, style and format (1-based numbers); keep cell text short and use set_font for sizes; set_cells outside the table is refused, so insert rows or columns first.
            edit_slide_chart create needs chart_type, categories and series with one value per category (null for gaps); never invent numbers. An update with only title, legend or data_labels keeps the chart as it is; changing chart_type, categories or series rebuilds the chart and replaces custom formatting; pie and doughnut use the first series only.
            set_slide_background sets a solid, gradient, picture or inherited background on the given slides, or on the slide masters with slides "all"; set_slide_size changes the deck size and scales everything (ensure_fit or maximize); set_deck_theme applies a theme, colors, fonts or a dark background style; set_slide_footer shows or hides footer text, slide number and date, using the layout placeholders (a layout without them is skipped and reported).
            """;
    }
}
