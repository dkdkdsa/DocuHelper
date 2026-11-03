using System;
using System.Collections.Generic;
using DocuHelper.Dash.Elements;
using DocuHelper.Dash.Logic;

namespace DocuHelper.Dash
{
    public sealed class DashCheck
    {
        private readonly List<string> _problems = new();
        private readonly Stack<string> _items = new();

        public DashCheck(DashDocument document)
        {
            Document = document;
        }

        public DashDocument Document { get; }

        public IReadOnlyList<string> Problems => _problems;

        public bool IsValid => _problems.Count == 0;

        public bool InItem => _items.Count > 0;

        public void Fail(string problem)
        {
            if (!string.IsNullOrWhiteSpace(problem))
                _problems.Add(problem);
        }

        public string Label(Element owner)
        {
            if (owner == null)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(owner.Name))
                return owner.Name;

            return string.IsNullOrWhiteSpace(owner.Id) ? owner.Kind : owner.Id;
        }

        public DashParam ParamOf(string name) => Document?.FindParam(name);

        public bool IsParam(string name) => ParamOf(name) != null;

        public bool IsValue(string name) => Document?.FindValue(name) != null;

        public bool IsTable(string name) => Document?.FindValue(name) is StepsTable;

        public bool IsSource(string name) => Document?.FindSource(name) != null;

        public bool IsRangePart(string name)
        {
            foreach (var suffix in new[] { RangeParam.StartSuffix, RangeParam.EndSuffix })
            {
                if (name.EndsWith(suffix, StringComparison.Ordinal) && ParamOf(name.Substring(0, name.Length - suffix.Length)) is RangeParam)
                    return true;
            }

            return false;
        }

        public bool IsColumnOf(string name)
        {
            var dot = name.LastIndexOf('.');
            return dot > 0 && (IsTable(name.Substring(0, dot)) || IsSource(name.Substring(0, dot)));
        }

        public void RequireName(Element owner, string name) => RequireName(Label(owner), name);

        public void RequireName(string ownerLabel, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                Fail($"‘{ownerLabel}’에서 쓸 값이 정해지지 않았어요.");
                return;
            }

            if (IsParam(name) || IsValue(name) || IsRangePart(name) || IsColumnOf(name) || IsSource(name) || (DashNames.IsItem(name) && InItem))
                return;

            Fail($"‘{ownerLabel}’에서 쓰는 ‘{name}’을 찾을 수 없어요.");
        }

        public void RequireTable(Element owner, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                Fail($"‘{Label(owner)}’에서 쓸 표가 정해지지 않았어요.");
                return;
            }

            if (!IsTable(name) && !IsSource(name))
                Fail($"‘{Label(owner)}’에서 쓰는 ‘{name}’ 표를 찾을 수 없어요.");
        }

        public void RequireParam(Element owner, string name, Type kind)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                Fail($"‘{Label(owner)}’에서 쓸 조절값이 정해지지 않았어요.");
                return;
            }

            var param = ParamOf(name);
            if (param == null)
                Fail($"‘{Label(owner)}’에서 쓰는 ‘{name}’ 조절값을 찾을 수 없어요.");
            else if (kind != null && !kind.IsInstanceOfType(param))
                Fail($"‘{Label(owner)}’에서 쓰는 ‘{name}’ 조절값은 {KindText(kind)} 종류가 아니에요.");
        }

        public void PushItem(string table)
        {
            _items.Push(table);
        }

        public void PopItem()
        {
            if (_items.Count > 0)
                _items.Pop();
        }

        private static string KindText(Type kind) => DashKinds.ParamName(kind) switch
        {
            "pick" => "고르기",
            "range" => "범위",
            "number" => "숫자",
            "bool" => "켜고 끄기",
            _ => kind.Name,
        };
    }
}
