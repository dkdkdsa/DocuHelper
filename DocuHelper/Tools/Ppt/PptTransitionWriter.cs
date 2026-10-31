using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocuHelper.Tools;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public static class PptTransitionWriter
    {
        public const string PresentationNs = "http://schemas.openxmlformats.org/presentationml/2006/main";

        public const string P14Ns = "http://schemas.microsoft.com/office/powerpoint/2010/main";

        public const string McNs = "http://schemas.openxmlformats.org/markup-compatibility/2006";

        public static readonly string[] Effects = { "none", "fade", "push", "wipe", "split", "reveal" };

        public static PptTransitionInfo Read(P.Slide slide)
        {
            var info = new PptTransitionInfo();
            var element = Element(slide);
            if (element == null)
                return info;

            var choice = element is AlternateContent alternate ? alternate.Descendants().FirstOrDefault(d => d.LocalName == "Choice") : null;
            var transition = choice?.ChildElements.FirstOrDefault(c => c.LocalName == "transition")
                ?? (element is P.Transition direct ? direct : null)
                ?? element.Descendants().FirstOrDefault(d => d.LocalName == "transition");
            if (transition == null)
                return info;

            var effect = transition.ChildElements.FirstOrDefault(c => c.LocalName is "fade" or "push" or "wipe" or "split" or "reveal");
            info.Effect = effect?.LocalName ?? "none";
            info.Option = OptionOf(effect);
            var duration = choice?.GetAttributes().FirstOrDefault(a => a.LocalName == "dur").Value
                ?? transition.GetAttributes().FirstOrDefault(a => a.LocalName == "dur").Value;
            info.DurationMs = duration != null && int.TryParse(duration, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms)
                ? ms
                : SpeedMs(Attribute(transition, "spd"));
            info.AdvanceOnClick = Attribute(transition, "advClick") != "0";
            var after = Attribute(transition, "advTm");
            info.AdvanceAfterMs = after != null && int.TryParse(after, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tm) ? tm : null;
            info.SoundName = transition.Descendants().FirstOrDefault(d => d.LocalName == "snd")?.GetAttributes().FirstOrDefault(a => a.LocalName == "name").Value;
            return info;
        }

        public static OpenXmlElement Element(P.Slide slide)
        {
            foreach (var child in slide.ChildElements)
            {
                if (child.LocalName == "transition")
                    return child;

                if (child is AlternateContent && child.Descendants().Any(d => d.LocalName == "transition"))
                    return child;
            }

            return null;
        }

        public static void Remove(P.Slide slide, out OpenXmlElement sound)
        {
            sound = null;
            var element = Element(slide);
            if (element == null)
                return;

            sound = element.Descendants().FirstOrDefault(d => d.LocalName == "sndAc")?.CloneNode(true);
            element.Remove();
        }

        public static void Write(P.Slide slide, PptTransitionInfo info, OpenXmlElement carriedSound)
        {
            Remove(slide, out var previous);
            var sound = carriedSound ?? previous;
            var effect = info.Effect ?? "none";
            var duration = info.DurationMs;
            var hasDetails = effect != "none" || duration > 0 || !info.AdvanceOnClick || info.AdvanceAfterMs.HasValue || sound != null;
            if (!hasDetails)
                return;

            var attributes = new List<string>();
            var speed = Speed(duration > 0 ? duration : 750);
            attributes.Add($"spd=\"{speed}\"");
            if (!info.AdvanceOnClick)
                attributes.Add("advClick=\"0\"");

            if (info.AdvanceAfterMs is int after && after > 0)
                attributes.Add($"advTm=\"{after.ToString(CultureInfo.InvariantCulture)}\"");

            var soundXml = sound == null ? string.Empty : sound.OuterXml;
            var classic = ClassicEffect(effect, info.Option);
            var attrs = string.Join(" ", attributes);
            var needsChoice = duration > 0 || effect == "reveal";
            if (!needsChoice)
            {
                var plain = $"<p:transition xmlns:p=\"{PresentationNs}\" {attrs}>{classic}{soundXml}</p:transition>";
                slide.InsertAfter(new P.Transition(plain), Anchor(slide, new[] { "cSld", "clrMapOvr" }));
                return;
            }

            var choiceEffect = effect == "reveal" ? RevealEffect(info.Option) : classic;
            var durationAttr = duration > 0 ? $" p14:dur=\"{duration.ToString(CultureInfo.InvariantCulture)}\"" : string.Empty;
            var choice = $"<mc:Choice xmlns:mc=\"{McNs}\" xmlns:p14=\"{P14Ns}\" Requires=\"p14\"><p:transition xmlns:p14=\"{P14Ns}\" {attrs}{durationAttr}>{choiceEffect}{soundXml}</p:transition></mc:Choice>";
            var fallback = $"<mc:Fallback xmlns:mc=\"{McNs}\"><p:transition {attrs}>{classic}{soundXml}</p:transition></mc:Fallback>";
            var block = $"<mc:AlternateContent xmlns:mc=\"{McNs}\" xmlns:p=\"{PresentationNs}\">{choice}{fallback}</mc:AlternateContent>";
            slide.InsertAfter(new AlternateContent(block), Anchor(slide, new[] { "cSld", "clrMapOvr" }));
        }

        public static string Speed(int milliseconds) => milliseconds <= 500 ? "fast" : milliseconds <= 1000 ? "med" : "slow";

        public static int SpeedMs(string speed) => speed switch
        {
            "fast" => 500,
            "slow" => 1000,
            _ => 0,
        };

        public static string ClassicEffect(string effect, string option)
        {
            switch (effect)
            {
                case "fade":
                    return option == "through_black" ? "<p:fade thruBlk=\"1\"/>" : "<p:fade/>";
                case "push":
                    return $"<p:push dir=\"{Direction(option)}\"/>";
                case "wipe":
                    return $"<p:wipe dir=\"{Direction(option)}\"/>";
                case "split":
                    var (orient, dir) = SplitOf(option);
                    return $"<p:split orient=\"{orient}\" dir=\"{dir}\"/>";
                case "reveal":
                    return "<p:fade/>";
                default:
                    return string.Empty;
            }
        }

        public static string RevealEffect(string option)
        {
            var through = option is "black_from_left" or "black_from_right";
            var from = option is "smooth_from_right" or "black_from_right" ? "r" : "l";
            return "<p14:reveal" + (through ? " thruBlk=\"1\"" : string.Empty) + " dir=\"" + from + "\"/>";
        }

        public static string Direction(string option) => option switch
        {
            "from_top" => "d",
            "from_left" => "r",
            "from_right" => "l",
            _ => "u",
        };

        public static (string Orient, string Dir) SplitOf(string option) => option switch
        {
            "horizontal_out" => ("horz", "out"),
            "horizontal_in" => ("horz", "in"),
            "vertical_in" => ("vert", "in"),
            _ => ("vert", "out"),
        };

        public static string OptionOf(OpenXmlElement effect)
        {
            if (effect == null)
                return null;

            switch (effect.LocalName)
            {
                case "fade":
                    return Attribute(effect, "thruBlk") == "1" ? "through_black" : "smooth";
                case "push":
                case "wipe":
                    return Attribute(effect, "dir") switch
                    {
                        "d" => "from_top",
                        "r" => "from_left",
                        "l" => "from_right",
                        _ => "from_bottom",
                    };
                case "split":
                    var horizontal = Attribute(effect, "orient") == "horz";
                    var inward = Attribute(effect, "dir") == "in";
                    return (horizontal, inward) switch
                    {
                        (true, false) => "horizontal_out",
                        (true, true) => "horizontal_in",
                        (false, true) => "vertical_in",
                        _ => "vertical_out",
                    };
                case "reveal":
                    var through = Attribute(effect, "thruBlk") == "1";
                    var right = Attribute(effect, "dir") == "r";
                    return (through, right) switch
                    {
                        (true, false) => "black_from_left",
                        (true, true) => "black_from_right",
                        (false, true) => "smooth_from_right",
                        _ => "smooth_from_left",
                    };
                default:
                    return null;
            }
        }

        public static void Copy(P.Slide source, P.Slide target)
        {
            var element = Element(source);
            if (element == null)
            {
                Remove(target, out _);
                return;
            }

            var info = Read(source);
            Write(target, info, null);
        }

        private static string Attribute(OpenXmlElement element, string name) =>
            element?.GetAttributes().FirstOrDefault(a => a.LocalName == name).Value;

        private static OpenXmlElement Anchor(P.Slide slide, IEnumerable<string> names)
        {
            OpenXmlElement last = null;
            foreach (var child in slide.ChildElements)
            {
                if (names.Contains(child.LocalName))
                    last = child;
            }

            return last ?? slide.ChildElements.FirstOrDefault();
        }

    }
}
