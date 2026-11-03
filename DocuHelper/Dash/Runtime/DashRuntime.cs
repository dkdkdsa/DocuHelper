using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Dash.Elements;
using DocuHelper.Dash.Logic;

namespace DocuHelper.Dash.Runtime
{
    public sealed class DashRuntime
    {
        private const string WhenKey = "w:";
        private const string ValueKey = "v:";
        private const string SourceKey = "@";

        private readonly Dictionary<string, DashTable> _tables = new(StringComparer.Ordinal);
        private readonly HashSet<string> _computing = new(StringComparer.Ordinal);
        private readonly List<string> _problems = new();
        private readonly Dictionary<string, object> _results = new(StringComparer.Ordinal);
        private bool _calculated;

        public DashRuntime(DashDocument document, IDashDocuments documents, DashState previous = null)
        {
            Document = document ?? DashDocument.Blank();
            Documents = documents;
            State = DashState.Start(Document, previous);
        }

        public DashDocument Document { get; }

        public IDashDocuments Documents { get; }

        public DashState State { get; }

        public IReadOnlyList<string> Problems => _problems;

        public void Note(string problem)
        {
            if (!string.IsNullOrWhiteSpace(problem) && !_problems.Contains(problem))
                _problems.Add(problem);
        }

        public void Set(string name, object value)
        {
            var param = Document.FindParam(name);
            if (param == null)
                return;

            State.Set(param, value);
            _calculated = false;
        }

        public void Reset(IEnumerable<string> names = null)
        {
            State.Reset(Document, names);
            _calculated = false;
        }

        public void Apply(DashAction action, IReadOnlyDictionary<string, object> item = null)
        {
            switch (action)
            {
                case SetAction set:
                    var text = set.Value is System.Text.Json.Nodes.JsonValue node && node.GetValueKind() == System.Text.Json.JsonValueKind.String ? node.GetValue<string>() : null;
                    var column = DashNames.ItemColumn(text);
                    if (column != null)
                        Set(set.Param, item != null && item.TryGetValue(column, out var picked) ? picked : null);
                    else
                        Set(set.Param, set.Value);
                    break;
                case ResetAction reset:
                    Reset(reset.Params);
                    break;
            }
        }

        public ElementResult Render()
        {
            _problems.Clear();
            Calculate();
            return Document.Root.Resolve(new DashScope(this));
        }

        public DashTable Table(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new DashException("표 이름이 비어 있어요.");

            var source = Document.FindSource(name);
            if (source != null)
                return Source(source);

            if (Document.FindValue(name) is not StepsTable table)
                throw new DashException($"‘{name}’ 표를 찾을 수 없어요.");

            var key = name + "#" + State.Signature(ParamsOf(name, new HashSet<string>(StringComparer.Ordinal)));
            if (_tables.TryGetValue(key, out var cached))
                return cached;

            if (!_computing.Add(name))
                throw new DashException($"‘{name}’ 표가 돌고 돌아 자기 자신을 다시 가져오고 있어요.");

            try
            {
                var result = table.Compute(this);
                _tables[key] = result;
                return result;
            }
            finally
            {
                _computing.Remove(name);
            }
        }

        public DashTable TryTable(string name)
        {
            try
            {
                return Table(name);
            }
            catch (DashException e)
            {
                Note(e.Message);
                return null;
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                Note($"‘{name}’ 표를 셈하지 못했어요. {e.Message}");
                return null;
            }
        }

        public DashTable Preview(string name, int steps)
        {
            try
            {
                return Document.FindValue(name) is StepsTable table ? table.Compute(this, steps) : Table(name);
            }
            catch (DashException e)
            {
                Note(e.Message);
                return null;
            }
        }

        public IReadOnlyList<string> OptionsOf(PickParam param)
        {
            var options = new List<string>();
            if (param == null)
                return options;

            if (!string.IsNullOrWhiteSpace(param.All))
                options.Add(param.All.Trim());

            if (!string.IsNullOrWhiteSpace(param.OptionsFrom))
            {
                var dot = param.OptionsFrom.LastIndexOf('.');
                var table = dot > 0 ? TryTable(param.OptionsFrom.Substring(0, dot)) : null;
                var column = dot > 0 ? param.OptionsFrom.Substring(dot + 1) : null;
                var index = table?.IndexOf(column) ?? -1;
                if (index >= 0)
                    options.AddRange(table.Rows.Select(row => DashCells.Text(DashTable.Get(row, index)).Trim()).Where(text => text.Length > 0));
                else
                    Note($"‘{param.Name}’ 조절값이 고를 것을 가져올 ‘{param.OptionsFrom}’ 칸을 찾을 수 없어요.");
            }
            else if (param.Options != null)
            {
                options.AddRange(param.Options.Where(option => !string.IsNullOrWhiteSpace(option)).Select(option => option.Trim()));
            }

            return options.Distinct(StringComparer.Ordinal).ToList();
        }

