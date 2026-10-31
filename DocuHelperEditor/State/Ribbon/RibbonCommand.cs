using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DocuHelper.Editors;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed class RibbonCommand
    {
        public const string NotNowReason = "지금은 쓸 수 없어요.";

        private readonly List<string> _tools = new List<string>();
        private readonly List<Func<string>> _blockers = new List<Func<string>>();

        public RibbonCommand(string id, RibbonCommandClass kind)
        {
            Id = id;
            Class = kind;
        }

        public string Id { get; }

        public RibbonCommandClass Class { get; }

        public IReadOnlyList<string> Tools => _tools;

        public bool IsAllowedWhileLocked { get; private set; }

        public string DisabledText { get; internal set; }

        internal Func<string, IReadOnlyList<EditCommand>> Build { get; set; }

        internal Func<string, Task> Run { get; set; }

        internal Func<string> Prompt { get; set; }

        internal Func<bool> PressedPredicate { get; private set; }

        internal Func<string> ValueSource { get; private set; }

        internal Func<string> ColorSource { get; private set; }

        internal Func<IReadOnlyList<RibbonMenuItem>> MenuSource { get; private set; }

        public RibbonCommand EnabledWhen(Func<bool> predicate, string reason = null)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            _blockers.Add(() => predicate() ? null : (string.IsNullOrWhiteSpace(reason) ? NotNowReason : reason));
            return this;
        }

        public RibbonCommand Unless(Func<string> blocker)
        {
            if (blocker == null)
                throw new ArgumentNullException(nameof(blocker));

            _blockers.Add(blocker);
            return this;
        }

        public RibbonCommand PressedWhen(Func<bool> predicate)
        {
            PressedPredicate = predicate;
            return this;
        }

        public RibbonCommand ValueFrom(Func<string> value)
        {
            ValueSource = value;
            return this;
        }

        public RibbonCommand ColorFrom(Func<string> color)
        {
            ColorSource = color;
            return this;
        }

        public RibbonCommand MenuFrom(Func<IReadOnlyList<RibbonMenuItem>> menu)
        {
            MenuSource = menu;
            return this;
        }

        public RibbonCommand Needs(params string[] tools)
        {
            foreach (var tool in tools ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(tool) && !_tools.Contains(tool))
                    _tools.Add(tool);
            }

            return this;
        }

        public RibbonCommand AllowWhileLocked()
        {
            IsAllowedWhileLocked = true;
            return this;
        }

        internal string BlockedBy()
        {
            foreach (var blocker in _blockers)
            {
                var reason = blocker();
                if (!string.IsNullOrWhiteSpace(reason))
                    return reason.Trim();
            }

            return null;
        }
    }
}
