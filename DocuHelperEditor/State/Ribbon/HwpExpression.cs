using System;
using System.Globalization;

namespace DocuHelperEditor.State.Ribbon
{
    internal sealed class HwpExpression
    {
        private static readonly char[] s_operators = { '+', '-', '*', '/' };

        private readonly string _text;
        private readonly Func<string, double?> _resolve;
        private int _position;

        private HwpExpression(string text, Func<string, double?> resolve)
        {
            _text = text;
            _resolve = resolve;
        }

        public static bool TryEvaluate(string text, Func<string, double?> resolve, out double result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(text) || text.IndexOfAny(s_operators) < 0)
                return false;

            try
            {
                var parser = new HwpExpression(text, resolve);
                var value = parser.Sum();
                parser.Skip();
                if (value == null || parser._position != text.Length || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
                    return false;

                result = value.Value;
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private double? Sum()
        {
            var left = Product();
            while (true)
            {
                Skip();
                var op = Peek();
                if (op != '+' && op != '-')
                    return left;

                _position++;
                var right = Product();
                left = left == null || right == null ? null : op == '+' ? left + right : left - right;
            }
        }

        private double? Product()
        {
            var left = Unary();
            while (true)
            {
                Skip();
                var op = Peek();
                if (op != '*' && op != '/')
                    return left;

                _position++;
                var right = Unary();
                if (left == null || right == null || (op == '/' && right == 0))
                    left = null;
                else
                    left = op == '*' ? left * right : left / right;
            }
        }

        private double? Unary()
        {
            Skip();
            if (Peek() == '-')
            {
                _position++;
                return -Unary();
            }

            return Atom();
        }

        private double? Atom()
        {
            Skip();
            var ch = Peek();
            if (ch == '(')
            {
                _position++;
                var inner = Sum();
                Skip();
                if (Peek() != ')')
                    throw new FormatException();

                _position++;
                return inner;
            }

            var start = _position;
            if (char.IsDigit(ch) || ch == '.')
            {
                while (_position < _text.Length && (char.IsDigit(_text[_position]) || _text[_position] == '.'))
                    _position++;

                return double.Parse(_text.Substring(start, _position - start), NumberStyles.Float, CultureInfo.InvariantCulture);
            }

            if (char.IsLetter(ch) || ch == '_')
            {
                while (_position < _text.Length && (char.IsLetterOrDigit(_text[_position]) || _text[_position] == '_' || _text[_position] == '.'))
                    _position++;

                return _resolve(_text.Substring(start, _position - start));
            }

            throw new FormatException();
        }

        private char Peek() => _position < _text.Length ? _text[_position] : '\0';

        private void Skip()
        {
            while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
                _position++;
        }
    }
}
