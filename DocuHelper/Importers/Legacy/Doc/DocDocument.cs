using System.Collections.Generic;
using DocuHelper.Importers.Hwp;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal sealed class DocDocument
    {
        public byte[] Word { get; private set; }

        public byte[] Data { get; private set; }

        public string Text { get; private set; }

        public DocPieceTable Pieces { get; private set; }

        public DocFkpIndex Characters { get; private set; }

        public DocFkpIndex Paragraphs { get; private set; }

        public DocStyleSheet Styles { get; private set; }

        public DocFontTable Fonts { get; private set; }

        public DocListTable Lists { get; private set; }

        public List<DocSection> Sections { get; private set; }

        public static DocDocument Read(byte[] content)
        {
            if (!HwpCompoundFile.IsCompoundFile(content))
                throw new LegacyOfficeException(DocMessages.NotWord);

            var file = new HwpCompoundFile(content);
            var word = file.ReadStream("WordDocument");
            if (word == null)
                throw new LegacyOfficeException(DocMessages.NotWord);

            var fib = DocFib.Read(word);
            var table = file.ReadStream(fib.UsesTable1 ? "1Table" : "0Table");
            if (table == null)
                throw new LegacyOfficeException(DocMessages.Broken);

            var data = file.ReadStream("Data");
            var pieces = DocPieceTable.Read(table, fib.Clx, fib.CcpText, word.Length);
            return new DocDocument
            {
                Word = word,
                Data = data,
                Pieces = pieces,
                Text = pieces.Decode(word),
                Characters = DocFkpIndex.ReadCharacters(word, table, fib.PlcfBteChpx),
                Paragraphs = DocFkpIndex.ReadParagraphs(word, table, data, fib.PlcfBtePapx),
                Styles = DocStyleSheet.Read(table, fib.Stshf),
                Fonts = DocFontTable.Read(table, fib.SttbfFfn),
                Lists = DocListTable.Read(table, fib.PlfLst, fib.PlfLfo),
                Sections = DocSectionTable.Read(word, table, fib.PlcfSed, pieces.Length),
            };
        }
    }
}
