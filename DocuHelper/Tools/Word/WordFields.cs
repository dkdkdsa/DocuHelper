using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordFields
    {
        public const int MaxInstructionLength = 255;

        private static readonly string[] s_allowed =
        {
            "SEQ", "REF", "PAGEREF", "NOTEREF", "STYLEREF", "SECTION", "SECTIONPAGES", "DOCPROPERTY", "DATE", "TIME",
            "PAGE", "NUMPAGES", "FILENAME", "AUTHOR", "TITLE", "SUBJECT", "MERGEFIELD", "SET", "IF",
            "SAVEDATE", "CREATEDATE", "PRINTDATE", "NUMWORDS", "NUMCHARS",
        };

        private static readonly string[] s_structural =
        {
            "TOC", "TC", "XE", "INDEX", "HYPERLINK", "BOOKMARK",
        };

        private static readonly string[] s_dangerous =
        {
            "INCLUDETEXT", "INCLUDEPICTURE", "INCLUDE", "LINK", "DDE", "DDEAUTO", "MACROBUTTON", "IMPORT", "AUTOTEXT", "AUTOTEXTLIST",
            "ASK", "FILLIN", "DATABASE", "EMBED", "GOTOBUTTON", "PRINT", "ADDIN",
        };

        private static readonly Regex s_name = new(@"^[A-Za-z]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex s_nested = new(@"\{\s*([^\s{}]*)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static IReadOnlyList<string> Allowed => s_allowed;

        public static IEnumerable<OpenXmlElement> Complex(string instruction, string result, RunProperties props)
        {
            var code = Code(instruction);
            var runs = new List<OpenXmlElement>
            {
                Marker(FieldCharValues.Begin, props),
                CodeRun(code, props),
                Marker(FieldCharValues.Separate, props),
            };

            var value = WordText.Clean(result);
            if (value.Length > 0)
                runs.Add(WordText.NewRun(value, Clone(props)));

            runs.Add(Marker(FieldCharValues.End, props));
            return runs;
        }

        public static string Name(string instruction)
        {
            var match = s_name.Match((instruction ?? string.Empty).Trim());
            return match.Success ? match.Value.ToUpperInvariant() : string.Empty;
        }

        public static bool IsStructural(string instruction) => s_structural.Contains(Name(instruction));

        public static IReadOnlyList<string> Tokenize(string instruction)
        {
            var tokens = new List<string>();
            var text = instruction ?? string.Empty;
            var current = new StringBuilder();
            var quoted = false;
            var has = false;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '\\' && i + 1 < text.Length && !quoted && text[i + 1] != ' ')
                {
                    if (has)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                        has = false;
                    }

                    current.Append(c).Append(text[++i]);
                    has = true;
                    continue;
                }

                if (c == '"')
                {
                    quoted = !quoted;
                    has = true;
                    continue;
                }

                if (char.IsWhiteSpace(c) && !quoted)
                {
                    if (has)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                        has = false;
                    }

                    continue;
                }

                current.Append(c);
                has = true;
            }

            if (has)
                tokens.Add(current.ToString());

            return tokens;
        }

        public static IReadOnlyList<WordField> Read(OpenXmlElement container)
        {
            var fields = new List<WordField>();
            var open = new Stack<Frame>();
            if (container == null)
                return fields;

            foreach (var element in container.Descendants())
            {
                switch (element)
                {
                    case FieldChar chars:
                        var type = chars.FieldCharType?.Value;
                        if (type == FieldCharValues.Begin)
                        {
                            var field = new WordField { Begin = chars, IsComplete = false };
                            fields.Add(field);
                            open.Push(new Frame(field));
                        }
                        else if (type == FieldCharValues.Separate && open.Count > 0)
                        {
                            open.Peek().Field.Separate = chars;
                            open.Peek().Separated = true;
                        }
                        else if (type == FieldCharValues.End && open.Count > 0)
                        {
                            var frame = open.Pop();
                            frame.Field.End = chars;
                            frame.Field.IsComplete = true;
                            Finish(frame);
                        }

                        break;
                    case FieldCode code:
                        if (open.Count > 0 && !open.Peek().Separated)
                            open.Peek().Instruction.Append(code.Text);

                        break;
                    case Text text:
                        foreach (var frame in open)
                        {
                            if (frame.Separated)
                                frame.Result.Append(text.Text);
                        }

                        break;
                    case SimpleField simple:
                        var instruction = (simple.Instruction?.Value ?? string.Empty).Trim();
                        fields.Add(new WordField { Simple = simple, Instruction = instruction, Name = Name(instruction), Result = WordText.Of(simple) });
                        break;
                }
            }

            foreach (var frame in open)
                Finish(frame);

            return fields;
        }

        public static bool IsAllowed(string instruction, out string reason)
        {
            reason = null;
            var text = (instruction ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                reason = "필드 코드가 비어 있어요.";
                return false;
            }

            if (text.Length > MaxInstructionLength)
            {
                reason = $"필드 코드는 {MaxInstructionLength}자까지만 쓸 수 있어요.";
                return false;
            }

            if (text.Any(c => char.IsControl(c)))
            {
                reason = "필드 코드에 줄 바꿈이나 제어 문자를 넣을 수 없어요.";
                return false;
            }

            var name = Name(text);
            if (name.Length == 0)
            {
                reason = "필드 코드는 SEQ, REF처럼 필드 이름으로 시작해야 해요. 계산식 필드는 지원하지 않아요.";
                return false;
            }

            if (!Check(name, out reason))
                return false;

            if (name == "IF")
            {
                foreach (Match nested in s_nested.Matches(text))
                {
                    var inner = Name(nested.Groups[1].Value);
                    if (inner.Length == 0)
                    {
                        reason = "IF 필드 안의 필드 이름을 알 수 없어요.";
                        return false;
                    }

                    if (!Check(inner, out reason))
                        return false;
                }
            }

            return true;
        }

        private static bool Check(string name, out string reason)
        {
            reason = null;
            if (s_dangerous.Contains(name))
            {
                reason = $"‘{name}’ 필드는 다른 파일이나 프로그램을 불러올 수 있어서 넣을 수 없어요.";
                return false;
            }

            if (!s_allowed.Contains(name))
            {
                reason = $"‘{name}’ 필드는 지원하지 않아요. 쓸 수 있는 필드: {string.Join(", ", s_allowed)}.";
                return false;
            }

            return true;
        }

        private sealed class Frame
        {
            public Frame(WordField field)
            {
                Field = field;
            }

            public WordField Field { get; }

            public bool Separated { get; set; }

            public StringBuilder Instruction { get; } = new();

            public StringBuilder Result { get; } = new();
        }

        private static void Finish(Frame frame)
        {
            frame.Field.Instruction = frame.Instruction.ToString().Trim();
            frame.Field.Name = Name(frame.Field.Instruction);
            frame.Field.Result = frame.Result.ToString();
        }

        private static string Code(string instruction)
        {
            var value = (instruction ?? string.Empty).Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim();
            if (value.Length == 0)
                throw new ToolInputException("필드 코드가 비어 있어요.");

            return value;
        }

        private static RunProperties Clone(RunProperties props) =>
            props == null || !props.HasChildren ? null : (RunProperties)props.CloneNode(true);

        private static Run Marker(FieldCharValues type, RunProperties props)
        {
            var run = new Run();
            var clone = Clone(props);
            if (clone != null)
                run.AppendChild(clone);

            run.AppendChild(new FieldChar { FieldCharType = type });
            return run;
        }

        private static Run CodeRun(string code, RunProperties props)
        {
            var run = new Run();
            var clone = Clone(props);
            if (clone != null)
                run.AppendChild(clone);

            run.AppendChild(new FieldCode(" " + code + " ") { Space = SpaceProcessingModeValues.Preserve });
            return run;
        }
    }
}
