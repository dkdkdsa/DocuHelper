using System.IO;
using System.Linq;
using DocuHelper.Tools.Word;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal static class DocWriter
    {
        public static byte[] Write(DocDocument doc)
        {
            using var stream = new MemoryStream();
            var blank = WordTemplate.Blank();
            stream.Write(blank, 0, blank.Length);
            stream.Position = 0;

            using (var word = WordprocessingDocument.Open(stream, true))
            {
                var main = word.MainDocumentPart;
                var body = main.Document.Body;
                var section = body.Elements<SectionProperties>().Last();
                foreach (var paragraph in body.Elements<Paragraph>().ToList())
                    paragraph.Remove();

                var lists = DocWordNumbering.Write(word, doc.Lists, doc.Fonts);
                var map = new DocStyleMap(doc.Styles);
                var builder = new DocBodyBuilder(doc, map, lists, new DocWordMedia(main));
                var elements = builder.Build();

                section.Remove();
                foreach (var element in elements)
                    body.AppendChild(element);

                DocWordPage.Fill(section, doc.Sections[^1]);
                body.AppendChild(section);
                DocWordStyles.Write(word, doc, map);
                main.Document.Save();
            }

            return stream.ToArray();
        }
    }
}
