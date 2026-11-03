using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DocuHelperEditor.Services;
using Microsoft.JSInterop;

namespace DocuHelperEditor.State.Ribbon
{
    public abstract class SpecRouter : RibbonRouter
    {
        protected const string NoChangeReason = "도우미가 바꾼 곳이 없어요.";

        private readonly Dictionary<string, CommandSpec> _specs = new Dictionary<string, CommandSpec>(StringComparer.Ordinal);
        private readonly Dictionary<string, Func<string, Task>> _provided = new Dictionary<string, Func<string, Task>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Func<string>> _providedBlockers = new Dictionary<string, Func<string>>(StringComparer.Ordinal);

        protected SpecRouter(WorkspaceStore workspace, AssistantStore assistant, IJSRuntime js)
            : base(workspace, assistant, js)
        {
        }

        protected IReadOnlyDictionary<string, CommandSpec> Specs => _specs;

        protected CommandHandlers Handlers { get; private set; } = new CommandHandlers();

        protected void Load(IEnumerable<CommandSpec> specs, CommandHandlers handlers)
        {
            Handlers = handlers;
            foreach (var spec in specs)
            {
                _specs[spec.Id] = spec;
                Register(spec);
                Require(spec, "pressed state", spec.Pressed, Handlers.Pressed);
                Require(spec, "value", spec.Value, Handlers.Values);
                Require(spec, "need", spec.Need, Handlers.Needs);
                Require(spec, "menu", spec.MenuStyle, Handlers.Menus);
            }
        }

        protected abstract void Register(CommandSpec spec);

        protected void Provide(string action, Func<string, Task> run, Func<string> blocker = null)
        {
            if (string.IsNullOrWhiteSpace(action) || run == null)
                throw new ArgumentException("A provided ribbon command needs an action and a handler.");

            if (!_specs.Values.Any(s => s.Action == action))
                throw new ArgumentException($"No ribbon command uses the action '{action}'.", nameof(action));

            _provided[action] = run;
            if (blocker != null)
                _providedBlockers[action] = blocker;

            RaiseChanged();
        }

        protected bool IsProvided(CommandSpec spec) => spec.Action != null && _provided.ContainsKey(spec.Action);

        protected Task RunProvidedAsync(CommandSpec spec, string arg)
        {
            return spec.Action != null && _provided.TryGetValue(spec.Action, out var run) ? run(arg) : Task.CompletedTask;
        }

        protected string ProvidedBlocker(CommandSpec spec) => _providedBlockers.TryGetValue(spec.Action, out var blocker) ? blocker() : null;

        protected bool IsPressed(CommandSpec spec) => spec.Pressed != null && Handlers.Pressed[spec.Pressed]();

        protected string NeedBlocker(CommandSpec spec) => NeedBlocker(spec.Need, spec);

        protected string NeedBlocker(string need, CommandSpec spec) => need == null ? null : Handlers.Needs[need](spec);

        protected RibbonCommand RegisterFile(CommandSpec spec)
        {
            var command = spec.Action switch
            {
                "undo" => AddUndo(spec.Id),
                "redo" => AddRedo(spec.Id),
                "save" => AddSave(spec.Id),
                "saveAs" => AddSaveCopy(spec.Id),
                "revertAll" => AddFile(spec.Id, _ => Workspace.RevertAllAsync(File.Id)).EnabledWhen(() => File.Changes.Count > 0, NoChangeReason),
                _ => AddFile(spec.Id, arg => RunProvidedAsync(spec, arg)).Unless(() => IsProvided(spec) || spec.Need != null ? NeedBlocker(spec) : PendingReason),
            };

            return spec.WhileLocked ? command.AllowWhileLocked() : command;
        }

        private static void Require<T>(CommandSpec spec, string what, string name, IReadOnlyDictionary<string, T> handlers)
        {
            if (name != null && handlers != null && !handlers.ContainsKey(name))
                throw new ArgumentException($"Ribbon command '{spec.Id}' names an unknown {what} '{name}'.");
        }

        protected sealed class CommandHandlers
        {
            public Dictionary<string, Func<bool>> Pressed { get; init; } = new Dictionary<string, Func<bool>>(StringComparer.Ordinal);

            public Dictionary<string, Func<string>> Values { get; init; } = new Dictionary<string, Func<string>>(StringComparer.Ordinal);

            public Dictionary<string, Func<CommandSpec, string>> Needs { get; init; } = new Dictionary<string, Func<CommandSpec, string>>(StringComparer.Ordinal);

            public Dictionary<string, Func<CommandSpec, IReadOnlyList<RibbonMenuItem>>> Menus { get; init; } = new Dictionary<string, Func<CommandSpec, IReadOnlyList<RibbonMenuItem>>>(StringComparer.Ordinal);

            public Dictionary<string, Func<CommandSpec, CommandMenuSpec, JsonObject, bool?>> Checks { get; init; } = new Dictionary<string, Func<CommandSpec, CommandMenuSpec, JsonObject, bool?>>(StringComparer.Ordinal);
        }
    }
}
