using System.Collections.Generic;

namespace DocuHelper.Importers.Hwp
{
    internal sealed class HwpDocInfo
    {
        private const int HangulFontSlot = 1;
        private const int ParaShapeSlot = 13;
        private const int BulletCharOffset = 12;

        private readonly List<string> _fonts = new();
        private readonly List<HwpCharShape> _charShapes = new();
        private readonly List<HwpParaShape> _paraShapes = new();
        private readonly List<HwpNumbering> _numberings = new();
        private readonly List<string> _bullets = new();
        private int _paraShapeShift;

        public static HwpDocInfo Read(byte[] data, uint version)
        {
            var info = new HwpDocInfo();
            var hangulFonts = -1;
            var mappedParaShapes = -1;
            var faces = 0;
            foreach (var record in HwpRecord.ReadList(data))
            {
                switch (record.Tag)
                {
                    case HwpTag.IdMappings:
                        hangulFonts = record.Int32(HangulFontSlot * 4);
                        mappedParaShapes = record.Has(ParaShapeSlot * 4, 4) ? record.Int32(ParaShapeSlot * 4) : -1;
                        break;
                    case HwpTag.FaceName:
                        if (hangulFonts < 0 || faces < hangulFonts)
                            info._fonts.Add(record.HwpString(1, out _));

                        faces++;
                        break;
                    case HwpTag.CharShape:
                        info._charShapes.Add(HwpCharShape.Read(record));
                        break;
                    case HwpTag.Numbering:
                        info._numberings.Add(HwpNumbering.Read(record, version));
                        break;
                    case HwpTag.Bullet:
                        info._bullets.Add(HwpBullet.Text((char)record.UInt16(BulletCharOffset)));
                        break;
                    case HwpTag.ParaShape:
                        info._paraShapes.Add(HwpParaShape.Read(record, version));
                        break;
                }
            }

            if (mappedParaShapes >= 0)
                info._paraShapeShift = info._paraShapes.Count - mappedParaShapes;

            return info;
        }

        public HwpParaShape ParaShape(int id)
        {
            var shifted = id + _paraShapeShift;
            if (shifted >= 0 && shifted < _paraShapes.Count)
                return _paraShapes[shifted];

            return id >= 0 && id < _paraShapes.Count ? _paraShapes[id] : null;
        }

        public HwpCharShape CharShape(int id) => id >= 0 && id < _charShapes.Count ? _charShapes[id] : null;

        public string Font(int faceId) => faceId >= 0 && faceId < _fonts.Count ? _fonts[faceId] : null;

        public HwpNumbering Numbering(int id) => id >= 1 && id <= _numberings.Count ? _numberings[id - 1] : null;

        public string Bullet(int id) => id >= 1 && id <= _bullets.Count ? _bullets[id - 1] : null;
    }
}
