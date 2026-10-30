using System;
using System.IO;
using DocuHelper.Docus;
using DocuHelper.Importers.Hwp;
using DocuHelper.Tools.Word;

namespace DocuHelper.Editors
{
    public class HwpEditor : WordEditor
    {
        public HwpEditor(IHancomOffice hancom)
        {
            Hancom = hancom;
        }

        public IHancomOffice Hancom { get; }

        public bool AlwaysHwpx { get; set; }

        public bool NewAsHwp { get; set; }

        public bool CanWriteHwp => Hancom?.IsAvailable == true;

        public override DocuType TargetType => DocuType.Hwp;

        public override string Extension => NewAsHwp && CanWriteHwp ? ".hwp" : ".hwpx";

        public override bool Accepts(string extension) =>
            string.Equals(extension, ".hwpx", StringComparison.OrdinalIgnoreCase) || string.Equals(extension, ".hwp", StringComparison.OrdinalIgnoreCase);

        public override Docu CreateBlank(string filePath)
        {
            var path = HwpDocu.IsHwpPath(filePath) && !CanWriteHwp ? Path.ChangeExtension(filePath, ".hwpx") : filePath;
            return new HwpDocu(path, WordTemplate.Blank());
        }

        public override string SavePath(Docu docu)
        {
            if (!HwpDocu.IsHwpPath(docu.FilePath) || (!AlwaysHwpx && CanWriteHwp))
                return docu.FilePath;

            return Path.ChangeExtension(docu.FilePath, ".hwpx");
        }

        public override byte[] Export(Docu docu, string targetPath)
        {
            if (docu is not HwpDocu hwp)
                return base.Export(docu, targetPath);

            var hwpx = hwp.ToHwpx();
            if (!HwpDocu.IsHwpPath(targetPath))
                return hwpx;

            if (!CanWriteHwp)
                throw new HwpException("이 컴퓨터에 한글이 없어서 .hwp로 저장할 수 없어요.");

            return Hancom.ToHwp(hwpx);
        }
    }
}
