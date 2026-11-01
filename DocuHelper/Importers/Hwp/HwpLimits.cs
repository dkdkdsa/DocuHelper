using System.Collections.Generic;
using System.Linq;
using DocuHelper.Docus;

namespace DocuHelper.Importers.Hwp
{
    public static class HwpLimits
    {
        private const string Separator = ", ";
        private const string SkippedReason = "화면에 안 보이고, 저장하면 빠져요.";
        private const string DrawingReason = "모양은 저장하면 빠지고, 안의 글만 남아요.";
        private const string FeatureReason = "이 앱이 아직 저장하지 못해서, 파일에 있으면 저장할 때 빠져요.";
        private const string TocNote = "A table of contents made here is built from heading styles and its page numbers are estimates, so the user should refresh its fields in 한글 for exact numbers. Figure and table lists and indexes cannot be made here; the user can make them in 한글.";
        private const string PromptTail = " in a 한글 file are not shown to you and are not kept when it is saved, so don't promise to change them; the user can fix those in 한글 with [한글에서 열기].";

        public static IReadOnlyList<HwpLimit> For(HwpDocu docu)
        {
            var limits = InFile(docu, out var covered);
            foreach (var feature in HwpCapabilities.Missing.Where(f => !covered.Contains(f)))
            {
                limits.Add(new HwpLimit(HwpLimitKind.Feature, HwpFeatureNames.Korean(feature), FeatureReason, HwpFeatureNames.English(feature)));
            }

            return limits;
        }

        public static string Names(HwpDocu docu)
        {
            var names = InFile(docu, out _).Select(l => l.Text).ToList();
            return names.Count == 0 ? null : string.Join(Separator, names);
        }

        public static string Notice(string fileName, HwpDocu docu)
        {
            var names = InFile(docu, out _).Where(l => l.Kind == HwpLimitKind.Skipped).Select(l => l.Text).ToList();
            if (names.Count == 0)
                return null;

            return $"‘{fileName}’의 {HwpParticle.Topic(string.Join(Separator, names))} 저장하면 빠져요. 원래 모습은 [기록]에 남아 있어요.";
        }

        public static string SettingsNote()
        {
            var names = HwpCapabilities.Missing.Select(HwpFeatureNames.Korean).ToList();
            return names.Count == 0
                ? "지금 이 앱이 다루는 내용은 모두 저장돼요."
                : $"아직 이 앱이 저장하지 못하는 것은 {string.Join(Separator, names)}이에요. 파일에 있으면 저장할 때 빠져요.";
        }

        public static string PromptText()
        {
            var names = HwpFeatureNames.Prompt.Where(f => !HwpCapabilities.Has(f)).Select(HwpFeatureNames.English).ToList();
            var lines = new List<string>();
            if (names.Count > 0)
            {
                var list = names.Count == 1 ? names[0] : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1];
                lines.Add(char.ToUpperInvariant(list[0]) + list.Substring(1) + PromptTail);
            }

            lines.Add(TocNote);
            return string.Join(" ", lines);
        }

        private static List<HwpLimit> InFile(HwpDocu docu, out HashSet<HwpFeature> covered)
        {
            var limits = new List<HwpLimit>();
            covered = new HashSet<HwpFeature>();
            if (docu == null)
                return limits;

            foreach (var skipped in docu.Skipped.OrderBy(s => s))
            {
                limits.Add(new HwpLimit(HwpLimitKind.Skipped, Text(skipped), skipped == HwpSkipped.Drawing ? DrawingReason : SkippedReason, English(skipped)));
                covered.UnionWith(Covers(skipped));
            }

            return limits;
        }

        private static string Text(HwpSkipped skipped) => skipped switch
        {
            HwpSkipped.Picture => "그림",
            HwpSkipped.Equation => "수식",
            HwpSkipped.Chart => "차트",
            HwpSkipped.HeaderFooter => "머리말·꼬리말",
            HwpSkipped.Drawing => "모양 있는 개체",
            _ => "개체",
        };

        private static string English(HwpSkipped skipped) => skipped switch
        {
            HwpSkipped.Picture => "pictures",
            HwpSkipped.Equation => "equations",
            HwpSkipped.Chart => "charts",
            HwpSkipped.HeaderFooter => "headers and footers",
            HwpSkipped.Drawing => "styled objects",
            _ => "other objects",
        };

        private static HwpFeature[] Covers(HwpSkipped skipped) => skipped switch
        {
            HwpSkipped.Picture => new[] { HwpFeature.Pictures },
            HwpSkipped.HeaderFooter => new[] { HwpFeature.HeaderFooter, HwpFeature.PageNumber },
            _ => new[] { HwpFeature.KeepObjects },
        };
    }
}
