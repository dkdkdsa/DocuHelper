using System;
using System.Collections.Generic;

namespace DocuHelperEditor.State.Ribbon
{
    internal sealed record CommandSpec
    {
        public string Id { get; init; }

        public string Label { get; init; }

        public RibbonCommandClass Class { get; init; }

        public string Action { get; init; }

        public string Tool { get; init; }

        public string Args { get; init; }

        public string ArgsOff { get; init; }

        public string Prompt { get; init; }

        public string SelectionPrompt { get; init; }

        public string Reason { get; init; }

        public string Need { get; init; }

        public string Pressed { get; init; }

        public IReadOnlyList<string> Gates { get; init; } = Array.Empty<string>();

        public IReadOnlyList<CommandMenuSpec> Menu { get; init; } = Array.Empty<CommandMenuSpec>();

        public DialogSpec Dialog { get; init; }
    }
}
