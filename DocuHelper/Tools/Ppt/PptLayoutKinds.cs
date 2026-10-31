using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public static class PptLayoutKinds
    {
        public const string Title = "title";

        public const string Content = "title_content";

        public const string Section = "section";

        public const string TwoContent = "two_content";

        public const string Comparison = "comparison";

        public const string TitleOnly = "title_only";

        public const string Blank = "blank";

        public const string ContentCaption = "content_caption";

        public const string PictureCaption = "picture_caption";

        public const string SameAsCurrent = "same_as_current";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Title, Content, Section, TwoContent, Comparison, TitleOnly, Blank, ContentCaption, PictureCaption,
        };

        private static readonly Dictionary<string, string> s_tokens = BuildTokens();

        public static string Parse(string token)
        {
            var key = Normalize(token);
            return key.Length > 0 && s_tokens.TryGetValue(key, out var kind) ? kind : null;
        }

        public static string Normalize(string token) =>
            (token ?? string.Empty).Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_').Replace("(", string.Empty).Replace(")", string.Empty);

        public static P.SlideLayoutValues TypeOf(string kind) => kind switch
        {
            Title => P.SlideLayoutValues.Title,
            Content => P.SlideLayoutValues.Object,
            Section => P.SlideLayoutValues.SectionHeader,
            TwoContent => P.SlideLayoutValues.TwoObjects,
            Comparison => P.SlideLayoutValues.TwoTextAndTwoObjects,
            TitleOnly => P.SlideLayoutValues.TitleOnly,
            Blank => P.SlideLayoutValues.Blank,
            ContentCaption => P.SlideLayoutValues.ObjectText,
            _ => P.SlideLayoutValues.PictureText,
        };

        public static string EnglishName(string kind) => kind switch
        {
            Title => "Title Slide",
            Content => "Title and Content",
            Section => "Section Header",
            TwoContent => "Two Content",
            Comparison => "Comparison",
            TitleOnly => "Title Only",
            Blank => "Blank",
            ContentCaption => "Content with Caption",
            _ => "Picture with Caption",
        };

        public static string KoreanName(string kind) => kind switch
        {
            Title => "제목 슬라이드",
            Content => "제목 및 내용",
            Section => "구역 머리글",
            TwoContent => "내용 2개",
            Comparison => "비교",
            TitleOnly => "제목만",
            Blank => "빈 화면",
            ContentCaption => "내용(캡션 포함)",
            _ => "그림(캡션 포함)",
        };

        public static string Classify(SlideLayoutPart layout)
        {
            var root = layout?.SlideLayout;
            if (root == null)
                return null;

            var typed = TypedKind(root);
            if (typed != null)
                return typed;

            if (root.Type != null && root.Type.HasValue && root.Type.Value != P.SlideLayoutValues.Custom)
                return null;

            return Infer(root);
        }

        public static string TypedKind(P.SlideLayout root)
        {
            if (root?.Type == null || !root.Type.HasValue)
                return null;

            var type = root.Type.Value;
            foreach (var kind in All)
            {
                if (TypeOf(kind) == type)
                    return kind;
            }

            return null;
        }

        private static string Infer(P.SlideLayout root)
        {
            var types = root.CommonSlideData?.ShapeTree?.Elements<P.Shape>()
                .Select(PptPlaceholder.Of)
                .Where(p => p != null)
                .Select(PptPlaceholder.TypeOf)
                .Where(t => PptPlaceholder.RoleOf(t) != PptShapeRole.Footer)
                .ToList() ?? new List<string>();

            var titles = types.Count(t => PptPlaceholder.Family(t) == "title");
            var others = types.Where(t => PptPlaceholder.Family(t) != "title").ToList();
            if (types.Count == 0)
                return Blank;

            if (titles == 0)
                return null;

            if (others.Count == 1 && others[0] == "subTitle")
                return Title;

            if (others.Count == 0)
                return TitleOnly;

            if (others.Contains("pic") && others.Count == 2)
                return PictureCaption;

            var objects = others.Count(t => t == "obj");
            var texts = others.Count(t => t == "body" || t == "subTitle");
            if (others.Count == 4 && objects == 2 && texts == 2)
                return Comparison;

            if (others.Count == 2 && objects == 2)
                return TwoContent;

            if (others.Count == 2 && objects == 1 && texts == 1)
                return ContentCaption;

            if (others.Count == 1 && objects == 1)
                return Content;

            return null;
        }

        private static Dictionary<string, string> BuildTokens()
        {
            var map = new Dictionary<string, string>();
            foreach (var kind in All)
            {
                map[kind] = kind;
                map[Normalize(EnglishName(kind))] = kind;
                map[Normalize(KoreanName(kind))] = kind;
            }

            foreach (var (kind, aliases) in new (string, string[])[]
            {
                (Title, new[] { "cover", "title_slide", "제목", "표지" }),
                (Content, new[] { "content", "title_and_content", "title_and_body", "body", "title_body", "제목_및_내용", "제목과_내용" }),
                (Section, new[] { "section_header", "section_title", "구역", "구역_제목" }),
                (TwoContent, new[] { "two_contents", "two_column", "two_columns", "2_content", "콘텐츠_2개", "내용_두개" }),
                (Comparison, new[] { "compare" }),
                (TitleOnly, new[] { "only_title", "제목_만" }),
                (Blank, new[] { "empty", "빈", "빈_슬라이드" }),
                (ContentCaption, new[] { "content_with_caption", "caption_content" }),
                (PictureCaption, new[] { "picture_with_caption", "caption_picture", "image_caption" }),
            })
            {
                foreach (var alias in aliases)
                    map[Normalize(alias)] = kind;
            }

            return map;
        }
    }
}
