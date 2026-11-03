using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocuHelper.Dash.Elements;
using DocuHelper.Dash.Logic;

namespace DocuHelper.Dash
{
    public sealed class DashDocument
    {
        public const string FileKind = "dash";
        public const int CurrentVersion = 1;

        public string Kind { get; set; } = FileKind;

        public int Version { get; set; } = CurrentVersion;

        public PageElement Root { get; set; }

        public Dictionary<string, DashStyle> Styles { get; set; } = new(StringComparer.Ordinal);

        public List<DashSource> Sources { get; set; } = new();

        public List<DashParam> Params { get; set; } = new();

        public List<DashValue> Values { get; set; } = new();

        public static DashDocument Blank() => new() { Root = PageElement.A4Landscape() };

        public IEnumerable<Element> Walk()
        {
            if (Root == null)
                yield break;

            var stack = new Stack<Element>();
            stack.Push(Root);
            while (stack.Count > 0)
            {
                var element = stack.Pop();
                yield return element;
                var nested = element.Nested().Where(child => child != null).ToList();
                for (var i = nested.Count - 1; i >= 0; i--)
                    stack.Push(nested[i]);
            }
        }

        public Element Find(string id) =>
            string.IsNullOrEmpty(id) ? null : Walk().FirstOrDefault(element => string.Equals(element.Id, id, StringComparison.Ordinal));

        public DashParam FindParam(string name) =>
            string.IsNullOrEmpty(name) ? null : Present(Params).FirstOrDefault(param => string.Equals(param.Name, name, StringComparison.Ordinal));

        public DashValue FindValue(string name) =>
            string.IsNullOrEmpty(name) ? null : Present(Values).FirstOrDefault(value => string.Equals(value.Name, name, StringComparison.Ordinal));

        public DashSource FindSource(string name) =>
            string.IsNullOrEmpty(name) ? null : Present(Sources).FirstOrDefault(source => string.Equals(source.Name, name, StringComparison.Ordinal));

        public DashCheck Check()
        {
            var check = new DashCheck(this);
            Root?.Validate(check);

            foreach (var source in Present(Sources))
                source.Validate(check);

            foreach (var param in Present(Params))
                param.Validate(check);

            foreach (var value in Present(Values))
                value.Validate(check);

            CheckDuplicates(check, Present(Params).Select(param => param.Name).Concat(Present(Values).Select(value => value.Name)));
            CheckDuplicates(check, Present(Sources).Select(source => source.Name));
            return check;
        }

        public void Normalize()
        {
            Root ??= PageElement.A4Landscape();
            Styles ??= new Dictionary<string, DashStyle>(StringComparer.Ordinal);
            Sources ??= new List<DashSource>();
            Params ??= new List<DashParam>();
            Values ??= new List<DashValue>();
            if (string.IsNullOrWhiteSpace(Root.Id))
                Root.Id = "page";

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var next = 1;
            foreach (var element in Walk())
            {
                if (!string.IsNullOrWhiteSpace(element.Id) && seen.Add(element.Id))
                    continue;

                string id;
                do
                {
                    id = "e" + next.ToString(CultureInfo.InvariantCulture);
                    next++;
                }
                while (seen.Contains(id));

                element.Id = id;
                seen.Add(id);
            }
        }

        public string NewId(string prefix = "e")
        {
            var taken = new HashSet<string>(Walk().Select(element => element.Id), StringComparer.Ordinal);
            for (var n = 1; ; n++)
            {
                var id = prefix + n.ToString(CultureInfo.InvariantCulture);
                if (!taken.Contains(id))
                    return id;
            }
        }

        private static IEnumerable<T> Present<T>(List<T> items) where T : class =>
            items == null ? Enumerable.Empty<T>() : items.Where(item => item != null);

        private static void CheckDuplicates(DashCheck check, IEnumerable<string> names)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var reported = new HashSet<string>(StringComparer.Ordinal);
            foreach (var name in names)
            {
                if (!string.IsNullOrWhiteSpace(name) && !seen.Add(name) && reported.Add(name))
                    check.Fail($"‘{name}’ 이름이 두 번 쓰였어요.");
            }
        }
    }
}
