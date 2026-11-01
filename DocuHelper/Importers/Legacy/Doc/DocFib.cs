using System;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal sealed class DocFib
    {
        private const int WordIdent = 0xA5EC;
        private const int MinFib = 0x00C0;
        private const int EncryptedFlag = 0x0100;
        private const int WhichTableFlag = 0x0200;
        private const int ObfuscatedFlag = 0x8000;
        private const int MaxCcp = 1 << 28;

        private const int IndexStshf = 1;
        private const int IndexPlcfSed = 6;
        private const int IndexPlcfBteChpx = 12;
        private const int IndexPlcfBtePapx = 13;
        private const int IndexSttbfFfn = 15;
        private const int IndexClx = 33;
        private const int IndexPlfLst = 73;
        private const int IndexPlfLfo = 74;

        private int _pairCount;
        private int _pairStart;
        private byte[] _word;

        public bool UsesTable1 { get; private set; }

        public int CcpText { get; private set; }

        public DocRange Stshf => Pair(IndexStshf);

        public DocRange PlcfSed => Pair(IndexPlcfSed);

        public DocRange PlcfBteChpx => Pair(IndexPlcfBteChpx);

        public DocRange PlcfBtePapx => Pair(IndexPlcfBtePapx);

        public DocRange SttbfFfn => Pair(IndexSttbfFfn);

        public DocRange Clx => Pair(IndexClx);

        public DocRange PlfLst => Pair(IndexPlfLst);

        public DocRange PlfLfo => Pair(IndexPlfLfo);

        public static DocFib Read(byte[] word)
        {
            if (word == null || word.Length < 34)
                throw new LegacyOfficeException(DocMessages.Broken);

            if (DocBytes.U16(word, 0) != WordIdent)
                throw new LegacyOfficeException(DocMessages.TooOld);

            var version = DocBytes.U16(word, 2);
            if (version < MinFib)
                throw new LegacyOfficeException(DocMessages.TooOld);

            var flags = DocBytes.U16(word, 10);
            if ((flags & (EncryptedFlag | ObfuscatedFlag)) != 0)
                throw new LegacyOfficeException(DocMessages.Encrypted);

            var fib = new DocFib { UsesTable1 = (flags & WhichTableFlag) != 0, _word = word };
            var position = 32;
            var csw = DocBytes.U16(word, position);
            position += 2 + csw * 2;
            var cslw = DocBytes.U16(word, position);
            position += 2;
            if (cslw < 4)
                throw new LegacyOfficeException(DocMessages.Broken);

            var ccp = DocBytes.I32(word, position + 3 * 4);
            if (ccp < 0 || ccp > MaxCcp)
                throw new LegacyOfficeException(DocMessages.Broken);

            fib.CcpText = ccp;
            position += cslw * 4;
            fib._pairCount = DocBytes.U16(word, position);
            fib._pairStart = position + 2;
            if (!DocBytes.Has(word, fib._pairStart, (long)fib._pairCount * 8))
                fib._pairCount = Math.Max(0, (word.Length - fib._pairStart) / 8);

            return fib;
        }

        private DocRange Pair(int index)
        {
            if (index >= _pairCount)
                return DocRange.Empty;

            var offset = _pairStart + index * 8;
            return new DocRange(DocBytes.U32(_word, offset), DocBytes.U32(_word, offset + 4));
        }
    }
}
