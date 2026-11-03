using System.Collections.Generic;
using System.Linq;

namespace DocuHelper.Dash.Runtime
{
    public class TextResult : ElementResult
    {
        public List<TextRun> Runs { get; set; } = new();

        public string Text => string.Concat(Runs.Select(run => run.Text));
    }
}
