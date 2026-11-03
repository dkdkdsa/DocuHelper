using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using DocuHelper.Dash.Elements;

namespace DocuHelper.Dash.Runtime
{
    public sealed class DashScope
    {
        private static readonly Regex s_template = new("«([^«»]*)»", RegexOptions.CultureInvariant);

        public DashScope(DashRuntime runtime)
        {
            Runtime = runtime;
            Document = runtime.Document;
        }

        private DashScope(DashScope parent, DashTable table, int row)
        {
            Runtime = parent.Runtime;
            Document = parent.Document;
            ItemTable = table;
            ItemRow = row;
            var index = row.ToString(CultureInfo.InvariantCulture);
            Suffix = parent.Suffix == null ? index : parent.Suffix + "." + index;
        }

        public DashRuntime Runtime { get; }

        public DashDocument Document { get; }

        public DashTable ItemTable { get; }

        public int ItemRow { get; } = -1;

        public string Suffix { get; }

        public object[] Item => ItemTable != null && ItemRow >= 0 && ItemRow < ItemTable.Rows.Count ? ItemTable.Rows[ItemRow] : null;

        public DashScope ForItem(DashTable table, int row) => new(this, table, row);

        public Dictionary<string, object> ItemValues()
        {
            var values = new Dictionary<string, object>(System.StringComparer.Ordinal);
            if (ItemTable == null || Item == null)
                return values;

            for (var i = 0; i < ItemTable.Columns.Count; i++)
                values[ItemTable.Columns[i]] = DashTable.Get(Item, i);

            return values;
        }

        public T Base<T>(Element element, T result) where T : ElementResult
        {
            result.Source = element;
            result.Key = Suffix == null ? element.Id : element.Id + "#" + Suffix;
            result.Style = StyleOf(element);
            result.Visible = Runtime.IsVisible(element, ItemRow);
            return result;
        }

        public DashStyle StyleOf(Element element)
        {
            var style = new DashStyle();
            if (element.Classes != null && Document?.Styles != null)
            {
                foreach (var name in element.Classes)
                {
                    if (name != null && Document.Styles.TryGetValue(name, out var rule))
                        style = style.Merge(rule);
                }
            }

            return style.Merge(element.Style);
        }

        public bool Has(string name)
        {
            if (!DashNames.IsItem(name))
                return Runtime.Exists(name);

            var column = DashNames.ItemColumn(name);
            return column != null && ItemTable != null && ItemTable.Has(column);
        }

        public object Value(string name)
        {
            if (!DashNames.IsItem(name))
                return Runtime.Value(name);

            var column = DashNames.ItemColumn(name);
            return column == null || ItemTable == null ? null : ItemTable.Get(Item, column);
        }

        public string Display(string name) => DashNames.IsItem(name) ? DashFormats.Display(Value(name), null) : Runtime.Display(name);

        public DashTable Table(string name) => string.IsNullOrWhiteSpace(name) ? null : Runtime.TryTable(name);

        public List<TextRun> Fill(string template)
        {
            var runs = new List<TextRun>();
            if (string.IsNullOrEmpty(template))
                return runs;

            var position = 0;
            string previous = null;
            foreach (Match match in s_template.Matches(template))
            {
                if (match.Index > position)
                {
                    runs.Add(Plain(template.Substring(position, match.Index - position), previous));
                    previous = null;
                }

                var name = match.Groups[1].Value.Trim();
                if (Has(name))
                {
                    var text = Display(name);
                    runs.Add(new TextRun(TextRunKind.Value, text, name));
                    previous = text;
                }
                else
                {
                    runs.Add(new TextRun(TextRunKind.Missing, match.Value, name));
                    previous = null;
                }

                position = match.Index + match.Length;
            }

            if (position < template.Length)
                runs.Add(Plain(template.Substring(position), previous));

            return runs;
        }

        public string FillText(string template) => string.Concat(Fill(template).Select(run => run.Text));

        private static TextRun Plain(string text, string previous) =>
            new(TextRunKind.Plain, previous == null ? text : KoreanParticles.Attach(previous, text));
    }
}
