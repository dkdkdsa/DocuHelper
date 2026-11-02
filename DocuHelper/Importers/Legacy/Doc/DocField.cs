using System.Text;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal sealed class DocField
    {
        private const int MaxInstruction = 4096;

        private readonly StringBuilder _instruction = new();

        public bool InResult { get; set; }

        public string Url { get; set; }

        public string Instruction => _instruction.ToString();

        public void Append(char c)
        {
            if (_instruction.Length < MaxInstruction)
                _instruction.Append(c);
        }
    }
}
