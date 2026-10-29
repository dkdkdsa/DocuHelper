using System.IO;

namespace DocuHelper.Docus
{
    public abstract class Docu(string filePath)
    {
        public string FilePath { get; } = filePath;

        public string Name => Path.GetFileName(FilePath);

        public abstract DocuType Type { get; }
    }
}
