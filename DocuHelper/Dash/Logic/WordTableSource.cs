using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using DocuHelper.Dash.Runtime;
using DocuHelper.Docus;
using DocuHelper.Tools.Word;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Dash.Logic
{
    public sealed class WordTableSource : DashSource
    {
        public int Table { get; set; } = 1;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool NoHeader { get; set; }

        public override DashTable Read(Docu docu)
        {
            if (docu is not WordDocu word)
                throw new DashException($"‘{File}’은 워드나 한글 파일이 아니에요.");

            var body = word.Document?.MainDocumentPart?.Document?.Body;
            var tables = body == null
                ? new List<W.Table>()
                : body.Descendants<W.Table>().Where(table => !table.Ancestors<W.Table>().Any()).ToList();

            if (Table < 1 || Table > tables.Count)
                throw new DashException(tables.Count == 0
                    ? $"‘{File}’ 파일에는 표가 없어요."
                    : $"‘{File}’ 파일에는 표가 {tables.Count}개뿐이라 {Table}번째 표를 가져올 수 없어요.");

            return DashTables.FromGrid(WordTables.Texts(WordTables.Grid(tables[Table - 1])), NoHeader);
        }
    }
}
