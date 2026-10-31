using DocuHelper.Docus;
using DocuHelper.Tools;

namespace DocuHelper.Importers.Hwp
{
    public static class HwpGuard
    {
        private const string Instead = "한글에서 직접 고쳐 주세요.";
        private const string FallbackName = "이 기능";

        public static bool Allows(Docu docu, HwpFeature feature) => docu is not HwpDocu || HwpCapabilities.Has(feature);

        public static void Require(Docu docu, HwpFeature feature, string name = null, string instead = null)
        {
            if (Allows(docu, feature))
                return;

            throw Refusal(string.IsNullOrWhiteSpace(name) ? HwpFeatureNames.Korean(feature) : name, true, instead);
        }

        public static void Refuse(Docu docu, string name, string instead = null)
        {
            if (docu is HwpDocu)
                throw Refusal(string.IsNullOrWhiteSpace(name) ? FallbackName : name, false, instead);
        }

        private static ToolInputException Refusal(string name, bool temporary, string instead)
        {
            var when = temporary ? "아직 " : string.Empty;
            var hint = string.IsNullOrWhiteSpace(instead) ? Instead : instead.Trim();
            return new ToolInputException($"{HwpParticle.Topic(name.Trim())} 한글 파일에 저장되지 않아서 {when}쓸 수 없어요. {hint}");
        }
    }
}
