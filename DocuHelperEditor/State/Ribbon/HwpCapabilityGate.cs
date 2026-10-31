using System;
using System.Collections.Generic;
using DocuHelper.Importers.Hwp;

namespace DocuHelperEditor.State.Ribbon
{
    internal static class HwpCapabilityGate
    {
        private const string TextWidthGate = "WordTextWidth";
        private const string ListEnclosedGate = "WordListEnclosed";

        public static string Blocker(IReadOnlyList<string> gates, string reason, Func<string, string, bool> schemaHas)
        {
            foreach (var gate in gates)
            {
                if (!IsOpen(gate, schemaHas))
                    return string.IsNullOrWhiteSpace(reason) ? RibbonRouter.PendingReason : reason;
            }

            return null;
        }

        private static bool IsOpen(string gate, Func<string, string, bool> schemaHas)
        {
            switch (gate)
            {
                case TextWidthGate:
                    return schemaHas("format_text", "\"width\"");
                case ListEnclosedGate:
                    return schemaHas("set_list", "decimal_enclosed_paren");
            }

            return Enum.TryParse<HwpFeature>(gate, out var feature) && Enum.IsDefined(feature) && HwpCapabilities.Has(feature);
        }
    }
}
