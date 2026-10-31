using DocuHelper.Docus;

namespace DocuHelperEditor.State.Ribbon
{
    public static partial class RibbonIcons
    {
        private static readonly object s_gate = new object();

        public static void Register(string name, string path, DocuType? format = null)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path))
                return;

            lock (s_gate)
            {
                var target = format.HasValue ? OverridesOf(format.Value) : s_shared;
                target[name] = path.Trim();
            }
        }
    }
}
