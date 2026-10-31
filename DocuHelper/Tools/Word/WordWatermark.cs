using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Vml = DocumentFormat.OpenXml.Vml;

namespace DocuHelper.Tools.Word
{
    public static class WordWatermark
    {
        public const string Prefix = "PowerPlusWaterMarkObject";
        public const int MaxLength = 50;

        private const string OfficeNamespace = "urn:schemas-microsoft-com:office:office";

        private const string Namespaces =
            "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" "
            + "xmlns:v=\"urn:schemas-microsoft-com:vml\" "
            + "xmlns:o=\"urn:schemas-microsoft-com:office:office\" "
            + "xmlns:w10=\"urn:schemas-microsoft-com:office:word\"";

        private const string ShapeType =
            "<v:shapetype id=\"_x0000_t136\" coordsize=\"21600,21600\" o:spt=\"136\" adj=\"10800\" path=\"m@7,l@8,m@5,21600l@6,21600e\">"
            + "<v:formulas><v:f eqn=\"sum #0 0 10800\"/><v:f eqn=\"prod #0 2 1\"/><v:f eqn=\"sum 21600 0 @1\"/><v:f eqn=\"sum 0 0 @2\"/><v:f eqn=\"sum 21600 0 @3\"/>"
            + "<v:f eqn=\"if @0 @3 0\"/><v:f eqn=\"if @0 21600 @1\"/><v:f eqn=\"if @0 0 @2\"/><v:f eqn=\"if @0 @4 21600\"/><v:f eqn=\"mid @5 @6\"/>"
            + "<v:f eqn=\"mid @8 @5\"/><v:f eqn=\"mid @7 @8\"/><v:f eqn=\"mid @6 @7\"/><v:f eqn=\"sum @6 0 @5\"/></v:formulas>"
            + "<v:path textpathok=\"t\" o:connecttype=\"custom\" o:connectlocs=\"@9,0;@10,10800;@11,21600;@12,10800\" o:connectangles=\"270,180,90,0\"/>"
            + "<v:textpath on=\"t\" fitshape=\"t\"/><v:handles><v:h position=\"#0,bottomRight\" xrange=\"6629,14971\"/></v:handles><o:lock v:ext=\"edit\" text=\"t\" shapetype=\"t\"/></v:shapetype>";

        public static IEnumerable<Run> Runs(OpenXmlElement root) =>
            root.Descendants<Run>().Where(r => r.Descendants<Vml.Shape>().Any(s => s.Id?.Value?.StartsWith(Prefix, StringComparison.Ordinal) == true)).ToList();

        public static string Text(OpenXmlElement root) =>
            Runs(root).SelectMany(r => r.Descendants<Vml.TextPath>()).Select(t => t.String?.Value).FirstOrDefault(t => !string.IsNullOrEmpty(t));

        public static bool Remove(OpenXmlElement root)
        {
            var runs = Runs(root).ToList();
            foreach (var run in runs)
                run.Remove();

            return runs.Count > 0;
        }

        public static void Place(Paragraph paragraph, Run run)
        {
            var properties = paragraph.ParagraphProperties;
            if (properties == null)
                paragraph.InsertAt(run, 0);
            else
                properties.InsertAfterSelf(run);
        }

        public static void Add(WordDocu docu, OpenXmlCompositeElement root, string text, string font, double? sizePt, string color, bool transparent, bool diagonal, bool header)
        {
            Remove(root);
            var paragraph = root.Elements<Paragraph>().FirstOrDefault();
            if (paragraph == null)
            {
                paragraph = WordHeaderFooters.Blank(docu, header, WordIds.Collect(docu.Document));
                root.AppendChild(paragraph);
            }

            Place(paragraph, Build(docu, text, font, sizePt, color, transparent, diagonal));
        }

        public static void Reid(WordDocu docu, OpenXmlElement root)
        {
            foreach (var shape in root.Descendants<Vml.Shape>().Where(s => s.Id?.Value?.StartsWith(Prefix, StringComparison.Ordinal) == true))
            {
                shape.Id = NextId(docu);
                shape.SetAttribute(new OpenXmlAttribute("o", "spid", OfficeNamespace, NextSpid(docu)));
            }
        }

        private static Run Build(WordDocu docu, string text, string font, double? sizePt, string color, bool transparent, bool diagonal)
        {
            var units = text.Sum(c => c >= (char)0x1100 ? 1.0 : 0.6);
            var size = sizePt ?? Math.Clamp(360 / Math.Max(units, 1), 36, 144);
            var width = Math.Round(Math.Max(units, 1) * size, 1);
            var height = Math.Round(size * 1.2, 1);
            var style = string.Create(CultureInfo.InvariantCulture,
                $"position:absolute;margin-left:0;margin-top:0;width:{width}pt;height:{height}pt;{(diagonal ? "rotation:315;" : string.Empty)}z-index:-251658752;mso-position-horizontal:center;mso-position-horizontal-relative:margin;mso-position-vertical:center;mso-position-vertical-relative:margin");
            var fill = transparent ? "<v:fill opacity=\".5\"/>" : string.Empty;
            var xml = $"<w:r {Namespaces}><w:rPr><w:noProof/></w:rPr><w:pict>{ShapeType}"
                + $"<v:shape id=\"{NextId(docu)}\" o:spid=\"{NextSpid(docu)}\" type=\"#_x0000_t136\" alt=\"\" style=\"{style}\" o:allowincell=\"f\" fillcolor=\"#{color}\" stroked=\"f\">"
                + $"{fill}<v:textpath style=\"font-family:&quot;{SecurityElement.Escape(font)}&quot;;font-size:1pt\" string=\"{SecurityElement.Escape(text)}\"/>"
                + "<w10:wrap anchorx=\"margin\" anchory=\"margin\"/></v:shape></w:pict></w:r>";
            return new Run(xml);
        }

        private static string NextId(WordDocu docu)
        {
            var used = Shapes(docu).Select(s => s.Id?.Value).ToHashSet(StringComparer.Ordinal);
            string id;
            do
            {
                id = Prefix + Random.Shared.Next(100000000, 999999999).ToString(CultureInfo.InvariantCulture);
            }
            while (!used.Add(id));

            return id;
        }

        private static string NextSpid(WordDocu docu)
        {
            var max = 2048;
            foreach (var shape in Shapes(docu))
            {
                var value = shape.GetAttributes().FirstOrDefault(a => a.LocalName == "spid").Value;
                if (value != null && value.StartsWith("_x0000_s", StringComparison.Ordinal) && int.TryParse(value.AsSpan(8), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                    max = Math.Max(max, number);
            }

            return "_x0000_s" + (max + 1).ToString(CultureInfo.InvariantCulture);
        }

        private static IEnumerable<Vml.Shape> Shapes(WordDocu docu)
        {
            var main = docu.Document.MainDocumentPart;
            return main.HeaderParts.SelectMany(p => p.Header?.Descendants<Vml.Shape>() ?? Enumerable.Empty<Vml.Shape>()).ToList();
        }
    }
}
