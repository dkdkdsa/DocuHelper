using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public static class PptPlaceholder
    {
        public static P.PlaceholderShape Of(P.Shape shape) =>
            shape?.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape;

        public static string TypeOf(P.PlaceholderShape placeholder)
        {
            if (placeholder == null)
                return null;

            return placeholder.Type == null || !placeholder.Type.HasValue ? "obj" : placeholder.Type.InnerText;
        }

        public static uint IndexOf(P.PlaceholderShape placeholder) => placeholder?.Index?.Value ?? 0;

        public static string Family(string type) => type switch
        {
            null => null,
            "title" or "ctrTitle" => "title",
            "dt" or "ftr" or "sldNum" or "hdr" => type,
            _ => "body",
        };

        public static PptShapeRole RoleOf(string type) => type switch
        {
            "title" or "ctrTitle" => PptShapeRole.Title,
            "subTitle" => PptShapeRole.Subtitle,
            "body" or "obj" => PptShapeRole.Body,
            "dt" or "ftr" or "sldNum" or "hdr" => PptShapeRole.Footer,
            _ => PptShapeRole.Other,
        };

        public static string RoleName(PptShapeRole role) => role switch
        {
            PptShapeRole.Title => "title",
            PptShapeRole.Subtitle => "subtitle",
            PptShapeRole.Body => "body",
            PptShapeRole.TextBox => "text box",
            PptShapeRole.Footer => "footer",
            PptShapeRole.Picture => "picture",
            PptShapeRole.Table => "table",
            PptShapeRole.Chart => "chart",
            _ => "shape",
        };

        public static string KoreanName(PptShapeRole role) => role switch
        {
            PptShapeRole.Title => "제목",
            PptShapeRole.Subtitle => "부제목",
            PptShapeRole.Body => "본문",
            PptShapeRole.TextBox => "글상자",
            PptShapeRole.Footer => "바닥글",
            PptShapeRole.Picture => "그림",
            PptShapeRole.Table => "표",
            PptShapeRole.Chart => "차트",
            _ => "도형",
        };
    }
}
