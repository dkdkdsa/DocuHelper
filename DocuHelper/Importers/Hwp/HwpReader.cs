using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace DocuHelper.Importers.Hwp
{
    public static class HwpReader
    {
        private const string BrokenMessage = "한글 파일을 읽지 못했어요. 파일이 망가졌을 수 있어요.";
        private const string PasswordMessage = "암호가 걸린 한글 파일은 열 수 없어요. 한글에서 암호를 풀고 다시 넣어 주세요.";
        private const string DistributionMessage = "배포용으로 잠긴 한글 문서는 열 수 없어요.";
        private const string LegacyMessage = "한글 97 이전 형식의 파일은 열 수 없어요. 한글에서 새 형식(.hwp, .hwpx)으로 저장한 다음 넣어 주세요.";
        private const string Signature = "HWP Document File";
        private const string LegacySignature = "HWP Document File V";
        private const string SectionPrefix = "Section";
        private const int HeaderSize = 40;
        private const uint CompressedFlag = 0x1;
        private const uint PasswordFlag = 0x2;
        private const uint DistributionFlag = 0x4;
        private const uint EncryptedFlags = 0x10 | 0x100 | 0x400;

        public static HwpDocument Read(byte[] content)
        {
            try
            {
                return ReadDocument(content);
            }
            catch (Exception e) when (e is not HwpException)
            {
                throw new HwpException(BrokenMessage, e);
            }
        }

        private static HwpDocument ReadDocument(byte[] content)
        {
            if (!HwpCompoundFile.IsCompoundFile(content))
                throw new HwpException(IsLegacy(content) ? LegacyMessage : BrokenMessage);

            var file = new HwpCompoundFile(content);
            var header = file.ReadStream("FileHeader");
            if (header == null || header.Length < HeaderSize || Encoding.ASCII.GetString(header, 0, Signature.Length) != Signature)
                throw new HwpException(BrokenMessage);

            var version = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(32));
            var properties = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(36));
            if ((properties & PasswordFlag) != 0)
                throw new HwpException(PasswordMessage);

            if ((properties & DistributionFlag) != 0 || (!file.HasStorage("BodyText") && file.HasStorage("ViewText")))
                throw new HwpException(DistributionMessage);

            if ((properties & EncryptedFlags) != 0)
                throw new HwpException(BrokenMessage);

            var compressed = (properties & CompressedFlag) != 0;
            var info = HwpDocInfo.Read(Decode(file.ReadStream("DocInfo"), compressed), version);
            var sections = SectionNames(file);
            if (sections.Count == 0)
                throw new HwpException(BrokenMessage);

            var document = new HwpDocument();
            var body = new HwpBodyReader(info, new HwpBinData(file), document);
            foreach (var section in sections)
                body.ReadSection(Decode(file.ReadStream("BodyText/" + section), compressed));

            return document;
        }

        private static bool IsLegacy(byte[] content)
        {
            if (content == null || content.Length <= LegacySignature.Length)
                return false;

            var version = (char)content[LegacySignature.Length];
            return Encoding.ASCII.GetString(content, 0, LegacySignature.Length) == LegacySignature && version >= '1' && version <= '3';
        }

        private static byte[] Decode(byte[] data, bool compressed)
        {
            if (data == null)
                throw new InvalidDataException("A required stream is missing.");

            return compressed ? HwpDeflate.Inflate(data) : data;
        }

        private static List<string> SectionNames(HwpCompoundFile file)
        {
            var sections = new SortedDictionary<int, string>();
            foreach (var name in file.StreamNames("BodyText"))
            {
                if (name.Length > SectionPrefix.Length
                    && name.StartsWith(SectionPrefix, StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(name.AsSpan(SectionPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var index))
                    sections.TryAdd(index, name);
            }

            return new List<string>(sections.Values);
        }
    }
}