        public bool Exists(string name) =>
            !string.IsNullOrWhiteSpace(name) && (State.Has(name) || Document.FindValue(name) != null || RangePart(name) != null);

        public object Value(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            if (State.Has(name))
                return State.Get(name);

            var part = RangePart(name);
            if (part != null)
                return part;

            var value = Document.FindValue(name);
            if (value == null || value is StepsTable)
                return null;

            if (value is ExpressionValue)
            {
                Calculate();
                return _results.TryGetValue(ValueKey + name, out var result) ? result : null;
            }

            try
            {
                return value.Compute(this);
            }
            catch (DashException e)
            {
                Note(e.Message);
                return null;
            }
        }

        public string Display(string name)
        {
            var param = Document.FindParam(name);
            if (param != null)
                return param.Display(State.Get(name));

            var range = RangeParamOf(name);
            if (range != null)
                return DashFormats.Display(Value(name), range.Format);

            return DashFormats.Display(Value(name), Document.FindValue(name)?.Format);
        }

        public bool IsVisible(Element element, int row = -1)
        {
            if (element == null || element.Hidden)
                return false;

            if (string.IsNullOrWhiteSpace(element.When))
                return true;

            Calculate();
            if (row >= 0 && _results.TryGetValue(WhenKey + element.Id + "#" + row.ToString(System.Globalization.CultureInfo.InvariantCulture), out var perRow))
                return DashCells.Truthy(perRow);

            return _results.TryGetValue(WhenKey + element.Id, out var result) && DashCells.Truthy(result);
        }

        public DashTable EvaluateRows(DashTable table, string expression, string column)
        {
            var result = table.Copy();
            var index = result.IndexOf(column);
            if (index < 0)
            {
                result.Columns.Add(column);
                index = result.Columns.Count - 1;
                for (var i = 0; i < result.Rows.Count; i++)
                    result.Rows[i] = DashTable.Widen(result.Rows[i], result.Columns.Count);
            }

            if (table.Rows.Count == 0)
                return result;

            using var host = new DashFormulaHost();
            AddConstants(host);
            var sheet = host.Table(null, table);
            for (var row = 0; row < table.Rows.Count; row++)
            {
                var line = row + 2;
                var text = DashFormulaText.ReplaceNames(expression, name =>
                {
                    var at = table.IndexOf(name);
                    return at < 0 ? null : "'" + sheet + "'!" + Tools.Excel.CellAddress.ColumnName(at) + line.ToString(System.Globalization.CultureInfo.InvariantCulture);
                });
                host.Formula("r" + row.ToString(System.Globalization.CultureInfo.InvariantCulture), text);
            }

            host.Run();
            for (var row = 0; row < result.Rows.Count; row++)
                result.Rows[row][index] = host.Result("r" + row.ToString(System.Globalization.CultureInfo.InvariantCulture));

            return result;
        }

        private DashTable Source(DashSource source)
        {
            var key = SourceKey + source.Name;
            if (_tables.TryGetValue(key, out var cached))
                return cached;

            var docu = Documents?.Open(source.File)
                ?? throw new DashException($"‘{source.File}’ 파일을 작업 폴더에서 찾을 수 없어요.");
            var table = source.Read(docu);
            _tables[key] = table;
            return table;
        }

        private IEnumerable<string> ParamsOf(string tableName, HashSet<string> seen)
        {
            if (!seen.Add(tableName) || Document.FindValue(tableName) is not StepsTable table)
                yield break;

            foreach (var name in table.Uses())
            {
                if (Document.FindParam(name) != null)
                {
                    yield return name;
                    continue;
                }

                if (Document.FindValue(name) is StepsTable)
                {
                    foreach (var inner in ParamsOf(name, seen))
                        yield return inner;
                }
            }
        }

