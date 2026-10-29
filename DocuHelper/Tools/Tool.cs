using System.Collections.Generic;
using DocuHelper.Docus;

namespace DocuHelper.Tools
{
    public abstract class Tool
    {
        public abstract string Name { get; }

        public abstract void Execute(Docu docu, IReadOnlyDictionary<string, string> arguments);
    }
}
