using System.Collections.Generic;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Word
{
    public abstract class WordTool : Tool
    {
        public sealed override ToolResult Execute(Docu docu, ToolInput input)
        {
            if (docu is not WordDocu word || word.Document?.MainDocumentPart?.Document?.Body == null)
                return ToolResult.Fail("이 파일은 워드 문서로 열 수 없어요.");

            return Run(word, input);
        }

        protected abstract ToolResult Run(WordDocu docu, ToolInput input);

        protected static WordKind ParseStyle(string value, string name)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant().Replace(" ", string.Empty).Replace("_", string.Empty))
            {
                case "":
                case "body":
                case "normal":
                case "text":
                case "paragraph":
                    return WordKind.Body;
                case "title":
                    return WordKind.Title;
                case "heading1":
                case "h1":
                    return WordKind.Heading1;
                case "heading2":
                case "h2":
                    return WordKind.Heading2;
                case "bullet":
                case "bullets":
                    return WordKind.Bullet;
                case "number":
                case "numbered":
                    return WordKind.Number;
                default:
                    throw new ToolInputException($"'{name}' 값은 body, heading1, heading2, bullet, number 중 하나여야 해요.");
            }
        }

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
