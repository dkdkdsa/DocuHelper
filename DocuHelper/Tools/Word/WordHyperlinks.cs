using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public sealed record WordLinkTarget(string Address, string Anchor);

    public static class WordHyperlinks
    {
        public const int MaxAddressLength = 2000;

        private static readonly string[] s_schemes = { "http", "https", "mailto", "tel" };
        private static readonly Regex s_scheme = new(@"^(?<scheme>[A-Za-z][A-Za-z0-9+\-]*):", RegexOptions.CultureInvariant);
        private static readonly Regex s_email = new(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.CultureInvariant);

        public static WordLinkTarget Target(WordDocu docu, string url, string email, string bookmark)
        {
            var given = new[] { url, email, bookmark }.Count(v => !string.IsNullOrWhiteSpace(v));
            if (given == 0)
                throw new ToolInputException("링크 주소('hyperlink.url'), 메일 주소('hyperlink.email'), 책갈피 이름('hyperlink.bookmark') 중 하나를 넣어 주세요.");

            if (given > 1)
                throw new ToolInputException("링크 대상은 한 가지만 고를 수 있어요. url, email, bookmark 중 하나만 넣어 주세요.");

            if (!string.IsNullOrWhiteSpace(bookmark))
            {
                var name = bookmark.Trim();
                if (WordBookmarks.Find(docu.Document, name) == null)
                    throw new ToolInputException($"‘{name}’ 책갈피를 찾지 못했어요. 책갈피를 먼저 넣거나 이름을 다시 확인해 주세요.");

                return new WordLinkTarget(null, name);
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                var address = email.Trim();
                if (address.Length > MaxAddressLength || !s_email.IsMatch(address))
                    throw new ToolInputException("메일 주소 형식이 올바르지 않아요.");

                return new WordLinkTarget("mailto:" + address, null);
            }

            return new WordLinkTarget(Address(url), null);
        }

        public static Hyperlink Element(WordDocu docu, WordLinkTarget target, string tooltip)
        {
            var link = new Hyperlink { History = true };
            if (target.Address != null)
                link.Id = RelationshipId(docu.Document, target.Address);
            else
                link.Anchor = target.Anchor;

            if (!string.IsNullOrWhiteSpace(tooltip))
                link.Tooltip = WordText.Clean(tooltip).Trim();

            return link;
        }

        public static void Retarget(WordDocu docu, Hyperlink link, WordLinkTarget target, string tooltip)
        {
            link.Id = target.Address != null ? RelationshipId(docu.Document, target.Address) : null;
            link.Anchor = target.Anchor;
            if (!string.IsNullOrWhiteSpace(tooltip))
                link.Tooltip = WordText.Clean(tooltip).Trim();
        }

        public static Hyperlink Wrap(WordDocu docu, WordStyles styles, IReadOnlyList<Run> runs, WordLinkTarget target, string tooltip, RevisionContext ctx)
        {
            if (runs.Count == 0)
                throw new ToolInputException("링크로 만들 글을 골라 주세요.");

            var parent = runs[0].Parent;
            if (runs.Any(r => !ReferenceEquals(r.Parent, parent)))
                throw new ToolInputException("여러 조각에 걸친 글에는 링크를 걸 수 없어요. 글을 나눠서 골라 주세요.");

            if (parent is Hyperlink)
                throw new ToolInputException("이미 링크가 걸린 글이에요. 링크를 바꾸려면 링크를 고치기로 해 주세요.");

            var children = parent.ChildElements.ToList();
            runs = runs.Distinct().OrderBy(r => children.IndexOf(r)).ToList();

            IReadOnlyList<OpenXmlElement> units = runs;
            if (parent is InsertedRun wrapper)
            {
                if (wrapper.ChildElements.Any(c => !runs.Any(r => ReferenceEquals(r, c))))
                    throw new ToolInputException("추적 중인 고침의 일부에는 링크를 걸 수 없어요. 고침 전체를 골라 주세요.");

                units = new OpenXmlElement[] { wrapper };
            }

            var link = Element(docu, target, tooltip);
            units[0].InsertBeforeSelf(link);
            foreach (var unit in units.ToList())
            {
                unit.Remove();
                link.AppendChild(unit);
            }

            Style(styles, runs, ctx);
            return link;
        }

        public static void Unwrap(WordStyles styles, Hyperlink link, RevisionContext ctx)
        {
            Unstyle(styles, link.Descendants<Run>().ToList(), ctx);
            foreach (var child in link.ChildElements.ToList())
            {
                child.Remove();
                link.InsertBeforeSelf(child);
            }

            link.Remove();
        }

        public static void Style(WordStyles styles, IEnumerable<Run> runs, RevisionContext ctx)
        {
            var id = WordBuiltinStyles.Ensure(styles, "Hyperlink");
            foreach (var run in runs.ToList())
            {
                var props = run.RunProperties ?? (run.RunProperties = new RunProperties());
                if (props.Elements<RunStyle>().Any(s => s.Val?.Value == id))
                    continue;

                var old = (RunProperties)props.CloneNode(true);
                if (ctx != null && run.Parent is not InsertedRun && !props.Elements<RunPropertiesChange>().Any())
                    props.AppendChild(WordRevisions.RecordFormat(old, ctx));

                props.RemoveAllChildren<RunStyle>();
                props.InsertAt(new RunStyle { Val = id }, 0);
            }
        }

        public static void Unstyle(WordStyles styles, IEnumerable<Run> runs, RevisionContext ctx)
        {
            var id = WordBuiltinStyles.Ensure(styles, "Hyperlink");
            foreach (var run in runs.ToList())
            {
                var props = run.RunProperties;
                if (props == null || !props.Elements<RunStyle>().Any(s => s.Val?.Value == id))
                    continue;

                var old = (RunProperties)props.CloneNode(true);
                if (ctx != null && run.Parent is not InsertedRun && !props.Elements<RunPropertiesChange>().Any())
                    props.AppendChild(WordRevisions.RecordFormat(old, ctx));

                props.RemoveAllChildren<RunStyle>();
                if (!props.HasChildren)
                    run.RunProperties = null;
            }
        }

        private static string Address(string url)
        {
            var value = (url ?? string.Empty).Trim();
            if (value.Length == 0 || value.Length > MaxAddressLength)
                throw new ToolInputException("링크 주소는 1자 이상 2000자 이내로 써 주세요.");

            if (value.Any(char.IsControl) || value.Any(char.IsWhiteSpace))
                throw new ToolInputException("링크 주소에는 공백이나 줄 바꿈을 넣을 수 없어요.");

            var match = s_scheme.Match(value);
            if (!match.Success)
                value = "https://" + value;
            else if (!s_schemes.Contains(match.Groups["scheme"].Value.ToLowerInvariant()))
                throw new ToolInputException($"‘{match.Groups["scheme"].Value}:’ 링크는 넣을 수 없어요. http, https, mailto, tel 링크만 쓸 수 있어요.");

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
                throw new ToolInputException("링크 주소 형식이 올바르지 않아요.");

            var scheme = uri.Scheme.ToLowerInvariant();
            if (!s_schemes.Contains(scheme) || ((scheme == "http" || scheme == "https") && string.IsNullOrEmpty(uri.Host)))
                throw new ToolInputException("링크 주소 형식이 올바르지 않아요. http, https, mailto, tel 링크만 쓸 수 있어요.");

            return uri.AbsoluteUri;
        }

        private static string RelationshipId(WordprocessingDocument document, string address)
        {
            var main = document.MainDocumentPart;
            var existing = main.HyperlinkRelationships.FirstOrDefault(r => r.IsExternal && r.Uri.OriginalString == address);
            return existing?.Id ?? main.AddHyperlinkRelationship(new Uri(address), true).Id;
        }
    }
}
