using System.Globalization;
using DocuHelper.Tools;
using DocuHelper.Tools.Ppt;

namespace DocuHelper.Tools.Word
{
    public static class WordImageSniffer
    {
        public const long MaxBytes = 8L * 1024 * 1024;

        public const int MaxPixels = 20000;

        public static PptImageInfo Read(byte[] bytes, string name)
        {
            var label = string.IsNullOrWhiteSpace(name) ? "그림" : $"그림 ‘{name}’";
            if (bytes == null || bytes.Length == 0)
                throw new ToolInputException($"{label} 파일이 비어 있어요.");

            if (bytes.Length > MaxBytes)
                throw new ToolInputException($"{label}이 너무 커요. {MaxBytes / (1024 * 1024)}MB 이하의 그림만 넣을 수 있어요.");

            var info = PptImageSniffer.Sniff(bytes);
            if (info == null || info.Type is PptImageType.Tiff)
                throw new ToolInputException($"{label}은 지원하지 않는 형식이에요. png, jpg, gif, bmp 그림만 넣을 수 있어요.");

            if (info.WidthPx <= 0 || info.HeightPx <= 0)
                throw new ToolInputException($"{label}의 크기를 알 수 없어서 넣을 수 없어요. 파일이 망가졌는지 확인해 주세요.");

            if (info.WidthPx > MaxPixels || info.HeightPx > MaxPixels)
                throw new ToolInputException($"{label}의 가로세로가 너무 커요({info.WidthPx.ToString(CultureInfo.InvariantCulture)}x{info.HeightPx.ToString(CultureInfo.InvariantCulture)}).");

            return info;
        }
    }
}
