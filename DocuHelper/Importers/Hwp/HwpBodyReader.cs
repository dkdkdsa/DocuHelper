using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DocuHelper.Importers.Hwp
{
    internal sealed class HwpBodyReader
    {
        private const uint LastInList = 0x80000000;
        private const int MaxDepth = 24;
        private const int PageBreak = 0x04;
        private const int DefaultCharSize = 1000;
        private const int OutlineIdOffset = 18;
        private const int TopCaption = 2;
        private const int FootnoteNumber = 1;
        private const int EndnoteNumber = 2;
        private const int TextArtContentOffset = 32;
        private const int TabCode = 9;
        private const int LineBreakCode = 10;
        private const int HyphenCode = 24;
        private const int NonBreakingSpaceCode = 30;
        private const int FixedSpaceCode = 31;

        private static readonly HashSet<string> s_contentControls = new(StringComparer.Ordinal) { "tbl ", "gso ", "eqed", "atno" };

        private readonly HwpDocInfo _info;
        private readonly HwpBinData _binData;
        private readonly HwpDocument _document;
        private readonly HwpLabeler _labeler;
        private readonly Dictionary<int, HwpRun> _formats = new();
        private int _outlineId;
        private int _sections;
        private int _footnotes;
        private int _endnotes;
        private bool _pendingBreak;
        private bool _dropNumber;
        private bool _droppedNumber;

        public HwpBodyReader(HwpDocInfo info, HwpBinData binData, HwpDocument document)
        {
            _info = info;
            _binData = binData;
            _document = document;
            _labeler = new HwpLabeler(info);
        }

        public void ReadSection(byte[] data)
        {
            var roots = HwpRecord.ReadTree(data);
            var definition = FindSectionDefinition(roots);
            if (definition != null)
            {
                _outlineId = definition.UInt16(OutlineIdOffset);
                _document.Page ??= ReadPage(definition);
            }

            _pendingBreak = _sections++ > 0;
            foreach (var record in roots)
            {
                if (record.Tag == HwpTag.ParaHeader)
                    ReadParagraph(record, _document.Blocks, 0);
            }

            _pendingBreak = false;
        }

        private static HwpRecord FindSectionDefinition(List<HwpRecord> roots)
        {
            foreach (var root in roots)
            {
                foreach (var child in root.Children)
                {
                    if (child.Tag == HwpTag.CtrlHeader && child.Id == "secd")
                        return child;
                }
            }

            return null;
        }

        private static HwpPage ReadPage(HwpRecord definition)
        {
            foreach (var child in definition.Children)
            {
                if (child.Tag != HwpTag.PageDef)
                    continue;

                return new HwpPage
                {
                    Width = child.Int32(0),
                    Height = child.Int32(4),
                    MarginLeft = child.Int32(8),
                    MarginRight = child.Int32(12),
                    MarginTop = child.Int32(16),
                    MarginBottom = child.Int32(20),
                    MarginHeader = child.Int32(24),
                    MarginFooter = child.Int32(28),
                    Landscape = (child.UInt32(36) & 0x1) != 0,
                };
            }

            return null;
        }

        private void ReadParagraph(HwpRecord header, List<HwpBlock> output, int depth)
        {
            HwpRecord text = null;
            HwpRecord shapes = null;
            var controls = new List<HwpRecord>();
            foreach (var child in header.Children)
            {
                if (child.Tag == HwpTag.ParaText)
                    text ??= child;
                else if (child.Tag == HwpTag.ParaCharShape)
                    shapes ??= child;
                else if (child.Tag == HwpTag.CtrlHeader)
                    controls.Add(child);
            }

            var shape = _info.ParaShape(header.UInt16(8));
            var paragraph = new HwpParagraph();
            if (shape != null)
            {
                var charSize = _info.CharShape(shapes == null ? 0 : (int)shapes.UInt32(4))?.BaseSize ?? DefaultCharSize;
                paragraph.Label = _labeler.Label(shape, _outlineId);
                paragraph.Alignment = shape.Alignment;
                paragraph.LeftIndent = Length(shape.LeftMargin, charSize);
                paragraph.FirstLineIndent = Length(shape.Indent, charSize);
                paragraph.SpaceBefore = shape.SpaceBefore / 2;
                paragraph.SpaceAfter = shape.SpaceAfter / 2;
                paragraph.LineSpacing = shape.LineSpacing;
            }

            if (depth == 0)
            {
                paragraph.PageBreakBefore = _pendingBreak || (header.Byte(11) & PageBreak) != 0 || (shape != null && shape.PageBreakBefore);
                _pendingBreak = false;
            }

            var after = new List<HwpBlock>();
            paragraph.Runs = ReadText(text, shapes, controls, after, depth);
            var empty = paragraph.Runs.Count == 0 && paragraph.Label == null && !paragraph.PageBreakBefore;
            if (!empty || after.Count == 0)
                output.Add(paragraph);

            output.AddRange(after);
        }

        private static int Length(int raw, int charSize) => (raw & 1) == 0 ? raw / 2 : (raw - 1) / 2 * charSize;

        private List<HwpRun> ReadText(HwpRecord text, HwpRecord shapes, List<HwpRecord> controls, List<HwpBlock> after, int depth)
        {
            var runs = new HwpRunBuilder();
            if (text == null)
                return runs.Build();

            var length = text.Size / 2;
            var shapeCount = shapes == null ? 0 : shapes.Size / 8;
            var shapeIndex = 0;
            var controlIndex = 0;
            var i = 0;
            while (i < length)
            {
                while (shapeIndex + 1 < shapeCount && shapes.UInt32((shapeIndex + 1) * 8) <= (uint)i)
                    shapeIndex++;

                var format = Format(shapeCount > 0 ? (int)shapes.UInt32(shapeIndex * 8 + 4) : 0);
                var code = (char)text.UInt16(i * 2);
                if (char.IsHighSurrogate(code) && i + 1 < length && char.IsLowSurrogate((char)text.UInt16(i * 2 + 2)))
                {
                    runs.Append(code, format);
                    runs.Append((char)text.UInt16(i * 2 + 2), format);
                    i += 2;
                    continue;
                }

                if (code >= ' ')
                {
                    if (IsTextChar(code))
                        runs.Append(code, format);

                    i++;
                    continue;
                }

                switch ((int)code)
                {
                    case TabCode:
                        runs.Append('\t', format);
                        i += 8;
                        break;
                    case LineBreakCode:
                        runs.Append('\n', format);
                        i++;
                        break;
                    case HyphenCode:
                        runs.Append('-', format);
                        i++;
                        break;
                    case NonBreakingSpaceCode:
                    case FixedSpaceCode:
                        runs.Append(' ', format);
                        i++;
                        break;
                    default:
                        if (IsExtended(code))
                        {
                            var control = NextControl(text.UInt32(i * 2 + 2), controls, ref controlIndex);
                            if (control != null)
                                ReadControl(control, runs, format, after, depth);

                            i += 8;
                        }
                        else
                        {
                            i += IsInline(code) ? 8 : 1;
                        }

                        break;
                }
            }

            return runs.Build();
        }

        private static bool IsExtended(int code) =>
            code is >= 1 and <= 3 or 11 or 12 or >= 14 and <= 18 or >= 21 and <= 23;

        private static bool IsInline(int code) => code is >= 4 and <= 9 or 19 or 20;

        private static HwpRecord NextControl(uint id, List<HwpRecord> controls, ref int index)
        {
            for (var k = index; k < controls.Count; k++)
            {
                if (controls[k].UInt32(0) != id)
                    continue;

                index = k + 1;
                return controls[k];
            }

            return null;
        }

        private void ReadControl(HwpRecord control, HwpRunBuilder runs, HwpRun format, List<HwpBlock> after, int depth)
        {
            var id = control.Id;
            switch (id)
            {
                case "tbl ":
                    if (depth < MaxDepth)
                        ReadTable(control, after, depth);
                    break;
                case "gso ":
                    if (depth < MaxDepth)
                        ReadDrawing(control, format, after, depth);
                    break;
                case "eqed":
                    _document.Skipped.Add(HwpSkipped.Equation);
                    if (depth < MaxDepth)
                        ReadDrawing(control, format, after, depth);
                    break;
                case "head":
                case "foot":
                    if (HasContent(control))
                        _document.Skipped.Add(HwpSkipped.HeaderFooter);
                    break;
                case "pgnp":
                    if (((control.UInt32(4) >> 8) & 0xF) != 0)
                        _document.Skipped.Add(HwpSkipped.HeaderFooter);
                    break;
                case "fn  ":
                case "en  ":
                    ReadNote(control, id == "en  ", runs, format, depth);
                    break;
                case "atno":
                    ReadAutoNumber(control, runs, format);
                    break;
                case "tdut":
                    runs.Append(Dutmal(control), format);
                    break;
                case "tcps":
                    runs.Append(Overlap(control), format);
                    break;
                case "form":
                    _document.Skipped.Add(HwpSkipped.Object);
                    break;
            }
        }

        private void ReadTable(HwpRecord control, List<HwpBlock> output, int depth)
        {
            var table = new HwpTable();
            var above = new List<HwpBlock>();
            var below = new List<HwpBlock>();
            var children = control.Children;
            var started = false;
            for (var k = 0; k < children.Count; k++)
            {
                var child = children[k];
                if (child.Tag == HwpTag.Table)
                {
                    table.RowCount = child.UInt16(4);
                    table.ColumnCount = child.UInt16(6);
                    started = true;
                }
                else if (child.Tag == HwpTag.ListHeader && !started)
                {
                    ReadVisibleList(children, k, IsTopCaption(child) ? above : below, depth);
                }
                else if (child.Tag == HwpTag.ListHeader)
                {
                    var cell = new HwpCell
                    {
                        Column = child.UInt16(8),
                        Row = child.UInt16(10),
                        ColumnSpan = Math.Max(1, (int)child.UInt16(12)),
                        RowSpan = Math.Max(1, (int)child.UInt16(14)),
                        Width = child.Int32(16),
                    };
                    ReadList(children, k, cell.Blocks, depth);
                    table.Cells.Add(cell);
                    table.RowCount = Math.Max(table.RowCount, cell.Row + cell.RowSpan);
                    table.ColumnCount = Math.Max(table.ColumnCount, cell.Column + cell.ColumnSpan);
                }
            }

            output.AddRange(above);
            output.Add(table);
            output.AddRange(below);
        }

        private void ReadDrawing(HwpRecord control, HwpRun format, List<HwpBlock> output, int depth)
        {
            var above = new List<HwpBlock>();
            var body = new List<HwpBlock>();
            var below = new List<HwpBlock>();
            var children = control.Children;
            for (var k = 0; k < children.Count; k++)
            {
                var child = children[k];
                if (child.Tag == HwpTag.ListHeader)
                    ReadVisibleList(children, k, IsTopCaption(child) ? above : below, depth);
                else if (child.Tag == HwpTag.ShapeComponent)
                    ReadShape(child, format, body, depth);
            }

            output.AddRange(above);
            output.AddRange(body);
            output.AddRange(below);
        }

        private void ReadShape(HwpRecord shape, HwpRun format, List<HwpBlock> output, int depth)
        {
            var children = shape.Children;
            for (var k = 0; k < children.Count; k++)
            {
                var child = children[k];
                switch (child.Tag)
                {
                    case HwpTag.ListHeader:
                        ReadVisibleList(children, k, output, depth);
                        break;
                    case HwpTag.ShapeComponent:
                        if (depth < MaxDepth)
                            ReadShape(child, format, output, depth + 1);
                        break;
                    case HwpTag.ShapeComponentPicture:
                        _document.Skipped.Add(HwpSkipped.Picture);
                        break;
                    case HwpTag.ShapeComponentOle:
                        _document.Skipped.Add(_binData.IsChart(child.UInt16(12)) ? HwpSkipped.Chart : HwpSkipped.Object);
                        break;
                    case HwpTag.ChartData:
                        _document.Skipped.Add(HwpSkipped.Chart);
                        break;
                    case HwpTag.VideoData:
                        _document.Skipped.Add(HwpSkipped.Object);
                        break;
                    case HwpTag.ShapeComponentTextArt:
                        ReadTextArt(child, format, output);
                        break;
                }
            }
        }

        private static void ReadTextArt(HwpRecord record, HwpRun format, List<HwpBlock> output)
        {
            var content = Clean(record.HwpString(TextArtContentOffset, out _).Replace("\r\n", "\n").Replace('\r', '\n')).Trim();
            if (content.Length == 0)
                return;

            var paragraph = new HwpParagraph();
            paragraph.Runs.Add(HwpRunBuilder.Copy(format, content));
            output.Add(paragraph);
        }

        private void ReadList(List<HwpRecord> siblings, int headerIndex, List<HwpBlock> output, int depth)
        {
            foreach (var nested in siblings[headerIndex].Children)
            {
                if (nested.Tag == HwpTag.ParaHeader)
                    ReadParagraph(nested, output, depth + 1);
            }

            for (var k = headerIndex + 1; k < siblings.Count && siblings[k].Tag == HwpTag.ParaHeader; k++)
            {
                ReadParagraph(siblings[k], output, depth + 1);
                if ((siblings[k].UInt32(0) & LastInList) != 0)
                    break;
            }
        }

        private void ReadVisibleList(List<HwpRecord> siblings, int headerIndex, List<HwpBlock> output, int depth)
        {
            var blocks = new List<HwpBlock>();
            ReadList(siblings, headerIndex, blocks, depth);
            if (blocks.Exists(b => b is HwpTable || (b is HwpParagraph p && (p.Runs.Count > 0 || p.Label != null))))
                output.AddRange(blocks);
        }

        private static bool IsTopCaption(HwpRecord listHeader) => (listHeader.UInt32(8) & 0x3) == TopCaption;

        private void ReadNote(HwpRecord control, bool endnote, HwpRunBuilder runs, HwpRun format, int depth)
        {
            var number = endnote ? ++_endnotes : ++_footnotes;
            var mark = (endnote ? "미주" : string.Empty) + number.ToString(CultureInfo.InvariantCulture) + ")";
            var markFormat = HwpRunBuilder.Copy(format, null);
            markFormat.Superscript = true;
            markFormat.Subscript = false;
            runs.Append(mark, markFormat);

            var note = new HwpNote { Mark = mark };
            _document.Notes.Add(note);
            var drop = _dropNumber;
            var dropped = _droppedNumber;
            _dropNumber = true;
            _droppedNumber = false;
            var children = control.Children;
            for (var k = 0; k < children.Count && depth < MaxDepth; k++)
            {
                if (children[k].Tag != HwpTag.ListHeader)
                    continue;

                ReadList(children, k, note.Blocks, depth);
                break;
            }

            if (_droppedNumber)
                TrimLeadingSpace(note.Blocks);

            _dropNumber = drop;
            _droppedNumber = dropped;
        }

        private static void TrimLeadingSpace(List<HwpBlock> blocks)
        {
            if (blocks.Count == 0 || blocks[0] is not HwpParagraph paragraph)
                return;

            while (paragraph.Runs.Count > 0)
            {
                var trimmed = paragraph.Runs[0].Text.TrimStart(' ');
                if (trimmed.Length > 0)
                {
                    paragraph.Runs[0].Text = trimmed;
                    return;
                }

                paragraph.Runs.RemoveAt(0);
            }
        }

        private void ReadAutoNumber(HwpRecord control, HwpRunBuilder runs, HwpRun format)
        {
            var attributes = control.UInt32(4);
            var kind = attributes & 0xF;
            if (_dropNumber && (kind == FootnoteNumber || kind == EndnoteNumber))
            {
                _dropNumber = false;
                _droppedNumber = true;
                return;
            }

            var number = HwpNumberFormat.Format(control.UInt16(8), (int)((attributes >> 4) & 0xFF));
            runs.Append(Decoration(control.UInt16(12)) + number + Decoration(control.UInt16(14)), format);
        }

        private static string Dutmal(HwpRecord control)
        {
            var main = control.HwpString(4, out var next);
            var sub = control.HwpString(next, out _).Trim();
            return Clean(sub.Length == 0 ? main : main + "(" + sub + ")");
        }

        private static bool HasContent(HwpRecord control)
        {
            var pending = new Stack<HwpRecord>(control.Children);
            while (pending.Count > 0)
            {
                var record = pending.Pop();
                if ((record.Tag == HwpTag.ParaText && HasVisibleText(record)) || (record.Tag == HwpTag.CtrlHeader && s_contentControls.Contains(record.Id)))
                    return true;

                foreach (var child in record.Children)
                    pending.Push(child);
            }

            return false;
        }

        private static bool HasVisibleText(HwpRecord text)
        {
            var length = text.Size / 2;
            var i = 0;
            while (i < length)
            {
                var code = text.UInt16(i * 2);
                if (code >= ' ' && !char.IsWhiteSpace((char)code))
                    return true;

                i += code < ' ' && (IsExtended(code) || IsInline(code)) ? 8 : 1;
            }

            return false;
        }

        private static string Decoration(ushort value) => value >= ' ' ? ((char)value).ToString() : string.Empty;

        private static string Overlap(HwpRecord control)
        {
            var count = control.UInt16(4);
            var builder = new StringBuilder();
            for (var k = 0; k < count && control.Has(6 + k * 2, 2); k++)
                builder.Append((char)control.UInt16(6 + k * 2));

            return Clean(builder.ToString());
        }

        private static string Clean(string value)
        {
            var builder = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                {
                    builder.Append(c).Append(value[++i]);
                    continue;
                }

                if (c == '\n' || c == '\t' || IsTextChar(c))
                    builder.Append(c);
            }

            return builder.ToString();
        }

        private static bool IsTextChar(char c) =>
            c >= ' ' && (c < 0x7F || c > 0x9F) && !char.IsSurrogate(c) && c < 0xFFFE;

        private HwpRun Format(int id)
        {
            if (_formats.TryGetValue(id, out var format))
                return format;

            var shape = _info.CharShape(id);
            format = shape == null ? new HwpRun() : new HwpRun
            {
                Bold = shape.Bold,
                Italic = shape.Italic,
                Underline = shape.Underline,
                Strike = shape.Strike,
                Superscript = shape.Superscript,
                Subscript = shape.Subscript,
                Size = shape.BaseSize,
                Color = Color(shape.TextColor),
                Font = _info.Font(shape.FaceId),
            };
            _formats[id] = format;
            return format;
        }

        private static string Color(uint value)
        {
            var rgb = value & 0xFFFFFF;
            if (rgb == 0)
                return null;

            return (rgb & 0xFF).ToString("X2", CultureInfo.InvariantCulture)
                + ((rgb >> 8) & 0xFF).ToString("X2", CultureInfo.InvariantCulture)
                + ((rgb >> 16) & 0xFF).ToString("X2", CultureInfo.InvariantCulture);
        }
    }
}
