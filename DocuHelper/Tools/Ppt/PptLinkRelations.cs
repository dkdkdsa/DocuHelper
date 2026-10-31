using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;

namespace DocuHelper.Tools.Ppt
{
    public static class PptLinkRelations
    {
        public const string SlideJumpAction = "ppaction://hlinksldjump";

        public const string ShowJumpAction = "ppaction://hlinkshowjump?jump=";

        public const string FileAction = "ppaction://hlinkfile";

        public const int MaxUrlLength = 2000;

        public static string NormalizeUrl(string text)
        {
            var value = (text ?? string.Empty).Trim();
            if (value.Length == 0 || value.Length > MaxUrlLength || value.Any(char.IsControl) || value.Any(char.IsWhiteSpace))
                throw new ToolInputException("링크 주소를 읽지 못했어요. https://example.com 처럼 공백 없는 주소로 알려 주세요.");

            if (value.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                value = "https://" + value;
            else if (value.Contains('@') && !value.Contains(':') && !value.Contains('/'))
                value = "mailto:" + value;

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
                throw new ToolInputException("링크 주소를 읽지 못했어요. https://example.com 처럼 http나 https로 시작하는 주소로 알려 주세요.");

            if (uri.Scheme is not ("http" or "https" or "mailto" or "tel" or "ftp"))
                throw new ToolInputException("링크 주소는 http, https, mailto, tel, ftp 주소만 쓸 수 있어요.");

            return uri.OriginalString;
        }

        public static string ExternalId(OpenXmlPartContainer owner, string url)
        {
            var uri = new Uri(url, UriKind.Absolute);
            foreach (var existing in owner.HyperlinkRelationships)
            {
                if (existing.IsExternal && existing.Uri == uri)
                    return existing.Id;
            }

            return owner.AddHyperlinkRelationship(uri, true).Id;
        }

        public static string SlideId(OpenXmlPartContainer owner, SlidePart target)
        {
            foreach (var pair in owner.Parts)
            {
                if (ReferenceEquals(pair.OpenXmlPart, target))
                    return pair.RelationshipId;
            }

            return owner.GetIdOfPart(owner.AddPart(target));
        }

        public static A.HyperlinkOnClick External(OpenXmlPartContainer owner, string url, string tooltip = null)
        {
            var link = new A.HyperlinkOnClick { Id = ExternalId(owner, NormalizeUrl(url)) };
            if (!string.IsNullOrWhiteSpace(tooltip))
                link.Tooltip = tooltip.Trim();

            return link;
        }

        public static A.HyperlinkOnClick SlideJump(SlidePart owner, SlidePart target, string tooltip = null)
        {
            var link = new A.HyperlinkOnClick { Id = SlideId(owner, target), Action = SlideJumpAction };
            if (!string.IsNullOrWhiteSpace(tooltip))
                link.Tooltip = tooltip.Trim();

            return link;
        }

        public static A.HyperlinkOnClick ShowJump(string jump)
        {
            var token = jump switch
            {
                "next" => "nextslide",
                "previous" => "previousslide",
                "first" => "firstslide",
                "last" => "lastslide",
                "end" => "endshow",
                _ => throw new ToolInputException("이동 위치는 next, previous, first, last, end 중 하나여야 해요."),
            };

            return new A.HyperlinkOnClick { Id = string.Empty, Action = ShowJumpAction + token };
        }

        public static PptSceneLink Describe(OpenXmlPart owner, A.HyperlinkType link)
        {
            if (link == null)
                return null;

            var action = link.Action?.Value;
            var id = link.Id?.Value;
            if (!string.IsNullOrEmpty(action) && action.StartsWith(ShowJumpAction, StringComparison.Ordinal))
            {
                var kind = action.Substring(ShowJumpAction.Length) switch
                {
                    "nextslide" => "next",
                    "previousslide" => "previous",
                    "firstslide" => "first",
                    "lastslide" => "last",
                    "endshow" => "end",
                    _ => "other",
                };

                return new PptSceneLink { Kind = kind };
            }

            if (string.IsNullOrEmpty(id))
                return string.IsNullOrEmpty(action) ? null : new PptSceneLink { Kind = "other" };

            if (action == SlideJumpAction)
                return new PptSceneLink { Kind = "slide", Target = SlideNumber(owner, id) };

            var kindOfFile = action == FileAction ? "file" : "url";
            var hyperlink = owner.HyperlinkRelationships.FirstOrDefault(r => r.Id == id);
            if (hyperlink != null)
                return new PptSceneLink { Kind = kindOfFile, Target = hyperlink.Uri.OriginalString };

            var external = owner.ExternalRelationships.FirstOrDefault(r => r.Id == id);
            return external != null
                ? new PptSceneLink { Kind = kindOfFile, Target = external.Uri.OriginalString }
                : new PptSceneLink { Kind = "other" };
        }

        public static int Prune(OpenXmlPart owner)
        {
            var root = owner.RootElement;
            if (root == null)
                return 0;

            var used = PptPartCopier.ReferencedIds(root);
            var removed = 0;
            foreach (var relationship in owner.HyperlinkRelationships.Where(r => !used.Contains(r.Id)).ToList())
            {
                owner.DeleteReferenceRelationship(relationship);
                removed++;
            }

            foreach (var pair in owner.Parts.Where(p => p.OpenXmlPart is SlidePart && !used.Contains(p.RelationshipId)).ToList())
            {
                owner.DeletePart(pair.RelationshipId);
                removed++;
            }

            return removed;
        }

        private static string SlideNumber(OpenXmlPart owner, string relationshipId)
        {
            if (!owner.TryGetPartById(relationshipId, out var part) || part is not SlidePart target)
                return null;

            var document = owner.OpenXmlPackage as PresentationDocument;
            var slide = PptSlides.List(document).FirstOrDefault(s => ReferenceEquals(s.Part, target));
            return slide?.Number.ToString(CultureInfo.InvariantCulture);
        }
    }
}
