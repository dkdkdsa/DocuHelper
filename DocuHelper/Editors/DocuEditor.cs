using System;
using System.Collections.Generic;
using DocuHelper.Docus;
using DocuHelper.Tools;

namespace DocuHelper.Editors
{
    public abstract class DocuEditor
    {
        private readonly Dictionary<string, Tool> _tools = new();

        public abstract DocuType TargetType { get; }

        public IReadOnlyDictionary<string, Tool> Tools => _tools;

        public abstract string Read(Docu docu);

        public void Apply(Docu docu, EditCommand command)
        {
            if (!_tools.TryGetValue(command.Name, out Tool tool))
                throw new InvalidOperationException($"{TargetType} 에디터에 {command.Name} 툴이 없습니다.");

            tool.Execute(docu, command.Arguments);
        }

        public abstract void Save(Docu docu);

        protected void AddTool(Tool tool) => _tools[tool.Name] = tool;
    }
}
