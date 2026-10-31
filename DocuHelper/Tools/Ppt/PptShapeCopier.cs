using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public static class PptShapeCopier
    {
        public static void CheckCopyable(OpenXmlCompositeElement element)
        {
            var label = $"{PptTargets.IdOf(element).ToString(CultureInfo.InvariantCulture)}번({PptTargets.KoreanName(element)})";
            if (element is P.Shape shape && PptPlaceholder.Of(shape) != null)
                throw new ToolInputException($"{label}은 제목이나 본문 같은 자리 표시 상자라서 복사할 수 없어요. 새 글상자나 도형을 만들어 주세요.");

            if (element.Descendants<P.Shape>().Any(s => PptPlaceholder.Of(s) != null))
                throw new ToolInputException($"{label} 안에 자리 표시 상자가 있어서 복사할 수 없어요.");
        }

        public static List<OpenXmlCompositeElement> Copy(PptSlideRef source, PptSlideRef target, IReadOnlyList<OpenXmlCompositeElement> elements, long offsetX, long offsetY)
        {
            var clones = elements.Select(e => (OpenXmlCompositeElement)e.CloneNode(true)).ToList();
            var ids = new Dictionary<uint, uint>();
            var next = PptSlides.NextShapeId(target.Tree);
            foreach (var clone in clones)
            {
                foreach (var properties in clone.Descendants<P.NonVisualDrawingProperties>())
                {
                    var old = properties.Id?.Value ?? 0;
                    ids[old] = next;
                    properties.Id = next++;
                }
            }

            foreach (var clone in clones)
            {
                Reconnect(clone, ids);
                RenewCreationIds(clone);
                PptPartCopier.CopyReferences(source.Part, target.Part, clone);
                if (offsetX != 0 || offsetY != 0)
                {
                    var box = PptXfrm.Read(clone);
                    if (box != null)
                        PptXfrm.Write(clone, box.Value.Offset(offsetX, offsetY));
                }

                PptSlides.AppendToTree(target.Tree, clone);
            }

            return clones;
        }

        private static void Reconnect(OpenXmlElement clone, Dictionary<uint, uint> ids)
        {
            foreach (var start in clone.Descendants<A.StartConnection>().ToList())
            {
                if (start.Id?.Value is uint id && ids.TryGetValue(id, out var mapped))
                    start.Id = mapped;
                else
                    start.Remove();
            }

            foreach (var end in clone.Descendants<A.EndConnection>().ToList())
            {
                if (end.Id?.Value is uint id && ids.TryGetValue(id, out var mapped))
                    end.Id = mapped;
                else
                    end.Remove();
            }
        }

        private static void RenewCreationIds(OpenXmlElement clone)
        {
            foreach (var element in clone.Descendants().Where(e => e.LocalName == "creationId").ToList())
            {
                foreach (var attribute in element.GetAttributes().Where(a => a.LocalName == "id").ToList())
                    element.SetAttribute(new OpenXmlAttribute(attribute.Prefix, attribute.LocalName, attribute.NamespaceUri, "{" + Guid.NewGuid().ToString().ToUpperInvariant() + "}"));
            }
        }
    }
}
