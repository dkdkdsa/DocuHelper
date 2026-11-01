using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using static DocuHelper.Importers.Hwp.HwpxNames;

namespace DocuHelper.Importers.Hwp
{
    internal sealed class HwpxFieldScope
    {
        public string LinkId { get; set; }

        public string LinkTarget { get; set; }

        public string ControlId { get; set; }

        public SdtRun Control { get; set; }

        public bool Placeholder { get; set; }
    }

    internal sealed partial class HwpxWriteState
    {
        private int _fieldSeq;

        private string NextFieldId() => Number(++_fieldSeq);

        private string LinkTargetOf(OpenXmlElement run)
        {
            var hyperlink = run.Ancestors<Hyperlink>().FirstOrDefault();
            if (hyperlink == null)
                return null;

            if (hyperlink.Id?.Value is string id)
                return OwnerOf(run)?.HyperlinkRelationships.FirstOrDefault(r => r.Id == id)?.Uri?.OriginalString;

            return hyperlink.Anchor?.Value is string anchor ? "#" + anchor : null;
        }

        private static SdtRun ControlOf(OpenXmlElement run) => run.Ancestors<SdtRun>().FirstOrDefault();

        private void MoveScope(HwpxFieldScope scope, List<XElement> runs, string link, SdtRun control)
        {
            if (control != null)
                link = null;

            if (!ReferenceEquals(control, scope.Control))
            {
                if (scope.ControlId != null)
                    runs.Add(FieldEndRun(scope.ControlId));

                scope.ControlId = null;
                scope.Control = null;
                scope.Placeholder = false;
                if (control != null)
                {
                    scope.ControlId = NextFieldId();
                    scope.Control = control;
                    scope.Placeholder = control.SdtProperties?.GetFirstChild<ShowingPlaceholder>() != null;
                    runs.Add(ClickHereBegin(scope.ControlId, control));
                }
            }

            if (link != scope.LinkTarget)
            {
                if (scope.LinkId != null)
                    runs.Add(FieldEndRun(scope.LinkId));

                scope.LinkId = null;
                scope.LinkTarget = null;
                if (link != null)
                {
                    scope.LinkId = NextFieldId();
                    scope.LinkTarget = link;
                    runs.Add(HyperlinkBegin(scope.LinkId, link));
                }
            }
        }

        private void CloseScope(HwpxFieldScope scope, List<XElement> runs)
        {
            MoveScope(scope, runs, null, null);
        }

        private static XElement HyperlinkBegin(string id, string target) => FieldBegin(id, "HYPERLINK", string.Empty, "0",
            new XElement(Hp + "parameters", new XAttribute("cnt", "2"), new XAttribute("name", string.Empty),
                new XElement(Hp + "integerParam", new XAttribute("name", "Prop"), "0"),
                new XElement(Hp + "stringParam", new XAttribute("name", "Command"), target + ";1;0;0;")));

        private static XElement ClickHereBegin(string id, SdtRun control)
        {
            var name = control.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value ?? string.Empty;
            var guide = control.SdtProperties?.GetFirstChild<SdtAlias>()?.Val?.Value ?? string.Empty;
            return FieldBegin(id, "CLICK_HERE", name, "1",
                new XElement(Hp + "parameters", new XAttribute("cnt", "3"), new XAttribute("name", string.Empty),
                    new XElement(Hp + "integerParam", new XAttribute("name", "Prop"), "0"),
                    new XElement(Hp + "stringParam", new XAttribute("name", "Direction"), guide),
                    new XElement(Hp + "stringParam", new XAttribute("name", "HelpState"), string.Empty)));
        }

        private static XElement FieldBegin(string id, string type, string name, string editable, XElement parameters) =>
            new(Hp + "run", new XAttribute("charPrIDRef", "0"),
                new XElement(Hp + "ctrl",
                    new XElement(Hp + "fieldBegin",
                        new XAttribute("id", id), new XAttribute("type", type), new XAttribute("name", name),
                        new XAttribute("editable", editable), new XAttribute("dirty", "0"), new XAttribute("zorder", "-1"),
                        new XAttribute("fieldid", id), new XAttribute("metaTag", string.Empty),
                        parameters)));

        private static XElement FieldEndRun(string id) => new(Hp + "run", new XAttribute("charPrIDRef", "0"),
            new XElement(Hp + "ctrl", new XElement(Hp + "fieldEnd", new XAttribute("beginIDRef", id), new XAttribute("fieldid", id))));

        private static XElement BookmarkRun(string name) => new(Hp + "run", new XAttribute("charPrIDRef", "0"),
            new XElement(Hp + "ctrl", new XElement(Hp + "bookmark", new XAttribute("name", name))));
    }
}
