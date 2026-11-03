using System.Globalization;
using DocuHelper.Dash;

namespace DocuHelperEditor.Components.Paper.Dash
{
    internal static class DashNaming
    {
        public static string Free(DashDocument document, string stem)
        {
            for (var n = 1; ; n++)
            {
                var name = stem + n.ToString(CultureInfo.InvariantCulture);
                if (!Taken(document, name))
                    return name;
            }
        }

        public static string Problem(DashDocument document, string name, bool source)
        {
            var text = name?.Trim();
            if (string.IsNullOrEmpty(text))
                return "이름을 써 주세요.";

            var taken = source
                ? document.FindSource(text) != null
                : document.FindValue(text) != null || document.FindParam(text) != null;
            return taken ? $"‘{text}’ 이름은 벌써 있어요. 다른 이름을 골라 주세요." : null;
        }

        private static bool Taken(DashDocument document, string name) =>
            document.FindSource(name) != null || document.FindValue(name) != null || document.FindParam(name) != null;
    }
}
