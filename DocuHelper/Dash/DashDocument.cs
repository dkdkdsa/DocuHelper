using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocuHelper.Dash.Elements;

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

        public DashCheck Check()
        {
            var check = new DashCheck(this);
            foreach (var element in Walk())
                element.Validate(check);

            return check;
        }

        public void Normalize()
        {
            Root ??= PageElement.A4Landscape();
            Styles ??= new Dictionary<string, DashStyle>(StringComparer.Ordinal);
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
    }
}
