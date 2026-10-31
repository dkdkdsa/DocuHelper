using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using DocumentFormat.OpenXml.Packaging;

namespace DocuHelper.Tools.Ppt
{
    public static class PptImages
    {
        public const long MaxBytes = 20L * 1024 * 1024;

        public const int MaxPixels = 20000;

        public static byte[] DecodeBase64(string text, string name = "image_base64")
        {
            var value = (text ?? string.Empty).Trim();
            var marker = value.IndexOf(";base64,", StringComparison.OrdinalIgnoreCase);
            if (value.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && marker > 0)
                value = value.Substring(marker + ";base64,".Length);

            value = new string(value.Where(c => !char.IsWhiteSpace(c)).ToArray());
            if (value.Length == 0)
                throw new ToolInputException($"'{name}' 값이 비어 있어요. 그림 파일 내용을 base64로 알려 주세요.");

            if (value.Length / 4L * 3 > MaxBytes + 3)
                throw new ToolInputException($"그림이 너무 커요. {MaxBytes / (1024 * 1024)}MB 이하의 그림만 넣을 수 있어요.");

            var buffer = new byte[value.Length / 4 * 3 + 3];
            if (!Convert.TryFromBase64String(value, buffer, out var written))
                throw new ToolInputException($"'{name}' 값을 base64로 읽지 못했어요. 그림 파일 내용을 base64로 알려 주세요.");

            return buffer.AsSpan(0, written).ToArray();
        }

        public static PptImageInfo Check(byte[] bytes, string name = null)
        {
            var label = string.IsNullOrWhiteSpace(name) ? "그림" : $"그림 '{name}'";
            if (bytes == null || bytes.Length == 0)
                throw new ToolInputException($"{label} 파일이 비어 있어요.");

            if (bytes.Length > MaxBytes)
                throw new ToolInputException($"{label}이 너무 커요. {MaxBytes / (1024 * 1024)}MB 이하의 그림만 넣을 수 있어요.");

            var info = PptImageSniffer.Sniff(bytes);
            if (info == null)
                throw new ToolInputException($"{label}을 읽지 못했어요. png, jpg, gif, bmp, tif 형식의 그림만 넣을 수 있어요.");

            if (info.WidthPx <= 0 || info.HeightPx <= 0)
                throw new ToolInputException($"{label}의 크기를 알 수 없어서 넣을 수 없어요. 파일이 망가졌는지 확인해 주세요.");

            if (info.WidthPx > MaxPixels || info.HeightPx > MaxPixels)
                throw new ToolInputException($"{label}의 가로세로가 너무 커요({info.WidthPx.ToString(CultureInfo.InvariantCulture)}x{info.HeightPx.ToString(CultureInfo.InvariantCulture)}). 한 변이 {MaxPixels.ToString(CultureInfo.InvariantCulture)}픽셀 이하여야 해요.");

            return info;
        }

        public static (ImagePart Part, string RelId) Add(OpenXmlPartContainer owner, byte[] bytes, string name = null)
        {
            var info = Check(bytes, name);
            var hash = SHA256.HashData(bytes);
            foreach (var pair in owner.Parts)
            {
                if (pair.OpenXmlPart is not ImagePart existing || existing.ContentType != info.Mime)
                    continue;

                if (SameContent(existing, bytes, hash))
                    return (existing, pair.RelationshipId);
            }

            var part = owner.AddNewPart<ImagePart>(info.Mime);
            using (var stream = new MemoryStream(bytes, false))
                part.FeedData(stream);

            return (part, owner.GetIdOfPart(part));
        }

        public static byte[] Read(ImagePart part)
        {
            using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }

        public static string KeyOf(byte[] bytes) =>
            Convert.ToHexString(SHA256.HashData(bytes)).Substring(0, 32).ToLowerInvariant();

        private static bool SameContent(ImagePart existing, byte[] bytes, byte[] hash)
        {
            using var stream = existing.GetStream(FileMode.Open, FileAccess.Read);
            if (stream.CanSeek && stream.Length != bytes.Length)
                return false;

            return SHA256.HashData(stream).AsSpan().SequenceEqual(hash);
        }
    }
}
