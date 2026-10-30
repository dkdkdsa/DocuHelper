using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocuHelper.Docus;
using DocuHelper.Tools.Word;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Mapping
{
    public class WordPaperMapper : IPaperMapper
    {
        private const int BylineLength = 80;
        private const int BylineLineLength = 50;
        private const int BylineSearch = 4;
        private const int CaptionLength = 40;

        private static readonly HashSet<string> s_footers = new(StringComparer.OrdinalIgnoreCase) { "합계", "총계", "계", "소계", "누계", "총합", "total", "sum" };

        public DocuType Type => DocuType.Word;

        public PaperModel Map(Docu docu, PaperModel previous)
        {
            var old = previous as WordPaperModel;
            if (docu is not WordDocu word || word.Document?.MainDocumentPart?.Document?.Body == null)
                return old ?? new WordPaperModel();

            var scan = WordScan.Read(word.Document);
            var changes = ChangeIds(docu);
            var earlier = old?.Blocks.Where(b => b?.Id != null).GroupBy(b => b.Id).ToDictionary(g => g.Key, g => g.First()) ?? new Dictionary<string, WordBlockModel>();
            var blocks = new List<WordBlockModel>(scan.Blocks.Count);
            var looks = new List<WordLook>(scan.Blocks.Count);
            foreach (var block in scan.Blocks)
            {
                var look = WordLook.Of(scan, block);
                var model = block.IsTable ? MapTable(scan, block) : MapParagraph(block, look);
                model.Id = block.Id == null ? $"b:{block.Index}" : block.Anchor;
                model.Page = block.Page;
                model.IsBold = look.Bold;
                model.IsItalic = look.Italic;
                model.IsUnderline = look.Underline;
                model.ChangeId = changes.TryGetValue(model.Id, out var changeId) ? changeId : null;
                model.IsEditedByHand = earlier.TryGetValue(model.Id, out var before) && before.IsEditedByHand && before.ChangeId == model.ChangeId;
                blocks.Add(model);
                looks.Add(look);
            }

            MarkBylines(scan, blocks, looks);

            var selected = old?.SelectedBlockId;
            return new WordPaperModel
            {
                Blocks = blocks,
                SelectedBlockId = selected != null && blocks.Any(b => b.Id == selected) ? selected : null,
            };
        }

        private static Dictionary<string, string> ChangeIds(Docu docu)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var change in docu.Changes)
            {
                foreach (var anchor in change.Anchors)
                {
                    if (!string.IsNullOrEmpty(anchor))
                        map[anchor] = change.Id;
                }
            }

            return map;
        }

        private static WordBlockModel MapParagraph(WordBlock block, WordLook look) => new()
        {
            Kind = block.Kind switch
            {
                WordKind.Title or WordKind.Heading1 => WordBlockKind.Heading1,
                WordKind.Heading2 => WordBlockKind.Heading2,
                _ => WordBlockKind.Body,
            },
            Text = block.Text,
            ListStyle = block.Kind switch
            {
                WordKind.Bullet => WordListKind.Bullet,
                WordKind.Number => WordListKind.Number,
                _ => WordListKind.None,
            },
            Alignment = look.Centered ? WordAlignment.Center : WordAlignment.Left,
        };

        private static WordBlockModel MapTable(WordScan scan, WordBlock block)
        {
            var rows = WordTables.Texts(WordTables.Grid(block.Table));
            var hasHeader = WordTables.HasHeaderRow(scan.Styles, block.Table);
            return new WordBlockModel
            {
                Kind = WordBlockKind.Table,
                Table = new WordTableModel
                {
                    Rows = rows,
                    HasHeader = hasHeader,
                    HasFooter = IsFooter(rows, hasHeader),
                    NumericColumns = WordTables.NumericColumns(rows, hasHeader),
                    Caption = Caption(rows, hasHeader),
                },
            };
        }

        private static bool IsFooter(List<List<string>> rows, bool hasHeader)
        {
            if (rows.Count < (hasHeader ? 3 : 2))
                return false;

            var first = rows[^1].FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
            return first != null && s_footers.Contains(first.Replace(" ", string.Empty).Trim());
        }

        private static string Caption(List<List<string>> rows, bool hasHeader)
        {
            var subject = hasHeader && rows.Count > 0
                ? string.Join(", ", rows[0].Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Replace('\n', ' ').Trim()))
                : string.Empty;

            if (subject.Length == 0)
                return $"표 {rows.Count}줄";

            return subject.Length <= CaptionLength ? $"표: {subject}" : $"표: {subject.Substring(0, CaptionLength)}…";
        }

        private static void MarkBylines(WordScan scan, List<WordBlockModel> blocks, List<WordLook> looks)
        {
            var source = scan.Blocks;
            var first = -1;
            for (var i = 0; i < source.Count; i++)
            {
                if (source[i].IsTable || !source[i].IsEmpty)
                {
                    first = i;
                    break;
                }
            }

            if (first < 0 || (source[first].Kind != WordKind.Title && source[first].Kind != WordKind.Heading1))
                return;

            var normal = scan.Styles.NormalFontSize;
            for (var i = first + 1; i < source.Count && i <= first + BylineSearch; i++)
            {
                var block = source[i];
                if (block.IsTable)
                    break;

                if (block.IsEmpty)
                    continue;

                if (block.Kind != WordKind.Body || !IsByline(scan, block, looks[i], normal))
                    break;

                blocks[i].Kind = WordBlockKind.Byline;
            }
        }

        private static bool IsByline(WordScan scan, WordBlock block, WordLook look, int normal)
        {
            var text = block.Text.Trim();
            if (text.Length > BylineLength || text.Split('\n').Any(line => line.Length > BylineLineLength))
                return false;

            return (look.FontSize > 0 && look.FontSize < normal)
                || IsGray(look.Color)
                || scan.Styles.HasName(block.Paragraph, "subtitle");
        }

        private static bool IsGray(string color)
        {
            if (color == null || color.Length != 6 || !int.TryParse(color, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
                return false;

            var r = (value >> 16) & 0xFF;
            var g = (value >> 8) & 0xFF;
            var b = value & 0xFF;
            var max = Math.Max(r, Math.Max(g, b));
            var min = Math.Min(r, Math.Min(g, b));
            var average = (r + g + b) / 3;
            return max - min <= 0x20 && average >= 0x40 && average <= 0xD0;
        }
    }
}
