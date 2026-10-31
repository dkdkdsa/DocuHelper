using System;
using System.Text.RegularExpressions;

namespace DocuHelper.Tools.Ppt
{
    public sealed class PptLinkSpec
    {
        public const int MaxTooltip = 255;

        public const int MaxSubject = 200;

        private static readonly Regex SchemePattern = new(@"^[a-z][a-z0-9+.\-]*:(?!\d+(?:[/?#]|$))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex AddressPattern = new(@"^[^@\s<>""?&,;:]+@[^@\s<>""?&,;:]+\.[^@\s<>""?&,;:]+$", RegexOptions.CultureInvariant);

        private PptLinkSpec(string type, string url, int slide, string tooltip)
        {
            Type = type;
            Url = url;
            Slide = slide;
            Tooltip = tooltip;
        }

        public string Type { get; }

        public string Url { get; }

        public int Slide { get; }

        public string Tooltip { get; }

        public bool IsNone => Type == "none";

        public bool IsExternal => Type is "url" or "email";

        public static PptLinkSpec Parse(ToolInput input, string name = "target")
        {
            var nested = PptInputs.Object(input, name) ?? throw new ToolInputException($"'{name}' 값이 필요해요.");
            var type = PptInputs.Choice(
                nested,
                "type",
                "url",
                "email",
                "slide",
                "previous_slide|previous",
                "next_slide|next",
                "first_slide|first",
                "last_slide|last",
                "none|remove") ?? throw new ToolInputException($"'{name}'의 type 값이 필요해요. url, email, slide, previous_slide, next_slide, first_slide, last_slide, none 중 하나로 알려 주세요.");

            var tooltip = type == "none" ? null : PptInputs.Text(nested, "tooltip", MaxTooltip);
            if (string.IsNullOrEmpty(tooltip))
                tooltip = null;

            switch (type)
            {
                case "url":
                    var address = nested.GetOptionalString("url");
                    if (string.IsNullOrWhiteSpace(address))
                        throw new ToolInputException("웹 주소가 필요해요. target의 url에 https://example.com 처럼 알려 주세요.");

                    return new PptLinkSpec(type, WebAddress(address), 0, tooltip);
                case "email":
                    return new PptLinkSpec(type, MailAddress(nested), 0, tooltip);
                case "slide":
                    var number = PptInputs.Integer(nested, "slide", 1, int.MaxValue)
                        ?? throw new ToolInputException("이동할 슬라이드 번호가 필요해요. target의 slide에 번호를 알려 주세요.");

                    return new PptLinkSpec(type, null, number, tooltip);
                default:
                    return new PptLinkSpec(type, null, 0, tooltip);
            }
        }

        public static string WebAddress(string text)
        {
            var value = (text ?? string.Empty).Trim();
            if (SchemePattern.IsMatch(value))
            {
                var scheme = value.Substring(0, value.IndexOf(':'));
                if (!scheme.Equals("http", StringComparison.OrdinalIgnoreCase) && !scheme.Equals("https", StringComparison.OrdinalIgnoreCase) && !scheme.Equals("mailto", StringComparison.OrdinalIgnoreCase))
                    throw new ToolInputException("링크 주소는 http, https, mailto 주소만 쓸 수 있어요.");
            }
            else if (value.StartsWith("//", StringComparison.Ordinal))
                value = "https:" + value;
            else if (value.Length > 0)
                value = "https://" + value;

            var normalized = PptLinkRelations.NormalizeUrl(value);
            var uri = new Uri(normalized, UriKind.Absolute);
            if (uri.Scheme is "http" or "https" && string.IsNullOrEmpty(uri.Host))
                throw new ToolInputException("링크 주소를 읽지 못했어요. https://example.com 처럼 사이트 이름이 들어간 주소로 알려 주세요.");

            return normalized;
        }

        private static string MailAddress(ToolInput target)
        {
            var address = (target.GetOptionalString("address") ?? string.Empty).Trim();
            if (address.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                address = address.Substring(7);

            if (address.Length == 0)
                throw new ToolInputException("메일 주소가 필요해요. target의 address에 name@example.com 처럼 알려 주세요.");

            if (!AddressPattern.IsMatch(address))
                throw new ToolInputException("메일 주소를 읽지 못했어요. name@example.com 처럼 알려 주세요.");

            var subject = PptInputs.Text(target, "subject", MaxSubject);
            var text = "mailto:" + address;
            if (!string.IsNullOrEmpty(subject))
                text += "?subject=" + Uri.EscapeDataString(subject);

            return PptLinkRelations.NormalizeUrl(text);
        }
    }
}
