using DocuHelper.Dash;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Docus
{
    public class DashDocu : Docu
    {
        public DashDocu(string filePath, byte[] content)
            : base(filePath)
        {
            Load(content);
        }

        public override DocuType Type => DocuType.Dash;

        public DashDocument Document { get; private set; }

        public IDashDocuments Documents { get; set; }

        public override byte[] ToBytes() => DashFile.Write(Document);

        public override void Load(byte[] content) => Document = DashFile.Read(content);
    }
}
