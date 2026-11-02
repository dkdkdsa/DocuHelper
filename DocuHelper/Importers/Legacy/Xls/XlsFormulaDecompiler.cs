using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace DocuHelper.Importers.Legacy.Xls
{
    internal sealed class XlsFormulaDecompiler
    {
        private const int MaxRow = 0xFFFF;
        private const int MaxColumn = 0xFF;

        private static readonly string[] s_binary =
        {
            "+", "-", "*", "/", "^", "&", "<", "<=", "=", ">=", ">", "<>", " ", ",", ":",
        };

        private static readonly string[] s_futurePrefixed = { "IFERROR", "SUMIFS", "COUNTIFS", "AVERAGEIF", "AVERAGEIFS" };

        private readonly XlsWorkbookInfo _info;

        public XlsFormulaDecompiler(XlsWorkbookInfo info)
        {
            _info = info;
        }

        public string Decompile(byte[] tokens, byte[] extra, int row, int column, bool shared = false)
        {
            try
            {
                return Run(tokens, extra, row, column, shared);
            }
            catch (Exception ex) when (ex is InvalidDataException || ex is IndexOutOfRangeException || ex is ArgumentException || ex is InvalidOperationException || ex is OverflowException)
            {
                return null;
            }
        }

        private string Run(byte[] tokens, byte[] extra, int row, int column, bool shared)
        {
            var reader = new XlsDataReader(tokens);
            var array = new XlsDataReader(extra ?? Array.Empty<byte>());
            var stack = new List<string>();
            while (reader.Remaining > 0)
            {
                var id = reader.ReadByte();
                if (id >= 0x20)
                    id = (byte)((id & 0x1F) | 0x20);

                if (id >= 0x03 && id <= 0x11)
                {
                    var right = Pop(stack);
                    var left = Pop(stack);
                    stack.Add(left + s_binary[id - 0x03] + right);
                    continue;
                }

                switch (id)
                {
                    case 0x12:
                        stack.Add("+" + Pop(stack));
                        break;
                    case 0x13:
                        stack.Add("-" + Pop(stack));
                        break;
                    case 0x14:
                        stack.Add(Pop(stack) + "%");
                        break;
                    case 0x15:
                        stack.Add("(" + Pop(stack) + ")");
                        break;
                    case 0x16:
                        stack.Add(string.Empty);
                        break;
                    case 0x17:
                        stack.Add("\"" + reader.ReadShortString().Replace("\"", "\"\"") + "\"");
                        break;
                    case 0x19:
                        ReadAttribute(reader, stack);
                        break;
                    case 0x1C:
                        stack.Add(ErrorText(reader.ReadByte()));
                        break;
                    case 0x1D:
                        stack.Add(reader.ReadByte() != 0 ? "TRUE" : "FALSE");
                        break;
                    case 0x1E:
                        stack.Add(reader.ReadUInt16().ToString(CultureInfo.InvariantCulture));
                        break;
                    case 0x1F:
                        stack.Add(NumberText(reader.ReadDouble()));
                        break;
                    case 0x20:
                        reader.Skip(7);
                        stack.Add(ReadArrayConstant(array));
                        break;
                    case 0x21:
                        ReadFunction(reader, stack, false);
                        break;
                    case 0x22:
                        ReadFunction(reader, stack, true);
                        break;
                    case 0x23:
                        stack.Add(NameText(reader.ReadUInt32()));
                        break;
                    case 0x24:
                        stack.Add(ReadReference(reader, row, column, false));
                        break;
                    case 0x25:
                        stack.Add(ReadArea(reader, row, column, false));
                        break;
                    case 0x26:
                        reader.Skip(6);
                        array.Skip(2 + 8 * array.ReadUInt16());
                        break;
                    case 0x27:
                    case 0x28:
                        reader.Skip(6);
                        break;
                    case 0x29:
                        reader.Skip(2);
                        break;
                    case 0x2A:
                        reader.Skip(4);
                        stack.Add("#REF!");
                        break;
                    case 0x2B:
                        reader.Skip(8);
                        stack.Add("#REF!");
                        break;
                    case 0x2C:
                        stack.Add(ReadReference(reader, row, column, true));
                        break;
                    case 0x2D:
                        stack.Add(ReadArea(reader, row, column, true));
                        break;
                    case 0x39:
                        stack.Add(ReadExternName(reader));
                        break;
                    case 0x3A:
                        stack.Add(Required(_info.SheetPrefix(reader.ReadUInt16())) + ReadReference(reader, row, column, shared));
                        break;
                    case 0x3B:
                        stack.Add(Required(_info.SheetPrefix(reader.ReadUInt16())) + ReadArea(reader, row, column, shared));
                        break;
                    case 0x3C:
                        reader.Skip(6);
                        stack.Add("#REF!");
                        break;
                    case 0x3D:
                        reader.Skip(10);
                        stack.Add("#REF!");
                        break;
                    default:
                        throw new InvalidDataException("Unsupported token.");
                }
            }

            if (stack.Count != 1)
                throw new InvalidDataException("The formula is unbalanced.");

            return stack[0];
        }

        private void ReadAttribute(XlsDataReader reader, List<string> stack)
        {
            var type = reader.ReadByte();
            var data = reader.ReadUInt16();
            if ((type & 0x04) != 0)
                reader.Skip((data + 1) * 2);
            else if ((type & 0x10) != 0)
                stack.Add("SUM(" + Pop(stack) + ")");
        }

        private void ReadFunction(XlsDataReader reader, List<string> stack, bool variable)
        {
            int count;
            int id;
            if (variable)
            {
                count = reader.ReadByte() & 0x7F;
                id = reader.ReadUInt16() & 0x7FFF;
            }
            else
            {
                id = reader.ReadUInt16();
                count = -1;
            }

            string name = null;
            if (id != 255)
            {
                if (!XlsFunctionTable.TryGet(id, out name, out var fixedCount))
                    throw new InvalidDataException("Unknown function.");

                if (!variable)
                    count = fixedCount;
            }

            if (count < 0 || count > stack.Count)
                throw new InvalidDataException("The function arguments are broken.");

            var arguments = new string[count];
            for (var i = count - 1; i >= 0; i--)
                arguments[i] = Pop(stack);

            var first = 0;
            if (id == 255)
            {
                if (count == 0 || arguments[0].Length == 0)
                    throw new InvalidDataException("The function name is missing.");

                name = StripFuturePrefix(arguments[0]);
                first = 1;
            }

            stack.Add(name + "(" + string.Join(",", arguments, first, count - first) + ")");
        }

        private static string StripFuturePrefix(string name)
        {
            const string prefix = "_xlfn.";
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && Array.IndexOf(s_futurePrefixed, name.Substring(prefix.Length).ToUpperInvariant()) >= 0)
                return name.Substring(prefix.Length);

            return name;
        }

        private string NameText(uint index)
        {
            if (index < 1 || index > _info.Names.Count)
                throw new InvalidDataException("The name does not exist.");

            return _info.Names[(int)index - 1].DisplayName;
        }

        private string ReadExternName(XlsDataReader reader)
        {
            var xti = reader.ReadUInt16();
            var index = reader.ReadUInt32();
            return _info.ExternName(xti, (int)Math.Min(index, int.MaxValue)) ?? throw new InvalidDataException("The external name does not exist.");
        }

        private static string Required(string prefix) => prefix ?? throw new InvalidDataException("The sheet reference is not supported.");

        private static string ReadReference(XlsDataReader reader, int cellRow, int cellColumn, bool relative)
        {
            var rowWord = reader.ReadUInt16();
            var columnWord = reader.ReadUInt16();
            return Cell(rowWord, columnWord, cellRow, cellColumn, relative, out _, out _, out _, out _);
        }

        private static string ReadArea(XlsDataReader reader, int cellRow, int cellColumn, bool relative)
        {
            var firstRowWord = reader.ReadUInt16();
            var lastRowWord = reader.ReadUInt16();
            var firstColumnWord = reader.ReadUInt16();
            var lastColumnWord = reader.ReadUInt16();
            var first = Cell(firstRowWord, firstColumnWord, cellRow, cellColumn, relative, out var firstRow, out var firstColumn, out var firstRowAbsolute, out var firstColumnAbsolute);
            var last = Cell(lastRowWord, lastColumnWord, cellRow, cellColumn, relative, out var lastRow, out var lastColumn, out var lastRowAbsolute, out var lastColumnAbsolute);
            if (firstRow == 0 && lastRow == MaxRow)
                return (firstColumnAbsolute ? "$" : string.Empty) + XlsCellName.Column(firstColumn) + ":" + (lastColumnAbsolute ? "$" : string.Empty) + XlsCellName.Column(lastColumn);

            if (firstColumn == 0 && lastColumn == MaxColumn)
                return (firstRowAbsolute ? "$" : string.Empty) + (firstRow + 1) + ":" + (lastRowAbsolute ? "$" : string.Empty) + (lastRow + 1);

            return first + ":" + last;
        }

        private static string Cell(int rowWord, int columnWord, int cellRow, int cellColumn, bool relative, out int row, out int column, out bool rowAbsolute, out bool columnAbsolute)
        {
            rowAbsolute = (columnWord & 0x8000) == 0;
            columnAbsolute = (columnWord & 0x4000) == 0;
            row = rowWord;
            column = columnWord & 0xFF;
            if (relative)
            {
                if (!rowAbsolute)
                    row = (cellRow + (short)rowWord) & MaxRow;

                if (!columnAbsolute)
                    column = (cellColumn + (sbyte)(columnWord & 0xFF)) & MaxColumn;
            }

            return (columnAbsolute ? "$" : string.Empty) + XlsCellName.Column(column) + (rowAbsolute ? "$" : string.Empty) + (row + 1);
        }

        private static string ReadArrayConstant(XlsDataReader reader)
        {
            var columns = reader.ReadByte() + 1;
            var rows = reader.ReadUInt16() + 1;
            if (columns * rows > reader.Remaining)
                throw new InvalidDataException("The array constant is cut off.");

            var builder = new StringBuilder("{");
            for (var r = 0; r < rows; r++)
            {
                if (r > 0)
                    builder.Append(';');

                for (var c = 0; c < columns; c++)
                {
                    if (c > 0)
                        builder.Append(',');

                    builder.Append(ReadArrayItem(reader));
                }
            }

            return builder.Append('}').ToString();
        }

        private static string ReadArrayItem(XlsDataReader reader)
        {
            var type = reader.ReadByte();
            switch (type)
            {
                case 0x00:
                    reader.Skip(8);
                    return string.Empty;
                case 0x01:
                    return NumberText(reader.ReadDouble());
                case 0x02:
                    return "\"" + reader.ReadLongString().Replace("\"", "\"\"") + "\"";
                case 0x04:
                {
                    var value = reader.ReadByte() != 0;
                    reader.Skip(7);
                    return value ? "TRUE" : "FALSE";
                }

                case 0x10:
                {
                    var code = reader.ReadByte();
                    reader.Skip(7);
                    return ErrorText(code);
                }

                default:
                    throw new InvalidDataException("Unknown array item.");
            }
        }

        public static string ErrorText(byte code) => code switch
        {
            0x00 => "#NULL!",
            0x07 => "#DIV/0!",
            0x0F => "#VALUE!",
            0x17 => "#REF!",
            0x1D => "#NAME?",
            0x24 => "#NUM!",
            0x2A => "#N/A",
            _ => throw new InvalidDataException("Unknown error code."),
        };

        private static string NumberText(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new InvalidDataException("The number is not finite.");

            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string Pop(List<string> stack)
        {
            if (stack.Count == 0)
                throw new InvalidDataException("The formula stack is empty.");

            var value = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            return value;
        }
    }
}
