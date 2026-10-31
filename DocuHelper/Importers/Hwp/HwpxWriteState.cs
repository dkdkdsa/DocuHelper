using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocuHelper.Tools.Word;
using DocumentFormat.OpenXml.Packaging;

namespace DocuHelper.Importers.Hwp
{
    internal sealed partial class HwpxWriteState
    {
        private const string DefaultFont = "함초롬바탕";
        private const int DefaultHeight = 1000;
        private const int DefaultLineSpacing = 160;
        private const int PlainBorder = 1;
        private const int CellBorder = 2;
        private const int RowHeight = 1000;
        private const int MaxDepth = 32;

        private readonly WordprocessingDocument _document;
        private readonly WordStyles _styles;
        private readonly WordNumbering _numbering;
        private readonly string _defaultFont;
        private readonly int _defaultHeight;
        private int _depth;
        private readonly Dictionary<string, int> _pageBorders = new(StringComparer.Ordinal);
        private readonly List<(int Id, HwpBorderLine[] Lines, string Fill)> _pageBorderSpecs = new();

        public HwpxWriteState(WordprocessingDocument document)
        {
            _document = document;
            _styles = new WordStyles(document);
            _numbering = new WordNumbering(document, _styles);
            _defaultFont = FontOf(_styles.Root?.DocDefaults?.RunPropertiesDefault?.RunPropertiesBaseStyle?.RunFonts) ?? DefaultFont;
            _defaultHeight = HeightOf(_styles.Root?.DocDefaults?.RunPropertiesDefault?.RunPropertiesBaseStyle?.FontSize) ?? DefaultHeight;
            CharId(new HwpxCharKey(_defaultFont, _defaultHeight, false, false, false, false, null, false, false));
            ParaId(new HwpxParaKey("JUSTIFY", 0, 0, 0, 0, 0, DefaultLineSpacing, false));
        }

        private int PageBorderId(HwpBorderLine[] lines, string fill)
        {
            var key = string.Join("|", lines.Select(l => l == null ? "-" : l.Type + "/" + l.WidthMm.ToString("0.00", CultureInfo.InvariantCulture) + "/" + l.Color)) + "|" + (fill ?? "-");
            if (_pageBorders.TryGetValue(key, out var id))
                return id;

            id = 3 + _fills.Count + _pageBorderSpecs.Count;
            _pageBorders[key] = id;
            _pageBorderSpecs.Add((id, lines, fill));
            return id;
        }

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static int Units(int twips) => twips * 5;

        private static int ParseInt(string value, int fallback) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : fallback;
    }
}
