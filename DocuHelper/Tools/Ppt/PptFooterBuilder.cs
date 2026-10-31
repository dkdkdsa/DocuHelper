using System;
using System.Globalization;
using DocumentFormat.OpenXml;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public static class PptFooterBuilder
    {
        public static readonly CultureInfo Korean = CultureInfo.GetCultureInfo("ko-KR");

        public static P.Shape Create(uint id, string kind, P.PlaceholderShape placeholder, A.Paragraph paragraph)
        {
            var label = LabelOf(kind);
            return new P.Shape(
                new P.NonVisualShapeProperties(
                    new P.NonVisualDrawingProperties { Id = id, Name = $"{label} {(id - 1).ToString(CultureInfo.InvariantCulture)}" },
                    new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                    new P.ApplicationNonVisualDrawingProperties((OpenXmlElement)placeholder.CloneNode(true))),
                new P.ShapeProperties(),
                new P.TextBody(new A.BodyProperties(), new A.ListStyle(), paragraph));
        }

        public static A.Paragraph Text(string text) =>
            new(new A.Run(new A.Text(text)));

        public static A.Paragraph Number(int number) =>
            new(Field("slidenum", number.ToString(CultureInfo.InvariantCulture)));

        public static A.Paragraph Date(string type, string cached) =>
            new(Field(type, cached));

        public static (string Type, string Format) DateKind(string format) => format switch
        {
            "long" => ("datetime2", "yyyy년 M월 d일 dddd"),
            "time" => ("datetime12", "tt h:mm"),
            _ => ("datetime1", "yyyy-MM-dd"),
        };

        public static string Cached(DateTime value, string format) => value.ToString(format, Korean);

        public static string LabelOf(string kind) => kind switch
        {
            PptFooters.Footer => "Footer Placeholder",
            PptFooters.SlideNumber => "Slide Number Placeholder",
            _ => "Date Placeholder",
        };

        private static A.Field Field(string type, string cached) =>
            new(new A.Text(cached))
            {
                Type = type,
                Id = "{" + Guid.NewGuid().ToString("D").ToUpperInvariant() + "}",
            };
    }
}
