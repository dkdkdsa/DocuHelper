using System.Collections.Generic;
using DocuHelper.Docus;
using DocuHelper.Importers.Hwp;

namespace DocuHelper.Tools.Word
{
    public abstract class WordTool : Tool
    {
        public sealed override ToolResult Execute(Docu docu, ToolInput input)
        {
            if (docu is not WordDocu word || word.Document?.MainDocumentPart?.Document?.Body == null)
                return ToolResult.Fail("이 파일은 워드 문서로 열 수 없어요.");

            var refusal = WordProtection.Check(word, this);
            if (refusal != null)
                return ToolResult.Fail(refusal);

            if (word is HwpDocu && Hwp == WordHwpSupport.None)
                return ToolResult.Fail("한글 파일에서는 이 기능을 쓸 수 없어요.");

            return Run(word, input);
        }

        public WordHwpSupport HwpSupport => Hwp;

        protected virtual WordHwpSupport Hwp => WordHwpSupport.Full;

        protected abstract ToolResult Run(WordDocu docu, ToolInput input);

        protected IReadOnlyList<WordRange> Targets(WordScan scan, ToolInput input, string name = "targets") =>
            WordRangeTarget.Resolve(scan, input, name);

        protected static void RefuseInHwp(WordDocu docu, string feature, bool used = true)
        {
            if (used)
                HwpGuard.Refuse(docu, feature);
        }

        protected static bool TryParseStyle(string value, out WordKind kind)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant().Replace(" ", string.Empty).Replace("_", string.Empty))
            {
                case "":
                case "body":
                case "normal":
                case "text":
                case "paragraph":
                    kind = WordKind.Body;
                    return true;
                case "title":
                    kind = WordKind.Title;
                    return true;
                case "heading1":
                case "h1":
                    kind = WordKind.Heading1;
                    return true;
                case "heading2":
                case "h2":
                    kind = WordKind.Heading2;
                    return true;
                case "bullet":
                case "bullets":
                    kind = WordKind.Bullet;
                    return true;
                case "number":
                case "numbered":
                    kind = WordKind.Number;
                    return true;
                default:
                    kind = WordKind.Body;
                    return false;
            }
        }

        protected static string ResolveStyle(WordScan scan, string value, string name)
        {
            if (TryParseStyle(value, out var kind))
            {
                switch (kind)
                {
                    case WordKind.Bullet:
                    case WordKind.Number:
                        throw new ToolInputException($"'{name}' 값으로 목록은 만들 수 없어요. 글머리 기호와 번호는 set_list로 만들어 주세요.");
                    case WordKind.Body:
                        return WordBuiltinStyles.Ensure(scan.Styles, "Normal");
                    default:
                        return scan.Styles.EnsureStyle(kind);
                }
            }

            return WordBuiltinStyles.Ensure(scan.Styles, value);
        }

        protected static WordKind ParseStyle(string value, string name) =>
            TryParseStyle(value, out var kind)
                ? kind
                : throw new ToolInputException($"'{name}' 값은 body, heading1, heading2, bullet, number 중 하나여야 해요.");

        protected static string KindName(WordKind kind) => kind switch
        {
            WordKind.Title => "title",
            WordKind.Heading1 => "heading1",
            WordKind.Heading2 => "heading2",
            WordKind.Bullet => "bullet",
            WordKind.Number => "number",
            WordKind.Table => "table",
            _ => "body",
        };

        protected static ToolInputException NotFound(string id) =>
            new($"‘{WordIds.Normalize(id) ?? id}’ 문단을 찾지 못했어요. read_paragraphs로 id를 다시 확인해 주세요.");

        protected static DocuChange Change(string location, string icon, IEnumerable<string> anchors)
        {
            var change = new DocuChange { Location = location, Icon = icon };
            change.Anchors.AddRange(anchors);
            return change;
        }
    }
}
