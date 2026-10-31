using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;

namespace DocuHelper.Tools.Ppt
{
    public static class PptLinkWriter
    {
        public static void Check(PresentationDocument document, PptLinkSpec spec)
        {
            if (spec.Type != "slide")
                return;

            PptSlides.Get(document, spec.Slide);
        }

        public static A.HyperlinkOnClick Create(PresentationDocument document, PptSlideRef owner, PptLinkSpec spec)
        {
            switch (spec.Type)
            {
                case "url":
                case "email":
                    return PptLinkRelations.External(owner.Part, spec.Url, spec.Tooltip);
                case "slide":
                    return PptLinkRelations.SlideJump(owner.Part, PptSlides.Get(document, spec.Slide).Part, spec.Tooltip);
                default:
                    var link = PptLinkRelations.ShowJump(spec.Type.Substring(0, spec.Type.IndexOf('_')));
                    if (!string.IsNullOrEmpty(spec.Tooltip))
                        link.Tooltip = spec.Tooltip;

                    return link;
            }
        }

        public static int ApplyToText(IEnumerable<OpenXmlElement> elements, A.HyperlinkOnClick link)
        {
            var count = 0;
            foreach (var element in elements)
            {
                if (element is not (A.Run or A.Field))
                    continue;

                PptRunProps.SetHyperlink(PptRunProps.Of(element), (A.HyperlinkOnClick)link.CloneNode(true));
                count++;
            }

            return count;
        }

        public static int ClearText(IEnumerable<OpenXmlElement> elements)
        {
            var count = 0;
            foreach (var element in elements)
            {
                if (element is not (A.Run or A.Field))
                    continue;

                var props = element is A.Run run ? run.RunProperties : ((A.Field)element).RunProperties;
                if (props?.GetFirstChild<A.HyperlinkOnClick>() == null)
                    continue;

                PptRunProps.ClearHyperlink(props);
                count++;
            }

            return count;
        }

        public static void ApplyToShape(OpenXmlCompositeElement target, A.HyperlinkOnClick link)
        {
            PptTargets.PropertiesOf(target).HyperlinkOnClick = link;
        }

        public static bool ClearShape(OpenXmlCompositeElement target)
        {
            var props = PptTargets.PropertiesOf(target);
            if (props?.HyperlinkOnClick == null)
                return false;

            props.HyperlinkOnClick = null;
            return true;
        }

        public static bool HasShapeLink(OpenXmlCompositeElement target) =>
            PptTargets.PropertiesOf(target)?.HyperlinkOnClick != null;

        public static int Prune(PptSlideRef owner) => PptLinkRelations.Prune(owner.Part);
    }
}