        private RangeParam RangeParamOf(string name)
        {
            if (name == null)
                return null;

            foreach (var suffix in new[] { RangeParam.StartSuffix, RangeParam.EndSuffix })
            {
                if (name.EndsWith(suffix, StringComparison.Ordinal) && Document.FindParam(name.Substring(0, name.Length - suffix.Length)) is RangeParam range)
                    return range;
            }

            return null;
        }

        private object RangePart(string name)
        {
            var param = RangeParamOf(name);
            if (param == null || State.Get(param.Name) is not DashRange range)
                return null;

            return name.EndsWith(RangeParam.StartSuffix, StringComparison.Ordinal) ? range.From : range.To;
        }

        private void AddConstants(DashFormulaHost host)
        {
            foreach (var param in Document.Params.Where(param => param != null && !string.IsNullOrWhiteSpace(param.Name)))
            {
                var value = State.Get(param.Name);
                if (value is DashRange range)
                {
                    host.Constant(param.Name + RangeParam.StartSuffix, range.From);
                    host.Constant(param.Name + RangeParam.EndSuffix, range.To);
                }
                else
                {
                    host.Constant(param.Name, value);
                }
            }

            foreach (var value in Document.Values.Where(value => value is PickValue or FixedValue && !string.IsNullOrWhiteSpace(value.Name)))
                host.Constant(value.Name, Value(value.Name));
        }

        private void Calculate()
        {
            if (_calculated)
                return;

            _calculated = true;
            _results.Clear();
            var expressions = Document.Values.OfType<ExpressionValue>().Where(value => !string.IsNullOrWhiteSpace(value.Name)).ToList();
            var conditions = Conditions().ToList();
            if (expressions.Count == 0 && conditions.Count == 0)
                return;

            try
            {
                using var host = new DashFormulaHost();
                AddConstants(host);
                var names = expressions.SelectMany(value => value.Uses())
                    .Concat(conditions.SelectMany(condition => DashNames.InExpression(condition.Expression)));
                foreach (var table in TablesIn(names))
                {
                    var data = TryTable(table);
                    if (data != null)
                        host.Table(table, data);
                }

                foreach (var value in expressions)
                    host.Formula(ValueKey + value.Name, value.Expression, value.Name);

                foreach (var condition in conditions)
                    host.Formula(condition.Key, condition.Expression);

                host.Run();
                foreach (var value in expressions)
                    _results[ValueKey + value.Name] = host.Result(ValueKey + value.Name);

                foreach (var condition in conditions)
                    _results[condition.Key] = host.Result(condition.Key);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                Note($"식을 셈하지 못했어요. {e.Message}");
            }
        }

        private IEnumerable<string> TablesIn(IEnumerable<string> names)
        {
            var tables = new List<string>();
            foreach (var name in names.Where(name => !string.IsNullOrWhiteSpace(name)))
            {
                if (IsTableName(name))
                    tables.Add(name);

                var dot = name.LastIndexOf('.');
                if (dot > 0 && IsTableName(name.Substring(0, dot)))
                    tables.Add(name.Substring(0, dot));
            }

            return tables.Distinct(StringComparer.Ordinal);
        }

        private bool IsTableName(string name) => Document.FindSource(name) != null || Document.FindValue(name) is StepsTable;

        private IEnumerable<(string Key, string Expression)> Conditions()
        {
            var found = new List<(string, string)>();
            Collect(Document.Root, null, found);
            return found;
        }

        private void Collect(Element element, RepeatElement repeat, List<(string Key, string Expression)> found)
        {
            if (element == null)
                return;

            if (!string.IsNullOrWhiteSpace(element.When))
            {
                var usesItem = DashNames.InExpression(element.When).Any(DashNames.IsItem);
                var table = usesItem && repeat != null ? TryTable(repeat.Data) : null;
                if (table == null)
                {
                    found.Add((WhenKey + element.Id, element.When));
                }
                else
                {
                    for (var row = 0; row < table.Rows.Count; row++)
                    {
                        var values = table.Rows[row];
                        var expression = DashFormulaText.ReplaceNames(element.When, name =>
                            DashNames.IsItem(name) ? DashFormulaText.Literal(table.Get(values, DashNames.ItemColumn(name))) : null);
                        found.Add((WhenKey + element.Id + "#" + row.ToString(System.Globalization.CultureInfo.InvariantCulture), expression));
                    }
                }
            }

            var inner = element as RepeatElement ?? repeat;
            foreach (var child in element.Nested())
                Collect(child, inner, found);
        }
    }
}
