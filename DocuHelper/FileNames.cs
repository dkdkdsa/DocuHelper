namespace DocuHelper
{
    public static class FileNames
    {
        public const string ForbiddenText = "\\ / : * ? \" < > |";

        private static readonly char[] s_forbidden = { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };

        public static bool HasForbidden(string name) => name != null && name.IndexOfAny(s_forbidden) >= 0;

        public static string Problem(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "이름을 적어 주세요.";

            if (HasForbidden(name))
                return $"이름에 {ForbiddenText} 는 넣을 수 없어요.";

            if (name.TrimEnd().EndsWith("."))
                return "이름 끝에는 점(.)을 넣을 수 없어요.";

            return null;
        }
    }
}
