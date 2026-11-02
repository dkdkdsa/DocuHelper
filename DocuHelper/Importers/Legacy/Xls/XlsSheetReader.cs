using System;
using System.Collections.Generic;
using System.IO;

namespace DocuHelper.Importers.Legacy.Xls
{
    internal sealed class XlsSheetReader
    {
        private const int MaxColumn = 255;

        private readonly List<string> _strings;
        private readonly XlsFormulaDecompiler _decompiler;
        private readonly Dictionary<long, XlsSharedFormula> _shared = new();
        private readonly Dictionary<long, XlsSharedFormula> _arrays = new();
        private XlsSheet _sheet;
        private XlsCell _pendingString;

        public XlsSheetReader(List<string> strings, XlsFormulaDecompiler decompiler)
        {
            _strings = strings;
            _decompiler = decompiler;
        }

        public XlsSheet Read(List<XlsRecord> records, int start, XlsSheetInfo info)
        {
            _sheet = new XlsSheet { Info = info };
            var depth = 0;
            var finished = false;
            for (var i = start; i < records.Count; i++)
            {
                var record = records[i];
                if (record.Id == XlsIds.Bof)
                {
                    depth++;
                    continue;
                }

                if (record.Id == XlsIds.Eof)
                {
                    if (--depth <= 0)
                    {
                        finished = true;
                        break;
                    }

                    continue;
                }

                if (depth == 1)
                    Handle(record);
            }

            if (!finished)
                throw new InvalidDataException("The sheet has no end.");

            ResolveFormulas();
            return _sheet;
        }

        private void Handle(XlsRecord record)
        {
            if (record.Id != XlsIds.String && record.Id != XlsIds.SharedFormula && record.Id != XlsIds.Array && record.Id != XlsIds.Table && record.Id != XlsIds.Formula)
                _pendingString = null;

            var reader = new XlsDataReader(record);
            switch (record.Id)
            {
                case XlsIds.Row:
                    ReadRow(reader);
                    break;
                case XlsIds.Number:
                    AddNumber(reader);
                    break;
                case XlsIds.Rk:
                    AddRk(reader);
                    break;
                case XlsIds.MulRk:
                    AddMulRk(record, reader);
                    break;
                case XlsIds.LabelSst:
                    AddLabelSst(reader);
                    break;
                case XlsIds.Label:
                    AddLabel(reader);
                    break;
                case XlsIds.BoolErr:
                    AddBoolErr(reader);
                    break;
                case XlsIds.Blank:
                    AddBlank(reader);
                    break;
                case XlsIds.MulBlank:
                    AddMulBlank(record, reader);
                    break;
                case XlsIds.Formula:
                    AddFormula(reader);
                    break;
                case XlsIds.String:
                    if (_pendingString != null)
                        _pendingString.Text = reader.ReadLongString();

                    _pendingString = null;
                    break;
                case XlsIds.SharedFormula:
                    AddSharedFormula(reader, _shared, 2);
                    break;
                case XlsIds.Array:
                    AddSharedFormula(reader, _arrays, 6);
                    break;
                case XlsIds.ColInfo:
                    ReadColumn(reader);
                    break;
                case XlsIds.DefColWidth:
                    _sheet.DefaultColumnChars = Math.Max(1, (int)reader.ReadUInt16());
                    break;
                case XlsIds.StandardWidth:
                    _sheet.StandardWidth = reader.ReadUInt16() / 256.0;
                    break;
                case XlsIds.DefaultRowHeight:
                    reader.Skip(2);
                    var height = (reader.ReadUInt16() & 0x7FFF) / 20.0;
                    _sheet.DefaultRowHeight = height > 0 ? height : null;
                    break;
                case XlsIds.MergeCells:
                    ReadMerges(reader);
                    break;
                case XlsIds.Pane:
                    ReadPane(reader);
                    break;
                case XlsIds.Window2:
                    ReadWindow(reader);
                    break;
            }
        }

        private void ReadRow(XlsDataReader reader)
        {
            var index = reader.ReadUInt16();
            reader.Skip(4);
            var height = reader.ReadUInt16();
            reader.Skip(4);
            var flags = reader.ReadUInt32();
            var row = new XlsRow
            {
                Index = index,
                Hidden = (flags & 0x20) != 0,
                CustomHeight = (flags & 0x40) != 0,
                Outline = (int)(flags & 0x07),
                Collapsed = (flags & 0x10) != 0,
            };

            if ((height & 0x8000) == 0 && (height & 0x7FFF) > 0)
                row.Height = (height & 0x7FFF) / 20.0;

            if ((flags & 0x80) != 0)
                row.Style = (int)((flags >> 16) & 0xFFF);

            _sheet.Rows[index] = row;
        }

