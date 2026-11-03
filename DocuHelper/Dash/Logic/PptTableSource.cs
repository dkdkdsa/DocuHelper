using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using DocuHelper.Dash.Runtime;
using DocuHelper.Docus;
using DocuHelper.Tools.Ppt;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Dash.Logic
{
    public sealed class PptTableSource : DashSource
    {
        public int Slide { get; set; } = 1;

        public int Table { get; set; } = 1;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool NoHeader { get; set; }

        public override DashTable Read(Docu docu)
        {
            if (docu is not PptDocu ppt)
                throw new DashException($"‘{File}’은 파워포인트 파일이 아니에요.");

            var slides = PptSlides.List(ppt.Document);
            if (Slide < 1 || Slide > slides.Count)
                throw new DashException($"‘{File}’ 파일에서 {Slide}번 슬라이드를 찾을 수 없어요. 슬라이드는 {slides.Count}장이에요.");

            var tables = PptTargets.All(slides[Slide - 1].Tree)
                .OfType<P.GraphicFrame>()
                .Select(frame => frame.Graphic?.GraphicData?.GetFirstChild<A.Table>())
                .Where(table => table != null)
                .ToList();

            if (Table < 1 || Table > tables.Count)
                throw new DashException(tables.Count == 0
                    ? $"{Slide}번 슬라이드에는 표가 없어요."
                    : $"{Slide}번 슬라이드에는 표가 {tables.Count}개뿐이라 {Table}번째 표를 가져올 수 없어요.");

            var grid = tables[Table - 1].Elements<A.TableRow>()
                .Select(row => (IReadOnlyList<string>)row.Elements<A.TableCell>().Select(cell => PptTableCells.TextOf(cell).Trim()).ToList())
                .ToList();

            return DashTables.FromGrid(grid, NoHeader);
        }
    }
}
