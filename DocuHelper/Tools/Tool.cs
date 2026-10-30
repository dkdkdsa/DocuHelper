using DocuHelper.Docus;

namespace DocuHelper.Tools
{
    public abstract class Tool
    {
        public abstract string Name { get; }

        public abstract string Description { get; }

        public abstract string InputSchema { get; }

        public virtual bool IsReadOnly => false;

        public abstract ToolResult Execute(Docu docu, ToolInput input);
    }
}
