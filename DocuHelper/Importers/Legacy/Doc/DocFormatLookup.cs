using System;
using System.Collections.Generic;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal sealed class DocFormatLookup
    {
        private readonly DocDocument _doc;
        private readonly DocChp _plain = new();
        private readonly Dictionary<(DocFkpEntry, DocPiece), DocChp> _modified = new();
        private int _pieceHint = -1;
        private int _characterHint = -1;
        private int _paragraphHint = -1;

        public DocFormatLookup(DocDocument doc)
        {
            _doc = doc;
        }

        public DocChp Characters(int cp, int paragraphStyle)
        {
            var index = _doc.Pieces.FindIndex(cp, _pieceHint);
            if (index < 0)
                return _plain;

            _pieceHint = index;
            var piece = _doc.Pieces.Pieces[index];
            var offset = piece.ByteOf(cp);
            var entry = offset <= int.MaxValue ? _doc.Characters.Find((int)offset, ref _characterHint) : null;
            if (piece.Modifier == null)
                return entry == null ? _plain : Direct(entry, paragraphStyle);

            var key = (entry, piece);
            if (_modified.TryGetValue(key, out var cached))
                return cached;

            var chp = entry == null ? new DocChp() : Direct(entry, paragraphStyle).Clone();
            DocChpReader.Apply(chp, piece.Modifier, 0, piece.Modifier.Length, null);
            if (_modified.Count < 100000)
                _modified[key] = chp;

            return chp;
        }

        public DocPap Paragraph(int cp)
        {
            var index = _doc.Pieces.FindIndex(cp, _pieceHint);
            if (index < 0)
                return new DocPap();

            _pieceHint = index;
            var piece = _doc.Pieces.Pieces[index];
            var offset = piece.ByteOf(cp);
            var entry = offset <= int.MaxValue ? _doc.Paragraphs.Find((int)offset, ref _paragraphHint) : null;
            if (entry != null && piece.Modifier == null && entry.Pap != null)
                return entry.Pap;

            var pap = new DocPap();
            if (entry != null)
            {
                pap.Istd = entry.Istd;
                DocPapReader.Apply(pap, entry.Grpprl, 0, entry.Grpprl.Length);
            }

            if (piece.Modifier != null)
                DocPapReader.Apply(pap, piece.Modifier, 0, piece.Modifier.Length);
            else if (entry != null)
                entry.Pap = pap;

            return pap;
        }

        private DocChp Direct(DocFkpEntry entry, int paragraphStyle)
        {
            if (entry.Chp != null && entry.ChpStyle == paragraphStyle)
                return entry.Chp;

            var style = _doc.Styles.ResolveCharacters(paragraphStyle);
            entry.Chp = DocChpReader.Read(entry.Grpprl, style);
            entry.ChpStyle = paragraphStyle;
            return entry.Chp;
        }
    }
}
