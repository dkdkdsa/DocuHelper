using System;
using System.Collections.Generic;
using System.Globalization;
using DocumentFormat.OpenXml;
using A = DocumentFormat.OpenXml.Drawing;

namespace DocuHelper.Tools.Ppt
{
    public class PptShapeStyle
    {
        private const double EmuPerPoint = 12700;

        public PptShapeStyle(string family, IReadOnlyList<OpenXmlElement> listStyles, IReadOnlyList<A.BodyProperties> bodies, A.Transform2D transform)
        {
            Family = family;
            ListStyles = listStyles;
            Bodies = bodies;
            Transform = transform;
        }

        public string Family { get; }

        public IReadOnlyList<OpenXmlElement> ListStyles { get; }

        public IReadOnlyList<A.BodyProperties> Bodies { get; }

        public A.Transform2D Transform { get; }

        public PptAutofit Autofit
        {
            get
            {
                foreach (var body in Bodies)
                {
                    if (body.GetFirstChild<A.NormalAutoFit>() != null)
                        return PptAutofit.Shrink;

                    if (body.GetFirstChild<A.ShapeAutoFit>() != null)
                        return PptAutofit.Resize;

                    if (body.GetFirstChild<A.NoAutoFit>() != null)
                        return PptAutofit.None;
                }

                return PptAutofit.None;
            }
        }

        public string TextAnchor
        {
            get
            {
                var anchor = BodyValue(b => b.Anchor);
                return anchor != null && anchor.HasValue ? anchor.InnerText : "t";
            }
        }

        public bool NoWrap
        {
            get
            {
                var wrap = BodyValue(b => b.Wrap);
                return wrap != null && wrap.HasValue && wrap.Value == A.TextWrappingValues.None;
            }
        }

        public long Inset(Func<A.BodyProperties, Int32Value> pick, long fallback)
        {
            var value = BodyValue(pick);
            return value != null && value.HasValue ? value.Value : fallback;
        }

        public T BodyValue<T>(Func<A.BodyProperties, T> pick)
            where T : class
        {
            foreach (var body in Bodies)
            {
                var value = pick(body);
                if (value != null)
                    return value;
            }

            return null;
        }

        public T ParagraphValue<T>(A.Paragraph paragraph, int level, Func<A.TextParagraphPropertiesType, T> pick)
            where T : class
        {
            var own = paragraph?.ParagraphProperties;
            if (own != null)
            {
                var value = pick(own);
                if (value != null)
                    return value;
            }

            foreach (var props in Levels(level))
            {
                var value = pick(props);
                if (value != null)
                    return value;
            }

            return null;
        }

        public T RunValue<T>(A.TextCharacterPropertiesType props, int level, Func<A.TextCharacterPropertiesType, T> pick)
            where T : class
        {
            if (props != null)
            {
                var value = pick(props);
                if (value != null)
                    return value;
            }

            foreach (var levelProps in Levels(level))
            {
                var defaults = levelProps.GetFirstChild<A.DefaultRunProperties>();
                if (defaults == null)
                    continue;

                var value = pick(defaults);
                if (value != null)
                    return value;
            }

            return null;
        }

        public double FontSizeOf(A.Paragraph paragraph)
        {
            var level = PptText.LevelOf(paragraph);
            var size = RunValue(PptText.FirstRunProperties(paragraph), level, r => r.FontSize);
            return size != null && size.HasValue ? size.Value / 100.0 : FallbackSize(level);
        }

