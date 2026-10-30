using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DocuHelper.Docus;
using DocuHelper.Tools;

namespace DocuHelper.Editors
{
    public abstract class DocuEditor
    {
        private readonly Dictionary<string, Tool> _tools = new();

        public abstract DocuType TargetType { get; }

        public abstract string Extension { get; }

        public IReadOnlyDictionary<string, Tool> Tools => _tools;

        public abstract string Describe(Docu docu);

        public abstract Docu CreateBlank(string filePath);

        public ToolResult Apply(Docu docu, EditCommand command)
        {
            if (!_tools.TryGetValue(command.Name, out var tool))
                return ToolResult.Fail($"'{command.Name}' 도구가 없어요.");

            if (!tool.IsReadOnly && docu.IsLocked)
                return ToolResult.Fail("도우미가 이 파일을 고치는 중이에요. 끝나면 다시 고칠 수 있어요.");

            var result = Run(docu, tool, command.Arguments);
            if (result.IsError || tool.IsReadOnly)
                return result;

            if (result.Change != null)
                docu.DropAnchors(result.Change.Anchors);

            docu.MarkDirty();
            return result;
        }

        public ToolResult Execute(Docu docu, string toolName, JsonElement arguments)
        {
            if (!_tools.TryGetValue(toolName, out var tool))
                return ToolResult.Fail($"'{toolName}' 도구가 없어요.");

            return Run(docu, tool, arguments);
        }

        public void Save(Docu docu) => File.WriteAllBytes(docu.FilePath, docu.ToBytes());

        protected void AddTool(Tool tool) => _tools[tool.Name] = tool;

        private static ToolResult Run(Docu docu, Tool tool, JsonElement arguments)
        {
            try
            {
                return tool.Execute(docu, new ToolInput(arguments));
            }
            catch (ToolInputException e)
            {
                return ToolResult.Fail(e.Message);
            }
            catch (Exception e)
            {
                return ToolResult.Fail($"'{tool.Name}' 도구를 쓰다가 문제가 생겼어요: {e.Message}");
            }
        }
    }
}