        private XlsCell NewCell(XlsDataReader reader, XlsCellKind kind)
        {
            var cell = new XlsCell { Row = reader.ReadUInt16(), Column = reader.ReadUInt16(), Style = reader.ReadUInt16(), Kind = kind };
            return cell.Column <= MaxColumn ? cell : null;
        }

        private void AddNumber(XlsDataReader reader)
        {
            var cell = NewCell(reader, XlsCellKind.Number);
            if (cell == null)
                return;

            cell.Number = reader.ReadDouble();
            _sheet.Cells.Add(cell);
        }

        private void AddRk(XlsDataReader reader)
        {
            var cell = NewCell(reader, XlsCellKind.Number);
            if (cell == null)
                return;

            cell.Number = DecodeRk(reader.ReadUInt32());
            _sheet.Cells.Add(cell);
        }

        private void AddMulRk(XlsRecord record, XlsDataReader reader)
        {
            var row = reader.ReadUInt16();
            var column = reader.ReadUInt16();
            var count = (record.Data.Length - 6) / 6;
            for (var i = 0; i < count; i++)
            {
                var style = reader.ReadUInt16();
                var value = DecodeRk(reader.ReadUInt32());
                if (column + i <= MaxColumn)
                    _sheet.Cells.Add(new XlsCell { Row = row, Column = column + i, Style = style, Kind = XlsCellKind.Number, Number = value });
            }
        }

        private void AddLabelSst(XlsDataReader reader)
        {
            var cell = NewCell(reader, XlsCellKind.Text);
            if (cell == null)
                return;

            var index = reader.ReadUInt32();
            cell.Text = index < _strings.Count ? _strings[(int)index] : string.Empty;
            _sheet.Cells.Add(cell);
        }

        private void AddLabel(XlsDataReader reader)
        {
            var cell = NewCell(reader, XlsCellKind.Text);
            if (cell == null)
                return;

            cell.Text = reader.ReadLongString();
            _sheet.Cells.Add(cell);
        }

        private void AddBoolErr(XlsDataReader reader)
        {
            var cell = NewCell(reader, XlsCellKind.Bool);
            if (cell == null)
                return;

            cell.Code = reader.ReadByte();
            if (reader.ReadByte() != 0)
                cell.Kind = XlsCellKind.Error;

            _sheet.Cells.Add(cell);
        }

        private void AddBlank(XlsDataReader reader)
        {
            var cell = NewCell(reader, XlsCellKind.Blank);
            if (cell != null && cell.Style != 0)
                _sheet.Cells.Add(cell);
        }

        private void AddMulBlank(XlsRecord record, XlsDataReader reader)
        {
            var row = reader.ReadUInt16();
            var column = reader.ReadUInt16();
            var count = (record.Data.Length - 6) / 2;
            for (var i = 0; i < count; i++)
            {
                var style = reader.ReadUInt16();
                if (style != 0 && column + i <= MaxColumn)
                    _sheet.Cells.Add(new XlsCell { Row = row, Column = column + i, Style = style, Kind = XlsCellKind.Blank });
            }
        }

        private void AddFormula(XlsDataReader reader)
        {
            var cell = NewCell(reader, XlsCellKind.Number);
            _pendingString = null;
            if (cell == null)
                return;

            var value = reader.ReadBytes(8);
            reader.Skip(6);
            var length = reader.ReadUInt16();
            cell.Tokens = reader.ReadBytes(Math.Min(length, reader.Remaining));
            cell.Extra = reader.ReadBytes(reader.Remaining);
            if (value[6] == 0xFF && value[7] == 0xFF)
            {
                switch (value[0])
                {
                    case 0:
                        cell.Kind = XlsCellKind.Text;
                        cell.Text = string.Empty;
                        _pendingString = cell;
                        break;
                    case 1:
                        cell.Kind = XlsCellKind.Bool;
                        cell.Code = value[2];
                        break;
                    case 2:
                        cell.Kind = XlsCellKind.Error;
                        cell.Code = value[2];
                        break;
                    default:
                        cell.Kind = XlsCellKind.Text;
                        cell.Text = string.Empty;
                        break;
                }
            }
            else
            {
                cell.Number = BitConverter.ToDouble(value, 0);
            }

            _sheet.Cells.Add(cell);
        }