        public PptParagraphInfo Describe(A.Paragraph paragraph)
        {
            var level = PptText.LevelOf(paragraph);
            var runProps = PptText.FirstRunProperties(paragraph);
            var info = new PptParagraphInfo
            {
                Text = PptText.TextOf(paragraph),
                Level = level,
                FontSize = FontSizeOf(paragraph),
                ExplicitSize = PptText.ExplicitSize(paragraph),
                Bold = IsOn(RunValue(runProps, level, r => r.Bold)),
                Italic = IsOn(RunValue(runProps, level, r => r.Italic)),
            };

            var underline = RunValue(runProps, level, r => r.Underline);
            info.Underline = underline != null && underline.HasValue && underline.Value != A.TextUnderlineValues.None;

            var strike = RunValue(runProps, level, r => r.Strike);
            info.Strike = strike != null && strike.HasValue && strike.Value != A.TextStrikeValues.NoStrike;
            info.FontName = FontNameOf(runProps, level, info.Text);
            info.Color = PptColor.Read(RunValue(runProps, level, r => r.GetFirstChild<A.SolidFill>()))?.ToString();

            var align = ParagraphValue(paragraph, level, p => p.Alignment);
            info.Align = align != null && align.HasValue ? align.InnerText : "l";

            var spacing = ParagraphValue(paragraph, level, p => p.LineSpacing);
            var spacingPoints = spacing?.SpacingPoints?.Val;
            var spacingPercent = spacing?.SpacingPercent?.Val;
            if (spacingPoints != null && spacingPoints.HasValue)
                info.LineSpacingPoints = spacingPoints.Value / 100.0;
            else if (spacingPercent != null && spacingPercent.HasValue)
                info.LineSpacing = Math.Clamp(spacingPercent.Value / 100000.0, 0.5, 3);

            var before = ParagraphValue(paragraph, level, p => p.SpaceBefore);
            var beforePoints = before?.SpacingPoints?.Val;
            var beforePercent = before?.SpacingPercent?.Val;
            if (beforePoints != null && beforePoints.HasValue)
                info.SpaceBefore = beforePoints.Value / 100.0;
            else if (beforePercent != null && beforePercent.HasValue)
                info.SpaceBefore = beforePercent.Value / 100000.0 * info.FontSize;

            var after = ParagraphValue(paragraph, level, p => p.SpaceAfter);
            var afterPoints = after?.SpacingPoints?.Val;
            var afterPercent = after?.SpacingPercent?.Val;
            if (afterPoints != null && afterPoints.HasValue)
                info.SpaceAfter = afterPoints.Value / 100.0;
            else if (afterPercent != null && afterPercent.HasValue)
                info.SpaceAfter = afterPercent.Value / 100000.0 * info.FontSize;

            var margin = ParagraphValue(paragraph, level, p => p.LeftMargin);
            info.LeftMargin = margin != null && margin.HasValue ? Math.Max(0, margin.Value / EmuPerPoint) : 0;
            var indent = ParagraphValue(paragraph, level, p => p.Indent);
            info.Indent = indent != null && indent.HasValue ? indent.Value / EmuPerPoint : 0;
            info.TextAnchor = TextAnchor;
            PptBullets.Fill(info, this, paragraph, level);
            return info;
        }

        private string FontNameOf(A.TextCharacterPropertiesType runProps, int level, string text)
        {
            var latin = RunValue(runProps, level, r => r.GetFirstChild<A.LatinFont>())?.Typeface?.Value;
            var eastAsian = RunValue(runProps, level, r => r.GetFirstChild<A.EastAsianFont>())?.Typeface?.Value;
            return PptText.HasHangul(text) ? eastAsian ?? latin : latin ?? eastAsian;
        }

        public double FallbackSize(int level) => Family switch
        {
            "title" => 40,
            "body" => level <= 0 ? 24 : level == 1 ? 20 : 18,
            "dt" or "ftr" or "sldNum" or "hdr" => 12,
            _ => 18,
        };

        private static bool IsOn(BooleanValue value) => value != null && value.HasValue && value.Value;

        private IEnumerable<A.TextParagraphPropertiesType> Levels(int level)
        {
            var name = "lvl" + (Math.Clamp(level, 0, 8) + 1).ToString(CultureInfo.InvariantCulture) + "pPr";
            foreach (var container in ListStyles)
            {
                A.TextParagraphPropertiesType match = null;
                A.TextParagraphPropertiesType fallback = null;
                foreach (var child in container.ChildElements)
                {
                    if (child is not A.TextParagraphPropertiesType props)
                        continue;

                    if (props.LocalName == name)
                        match = props;
                    else if (props.LocalName == "defPPr")
                        fallback = props;
                }

                if (match != null)
                    yield return match;

                if (fallback != null)
                    yield return fallback;
            }
        }
    }
}
