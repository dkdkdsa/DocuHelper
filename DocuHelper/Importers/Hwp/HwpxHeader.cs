using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace DocuHelper.Importers.Hwp
{
    internal sealed class HwpxHeader
    {
        private const int MaxLevels = 10;

        private readonly Dictionary<int, HwpxCharShape> _charShapes = new();
        private readonly Dictionary<int, HwpxParaShape> _paraShapes = new();
        private readonly Dictionary<int, HwpxNumberingLevel[]> _numberings = new();
        private readonly Dictionary<int, (string Normal, string Checked)> _bullets = new();
        private readonly HwpxCharShape _defaultChar = new();
        private readonly HwpxParaShape _defaultPara = new();

        public HwpxHeader(XElement root)
        {
            var fonts = Fonts(root);
            foreach (var element in root.Descendants())
            {
                var parent = element.Parent?.Name.LocalName;
                switch (element.Name.LocalName)
                {
                    case "charPr" when parent == "charProperties":
                        _charShapes[HwpxXml.Int(element, "id", -1)] = CharShape(element, fonts);
                        break;
                    case "paraPr" when parent == "paraProperties":
                        _paraShapes[HwpxXml.Int(element, "id", -1)] = ParaShape(element);
                        break;
                    case "numbering" when parent == "numberings":
                        _numberings[HwpxXml.Int(element, "id", -1)] = Levels(element);
                        break;
                    case "bullet" when parent == "bullets":
                        _bullets[HwpxXml.Int(element, "id", -1)] = Bullet(element);
                        break;
                }
            }
        }

        public HwpxCharShape CharShape(int id) => _charShapes.TryGetValue(id, out var shape) ? shape : _defaultChar;

        public HwpxParaShape ParaShape(int id) => _paraShapes.TryGetValue(id, out var shape) ? shape : _defaultPara;

        public HwpxNumberingLevel[] Numbering(int id) => _numberings.TryGetValue(id, out var levels) ? levels : null;

        public string Bullet(int id, bool isChecked) =>
            _bullets.TryGetValue(id, out var bullet) ? (isChecked ? bullet.Checked : bullet.Normal) : null;

        private static Dictionary<int, string> Fonts(XElement root)
        {
            var fonts = new Dictionary<int, string>();
            var face = root.Descendants().FirstOrDefault(e =>
                e.Name.LocalName == "fontface" && string.Equals(HwpxXml.Attribute(e, "lang"), "HANGUL", StringComparison.OrdinalIgnoreCase));
            var index = 0;
            foreach (var font in HwpxXml.Children(face, "font"))
            {
                var name = HwpxXml.Attribute(font, "face");
                if (!string.IsNullOrWhiteSpace(name))
                    fonts[HwpxXml.Int(font, "id", index)] = name.Trim();

                index++;
            }

            return fonts;
        }

        private static HwpxCharShape CharShape(XElement element, Dictionary<int, string> fonts)
        {
            var height = HwpxXml.Int(element, "height", 1000);
            var shape = new HwpxCharShape
            {
                Size = height > 0 ? height : 1000,
                Color = Color(HwpxXml.Attribute(element, "textColor")),
            };
            foreach (var child in HwpxXml.Children(element))
            {
                switch (child.Name.LocalName)
                {
                    case "fontRef":
                        shape.Font = fonts.TryGetValue(HwpxXml.Int(child, "hangul", -1), out var font) ? font : null;
                        break;
                    case "bold":
                        shape.Bold = true;
                        break;
                    case "italic":
                        shape.Italic = true;
                        break;
                    case "underline":
                        shape.Underline = IsLine(HwpxXml.Attribute(child, "type"));
                        break;
                    case "strikeout":
                        var line = HwpxXml.Attribute(child, "shape");
                        shape.Strike = IsLine(line) && line != "3D";
                        break;
                    case "supscript":
                        shape.Superscript = true;
                        break;
                    case "subscript":
                        shape.Subscript = true;
                        break;
                }
            }

            return shape;
        }

        private static HwpxParaShape ParaShape(XElement element)
        {
            var heading = HwpxXml.Child(element, "heading");
            var exact = Find(element, "margin", true);
            var legacy = Find(element, "margin", false);
            var spacing = Find(element, "lineSpacing", true) ?? Find(element, "lineSpacing", false);
            var percent = spacing != null && (HwpxXml.Attribute(spacing, "type") ?? "PERCENT") == "PERCENT";
            return new HwpxParaShape
            {
                Alignment = Alignment(HwpxXml.Attribute(HwpxXml.Child(element, "align"), "horizontal")),
                LeftIndent = Length(exact, legacy, "left"),
                FirstLineIndent = Length(exact, legacy, "intent"),
                SpaceBefore = Length(exact, legacy, "prev"),
                SpaceAfter = Length(exact, legacy, "next"),
                LineSpacing = percent ? Math.Max(0, HwpxXml.Int(spacing, "value", 0)) : 0,
                PageBreakBefore = HwpxXml.Flag(HwpxXml.Child(element, "breakSetting"), "pageBreakBefore"),
                HeadingType = HwpxXml.Attribute(heading, "type") ?? "NONE",
                HeadingId = HwpxXml.Int(heading, "idRef", 0),
                HeadingLevel = HwpxXml.Int(heading, "level", 0),
                Checked = HwpxXml.Flag(element, "checked"),
            };
        }

        private static XElement Find(XElement element, string name, bool exact)
        {
            foreach (var child in element.Elements())
            {
                if (!exact && child.Name.LocalName == name)
                    return child;

                if (child.Name.LocalName != "switch")
                    continue;

                foreach (var option in child.Elements())
                {
                    var wanted = exact
                        ? option.Name.LocalName == "case"
                            && (HwpxXml.Attribute(option, "required-namespace") ?? string.Empty).EndsWith("HwpUnitChar", StringComparison.Ordinal)
                        : option.Name.LocalName == "default";
                    if (!wanted)
                        continue;

                    var found = option.Elements().FirstOrDefault(e => e.Name.LocalName == name);
                    if (found != null)
                        return found;
                }
            }

            return null;
        }

        private static int Length(XElement exact, XElement legacy, string name)
        {
            var value = Item(exact, name);
            if (value != null && !string.Equals(HwpxXml.Attribute(value, "unit"), "CHAR", StringComparison.OrdinalIgnoreCase))
                return HwpxXml.Int(value, "value", 0);

            var old = Item(legacy, name);
            return old == null ? 0 : HwpxXml.Int(old, "value", 0) / 2;
        }

        private static XElement Item(XElement margin, string name) =>
            margin?.Elements().FirstOrDefault(e => e.Name.LocalName == name || (name == "intent" && e.Name.LocalName == "indent"));

        private static HwpxNumberingLevel[] Levels(XElement element)
        {
            var levels = new Dictionary<int, HwpxNumberingLevel>();
            foreach (var head in HwpxXml.Children(element, "paraHead"))
            {
                var level = HwpxXml.Int(head, "level", levels.Count + 1);
                if (level < 1 || level > MaxLevels || levels.ContainsKey(level))
                    continue;

                levels[level] = new HwpxNumberingLevel
                {
                    Start = HwpxXml.Int(head, "start", 1),
                    Format = HwpxXml.Attribute(head, "numFormat") ?? "DIGIT",
                    Text = head.Value,
                };
            }

            if (levels.Count == 0)
                return [];

            var result = new HwpxNumberingLevel[levels.Keys.Max()];
            for (var i = 0; i < result.Length; i++)
                result[i] = levels.TryGetValue(i + 1, out var level) ? level : new HwpxNumberingLevel();

            return result;
        }

        private static (string Normal, string Checked) Bullet(XElement element)
        {
            var normal = HwpxNumberFormat.Symbol(HwpxXml.Attribute(element, "char"));
            var marked = HwpxXml.Attribute(element, "checkedChar");
            return (normal, string.IsNullOrEmpty(marked) ? normal : HwpxNumberFormat.Symbol(marked));
        }

        private static HwpAlignment Alignment(string value) => value switch
        {
            "LEFT" => HwpAlignment.Left,
            "RIGHT" => HwpAlignment.Right,
            "CENTER" => HwpAlignment.Center,
            "DISTRIBUTE" or "DISTRIBUTE_SPACE" => HwpAlignment.Distribute,
            _ => HwpAlignment.Justify,
        };

        private static bool IsLine(string type) => !string.IsNullOrEmpty(type) && type != "NONE";

        private static string Color(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var hex = value.Trim().TrimStart('#');
            if (hex.Length == 8)
                hex = hex.Substring(2);

            if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
                return null;

            hex = hex.ToUpperInvariant();
            return hex == "000000" ? null : hex;
        }
    }
}
