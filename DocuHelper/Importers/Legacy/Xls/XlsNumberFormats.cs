using System.Collections.Generic;

namespace DocuHelper.Importers.Legacy.Xls
{
    internal sealed class XlsNumberFormats
    {
        private const uint FirstCustomId = 164;

        private static readonly Dictionary<int, string> s_localized = new()
        {
            [27] = "yyyy\"년\" m\"월\"",
            [28] = "m\"월\" d\"일\"",
            [29] = "m\"월\" d\"일\"",
            [30] = "m-d-yy",
            [31] = "yyyy\"년\" m\"월\" d\"일\"",
            [32] = "h\"시\" mm\"분\"",
            [33] = "h\"시\" mm\"분\" ss\"초\"",
            [34] = "AM/PM h\"시\" mm\"분\"",
            [35] = "AM/PM h\"시\" mm\"분\" ss\"초\"",
            [36] = "yyyy\"년\" m\"월\"",
            [50] = "yyyy\"년\" m\"월\"",
            [51] = "m\"월\" d\"일\"",
            [52] = "yyyy\"년\" m\"월\"",
            [53] = "m\"월\" d\"일\"",
            [54] = "m\"월\" d\"일\"",
            [55] = "AM/PM h\"시\" mm\"분\"",
            [56] = "AM/PM h\"시\" mm\"분\" ss\"초\"",
            [57] = "yyyy\"년\" m\"월\"",
            [58] = "m\"월\" d\"일\"",
        };

        private readonly Dictionary<int, string> _recorded;
        private readonly Dictionary<int, uint> _assigned = new();
        private readonly List<(uint Id, string Code)> _custom = new();

        public XlsNumberFormats(Dictionary<int, string> recorded)
        {
            _recorded = recorded;
        }

        public IReadOnlyList<(uint Id, string Code)> Custom => _custom;

        public uint Resolve(int formatId)
        {
            if (_assigned.TryGetValue(formatId, out var known))
                return known;

            string code;
            uint id;
            if (_recorded.TryGetValue(formatId, out var recorded) && !string.IsNullOrEmpty(recorded))
                code = recorded;
            else if (formatId <= 22 || (formatId >= 37 && formatId <= 49))
                code = null;
            else if (!s_localized.TryGetValue(formatId, out code))
                code = "General";

            if (code == null)
            {
                id = (uint)formatId;
            }
            else if (code == "General")
            {
                id = 0;
            }
            else
            {
                id = FirstCustomId + (uint)_custom.Count;
                _custom.Add((id, code));
            }

            _assigned[formatId] = id;
            return id;
        }
    }
}
