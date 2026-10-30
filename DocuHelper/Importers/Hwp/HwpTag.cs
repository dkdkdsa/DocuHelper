namespace DocuHelper.Importers.Hwp
{
    internal static class HwpTag
    {
        private const int Begin = 0x10;

        public const int IdMappings = Begin + 1;
        public const int FaceName = Begin + 3;
        public const int CharShape = Begin + 5;
        public const int Numbering = Begin + 7;
        public const int Bullet = Begin + 8;
        public const int ParaShape = Begin + 9;
        public const int ParaHeader = Begin + 50;
        public const int ParaText = Begin + 51;
        public const int ParaCharShape = Begin + 52;
        public const int CtrlHeader = Begin + 55;
        public const int ListHeader = Begin + 56;
        public const int PageDef = Begin + 57;
        public const int ShapeComponent = Begin + 60;
        public const int Table = Begin + 61;
        public const int ShapeComponentOle = Begin + 68;
        public const int ShapeComponentPicture = Begin + 69;
        public const int ShapeComponentTextArt = Begin + 74;
        public const int ChartData = Begin + 79;
        public const int VideoData = Begin + 82;
    }
}
