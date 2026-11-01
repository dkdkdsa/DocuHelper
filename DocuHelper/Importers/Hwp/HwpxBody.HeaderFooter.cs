using System;
using System.Text;
using System.Xml.Linq;

namespace DocuHelper.Importers.Hwp
{
    internal sealed partial class HwpxBody
    {
        private void ReadSectionControls(XElement properties)
        {
            var visibility = HwpxXml.Child(properties, "visibility");
            _section.HideFirstHeader = HwpxXml.Flag(visibility, "hideFirstHeader");
            _section.HideFirstFooter = HwpxXml.Flag(visibility, "hideFirstFooter");
            _section.StartPage = Math.Max(0, HwpxXml.Int(HwpxXml.Child(properties, "startNum"), "page", 0));
        }

        private void ReadHeaderFooter(XElement element, bool footer)
        {
            var saved = (_pendingHiding, _pendingNewNumber);
            _pendingHiding = null;
            _pendingNewNumber = null;
            var part = new HwpHeaderFooter { Footer = footer, Apply = HwpxXml.Attribute(element, "applyPageType") ?? "BOTH" };
            ReadList(HwpxXml.Child(element, "subList"), part.Blocks);
            (_pendingHiding, _pendingNewNumber) = saved;
            if (part.Blocks.Count > 0)
                _section.HeaderFooters.Add(part);
        }

        private void ReadPageNumber(XElement element)
        {
            var position = HwpxXml.Attribute(element, "pos");
            if (string.IsNullOrEmpty(position) || position == "NONE")
                return;

            _section.PageNumber = new HwpPageNumber
            {
                Position = position,
                Format = HwpxXml.Attribute(element, "formatType") ?? "DIGIT",
                SideChar = HwpxXml.Attribute(element, "sideChar") ?? string.Empty,
            };
        }

        private void ReadNewNumber(XElement element)
        {
            var number = HwpxXml.Int(element, "num", 0);
            if (HwpxXml.Attribute(element, "numType") == "PAGE" && number > 0)
                _pendingNewNumber = number;
        }

        private void ReadPageHiding(XElement element)
        {
            var flags = new StringBuilder();
            if (HwpxXml.Flag(element, "hideHeader"))
                flags.Append('h');

            if (HwpxXml.Flag(element, "hideFooter"))
                flags.Append('f');

            if (HwpxXml.Flag(element, "hidePageNum"))
                flags.Append('p');

            if (HwpxXml.Flag(element, "hideBorder"))
                flags.Append('b');

            if (HwpxXml.Flag(element, "hideFill"))
                flags.Append('g');

            if (flags.Length > 0)
                _pendingHiding = flags.ToString();
        }
    }
}
