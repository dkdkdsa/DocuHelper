using System;
using System.Globalization;
using System.IO;
using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public class AddSlidePictureTool : Tool
    {
        public const int MaxSidePixels = 8000;

        private const double SizeLimitFactor = 4;

        private const int MaxNameLength = 200;

        private const int MaxAltLength = 2000;

        private static readonly string[] s_extensions = { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tif", ".tiff" };

        public override string Name => "add_slide_picture";

        public override string Description =>
            "Insert a picture (png, jpg, gif, bmp, tif) or replace the picture of an existing picture shape. "
            + "Source: `attachment` (image attached to the chat), `path` (file in the deck folder) or `image_base64`. "
            + "x, y, w, h in cm from the top-left corner; leave out for a fit in the free area keeping proportions; give only w or only h to keep proportions. "
            + "Give alt_text (short Korean) for meaningful pictures. place \"master\" shows it on every slide (logo).";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "slide": {"type": "integer", "minimum": 1, "description": "Slide number (1-based)."},
                "image_base64": {"type": "string", "description": "Image bytes, base64 (the app uses this)."},
                "attachment": {"type": "string", "description": "Name of an image attached to the chat."},
                "path": {"type": "string", "description": "Image file in the deck folder (png jpg gif bmp tif)."},
                "name": {"type": "string", "description": "File name with extension (type detection for image_base64)."},
                "x": {"type": "number", "description": "Left, cm."},
                "y": {"type": "number", "description": "Top, cm."},
                "w": {"type": "number", "minimum": 0, "description": "Width, cm."},
                "h": {"type": "number", "minimum": 0, "description": "Height, cm."},
                "alt_text": {"type": "string", "description": "Alternative text."},
                "replace": {"type": "string", "description": "Shape id of a picture to swap; position and size stay."},
                "place": {"type": "string", "enum": ["slide", "master"]}
              },
              "required": ["slide"]
            }
            """;

        public override ToolResult Execute(Docu docu, ToolInput input)
        {
            var document = PptSlides.DocumentOf(docu);
            var slide = PptSlides.Get(document, input.GetInt("slide"));
            var place = PptInputs.Choice(input, "place", "slide", "master") ?? "slide";
            var deck = PptFrames.DeckOf(document);
            var maxX = PptUnits.ToCm(deck.Width) * SizeLimitFactor;
            var maxY = PptUnits.ToCm(deck.Height) * SizeLimitFactor;
            var x = PptInputs.Emu(input, "x", -maxX, maxX);
            var y = PptInputs.Emu(input, "y", -maxY, maxY);
            var width = PptInputs.Emu(input, "w", 0, maxX);
            var height = PptInputs.Emu(input, "h", 0, maxY);
            var alt = PptInputs.Text(input, "alt_text", MaxAltLength);
            var fileName = PptInputs.Text(input, "name", MaxNameLength);
            var replace = input.GetOptionalString("replace");
            var replacing = !string.IsNullOrWhiteSpace(replace);

            if (width == 0 || height == 0)
                throw new ToolInputException("그림의 너비(w)와 높이(h)는 0보다 커야 해요.");

            if (replacing && place == "master")
                throw new ToolInputException("마스터에 있는 그림은 바꿀 수 없어요. 슬라이드의 그림만 바꿀 수 있어요.");

            if (replacing && (x != null || y != null || width != null || height != null))
                throw new ToolInputException("그림을 바꿀 때는 위치와 크기를 함께 정할 수 없어요. 자리와 크기는 그대로 유지돼요. 옮기려면 format_slide_shape를 써 주세요.");

            var bytes = Source(docu, input, ref fileName);
            var image = PptImages.Check(bytes, fileName);
            if (image.WidthPx > MaxSidePixels || image.HeightPx > MaxSidePixels)
                throw new ToolInputException($"그림의 가로세로가 너무 커요({image.WidthPx.ToString(CultureInfo.InvariantCulture)}x{image.HeightPx.ToString(CultureInfo.InvariantCulture)}). 한 변이 {MaxSidePixels.ToString(CultureInfo.InvariantCulture)}픽셀 이하여야 해요.");

            return replacing
                ? Replace(slide, replace, bytes, alt, fileName)
                : Insert(document, slide, place, deck, image, bytes, x, y, width, height, alt, fileName);
        }

        private static ToolResult Insert(PresentationDocument document, PptSlideRef slide, string place, PptBox deck, PptImageInfo image, byte[] bytes, long? x, long? y, long? width, long? height, string alt, string fileName)
        {
            var tree = place == "master" ? MasterTree(document, slide) : slide.Tree ?? throw new ToolInputException($"{slide.Number}번 슬라이드를 읽지 못했어요.");
            var explicitFrame = x != null || y != null || width != null || height != null;
            var empty = explicitFrame || place == "master" ? null : PptFrames.EmptyPlaceholder(slide, "pic");
            if (PptFrames.Free(tree, empty) >= AddSlideShapeTool.MaxShapesPerSlide)
                throw new ToolInputException($"한 장에는 도형을 {AddSlideShapeTool.MaxShapesPerSlide.ToString(CultureInfo.InvariantCulture)}개까지만 둘 수 있어요. 안 쓰는 도형을 먼저 지워 주세요.");

            var area = place == "master" ? deck.Inset(PptFrames.MarginSide, PptFrames.MarginSide, PptFrames.MarginSide, PptFrames.MarginBottom) : PptFrames.Area(slide, deck, empty);
            var size = PptPictureBuilder.Size(image, area, deck, width, height);
            var left = x ?? area.X + (area.Width - size.Width) / 2;
            var top = y ?? area.Y + (area.Height - size.Height) / 2;
            var box = size.MoveTo(left, top);

            OpenXmlPartContainer owner = place == "master" ? MasterPart(document, slide) : slide.Part;
            var (_, relationshipId) = PptImages.Add(owner, bytes, fileName);
            var id = PptSlides.NextShapeId(tree);
            var picture = PptPictureBuilder.Create(id, $"그림 {(id - 1).ToString(CultureInfo.InvariantCulture)}", alt, relationshipId, box);
            PptFrames.Place(slide, tree, empty, picture);

            var content = $"Inserted picture [{id.ToString(CultureInfo.InvariantCulture)}] ({image.WidthPx.ToString(CultureInfo.InvariantCulture)}x{image.HeightPx.ToString(CultureInfo.InvariantCulture)} px) at x={PptUnits.CmText(box.X)} y={PptUnits.CmText(box.Y)} w={PptUnits.CmText(box.Width)} h={PptUnits.CmText(box.Height)} cm.";
            if (place == "master")
                content += " It is on the slide master and shows on every slide whose layout shows master shapes.";

            if (empty != null)
                content += " It took the place of the empty content placeholder.";

            if (string.IsNullOrEmpty(alt))
                content += " No alt_text was given; add a short Korean alt_text for meaningful pictures.";

            var change = new DocuChange
            {
                Location = $"{slide.Number.ToString(CultureInfo.InvariantCulture)}번",
                Icon = "image",
                Anchors = { place == "master" ? PptAnchor.Slide(slide.SlideId) : PptAnchor.Shape(slide.SlideId, id) },
            };

            return ToolResult.Changed(content, change, "그림 넣기");
        }

        private static ToolResult Replace(PptSlideRef slide, string key, byte[] bytes, string alt, string fileName)
        {
            var target = PptTargets.Find(slide, key);
            if (target is not P.Picture picture)
                throw new ToolInputException($"{PptTargets.IdOf(target).ToString(CultureInfo.InvariantCulture)}번은 그림이 아니라서 바꿀 수 없어요. 그림 번호를 알려 주세요. {PptTargets.Choices(slide)}");

            var blip = picture.BlipFill?.Blip ?? throw new ToolInputException("이 그림은 이미지를 읽을 수 없어서 바꿀 수 없어요.");
            var oldId = blip.Embed?.Value;
            var (_, relationshipId) = PptImages.Add(slide.Part, bytes, fileName);
            blip.Embed = relationshipId;
            blip.RemoveAllChildren<A.BlipExtensionList>();
            picture.BlipFill.SourceRectangle = null;
            if (alt != null)
            {
                var properties = picture.NonVisualPictureProperties.NonVisualDrawingProperties;
                properties.Description = alt.Length == 0 ? null : alt;
            }

            if (!string.IsNullOrEmpty(oldId) && oldId != relationshipId && !PptPartCopier.ReferencedIds(slide.Part.Slide).Contains(oldId))
                slide.Part.DeletePart(oldId);

            var id = PptTargets.IdOf(picture);
            var change = new DocuChange
            {
                Location = $"{slide.Number.ToString(CultureInfo.InvariantCulture)}번",
                Icon = "image",
                Anchors = { PptAnchor.Shape(slide.SlideId, id) },
            };

            return ToolResult.Changed($"Replaced the picture of [{id.ToString(CultureInfo.InvariantCulture)}]; position and size are unchanged, so a picture with other proportions is stretched.", change, "그림 바꿈");
        }

        private static byte[] Source(Docu docu, ToolInput input, ref string fileName)
        {
            var hasData = input.Has("image_base64");
            var hasAttachment = input.Has("attachment");
            var hasPath = input.Has("path");
            if (hasPath && (hasData || hasAttachment))
                throw new ToolInputException("그림은 attachment, path, image_base64 중 하나로만 알려 주세요.");

            if (hasData)
                return PptImages.DecodeBase64(input.GetString("image_base64"));

            if (hasAttachment)
                throw new ToolInputException($"첨부한 그림 '{input.GetOptionalString("attachment")}'을 읽지 못했어요. 그림을 채팅에 다시 첨부해 주세요.");

            if (!hasPath)
                throw new ToolInputException("넣을 그림을 알려 주세요. attachment(채팅에 첨부한 그림), path(문서와 같은 폴더의 그림 파일), image_base64 중 하나가 필요해요.");

            return ReadFile(docu, input.GetString("path"), ref fileName);
        }

        private static byte[] ReadFile(Docu docu, string path, ref string fileName)
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

            fileName ??= info.Name;
            try
            {
                return File.ReadAllBytes(full);
            }
            catch (IOException)
            {
                throw new ToolInputException($"'{info.Name}' 파일을 읽지 못했어요. 다른 프로그램에서 쓰고 있는지 확인해 주세요.");
            }
            catch (UnauthorizedAccessException)
            {
                throw new ToolInputException($"'{info.Name}' 파일을 읽을 권한이 없어요.");
            }
        }

        private static SlideMasterPart MasterPart(PresentationDocument document, PptSlideRef slide) =>
            slide.Part.SlideLayoutPart?.SlideMasterPart ?? document.PresentationPart.SlideMasterParts.FirstOrDefault()
            ?? throw new ToolInputException("이 파일에는 슬라이드 마스터가 없어서 마스터에 넣을 수 없어요.");

        private static P.ShapeTree MasterTree(PresentationDocument document, PptSlideRef slide) =>
            MasterPart(document, slide).SlideMaster?.CommonSlideData?.ShapeTree
            ?? throw new ToolInputException("이 파일에는 슬라이드 마스터가 없어서 마스터에 넣을 수 없어요.");
    }
}
