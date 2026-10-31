using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DocuHelper.Docus;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class ReadDocumentTool : WordTool
    {
        private const int Budget = 8000;
        private const int MaxLimit = 500;
        private const int AllLimit = 10;
        private const int TextLimit = 120;
        private const int CommentLimit = 300;

        private static readonly string[] s_kinds =
        {
            "outline", "page_setup", "headers_footers", "styles", "comments", "revisions", "notes", "bookmarks", "fields", "objects", "captions", "stats", "properties",
        };

        private static readonly string[] s_hwpKinds = { "outline", "page_setup", "styles", "stats", "properties" };

        public override string Name => "read_document";

        public override string Description =>
            "Read parts of the Word document that read_paragraphs does not show. what picks one: outline (headings with level and page), page_setup (per section: paper, margins, columns, "
            + "line numbers, which headers/footers apply), headers_footers (text, fields as {PAGE}), styles (use counts and look), comments, revisions (tracked changes), notes (footnotes and endnotes), "
            + "bookmarks, fields, objects (pictures, shapes, text boxes, charts), captions, stats (pages estimate, words, characters, paragraphs, lines), properties (document info and settings), or all. "
            + "Items carry the ids other tools take: paragraph ids, c:3 comments, r:4 revisions, o:12 objects, fn:2 / en:1 notes. section limits page_setup and headers_footers to one section; "
            + "start and limit page through long lists. Page numbers are an approximate layout (font metrics, line and paragraph spacing, table rows, pictures, headers and footers), not Word's exact pagination. "
            + "Output is capped at about 8000 characters and says how to continue.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "what": {
                  "type": "string",
                  "enum": ["outline", "page_setup", "headers_footers", "styles", "comments", "revisions", "notes", "bookmarks", "fields", "objects", "captions", "stats", "properties", "all"]
                },
                "section": {"type": "integer", "minimum": 1, "description": "page_setup, headers_footers: one section only."},
                "start": {"type": "integer", "minimum": 1, "description": "1-based index of the first item to list. Default 1."},
                "limit": {"type": "integer", "minimum": 1, "maximum": 500, "description": "At most this many items per list. Default 500 (all: 10)."}
              },
              "required": ["what"]
            }
            """;

        public override bool IsReadOnly => true;

        protected override WordHwpSupport Hwp => WordHwpSupport.Partial;

        protected override ToolResult Run(WordDocu docu, ToolInput input)
        {
            var what = (input.GetString("what") ?? string.Empty).Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
            if (what != "all" && !s_kinds.Contains(what))
                throw new ToolInputException("'what' 값은 outline, page_setup, headers_footers, styles, comments, revisions, notes, bookmarks, fields, objects, captions, stats, properties, all 중 하나여야 해요.");

            var section = input.GetOptionalInt("section");
            var start = Math.Max(1, input.GetOptionalInt("start") ?? 1);
            var limit = input.GetOptionalInt("limit");
            if (limit != null)
                limit = Math.Clamp(limit.Value, 1, MaxLimit);

            var hwp = docu is HwpDocu;
            if (section != null && (what == "page_setup" || what == "headers_footers" || what == "all"))
            {
                var count = WordParts.Sections(docu).Count;
                if (section < 1 || section > count)
                    throw new ToolInputException($"이 문서는 구역이 {count}개예요. section은 1~{count} 사이로 넣어 주세요.");
            }

            if (hwp && what != "all" && !s_hwpKinds.Contains(what))
                return ToolResult.Ok($"{what} is not read from 한글 files, so there is nothing to list.", "한글에는 없음");

            var scan = WordScan.Read(docu.Document);
            if (what == "all")
                return ReadAll(docu, scan, section, limit ?? AllLimit, hwp);

            var result = Build(what, docu, scan, section);
            return Page(result, what, start, limit ?? MaxLimit);
        }

        private static ToolResult ReadAll(WordDocu docu, WordScan scan, int? section, int limit, bool hwp)
        {
            var text = new StringBuilder();
            var empty = new List<string>();
            var skipped = new List<string>();
            var omitted = new List<string>();
            foreach (var what in s_kinds)
            {
                if (hwp && !s_hwpKinds.Contains(what))
                {
                    skipped.Add(what);
                    continue;
                }

                var result = Build(what, docu, scan, section);
                if (result.Items.Count == 0 && what != "stats" && what != "properties")
                {
                    empty.Add(what);
                    continue;
                }

                var block = Render(result, what, 1, limit, Budget - text.Length);
                if (block == null)
                {
                    omitted.Add(what);
                    continue;
                }

                text.Append(block).Append('\n');
            }

            if (empty.Count > 0)
                text.Append("Nothing found for: ").Append(string.Join(", ", empty)).Append(".\n");

            if (skipped.Count > 0)
                text.Append("Not read from 한글 files: ").Append(string.Join(", ", skipped)).Append(".\n");

            if (omitted.Count > 0)
                text.Append("Output cap reached. Call read_document with what=<name> for: ").Append(string.Join(", ", omitted)).Append(".\n");

            return ToolResult.Ok(text.ToString().TrimEnd(), "문서 전체 정보");
        }

        private static ToolResult Page(Listing result, string what, int start, int limit)
        {
            if (result.Items.Count > 0 && start > result.Items.Count)
                throw new ToolInputException($"목록에 항목이 {result.Items.Count}개 있어요. start는 1~{result.Items.Count} 사이로 넣어 주세요.");

            var text = Render(result, what, start, limit, Budget) ?? result.Title;
            return ToolResult.Ok(text.TrimEnd(), result.Detail);
        }

        private static string Render(Listing result, string what, int start, int limit, int budget)
        {
            var text = new StringBuilder();
            text.Append(result.Title).Append('\n');
            if (budget < text.Length + 200 && result.Items.Count > 0)
                return null;

            var shown = 0;
            var index = start - 1;
            for (; index < result.Items.Count && shown < limit; index++)
            {
                var item = result.Items[index] + "\n";
                if (shown > 0 && text.Length + item.Length > budget)
                    break;

                text.Append(item);
                shown++;
            }

            if (index < result.Items.Count)
                text.Append(CultureInfo.InvariantCulture, $"More items follow ({result.Items.Count - index} left). Call read_document with what={what} and start={index + 1} to continue.\n");

            return text.ToString();
        }

        private static Listing Build(string what, WordDocu docu, WordScan scan, int? section) => what switch
        {
            "outline" => Outline(scan),
            "page_setup" => PageSetup(docu, scan, section),
            "headers_footers" => HeadersFooters(docu, section),
            "styles" => Styles(docu, scan),
            "comments" => Comments(docu, scan),
            "revisions" => Revisions(docu, scan),
            "notes" => Notes(docu, scan),
            "bookmarks" => Bookmarks(docu, scan),
            "fields" => Fields(docu, scan),
            "objects" => Objects(docu, scan),
            "captions" => Captions(docu, scan),
            "stats" => Stats(docu, scan),
            _ => Properties(docu),
        };

        private static Listing Outline(WordScan scan)
        {
            var headings = WordOutline.Headings(scan);
            var listing = new Listing($"Outline: {headings.Count} heading(s); pages are estimates", $"제목 {headings.Count}개");
            foreach (var heading in headings)
            {
                var indent = new string(' ', Math.Max(0, heading.Level - 1) * 2);
                listing.Items.Add($"{indent}{heading.Index}. [{heading.Id ?? "no id"}] {WordOutline.Label(heading)}, p{heading.Page}: {Line(heading.Text, TextLimit)}");
            }

            return listing;
        }

        private static Listing PageSetup(WordDocu docu, WordScan scan, int? only)
        {
            var sections = WordInventory.Sections(docu, scan);
            var stories = WordInventory.HeadersFooters(docu);
            var oddEven = docu.Document.MainDocumentPart?.DocumentSettingsPart?.Settings?.GetFirstChild<EvenAndOddHeaders>();
            var listing = new Listing(
                $"Page setup: {sections.Count} section(s); different odd and even pages: {(WordStyles.IsOn(oddEven) ? "on" : "off")}; page breaks are estimates",
                $"구역 {sections.Count}개");
            foreach (var info in sections.Where(s => only == null || s.Number == only))
            {
                var text = new StringBuilder();
                text.Append(CultureInfo.InvariantCulture, $"Section {info.Number} [{info.Anchor}]: blocks {info.FirstBlock}-{info.LastBlock}, p{info.FirstPage}");
                if (info.LastPage != info.FirstPage)
                    text.Append(CultureInfo.InvariantCulture, $"-{info.LastPage}");

                text.Append(CultureInfo.InvariantCulture, $", starts as {info.BreakType}\n");
                text.Append("  paper ").Append(info.Paper ?? "custom").Append(info.Landscape ? " landscape " : " portrait ");
                text.Append(Size(info.WidthCm)).Append(" x ").Append(Size(info.HeightCm)).Append(" cm; margins cm: ");
                text.Append(CultureInfo.InvariantCulture, $"top {Size(info.TopCm)}, bottom {Size(info.BottomCm)}, left {Size(info.LeftCm)}, right {Size(info.RightCm)}, header {Size(info.HeaderCm)}, footer {Size(info.FooterCm)}, gutter {Size(info.GutterCm)}\n");
                text.Append(CultureInfo.InvariantCulture, $"  columns {info.Columns}");
                if (info.Columns > 1)
                    text.Append(CultureInfo.InvariantCulture, $", space {Size(info.ColumnSpaceCm)} cm, {(info.ColumnsEqual ? "equal width" : "custom widths")}{(info.ColumnLine ? ", line between" : string.Empty)}");

                text.Append('\n');
                var extras = new List<string>();
                if (info.LineNumbers != null)
                    extras.Add("line numbers " + info.LineNumbers);

                if (info.PageNumberFormat != null || info.PageNumberStart != null)
                    extras.Add("page numbers " + (info.PageNumberFormat ?? "decimal") + (info.PageNumberStart == null ? string.Empty : " from " + info.PageNumberStart.Value.ToString(CultureInfo.InvariantCulture)));

                if (info.VerticalAlign != null)
                    extras.Add("vertical align " + info.VerticalAlign);

                if (info.TitlePage)
                    extras.Add("different first page");

                if (info.PageBorders)
                    extras.Add("page borders");

                var parts = stories.Where(s => s.Section == info.Number && !s.IsEmpty).Select(s => $"{s.Kind} {s.Variant}{(s.InheritedFrom == null ? string.Empty : " (from section " + s.InheritedFrom.Value.ToString(CultureInfo.InvariantCulture) + ")")}").ToList();
                if (parts.Count > 0)
                    extras.Add("applies: " + string.Join(", ", parts));

                if (extras.Count > 0)
                    text.Append("  ").Append(string.Join("; ", extras)).Append('\n');

                listing.Items.Add(text.ToString().TrimEnd());
            }

            return listing;
        }

        private static Listing HeadersFooters(WordDocu docu, int? section)
        {
            var stories = WordInventory.HeadersFooters(docu, section);
            var listing = new Listing($"Headers and footers: {stories.Count} part(s)", $"머리글·바닥글 {stories.Count}개");
            foreach (var story in stories)
            {
                var text = new StringBuilder();
                text.Append(CultureInfo.InvariantCulture, $"Section {story.Section} {story.Kind} {story.Variant} [{story.Anchor}]: ");
                if (story.InheritedFrom != null)
                {
                    text.Append(CultureInfo.InvariantCulture, $"same as section {story.InheritedFrom.Value}");
                }
                else
                {
                    text.Append(story.Text.Length == 0 ? "(empty)" : Line(story.Text, TextLimit * 2));
                    if (story.Watermark != null)
                        text.Append(CultureInfo.InvariantCulture, $" watermark \"{Line(story.Watermark, 40)}\"");
                }

                listing.Items.Add(text.ToString());
            }

            return listing;
        }

        private static Listing Styles(WordDocu docu, WordScan scan)
        {
            var styles = WordInventory.Styles(docu, scan);
            var listing = new Listing($"Styles: {styles.Count}", $"스타일 {styles.Count}개");
            foreach (var style in styles)
            {
                var text = new StringBuilder();
                text.Append(CultureInfo.InvariantCulture, $"[{style.Id}] \"{style.Name}\" {style.Type}");
                if (style.IsDefault)
                    text.Append(" (default)");

                if (style.IsCustom)
                    text.Append(" (custom)");

                if (style.BasedOn != null)
                    text.Append(CultureInfo.InvariantCulture, $", based on \"{style.BasedOn}\"");

                text.Append(CultureInfo.InvariantCulture, $", used {style.Users}x");
                if (style.Summary.Length > 0)
                    text.Append(": ").Append(style.Summary);

                listing.Items.Add(text.ToString());
            }

            return listing;
        }

        private static Listing Comments(WordDocu docu, WordScan scan)
        {
            var comments = WordInventory.Comments(docu, scan);
            var done = comments.Count(c => c.Done);
            var listing = new Listing($"Comments: {comments.Count}{(done > 0 ? $" ({done} resolved)" : string.Empty)}", $"메모 {comments.Count}개");
            foreach (var comment in comments)
            {
                var text = new StringBuilder();
                text.Append(CultureInfo.InvariantCulture, $"{comment.Ref} {comment.Author ?? "unknown"}");
                if (comment.Date != null)
                    text.Append(CultureInfo.InvariantCulture, $" ({comment.Date.Value:yyyy-MM-dd HH:mm})");

                if (comment.ParagraphId != null)
                    text.Append(CultureInfo.InvariantCulture, $" on [{comment.ParagraphId}]{Page(comment.Page)}");

                if (comment.Anchored.Length > 0)
                    text.Append(CultureInfo.InvariantCulture, $" \"{Line(comment.Anchored, 60)}\"");

                text.Append(" -> ").Append(Line(comment.Text, CommentLimit));
                if (comment.ParentId != null)
                    text.Append(CultureInfo.InvariantCulture, $" [reply to c:{comment.ParentId}]");

                if (comment.Done)
                    text.Append(" [resolved]");

                listing.Items.Add(text.ToString());
            }

            return listing;
        }

        private static Listing Revisions(WordDocu docu, WordScan scan)
        {
            var revisions = WordInventory.Revisions(docu, scan);
            var listing = new Listing($"Tracked changes: {revisions.Count}; tracking is {(WordRevisions.Tracking(docu) ? "on" : "off")}", $"변경 {revisions.Count}개");
            foreach (var revision in revisions)
            {
                var text = new StringBuilder();
                text.Append(CultureInfo.InvariantCulture, $"{(revision.Id.Length == 0 ? "r:-" : revision.Ref)} {revision.Kind}");
                if (revision.Detail != null)
                    text.Append(CultureInfo.InvariantCulture, $" ({revision.Detail})");

                text.Append(CultureInfo.InvariantCulture, $" by {revision.Author ?? "unknown"}");
                if (revision.Date != null)
                    text.Append(CultureInfo.InvariantCulture, $" ({revision.Date.Value:yyyy-MM-dd HH:mm})");

                if (revision.Where != "body")
                    text.Append(" in ").Append(revision.Where);
                else if (revision.ParagraphId != null)
                    text.Append(CultureInfo.InvariantCulture, $" in [{revision.ParagraphId}]{Page(revision.Page)}");

                if (revision.Text.Length > 0)
                    text.Append(CultureInfo.InvariantCulture, $": \"{Line(revision.Text, TextLimit)}\"");

                listing.Items.Add(text.ToString());
            }

            return listing;
        }

        private static Listing Notes(WordDocu docu, WordScan scan)
        {
            var notes = WordInventory.Notes(docu, scan);
            var footnotes = notes.Count(n => !n.IsEndnote);
            var listing = new Listing($"Notes: {footnotes} footnote(s), {notes.Count - footnotes} endnote(s)", $"각주 {footnotes}개, 미주 {notes.Count - footnotes}개");
            foreach (var note in notes)
            {
                var text = new StringBuilder();
                text.Append(note.Ref);
                if (note.Number > 0)
                    text.Append(CultureInfo.InvariantCulture, $" (number {note.Number})");

                if (note.ParagraphId != null)
                    text.Append(CultureInfo.InvariantCulture, $" on [{note.ParagraphId}]{Page(note.Page)}");
                else
                    text.Append(" (not referenced in the text)");

                text.Append(": ").Append(Line(note.Text, TextLimit * 2));
                listing.Items.Add(text.ToString());
            }

            return listing;
        }

        private static Listing Bookmarks(WordDocu docu, WordScan scan)
        {
            var all = WordInventory.Bookmarks(docu, scan);
            var visible = all.Where(b => !b.Hidden).ToList();
            var hidden = all.Count - visible.Count;
            var listing = new Listing($"Bookmarks: {visible.Count}{(hidden > 0 ? $" (+{hidden} hidden, names starting with _)" : string.Empty)}", $"책갈피 {visible.Count}개");
            foreach (var bookmark in visible)
            {
                var text = new StringBuilder();
                text.Append(CultureInfo.InvariantCulture, $"{bookmark.Name}");
                if (bookmark.ParagraphId != null)
                    text.Append(CultureInfo.InvariantCulture, $" in [{bookmark.ParagraphId}]{Page(bookmark.Page)}");

                if (bookmark.Text.Length > 0)
                    text.Append(CultureInfo.InvariantCulture, $": \"{Line(bookmark.Text, TextLimit)}\"");
                else
                    text.Append(": (no text)");

                listing.Items.Add(text.ToString());
            }

            return listing;
        }

        private static Listing Fields(WordDocu docu, WordScan scan)
        {
            var fields = WordInventory.Fields(docu, scan);
            var listing = new Listing($"Fields: {fields.Count}", $"필드 {fields.Count}개");
            foreach (var field in fields)
            {
                var text = new StringBuilder();
                text.Append(field.Name.Length == 0 ? "?" : field.Name);
                var rest = field.Instruction.Trim();
                if (rest.StartsWith(field.Name, StringComparison.OrdinalIgnoreCase))
                    rest = rest.Substring(field.Name.Length).Trim();

                if (rest.Length > 0)
                    text.Append(' ').Append(Line(rest, 80));

                text.Append(CultureInfo.InvariantCulture, $" => \"{Line(field.Result, 60)}\"");
                if (field.ParagraphId != null)
                    text.Append(CultureInfo.InvariantCulture, $" in [{field.ParagraphId}]{Page(field.Page)}");

                if (field.Nested > 0)
                    text.Append(CultureInfo.InvariantCulture, $" (+{field.Nested} nested fields not listed)");

                listing.Items.Add(text.ToString());
            }

            return listing;
        }

        private static Listing Objects(WordDocu docu, WordScan scan)
        {
            var objects = WordInventory.Objects(docu, scan);
            var listing = new Listing($"Objects: {objects.Count}", $"개체 {objects.Count}개");
            foreach (var item in objects)
            {
                var text = new StringBuilder();
                text.Append(item.Ref ?? "-").Append(' ').Append(item.Kind);
                if (!string.IsNullOrEmpty(item.Name))
                    text.Append(CultureInfo.InvariantCulture, $" \"{Line(item.Name, 40)}\"");

                if (item.WidthCm != null && item.HeightCm != null)
                    text.Append(CultureInfo.InvariantCulture, $" {Size(item.WidthCm)} x {Size(item.HeightCm)} cm");

                if (item.Wrap != null)
                    text.Append(", ").Append(item.Wrap);

                if (item.ParagraphId != null)
                    text.Append(CultureInfo.InvariantCulture, $", in [{item.ParagraphId}]{Page(item.Page)}");

                if (item.Alt != null)
                    text.Append(CultureInfo.InvariantCulture, $", alt \"{Line(item.Alt, 60)}\"");

                if (item.Id == null)
                    text.Append(" (cannot be edited with edit_object)");

                listing.Items.Add(text.ToString());
            }

            return listing;
        }

        private static Listing Captions(WordDocu docu, WordScan scan)
        {
            var captions = WordInventory.Captions(docu, scan);
            var listing = new Listing($"Captions: {captions.Count}", $"캡션 {captions.Count}개");
            foreach (var caption in captions)
            {
                var label = (caption.Label + " " + caption.Number).Trim();
                listing.Items.Add($"[{caption.ParagraphId ?? "no id"}]{Page(caption.Page)} {(label.Length == 0 ? "caption" : label)}: {Line(caption.Text, TextLimit)}");
            }

            return listing;
        }

        private static Listing Stats(WordDocu docu, WordScan scan)
        {
            var counts = WordStats.Count(docu, true, scan);
            var listing = new Listing("Statistics (text boxes, footnotes and endnotes included)", $"단어 {counts.Words}개, 약 {counts.Pages}쪽");
            listing.Items.Add($"Pages (approximate layout): {counts.Pages}");
            listing.Items.Add($"Words: {counts.Words}");
            listing.Items.Add($"Characters (with spaces): {counts.Characters}");
            listing.Items.Add($"Characters (no spaces): {counts.CharactersNoSpaces}");
            listing.Items.Add($"Paragraphs: {counts.Paragraphs}");
            listing.Items.Add($"Lines (approx.): {counts.Lines}");
            listing.Items.Add($"Tables: {counts.Tables}");
            listing.Items.Add($"Objects: {counts.Objects}");
            return listing;
        }

        private static Listing Properties(WordDocu docu)
        {
            var properties = WordInventory.Properties(docu);
            var listing = new Listing("Document properties", "문서 정보");
            foreach (var property in properties)
                listing.Items.Add($"{property.Group}.{property.Name}: {Line(property.Value, TextLimit * 2)}");

            return listing;
        }

        private static string Page(int page) => page > 0 ? ", p" + page.ToString(CultureInfo.InvariantCulture) : string.Empty;

        private static string Size(double? value) => value == null ? "-" : value.Value.ToString("0.##", CultureInfo.InvariantCulture);

        private static string Line(string text, int limit)
        {
            var value = (text ?? string.Empty).Replace("\r", string.Empty).Replace("\n", "\\n").Replace("\t", "\\t");
            return value.Length <= limit ? value : value.Substring(0, limit) + $"… (+{value.Length - limit} chars)";
        }

        private sealed class Listing
        {
            public Listing(string title, string detail)
            {
                Title = title;
                Detail = detail;
            }

            public string Title { get; }

            public string Detail { get; }

            public List<string> Items { get; } = new();
        }
    }
}
