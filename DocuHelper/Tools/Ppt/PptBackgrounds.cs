using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public sealed record PptBackgroundFill(string Type, PptColorSpec Color, PptColorSpec Color2, double? Angle, double Transparency, byte[] Bytes, string FileName);

    public static class PptBackgrounds
    {
        private const double DefaultAngle = 90;

        private static readonly string[] s_extensions = { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tif", ".tiff" };

        public static bool IsAll(ToolInput input) =>
            input.Has("slides") && input.Get("slides").ValueKind == JsonValueKind.String
            && string.Equals(input.GetString("slides"), "all", StringComparison.OrdinalIgnoreCase);

        public static IReadOnlyList<PptSlideRef> Numbers(PresentationDocument document, ToolInput input)
        {
            var count = PptSlides.List(document).Count;
            if (count == 0)
                throw new ToolInputException("이 파일에는 아직 슬라이드가 없어요. add_slide로 먼저 슬라이드를 만들어 주세요.");

            var numbers = PptInputs.IntegerList(input, "slides", 1, count, count)
                ?? throw new ToolInputException("바꿀 슬라이드 번호('slides')를 알려 주세요. 전체는 \"all\"이에요.");
            return numbers.Distinct().Select(n => PptSlides.Get(document, n)).ToList();
        }

        public static PptBackgroundFill Parse(ToolInput fill, ToolInput root, Docu docu)
        {
            var type = PptInputs.Choice(fill, "type", "solid", "gradient", "picture", "inherit")
                ?? throw new ToolInputException("'fill'에는 type이 필요해요. solid, gradient, picture, inherit 중 하나로 알려 주세요.");
            var color = PptInputs.Color(fill, "color");
            var color2 = PptInputs.Color(fill, "color2");
            var angle = PptInputs.Number(fill, "angle", 0, 359);
            var transparency = PptInputs.Number(fill, "transparency", 0, 100);
            switch (type)
            {
                case "solid":
                    if (color == null)
                        throw new ToolInputException("단색 배경에는 색 color가 필요해요. #RRGGBB나 accent1 같은 색으로 알려 주세요.");

                    if (color2 != null || angle != null)
                        throw new ToolInputException("color2와 angle은 그라데이션 배경에만 쓸 수 있어요.");

                    return new PptBackgroundFill(type, color, null, null, transparency ?? 0, null, null);
                case "gradient":
                    if (color == null)
                        throw new ToolInputException("그라데이션 배경에는 시작 색 color가 필요해요.");

                    return new PptBackgroundFill(type, color, color2 ?? PptFillWriter.Lighter(color), angle ?? DefaultAngle, transparency ?? 0, null, null);
                case "picture":
                    if (color != null || color2 != null || angle != null || transparency != null)
                        throw new ToolInputException("그림 배경에는 색, 각도, 투명도를 함께 쓸 수 없어요.");

                    var (bytes, name) = PictureSource(root, fill, docu);
                    return new PptBackgroundFill(type, null, null, null, 0, bytes, name);
                default:
                    if (color != null || color2 != null || angle != null || transparency != null)
                        throw new ToolInputException("테마 따라가기에는 색, 각도, 투명도를 함께 쓸 수 없어요.");

                    return new PptBackgroundFill(type, null, null, null, 0, null, null);
            }
        }

        public static P.Background Build(PptBackgroundFill fill, OpenXmlPartContainer owner)
        {
            OpenXmlElement element = fill.Type switch
            {
                "solid" => new A.SolidFill(PptFillWriter.WithAlpha(fill.Color, fill.Transparency)),
                "gradient" => Gradient(fill),
                _ => Picture(owner, fill),
            };
            return new P.Background(new P.BackgroundProperties(element, new A.EffectList()));
        }

        public static string EmbedOf(P.Background background) =>
            background?.Descendants<A.Blip>().FirstOrDefault()?.Embed?.Value;

        public static void Release(OpenXmlPart part, string oldEmbed, OpenXmlElement root)
        {
            if (oldEmbed == null || root.Descendants<A.Blip>().Any(b => b.Embed?.Value == oldEmbed))
                return;

            part.DeletePart(oldEmbed);
        }

        private static A.GradientFill Gradient(PptBackgroundFill fill) =>
            new(
                new A.GradientStopList(
                    new A.GradientStop(PptFillWriter.WithAlpha(fill.Color, fill.Transparency)) { Position = 0 },
                    new A.GradientStop(PptFillWriter.WithAlpha(fill.Color2, fill.Transparency)) { Position = 100000 }),
                new A.LinearGradientFill { Angle = (int)Math.Round((fill.Angle ?? DefaultAngle) * PptUnits.AnglePerDegree), Scaled = false })
            {
                RotateWithShape = true,
            };

        private static A.BlipFill Picture(OpenXmlPartContainer owner, PptBackgroundFill fill)
        {
            var (_, relationshipId) = PptImages.Add(owner, fill.Bytes, fill.FileName);
            return new A.BlipFill(new A.Blip { Embed = relationshipId }, new A.Stretch(new A.FillRectangle()))
            {
                Dpi = 0,
                RotateWithShape = true,
            };
        }

        private static (byte[] Bytes, string Name) PictureSource(ToolInput root, ToolInput fill, Docu docu)
        {
            var hasData = fill.Has("image_base64");
            var hasAttachment = fill.Has("attachment");
            var hasPath = fill.Has("path");
            if (hasData ? hasAttachment || hasPath : hasAttachment && hasPath)
                throw new ToolInputException("그림은 attachment, path, image_base64 중 하나로만 알려 주세요.");

            if (hasData)
                return Checked(PptImages.DecodeBase64(fill.GetString("image_base64")), null);

            if (hasAttachment)
                throw new ToolInputException($"첨부한 그림 '{fill.GetOptionalString("attachment")}'을 읽지 못했어요. 그림을 채팅에 다시 첨부해 주세요.");

            if (!hasPath)
                throw new ToolInputException("배경 그림을 알려 주세요. attachment(채팅에 첨부한 그림), path(문서와 같은 폴더의 그림 파일), image_base64 중 하나가 필요해요.");

            var bytes = ReadDeckFile(docu, fill.GetString("path"), out var name);
            return Checked(bytes, name);
        }

        private static (byte[] Bytes, string Name) Checked(byte[] bytes, string name)
        {
            var image = PptImages.Check(bytes, name);
            if (image.WidthPx > AddSlidePictureTool.MaxSidePixels || image.HeightPx > AddSlidePictureTool.MaxSidePixels)
                throw new ToolInputException($"그림의 가로세로가 너무 커요({image.WidthPx.ToString(System.Globalization.CultureInfo.InvariantCulture)}x{image.HeightPx.ToString(System.Globalization.CultureInfo.InvariantCulture)}). 한 변이 {AddSlidePictureTool.MaxSidePixels.ToString(System.Globalization.CultureInfo.InvariantCulture)}픽셀 이하여야 해요.");

            return (bytes, name);
        }

        private static byte[] ReadDeckFile(Docu docu, string path, out string fileName)
        {
            var folder = Path.GetDirectoryName(Path.GetFullPath(docu.FilePath ?? string.Empty));
            if (string.IsNullOrEmpty(folder))
                throw new ToolInputException("문서가 있는 폴더를 알 수 없어서 그림 파일을 열 수 없어요.");

            string full;
            try
            {
                full = Path.GetFullPath(Path.Combine(folder, path.Trim()));
            }
            catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
            {
                throw new ToolInputException($"'{path}' 경로를 읽지 못했어요. 문서와 같은 폴더의 그림 파일 이름을 알려 주세요.");
            }

            var root = folder.EndsWith(Path.DirectorySeparatorChar) ? folder : folder + Path.DirectorySeparatorChar;
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new ToolInputException("문서가 있는 폴더 안의 그림 파일만 넣을 수 있어요.");

            if (!s_extensions.Contains(Path.GetExtension(full).ToLowerInvariant()))
                throw new ToolInputException("png, jpg, gif, bmp, tif 형식의 그림 파일만 넣을 수 있어요.");

            var info = new FileInfo(full);
            if (!info.Exists)
                throw new ToolInputException($"'{Path.GetFileName(full)}' 파일을 문서 폴더에서 찾지 못했어요. 파일 이름을 다시 확인해 주세요.");

            if (info.Length > PptImages.MaxBytes)
                throw new ToolInputException($"그림이 너무 커요. {PptImages.MaxBytes / (1024 * 1024)}MB 이하의 그림만 넣을 수 있어요.");

            fileName = info.Name;
            try
            {
                return File.ReadAllBytes(full);
            }
            catch (IOException)
            {
                throw new ToolInputException($"'{info.Name}' 그림을 읽지 못했어요. 파일이 다른 프로그램에서 열려 있는지 확인해 주세요.");
            }
        }
    }
}
