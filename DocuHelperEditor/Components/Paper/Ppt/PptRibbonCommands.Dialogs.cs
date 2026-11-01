using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using DocuHelper.Editors;
using DocuHelper.Tools;

namespace DocuHelperEditor.Components.Paper.Ppt
{
    public sealed partial class PptRibbonCommands
    {
        private Func<IReadOnlyDictionary<string, JsonNode>, string> ValidatorOf(PptCommandSpec spec, PptMenuSpec entry, string arg)
        {
            if (KindOf(spec, entry) != PptCommandKind.Tool)
                return null;

            return values =>
            {
                foreach (var pair in values)
                    _fields[pair.Key] = pair.Value;

                try
                {
                    var command = PlanTool(spec, entry, arg);
                    return command == null ? null : DryRunError(command);
                }
                catch (ToolInputException e)
                {
                    return e.Message;
                }
            };
        }

        private string DryRunError(EditCommand command)
        {
            var docu = File?.Docu;
            if (docu == null || !Workspace.Workspace.Editors.TryGetValue(File.Type, out var editor))
                return null;

            var state = docu.Capture();
            try
            {
                var result = editor.Apply(docu, command);
                return result.IsError ? result.Content : null;
            }
            finally
            {
                docu.Restore(state);
            }
        }
    }
}
