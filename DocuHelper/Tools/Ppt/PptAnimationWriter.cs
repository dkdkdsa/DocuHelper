using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public sealed class PptEffect
    {
        public PptAnimationInfo Info { get; set; }

        public OpenXmlElement Raw { get; set; }

        public bool IsOpaque => Raw != null;
    }

    public static class PptAnimationWriter
    {
        public const string PresentationNs = "http://schemas.openxmlformats.org/presentationml/2006/main";

        public const string DrawingNs = "http://schemas.openxmlformats.org/drawingml/2006/main";

        public const string RelationshipNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        public static readonly string[] Effects = { "appear", "fade", "fly_in", "zoom" };

        private static readonly Dictionary<string, int> s_presets = new(StringComparer.Ordinal)
        {
            ["appear"] = 1,
            ["fade"] = 10,
            ["fly_in"] = 2,
            ["zoom"] = 53,
        };

        private static readonly Dictionary<string, int> s_flySubtype = new(StringComparer.Ordinal)
        {
            ["from_top"] = 1,
            ["from_right"] = 2,
            ["from_bottom"] = 4,
            ["from_left"] = 8,
        };

        public static List<PptEffect> Read(P.Slide slide)
        {
            var result = new List<PptEffect>();
            var main = MainSequence(slide);
            if (main == null)
                return result;

            foreach (var group in ChildPars(main))
            {
                foreach (var inner in ChildPars(group))
                {
                    foreach (var effect in ChildPars(inner))
                        result.Add(ReadEffect(effect));
                }
            }

            return result;
        }

        public static void Write(P.Slide slide, List<PptEffect> effects, IEnumerable<uint> touched)
        {
            var existing = slide.ChildElements.FirstOrDefault(c => c.LocalName == "timing");
            var extras = ExtraRoots(existing);
            var keptBuilds = KeptBuilds(existing, effects, touched);
            existing?.Remove();
            var counter = new IdCounter();
            var groups = BuildGroups(effects, counter);
            if (groups.Count == 0 && extras.Count == 0)
                return;

            var main = groups.Count == 0
                ? string.Empty
                : "<p:seq concurrent=\"1\" nextAc=\"seek\"><p:cTn id=\"0\" dur=\"indefinite\" nodeType=\"mainSeq\"><p:childTnLst>" + string.Concat(groups)
                    + "</p:childTnLst></p:cTn><p:prevCondLst><p:cond evt=\"onPrev\" delay=\"0\"><p:tgtEl><p:sldTgt/></p:tgtEl></p:cond></p:prevCondLst>"
                    + "<p:nextCondLst><p:cond evt=\"onNext\" delay=\"0\"><p:tgtEl><p:sldTgt/></p:tgtEl></p:cond></p:nextCondLst></p:seq>";
            var builds = new List<string>(keptBuilds);
            builds.AddRange(BuildEntries(slide, effects));
            var build = builds.Count == 0 ? string.Empty : "<p:bldLst>" + string.Concat(builds) + "</p:bldLst>";
            var xml = $"<p:timing xmlns:p=\"{PresentationNs}\" xmlns:a=\"{DrawingNs}\" xmlns:r=\"{RelationshipNs}\"><p:tnLst><p:par>"
                + "<p:cTn id=\"1\" dur=\"indefinite\" restart=\"never\" nodeType=\"tmRoot\"><p:childTnLst>" + main + string.Concat(extras)
                + "</p:childTnLst></p:cTn></p:par></p:tnLst>" + build + "</p:timing>";
            var timing = new P.Timing(xml);
            RenumberTimeNodes(timing);
            InsertTiming(slide, timing);
        }

        public static P.Shape FindShape(P.Slide slide, uint id) =>
            PptTargets.All(slide.CommonSlideData?.ShapeTree).FirstOrDefault(t => PptTargets.IdOf(t) == id) as P.Shape;

        private static OpenXmlElement MainSequence(P.Slide slide)
        {
            var timing = slide.ChildElements.FirstOrDefault(c => c.LocalName == "timing");
            return timing?.Descendants().FirstOrDefault(d => d.LocalName == "cTn" && Attribute(d, "nodeType") == "mainSeq");
        }

        private static IEnumerable<OpenXmlElement> ChildPars(OpenXmlElement owner)
        {
            var list = owner?.Descendants().FirstOrDefault(d => d.LocalName == "childTnLst");
            if (list == null)
                yield break;

            foreach (var child in list.ChildElements.Where(c => c.LocalName == "par").ToList())
                yield return child;
        }

        private static List<string> ExtraRoots(OpenXmlElement timing)
        {
            var root = timing?.Descendants().FirstOrDefault(d => d.LocalName == "tnLst")?.ChildElements.FirstOrDefault(c => c.LocalName == "par")
                ?.ChildElements.FirstOrDefault(c => c.LocalName == "cTn");
            var list = root?.ChildElements.FirstOrDefault(c => c.LocalName == "childTnLst");
            if (list == null)
                return new List<string>();

            return list.ChildElements
                .Where(c => !(c.LocalName == "seq" && c.Descendants().Any(d => d.LocalName == "cTn" && Attribute(d, "nodeType") == "mainSeq")))
                .Select(c => c.OuterXml)
                .ToList();
        }

        private static List<string> KeptBuilds(OpenXmlElement timing, IEnumerable<PptEffect> effects, IEnumerable<uint> touched)
        {
            var kept = new List<string>();
            var list = timing?.Descendants().FirstOrDefault(d => d.LocalName == "bldLst");
            if (list == null)
                return kept;

            var active = new HashSet<uint>(effects.Select(e => e.Info.ShapeId));
            var affected = new HashSet<uint>(touched);
            foreach (var entry in list.ChildElements)
            {
                var spid = Attribute(entry, "spid");
                if (uint.TryParse(spid, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && (active.Contains(id) || affected.Contains(id)))
                    continue;

                kept.Add(entry.OuterXml);
            }

            return kept;
        }

        private static IEnumerable<string> BuildEntries(P.Slide slide, IReadOnlyList<PptEffect> effects)
        {
            var handled = new HashSet<uint>();
            foreach (var effect in effects)
            {
                if (effect.IsOpaque || !handled.Add(effect.Info.ShapeId))
                    continue;

                var shape = FindShape(slide, effect.Info.ShapeId);
                if (shape?.TextBody == null)
                    continue;

                var byParagraph = effects.Any(e => !e.IsOpaque && e.Info.ShapeId == effect.Info.ShapeId && e.Info.Paragraph.HasValue);
                var mode = byParagraph ? " build=\"p\"" : " animBg=\"1\"";
                yield return $"<p:bldP spid=\"{effect.Info.ShapeId.ToString(CultureInfo.InvariantCulture)}\" grpId=\"0\"{mode}/>";
            }
        }

        private static List<string> BuildGroups(IReadOnlyList<PptEffect> effects, IdCounter counter)
        {
            var groups = new List<List<List<string>>>();
            var innerEnds = new List<List<double>>();
            foreach (var effect in effects)
            {
                var start = effect.IsOpaque ? NodeStart(effect.Raw) : effect.Info.Start;
                if (groups.Count == 0 || start == "on_click")
                {
                    groups.Add(new List<List<string>> { new List<string>() });
                    innerEnds.Add(new List<double> { 0 });
                }
                else if (start == "after_previous")
                {
                    groups[^1].Add(new List<string>());
                    innerEnds[^1].Add(0);
                }

                var group = groups.Count - 1;
                var inner = groups[group].Count - 1;
                var nodeType = start switch
                {
                    "with_previous" => "withEffect",
                    "after_previous" => "afterEffect",
                    _ => "clickEffect",
                };
                var xml = effect.IsOpaque ? effect.Raw.OuterXml : EffectXml(effect.Info, nodeType, counter);
                groups[group][inner].Add(xml);
                var end = effect.IsOpaque ? 0 : effect.Info.DelayMs + Duration(effect.Info);
                innerEnds[group][inner] = Math.Max(innerEnds[group][inner], end);
            }

            var result = new List<string>();
            for (var g = 0; g < groups.Count; g++)
            {
                var clickId = counter.Next();
                var inners = new StringBuilder();
                var innerStart = 0.0;
                for (var i = 0; i < groups[g].Count; i++)
                {
                    var innerId = counter.Next();
                    inners.Append($"<p:par><p:cTn id=\"{innerId}\" fill=\"hold\"><p:stCondLst><p:cond delay=\"{Math.Round(innerStart).ToString(CultureInfo.InvariantCulture)}\"/></p:stCondLst><p:childTnLst>")
                        .Append(string.Concat(groups[g][i])).Append("</p:childTnLst></p:cTn></p:par>");
                    innerStart += innerEnds[g][i];
                }

                result.Add($"<p:par><p:cTn id=\"{clickId}\" fill=\"hold\"><p:stCondLst><p:cond delay=\"indefinite\"/></p:stCondLst><p:childTnLst>{inners}</p:childTnLst></p:cTn></p:par>");
            }

            return result;
        }

        private static string NodeStart(OpenXmlElement raw)
        {
            var node = Attribute(raw.Descendants().FirstOrDefault(d => d.LocalName == "cTn"), "nodeType");
            return node switch
            {
                "withEffect" => "with_previous",
                "afterEffect" => "after_previous",
                _ => "on_click",
            };
        }

        private static int Duration(PptAnimationInfo info) => info.Effect == "appear" ? 1 : Math.Max(1, info.DurationMs);

        private static string EffectXml(PptAnimationInfo info, string nodeType, IdCounter counter)
        {
            var exit = info.Phase == "exit";
            var presetId = s_presets.GetValueOrDefault(info.Effect, 10);
            var direction = info.Option ?? (info.Effect == "fly_in" ? "from_bottom" : "in");
            var subtype = info.Effect switch
            {
                "fly_in" => s_flySubtype.GetValueOrDefault(direction, 4),
                "zoom" => direction == "out" ? 32 : 16,
                _ => 0,
            };
            var target = Target(info);
            var duration = Duration(info);
            var behaviors = new StringBuilder();
            behaviors.Append(Set(target, exit ? "hidden" : "visible", exit ? Math.Max(0, duration - 1) : 0, counter));
            switch (info.Effect)
            {
                case "fade":
                    behaviors.Append(AnimEffect(target, exit ? "out" : "in", duration, counter));
                    break;
                case "fly_in":
                    var horizontal = direction is "from_left" or "from_right";
                    var moving = horizontal ? "ppt_x" : "ppt_y";
                    var fixedAxis = horizontal ? "ppt_y" : "ppt_x";
                    var away = direction switch
                    {
                        "from_top" => "0-#ppt_h/2",
                        "from_left" => "0-#ppt_w/2",
                        "from_right" => "1+#ppt_w/2",
                        _ => "1+#ppt_h/2",
                    };
                    var edge = exit ? away : "#" + moving;
                    var rest = exit ? "#" + moving : away;
                    behaviors.Append(Anim(target, moving, rest, edge, duration, counter, numeric: false));
                    behaviors.Append(Anim(target, fixedAxis, "#" + fixedAxis, "#" + fixedAxis, duration, counter, numeric: false));
                    break;
                case "zoom":
                    var grow = subtype == 16;
                    behaviors.Append(Anim(target, "ppt_w", grow ? "0" : "#ppt_w", grow ? "#ppt_w" : "0", duration, counter, numeric: true));
                    behaviors.Append(Anim(target, "ppt_h", grow ? "0" : "#ppt_h", grow ? "#ppt_h" : "0", duration, counter, numeric: true));
                    behaviors.Append(AnimEffect(target, exit ? "out" : "in", duration, counter));
                    break;
            }

            var id = counter.Next();
            var presetClass = exit ? "exit" : "entr";
            return $"<p:par><p:cTn id=\"{id}\" presetID=\"{presetId}\" presetClass=\"{presetClass}\" presetSubtype=\"{subtype}\" fill=\"hold\" grpId=\"0\" nodeType=\"{nodeType}\">"
                + $"<p:stCondLst><p:cond delay=\"{info.DelayMs.ToString(CultureInfo.InvariantCulture)}\"/></p:stCondLst><p:childTnLst>{behaviors}</p:childTnLst></p:cTn></p:par>";
        }

        private static string Target(PptAnimationInfo info)
        {
            var id = info.ShapeId.ToString(CultureInfo.InvariantCulture);
            return info.Paragraph.HasValue
                ? $"<p:tgtEl><p:spTgt spid=\"{id}\"><p:txEl><p:pRg st=\"{info.Paragraph.Value.ToString(CultureInfo.InvariantCulture)}\" end=\"{info.Paragraph.Value.ToString(CultureInfo.InvariantCulture)}\"/></p:txEl></p:spTgt></p:tgtEl>"
                : $"<p:tgtEl><p:spTgt spid=\"{id}\"/></p:tgtEl>";
        }

        private static string Set(string target, string value, int delayMs, IdCounter counter) =>
            $"<p:set><p:cBhvr><p:cTn id=\"{counter.Next()}\" dur=\"1\" fill=\"hold\"><p:stCondLst><p:cond delay=\"{delayMs.ToString(CultureInfo.InvariantCulture)}\"/></p:stCondLst></p:cTn>"
            + $"{target}<p:attrNameLst><p:attrName>style.visibility</p:attrName></p:attrNameLst></p:cBhvr><p:to><p:strVal val=\"{value}\"/></p:to></p:set>";

        private static string AnimEffect(string target, string transition, int duration, IdCounter counter) =>
            $"<p:animEffect transition=\"{transition}\" filter=\"fade\"><p:cBhvr><p:cTn id=\"{counter.Next()}\" dur=\"{duration.ToString(CultureInfo.InvariantCulture)}\"/>{target}</p:cBhvr></p:animEffect>";

        private static string ValueXml(string value, bool numeric) =>
            numeric && !value.Contains('#') ? $"<p:fltVal val=\"{value}\"/>" : $"<p:strVal val=\"{value}\"/>";

        private static string Anim(string target, string attribute, string from, string to, int duration, IdCounter counter, bool numeric)
        {
            var fromValue = ValueXml(from, numeric);
            var toValue = ValueXml(to, numeric);
            return $"<p:anim calcmode=\"lin\" valueType=\"num\"><p:cBhvr additive=\"base\"><p:cTn id=\"{counter.Next()}\" dur=\"{duration.ToString(CultureInfo.InvariantCulture)}\" fill=\"hold\"/>{target}"
                + $"<p:attrNameLst><p:attrName>{attribute}</p:attrName></p:attrNameLst></p:cBhvr><p:tavLst><p:tav tm=\"0\"><p:val>{fromValue}</p:val></p:tav><p:tav tm=\"100000\"><p:val>{toValue}</p:val></p:tav></p:tavLst></p:anim>";
        }

        private static PptEffect ReadEffect(OpenXmlElement effect)
        {
            var cTn = effect.Descendants().FirstOrDefault(d => d.LocalName == "cTn");
            var presetId = Attribute(cTn, "presetID");
            var presetClass = Attribute(cTn, "presetClass");
            var nodeType = Attribute(cTn, "nodeType");
            var start = nodeType switch
            {
                "withEffect" => "with_previous",
                "afterEffect" => "after_previous",
                _ => "on_click",
            };
            var target = effect.Descendants().FirstOrDefault(d => d.LocalName == "spTgt");
            var shape = uint.TryParse(Attribute(target, "spid"), NumberStyles.None, CultureInfo.InvariantCulture, out var spid) ? spid : 0u;
            var name = Named(presetId);
            var recognized = name != null && shape != 0 && (presetClass == "entr" || presetClass == "exit");
            if (!recognized)
                return new PptEffect { Info = new PptAnimationInfo { ShapeId = shape, Start = start, IsOther = true }, Raw = effect.CloneNode(true) };

            var paragraph = target.Descendants().FirstOrDefault(d => d.LocalName == "pRg");
            var delay = Attribute(cTn.ChildElements.FirstOrDefault(c => c.LocalName == "stCondLst")?.ChildElements.FirstOrDefault(), "delay");
            var duration = effect.Descendants().Where(d => d.LocalName == "cTn").Select(d => Attribute(d, "dur")).FirstOrDefault(v => v != null && v != "1" && v != "indefinite");
            return new PptEffect
            {
                Info = new PptAnimationInfo
                {
                    ShapeId = shape,
                    Effect = name,
                    Phase = presetClass == "exit" ? "exit" : "entrance",
                    Start = start,
                    DurationMs = duration != null && int.TryParse(duration, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms) ? ms : (name == "appear" ? 1 : 500),
                    DelayMs = delay != null && int.TryParse(delay, NumberStyles.Integer, CultureInfo.InvariantCulture, out var d) ? d : 0,
                    Option = OptionOf(name, Attribute(cTn, "presetSubtype")),
                    Paragraph = paragraph == null ? (int?)null : int.TryParse(Attribute(paragraph, "st"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var st) ? st : (int?)null,
                },
            };
        }

        private static string Named(string presetId) => presetId switch
        {
            "1" => "appear",
            "10" => "fade",
            "2" => "fly_in",
            "53" => "zoom",
            _ => null,
        };

        private static string OptionOf(string name, string subtype)
        {
            if (name == "fly_in")
                return s_flySubtype.FirstOrDefault(p => p.Value.ToString(CultureInfo.InvariantCulture) == subtype).Key ?? "from_bottom";

            if (name == "zoom")
                return subtype == "32" ? "out" : "in";

            return null;
        }

        private static string Attribute(OpenXmlElement element, string name) =>
            element?.GetAttributes().FirstOrDefault(a => a.LocalName == name).Value;

        private static void RenumberTimeNodes(OpenXmlElement timing)
        {
            var next = 1;
            foreach (var node in timing.Descendants().Where(d => d.LocalName == "cTn").ToList())
                node.SetAttribute(new OpenXmlAttribute("", "id", null, next++.ToString(CultureInfo.InvariantCulture)));
        }

        private static void InsertTiming(P.Slide slide, OpenXmlElement timing)
        {
            OpenXmlElement anchor = null;
            foreach (var child in slide.ChildElements)
            {
                if (child.LocalName is "cSld" or "clrMapOvr" or "transition" || child.Descendants().Any(d => d.LocalName == "transition"))
                    anchor = child;
            }

            if (anchor == null)
                slide.PrependChild(timing);
            else
                anchor.InsertAfterSelf(timing);
        }

        private sealed class IdCounter
        {
            private int _value;

            public int Next() => ++_value;
        }
    }
}
