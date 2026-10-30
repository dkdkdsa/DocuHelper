using System;
using System.Collections.Generic;

namespace DocuHelper.Tools.Excel
{
    public sealed class FormulaParser
    {
        private readonly List<FormulaToken> _tokens;
        private int _position;

        private FormulaParser(List<FormulaToken> tokens)
        {
            _tokens = tokens;
        }

        public static FormulaNode Parse(string formula)
        {
            var tokens = FormulaLexer.Tokenize(formula);
            if (tokens.Count == 0)
                throw new FormulaException("수식이 비어 있어요.");

            var parser = new FormulaParser(tokens);
            var node = parser.ParseComparison();
            if (parser._position < tokens.Count)
                throw new FormulaException($"‘{tokens[parser._position].Text}’ 부분을 알아볼 수 없어요.");

            return node;
        }

        public static bool TryParse(string formula, out FormulaNode node)
        {
            try
            {
                node = Parse(formula);
                return true;
            }
            catch (FormulaException)
            {
                node = null;
                return false;
            }
        }

        private FormulaToken Peek => _position < _tokens.Count ? _tokens[_position] : null;

        private bool IsOperator(params string[] operators)
        {
            var token = Peek;
            if (token == null || token.Kind != FormulaTokenKind.Operator)
                return false;

            foreach (var op in operators)
            {
                if (token.Value == op)
                    return true;
            }

            return false;
        }

        private FormulaNode ParseComparison()
        {
            var left = ParseConcat();
            while (IsOperator("=", "<>", "<", ">", "<=", ">="))
                left = Binary(left, ParseConcat);

            return left;
        }

        private FormulaNode ParseConcat()
        {
            var left = ParseAdditive();
            while (IsOperator("&"))
                left = Binary(left, ParseAdditive);

            return left;
        }

        private FormulaNode ParseAdditive()
        {
            var left = ParseMultiplicative();
            while (IsOperator("+", "-"))
                left = Binary(left, ParseMultiplicative);

            return left;
        }

        private FormulaNode ParseMultiplicative()
        {
            var left = ParsePower();
            while (IsOperator("*", "/"))
                left = Binary(left, ParsePower);

            return left;
        }

        private FormulaNode ParsePower()
        {
            var left = ParsePercent();
            while (IsOperator("^"))
                left = Binary(left, ParsePercent);

            return left;
        }

        private FormulaNode ParsePercent()
        {
            var node = ParseUnary();
            while (IsOperator("%"))
            {
                _position++;
                var percent = new FormulaNode(FormulaNodeKind.Percent);
                percent.Children.Add(node);
                node = percent;
            }

            return node;
        }

        private FormulaNode ParseUnary()
        {
            if (IsOperator("-", "+"))
            {
                var op = Peek.Value;
                _position++;
                var unary = new FormulaNode(FormulaNodeKind.Unary) { Value = op };
                unary.Children.Add(ParseUnary());
                return unary;
            }

            return ParsePrimary();
        }

        private FormulaNode ParsePrimary()
        {
            var token = Peek;
            if (token == null)
                throw new FormulaException("수식이 중간에 끝났어요.");

            _position++;
            switch (token.Kind)
            {
                case FormulaTokenKind.Number:
                    return new FormulaNode(FormulaNodeKind.Number) { Number = token.Number };
                case FormulaTokenKind.Text:
                    return new FormulaNode(FormulaNodeKind.Text) { Value = token.Value };
                case FormulaTokenKind.Boolean:
                    return new FormulaNode(FormulaNodeKind.Boolean) { Number = token.Number, Value = token.Value };
                case FormulaTokenKind.Error:
                    return new FormulaNode(FormulaNodeKind.Error) { Value = token.Value };
                case FormulaTokenKind.Reference:
                    return new FormulaNode(FormulaNodeKind.Reference) { Reference = token.Reference };
                case FormulaTokenKind.Name:
                    return new FormulaNode(FormulaNodeKind.Name) { Value = token.Value };
                case FormulaTokenKind.Unknown:
                    return new FormulaNode(FormulaNodeKind.Unknown) { Value = token.Text };
                case FormulaTokenKind.Function:
                    return ParseFunction(token);
                case FormulaTokenKind.OpenParen:
                    var inner = ParseComparison();
                    Expect(FormulaTokenKind.CloseParen, "괄호가 닫히지 않았어요.");
                    return inner;
                default:
                    throw new FormulaException($"‘{token.Text}’ 부분을 알아볼 수 없어요.");
            }
        }

        private FormulaNode ParseFunction(FormulaToken token)
        {
            Expect(FormulaTokenKind.OpenParen, $"{token.Value} 뒤에 여는 괄호가 필요해요.");
            var function = new FormulaNode(FormulaNodeKind.Function) { Value = token.Value };
            if (Peek?.Kind == FormulaTokenKind.CloseParen)
            {
                _position++;
                return function;
            }

            while (true)
            {
                var next = Peek;
                if (next == null)
                    throw new FormulaException($"{token.Value} 함수의 괄호가 닫히지 않았어요.");

                if (next.Kind == FormulaTokenKind.Comma || next.Kind == FormulaTokenKind.CloseParen)
                    function.Children.Add(new FormulaNode(FormulaNodeKind.Missing));
                else
                    function.Children.Add(ParseComparison());

                var separator = Peek;
                if (separator == null)
                    throw new FormulaException($"{token.Value} 함수의 괄호가 닫히지 않았어요.");

                _position++;
                if (separator.Kind == FormulaTokenKind.CloseParen)
                    return function;

                if (separator.Kind != FormulaTokenKind.Comma)
                    throw new FormulaException($"‘{separator.Text}’ 부분을 알아볼 수 없어요.");
            }
        }

        private void Expect(FormulaTokenKind kind, string message)
        {
            if (Peek?.Kind != kind)
                throw new FormulaException(message);

            _position++;
        }

        private FormulaNode Binary(FormulaNode left, Func<FormulaNode> right)
        {
            var op = Peek.Value;
            _position++;
            var node = new FormulaNode(FormulaNodeKind.Binary) { Value = op };
            node.Children.Add(left);
            node.Children.Add(right());
            return node;
        }
    }
}
