using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DocuHelper.Tools.Excel
{
    public static class FormulaLexer
    {
        private static readonly string[] s_errors =
        {
            "#DIV/0!", "#N/A", "#NAME?", "#NULL!", "#NUM!", "#REF!", "#VALUE!", "#GETTING_DATA",
            "#SPILL!", "#CALC!", "#FIELD!", "#BLOCKED!", "#CONNECT!", "#BUSY!", "#UNKNOWN!",
        };

        public static List<FormulaToken> Tokenize(string formula)
        {
            var text = formula ?? string.Empty;
            var tokens = new List<FormulaToken>();
            var i = 0;
            while (i < text.Length)
            {
                var c = text[i];
                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                switch (c)
                {
                    case '"':
                        i = ReadString(text, i, tokens);
                        continue;
                    case '\'':
                        i = ReadQuotedSheet(text, i, tokens);
                        continue;
                    case '#':
                        i = ReadError(text, i, tokens);
                        continue;
                    case '{':
                        i = ReadGroup(text, i, '{', '}', i, tokens);
                        continue;
                    case '[':
                        i = ReadGroup(text, i, '[', ']', i, tokens);
                        continue;
                    case '(':
                        tokens.Add(new FormulaToken(FormulaTokenKind.OpenParen, i, i + 1, "("));
                        i++;
                        continue;
                    case ')':
                        tokens.Add(new FormulaToken(FormulaTokenKind.CloseParen, i, i + 1, ")"));
                        i++;
                        continue;
                    case ',':
                        tokens.Add(new FormulaToken(FormulaTokenKind.Comma, i, i + 1, ","));
                        i++;
                        continue;
                    case '<':
                    case '>':
                        var two = i + 1 < text.Length && (text[i + 1] == '=' || (c == '<' && text[i + 1] == '>'));
                        var op = two ? text.Substring(i, 2) : c.ToString();
                        tokens.Add(new FormulaToken(FormulaTokenKind.Operator, i, i + op.Length, op));
                        i += op.Length;
                        continue;
                    case '+':
                    case '-':
                    case '*':
                    case '/':
                    case '^':
                    case '&':
                    case '=':
                    case '%':
                    case ':':
                        tokens.Add(new FormulaToken(FormulaTokenKind.Operator, i, i + 1, c.ToString()));
                        i++;
                        continue;
                }

                if (char.IsAsciiDigit(c) || (c == '.' && i + 1 < text.Length && char.IsAsciiDigit(text[i + 1])))
                {
                    if (TryReadReference(text, i, null, null, i, out var rows, out var rowsEnd))
                    {
                        tokens.Add(rows);
                        i = rowsEnd;
                        continue;
                    }

                    i = ReadNumber(text, i, tokens);
                    continue;
                }

                if (c == '$' || IsNameStart(c))
                {
                    i = ReadWord(text, i, tokens);
                    continue;
                }

                throw new FormulaException($"‘{c}’ 글자를 알아볼 수 없어요.");
            }

            return tokens;
        }

        public static bool IsNameStart(char c) => char.IsLetter(c) || c == '_' || c == '\\';

        public static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '\\' || c == '?';

        private static int ReadString(string text, int start, List<FormulaToken> tokens)
        {
            var builder = new StringBuilder();
            var i = start + 1;
            while (true)
            {
                if (i >= text.Length)
                    throw new FormulaException("따옴표(\")가 닫히지 않았어요.");

                if (text[i] == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        builder.Append('"');
                        i += 2;
                        continue;
                    }

                    i++;
                    break;
                }

                builder.Append(text[i]);
                i++;
            }

            tokens.Add(new FormulaToken(FormulaTokenKind.Text, start, i, text.Substring(start, i - start)) { Value = builder.ToString() });
            return i;
        }

        private static int ReadQuotedSheet(string text, int start, List<FormulaToken> tokens)
        {
            var builder = new StringBuilder();
            var i = start + 1;
            while (true)
            {
                if (i >= text.Length)
                    throw new FormulaException("시트 이름의 작은따옴표(')가 닫히지 않았어요.");

                if (text[i] == '\'')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\'')
                    {
                        builder.Append('\'');
                        i += 2;
                        continue;
                    }

                    i++;
                    break;
                }

                builder.Append(text[i]);
                i++;
            }

            if (i >= text.Length || text[i] != '!')
                throw new FormulaException("시트 이름 뒤에는 !와 칸 주소가 와야 해요.");

            var sheet = builder.ToString();
            var prefix = text.Substring(start, i - start + 1);
            return ReadAfterSheet(text, i + 1, sheet, prefix, start, tokens);
        }

        private static int ReadAfterSheet(string text, int position, string sheet, string prefix, int start, List<FormulaToken> tokens)
        {
            if (sheet.StartsWith("[", StringComparison.Ordinal))
            {
                var end = position;
                while (end < text.Length && (IsNameChar(text[end]) || text[end] == '$' || text[end] == ':'))
                    end++;

                tokens.Add(new FormulaToken(FormulaTokenKind.Unknown, start, end, text.Substring(start, end - start)));
                return end;
            }

            if (TryReadReference(text, position, sheet, prefix, start, out var reference, out var referenceEnd))
            {
                tokens.Add(reference);
                return referenceEnd;
            }

            if (string.Compare(text, position, ExcelValue.BadReference, 0, ExcelValue.BadReference.Length, StringComparison.OrdinalIgnoreCase) == 0)
            {
                var end = position + ExcelValue.BadReference.Length;
                tokens.Add(new FormulaToken(FormulaTokenKind.Error, start, end, text.Substring(start, end - start)) { Value = ExcelValue.BadReference });
                return end;
            }

            throw new FormulaException($"‘{prefix}’ 뒤의 칸 주소를 알아볼 수 없어요.");
        }

        private static int ReadError(string text, int start, List<FormulaToken> tokens)
        {
            foreach (var error in s_errors)
            {
                if (string.Compare(text, start, error, 0, error.Length, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    tokens.Add(new FormulaToken(FormulaTokenKind.Error, start, start + error.Length, text.Substring(start, error.Length)) { Value = error });
                    return start + error.Length;
                }
            }

            throw new FormulaException("‘#’로 시작하는 부분을 알아볼 수 없어요.");
        }

        private static int ReadGroup(string text, int position, char open, char close, int start, List<FormulaToken> tokens)
        {
            var depth = 0;
            var i = position;
            while (i < text.Length)
            {
                var c = text[i];
                if (c == '"')
                {
                    var end = text.IndexOf('"', i + 1);
                    if (end < 0)
                        throw new FormulaException("따옴표(\")가 닫히지 않았어요.");

                    i = end + 1;
                    continue;
                }

                if (c == open)
                {
                    depth++;
                }
                else if (c == close)
                {
                    depth--;
                    if (depth == 0)
                    {
                        i++;
                        if (open == '[' && i < text.Length && text[i] == '[')
                            return ReadGroup(text, i, open, close, start, tokens);

                        tokens.Add(new FormulaToken(FormulaTokenKind.Unknown, start, i, text.Substring(start, i - start)));
                        return i;
                    }
                }

                i++;
            }

            throw new FormulaException($"‘{open}’ 괄호가 닫히지 않았어요.");
        }

        private static int ReadNumber(string text, int start, List<FormulaToken> tokens)
        {
            var i = start;
            while (i < text.Length && char.IsAsciiDigit(text[i]))
                i++;

            if (i < text.Length && text[i] == '.')
            {
                i++;
                while (i < text.Length && char.IsAsciiDigit(text[i]))
                    i++;
            }

            if (i < text.Length && (text[i] == 'e' || text[i] == 'E'))
            {
                var j = i + 1;
                if (j < text.Length && (text[j] == '+' || text[j] == '-'))
                    j++;

                if (j < text.Length && char.IsAsciiDigit(text[j]))
                {
                    i = j;
                    while (i < text.Length && char.IsAsciiDigit(text[i]))
                        i++;
                }
            }

            var raw = text.Substring(start, i - start);
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                throw new FormulaException($"‘{raw}’ 숫자를 알아볼 수 없어요.");

            tokens.Add(new FormulaToken(FormulaTokenKind.Number, start, i, raw) { Number = number });
            return i;
        }

        private static int ReadWord(string text, int start, List<FormulaToken> tokens)
        {
            var end = start;
            while (end < text.Length && IsNameChar(text[end]))
                end++;

            var word = text.Substring(start, end - start);
            if (word.Length > 0 && end < text.Length && text[end] == '!')
                return ReadAfterSheet(text, end + 1, word, text.Substring(start, end - start + 1), start, tokens);

            if (word.Length > 0 && end < text.Length && text[end] == '(')
            {
                tokens.Add(new FormulaToken(FormulaTokenKind.Function, start, end, word) { Value = word.ToUpperInvariant() });
                return end;
            }

            if (word.Length > 0 && end < text.Length && text[end] == '[')
                return ReadGroup(text, end, '[', ']', start, tokens);

            if (TryReadReference(text, start, null, null, start, out var reference, out var referenceEnd))
            {
                tokens.Add(reference);
                return referenceEnd;
            }

            if (word.Length == 0)
                throw new FormulaException("‘$’ 뒤의 칸 주소를 알아볼 수 없어요.");

            if (string.Equals(word, "TRUE", StringComparison.OrdinalIgnoreCase) || string.Equals(word, "FALSE", StringComparison.OrdinalIgnoreCase))
            {
                var value = string.Equals(word, "TRUE", StringComparison.OrdinalIgnoreCase);
                tokens.Add(new FormulaToken(FormulaTokenKind.Boolean, start, end, word) { Value = value ? "TRUE" : "FALSE", Number = value ? 1 : 0 });
                return end;
            }

            tokens.Add(new FormulaToken(FormulaTokenKind.Name, start, end, word));
            return end;
        }

        private static bool TryReadReference(string text, int position, string sheet, string prefix, int start, out FormulaToken token, out int end)
        {
            token = null;
            end = position;
            if (!TryReadPart(text, position, out var first, out var firstEnd))
                return false;

            var reference = new FormulaReference { Sheet = sheet, SheetPrefix = prefix };
            if (firstEnd < text.Length && text[firstEnd] == ':' && TryReadPart(text, firstEnd + 1, out var second, out var secondEnd)
                && second.Kind == first.Kind && IsReferenceEnd(text, secondEnd))
            {
                reference.Kind = first.Kind switch
                {
                    'C' => FormulaReferenceKind.Area,
                    'L' => FormulaReferenceKind.Columns,
                    _ => FormulaReferenceKind.Rows,
                };
                Fill(reference, first, second);
                end = secondEnd;
            }
            else if (first.Kind == 'C' && IsReferenceEnd(text, firstEnd))
            {
                reference.Kind = FormulaReferenceKind.Cell;
                Fill(reference, first, first);
                end = firstEnd;
            }
            else
            {
                return false;
            }

            token = new FormulaToken(FormulaTokenKind.Reference, start, end, text.Substring(start, end - start)) { Reference = reference };
            return true;
        }

        private static void Fill(FormulaReference reference, (char Kind, int Column, int Row, bool ColumnAbsolute, bool RowAbsolute) first, (char Kind, int Column, int Row, bool ColumnAbsolute, bool RowAbsolute) second)
        {
            var swapColumns = first.Column > second.Column;
            var swapRows = first.Row > second.Row;
            reference.Column1 = swapColumns ? second.Column : first.Column;
            reference.Column1Absolute = swapColumns ? second.ColumnAbsolute : first.ColumnAbsolute;
            reference.Column2 = swapColumns ? first.Column : second.Column;
            reference.Column2Absolute = swapColumns ? first.ColumnAbsolute : second.ColumnAbsolute;
            reference.Row1 = swapRows ? second.Row : first.Row;
            reference.Row1Absolute = swapRows ? second.RowAbsolute : first.RowAbsolute;
            reference.Row2 = swapRows ? first.Row : second.Row;
            reference.Row2Absolute = swapRows ? first.RowAbsolute : second.RowAbsolute;
        }

        private static bool IsReferenceEnd(string text, int position) =>
            position >= text.Length || (!IsNameChar(text[position]) && text[position] != '(' && text[position] != '$' && text[position] != '!');

        private static bool TryReadPart(string text, int position, out (char Kind, int Column, int Row, bool ColumnAbsolute, bool RowAbsolute) part, out int end)
        {
            part = default;
            end = position;
            var i = position;
            var firstDollar = false;
            if (i < text.Length && text[i] == '$')
            {
                firstDollar = true;
                i++;
            }

            var letterStart = i;
            while (i < text.Length && char.IsAsciiLetter(text[i]) && i - letterStart < 4)
                i++;

            var letters = text.Substring(letterStart, i - letterStart);
            var afterLetters = i;
            var secondDollar = false;
            if (letters.Length > 0 && i < text.Length && text[i] == '$')
            {
                secondDollar = true;
                i++;
            }

            var digitStart = i;
            while (i < text.Length && char.IsAsciiDigit(text[i]) && i - digitStart < 8)
                i++;

            var digits = text.Substring(digitStart, i - digitStart);
            if (letters.Length > 0 && digits.Length > 0)
            {
                if (!CellAddress.TryParseColumn(letters, out var column) || !CellAddress.TryParseRow(digits, out var row))
                    return false;

                part = ('C', column, row, firstDollar, secondDollar);
                end = i;
                return true;
            }

            if (letters.Length > 0 && !secondDollar)
            {
                if (!CellAddress.TryParseColumn(letters, out var column))
                    return false;

                part = ('L', column, 0, firstDollar, false);
                end = afterLetters;
                return true;
            }

            if (letters.Length == 0 && digits.Length > 0)
            {
                if (!CellAddress.TryParseRow(digits, out var row))
                    return false;

                part = ('R', 0, row, false, firstDollar);
                end = i;
                return true;
            }

            return false;
        }
    }
}