        private static void AddSharedFormula(XlsDataReader reader, Dictionary<long, XlsSharedFormula> target, int skip)
        {
            var formula = new XlsSharedFormula
            {
                FirstRow = reader.ReadUInt16(),
                LastRow = reader.ReadUInt16(),
                FirstColumn = reader.ReadByte(),
                LastColumn = reader.ReadByte(),
            };

            reader.Skip(skip);
            var length = reader.ReadUInt16();
            formula.Tokens = reader.ReadBytes(Math.Min(length, reader.Remaining));
            formula.Extra = reader.ReadBytes(reader.Remaining);
            target[Key(formula.FirstRow, formula.FirstColumn)] = formula;
        }

        private void ResolveFormulas()
        {
            foreach (var cell in _sheet.Cells)
            {
                if (cell.Tokens == null)
                    continue;

                if (cell.Tokens.Length == 5 && cell.Tokens[0] == 0x01)
                    ResolveExpression(cell);
                else
                    cell.Formula = _decompiler.Decompile(cell.Tokens, cell.Extra, cell.Row, cell.Column);

                cell.Tokens = null;
                cell.Extra = null;
            }
        }

        private void ResolveExpression(XlsCell cell)
        {
            var row = cell.Tokens[1] | (cell.Tokens[2] << 8);
            var column = cell.Tokens[3] | (cell.Tokens[4] << 8);
            var key = Key(row, column);
            if (_arrays.TryGetValue(key, out var array))
            {
                if (row == cell.Row && column == cell.Column)
                {
                    cell.Formula = _decompiler.Decompile(array.Tokens, array.Extra, cell.Row, cell.Column);
                    cell.ArrayRange = XlsCellName.Cell(array.FirstRow, array.FirstColumn) + ":" + XlsCellName.Cell(array.LastRow, array.LastColumn);
                }

                return;
            }

            if (!_shared.TryGetValue(key, out var shared))
            {
                foreach (var candidate in _shared.Values)
                {
                    if (candidate.Contains(cell.Row, cell.Column))
                    {
                        shared = candidate;
                        break;
                    }
                }
            }

            if (shared != null)
                cell.Formula = _decompiler.Decompile(shared.Tokens, shared.Extra, cell.Row, cell.Column, true);
        }

        private void ReadColumn(XlsDataReader reader)
        {
            var first = reader.ReadUInt16();
            var last = Math.Min((int)reader.ReadUInt16(), MaxColumn);
            var width = reader.ReadUInt16();
            var style = reader.ReadUInt16();
            var flags = reader.ReadUInt16();
            if (first > last)
                return;

            _sheet.Columns.Add(new XlsColumn
            {
                First = first,
                Last = last,
                Width = width / 256.0,
                Style = style,
                Hidden = (flags & 0x01) != 0,
                Outline = (flags >> 8) & 0x07,
                Collapsed = (flags & 0x1000) != 0,
            });
        }

        private void ReadMerges(XlsDataReader reader)
        {
            var count = reader.ReadUInt16();
            for (var i = 0; i < count && reader.Remaining >= 8; i++)
            {
                var firstRow = reader.ReadUInt16();
                var lastRow = reader.ReadUInt16();
                var firstColumn = reader.ReadUInt16();
                var lastColumn = reader.ReadUInt16();
                if (firstRow <= lastRow && firstColumn <= lastColumn && lastColumn <= MaxColumn && (firstRow != lastRow || firstColumn != lastColumn))
                    _sheet.Merges.Add((firstRow, lastRow, firstColumn, lastColumn));
            }
        }

        private void ReadPane(XlsDataReader reader)
        {
            _sheet.HasPane = true;
            _sheet.PaneColumns = reader.ReadUInt16();
            _sheet.PaneRows = reader.ReadUInt16();
            _sheet.PaneTopRow = reader.ReadUInt16();
            _sheet.PaneLeftColumn = reader.ReadUInt16();
        }

        private void ReadWindow(XlsDataReader reader)
        {
            var flags = reader.ReadUInt16();
            _sheet.ShowGridlines = (flags & 0x0002) != 0;
            _sheet.Frozen = (flags & 0x0008) != 0;
            _sheet.Selected = (flags & 0x0200) != 0;
        }

        private static double DecodeRk(uint rk)
        {
            double value;
            if ((rk & 0x02) != 0)
                value = (int)rk >> 2;
            else
                value = BitConverter.Int64BitsToDouble((long)(rk & 0xFFFFFFFC) << 32);

            return (rk & 0x01) != 0 ? value / 100.0 : value;
        }

        private static long Key(int row, int column) => ((long)row << 8) | (uint)column;
    }
}
