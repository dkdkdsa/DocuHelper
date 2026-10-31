namespace DocuHelper.Tools.Ppt
{
    public sealed class PptShapeSpec
    {
        public PptShapeSpec(string token, string preset, string koreanName, PptShapeForm form, double widthCm, double heightCm, bool headEnd = false, bool tailEnd = false)
        {
            Token = token;
            Preset = preset;
            KoreanName = koreanName;
            Form = form;
            WidthCm = widthCm;
            HeightCm = heightCm;
            HeadEnd = headEnd;
            TailEnd = tailEnd;
        }

        public string Token { get; }

        public string Preset { get; }

        public string KoreanName { get; }

        public PptShapeForm Form { get; }

        public double WidthCm { get; }

        public double HeightCm { get; }

        public bool HeadEnd { get; }

        public bool TailEnd { get; }

        public bool IsLine => Form == PptShapeForm.Connector;

        public bool IsTextBox => Form == PptShapeForm.TextBox;

        public string Detail => Form switch
        {
            PptShapeForm.TextBox => "글상자 넣기",
            PptShapeForm.Connector => "선 넣기",
            _ => "도형 넣기",
        };
    }
}
