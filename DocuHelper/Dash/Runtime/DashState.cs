using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Dash.Logic;

namespace DocuHelper.Dash.Runtime
{
    public sealed class DashState
    {
        private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);
        private readonly Dictionary<string, IReadOnlyDictionary<string, object>> _popups = new(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, object> Values => _values;

        public static DashState Start(DashDocument document, DashState previous = null)
        {
            var state = new DashState();
            foreach (var param in document?.Params ?? new List<DashParam>())
            {
                if (param == null || string.IsNullOrWhiteSpace(param.Name))
                    continue;

                state._values[param.Name] = previous != null && previous._values.TryGetValue(param.Name, out var kept)
                    ? param.Normalize(kept)
                    : param.InitialValue();
            }

            if (previous != null)
            {
                foreach (var pair in previous._popups)
                {
                    if (document?.Find(pair.Key) is Elements.PopupElement)
                        state._popups[pair.Key] = pair.Value;
                }
            }

            return state;
        }

        public bool IsOpen(string id) => id != null && _popups.ContainsKey(id);

        public IReadOnlyDictionary<string, object> PopupItem(string id) => id != null && _popups.TryGetValue(id, out var item) ? item : null;

        public void Open(string id, IReadOnlyDictionary<string, object> item)
        {
            if (string.IsNullOrWhiteSpace(id))
                return;

            _popups[id.Trim()] = item == null
                ? new Dictionary<string, object>(StringComparer.Ordinal)
                : new Dictionary<string, object>(item, StringComparer.Ordinal);
        }

        public void Close(string id = null)
        {
            if (id == null)
                _popups.Clear();
            else
                _popups.Remove(id.Trim());
        }

        public object Get(string name) => name != null && _values.TryGetValue(name, out var value) ? value : null;

        public bool Has(string name) => name != null && _values.ContainsKey(name);

        public void Set(DashParam param, object value)
        {
            if (param == null || string.IsNullOrWhiteSpace(param.Name))
                return;

            _values[param.Name] = param.Normalize(value);
        }

        public void Reset(DashDocument document, IEnumerable<string> names = null)
        {
            var only = names == null ? null : new HashSet<string>(names, StringComparer.Ordinal);
            foreach (var param in document?.Params ?? new List<DashParam>())
            {
                if (param == null || string.IsNullOrWhiteSpace(param.Name) || (only != null && !only.Contains(param.Name)))
                    continue;

                _values[param.Name] = param.InitialValue();
            }
        }

        public bool IsInitial(DashDocument document) =>
            (document?.Params ?? new List<DashParam>())
                .Where(param => param != null && !string.IsNullOrWhiteSpace(param.Name))
                .All(param => Equals(Get(param.Name), param.InitialValue()));

        public string Signature(IEnumerable<string> names) =>
            string.Join("|", (names ?? Enumerable.Empty<string>())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .Select(name => name + "=" + DashCells.Text(Get(name) is DashRange range ? range.ToString() : Get(name))));
    }
}
