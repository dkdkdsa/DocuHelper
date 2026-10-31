using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;

namespace DocuHelper.Tools.Ppt
{
    public static class PptPartCopier
    {
        public const string RelationshipsNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        private static readonly MethodInfo s_addNewPart = typeof(OpenXmlPartContainer).GetMethods()
            .First(m => m.Name == nameof(OpenXmlPartContainer.AddNewPart) && m.IsGenericMethodDefinition
                && m.GetParameters().Length == 2 && m.GetParameters().All(p => p.ParameterType == typeof(string)));

        public static HashSet<string> ReferencedIds(OpenXmlElement element)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (element == null)
                return ids;

            foreach (var node in Nodes(element))
            {
                foreach (var attribute in node.GetAttributes())
                {
                    if (attribute.NamespaceUri == RelationshipsNamespace && !string.IsNullOrEmpty(attribute.Value))
                        ids.Add(attribute.Value);
                }
            }

            return ids;
        }

        public static IReadOnlyDictionary<string, string> CopyReferences(OpenXmlPart source, OpenXmlPart target, OpenXmlElement element)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            var broken = new HashSet<string>(StringComparer.Ordinal);
            var copier = new Copier(source, target);
            foreach (var id in ReferencedIds(element))
            {
                var copied = copier.Resolve(id);
                if (copied == null)
                    broken.Add(id);
                else
                    map[id] = copied;
            }

            Rewrite(element, map, broken);
            return map;
        }

        private static IEnumerable<OpenXmlElement> Nodes(OpenXmlElement element)
        {
            yield return element;
            foreach (var node in element.Descendants())
                yield return node;
        }

        private static void Rewrite(OpenXmlElement element, Dictionary<string, string> map, HashSet<string> broken)
        {
            var dropped = new List<OpenXmlElement>();
            foreach (var node in Nodes(element).ToList())
            {
                foreach (var attribute in node.GetAttributes().ToList())
                {
                    if (attribute.NamespaceUri != RelationshipsNamespace || string.IsNullOrEmpty(attribute.Value))
                        continue;

                    if (broken.Contains(attribute.Value))
                    {
                        if (node is A.HyperlinkType)
                            dropped.Add(node);
                        else
                            node.RemoveAttribute(attribute.LocalName, attribute.NamespaceUri);
                    }
                    else if (map.TryGetValue(attribute.Value, out var replacement) && replacement != attribute.Value)
                    {
                        node.SetAttribute(new OpenXmlAttribute(attribute.Prefix, attribute.LocalName, attribute.NamespaceUri, replacement));
                    }
                }
            }

            foreach (var node in dropped.Where(n => n.Parent != null))
                node.Remove();
        }

        private sealed class Copier
        {
            private readonly OpenXmlPart _source;
            private readonly OpenXmlPartContainer _target;
            private readonly bool _sameContainer;
            private readonly bool _samePackage;

            public Copier(OpenXmlPart source, OpenXmlPartContainer target)
            {
                _source = source;
                _target = target;
                _sameContainer = ReferenceEquals(source, target);
                _samePackage = ReferenceEquals(source.OpenXmlPackage, target is OpenXmlPart part ? part.OpenXmlPackage : null);
            }

            public string Resolve(string id)
            {
                if (_source.TryGetPartById(id, out var part))
                    return CopyPart(part, id);

                var data = _source.DataPartReferenceRelationships.FirstOrDefault(r => r.Id == id);
                if (data != null)
                    return _sameContainer ? id : CopyData(data);

                var hyperlink = _source.HyperlinkRelationships.FirstOrDefault(r => r.Id == id);
                if (hyperlink != null)
                {
                    if (_sameContainer)
                        return id;

                    var existing = _target.HyperlinkRelationships.FirstOrDefault(r => r.IsExternal == hyperlink.IsExternal && r.Uri == hyperlink.Uri);
                    return existing?.Id ?? _target.AddHyperlinkRelationship(hyperlink.Uri, hyperlink.IsExternal).Id;
                }

                var external = _source.ExternalRelationships.FirstOrDefault(r => r.Id == id);
                if (external != null)
                {
                    if (_sameContainer)
                        return id;

                    var existing = _target.ExternalRelationships.FirstOrDefault(r => r.RelationshipType == external.RelationshipType && r.Uri == external.Uri);
                    return existing?.Id ?? _target.AddExternalRelationship(external.RelationshipType, external.Uri).Id;
                }

                return null;
            }

            private string CopyPart(OpenXmlPart part, string id)
            {
                switch (part)
                {
                    case SlidePart slide:
                        if (_sameContainer)
                            return id;

                        return _samePackage ? PptLinkRelations.SlideId(_target, slide) : null;
                    case ImagePart image:
                        if (_sameContainer)
                            return id;

                        return _samePackage ? Share(_target, image) : CopyImage(_target, image, null);
                    case ChartPart:
                        return CloneTree(part, _target, null, _samePackage).Id;
                    default:
                        if (_sameContainer)
                            return id;

                        return CloneTree(part, _target, null, _samePackage).Id;
                }
            }

            private string CopyData(DataPartReferenceRelationship relationship)
            {
                if (_samePackage)
                {
                    var existing = _target.DataPartReferenceRelationships.FirstOrDefault(r =>
                        r.GetType() == relationship.GetType() && ReferenceEquals(r.DataPart, relationship.DataPart));

                    if (existing != null)
                        return existing.Id;
                }

                return AddData(_target, relationship, null, _samePackage).Id;
            }
        }

        private static string Share(OpenXmlPartContainer target, OpenXmlPart part)
        {
            foreach (var pair in target.Parts)
            {
                if (ReferenceEquals(pair.OpenXmlPart, part))
                    return pair.RelationshipId;
            }

            return target.GetIdOfPart(target.AddPart(part));
        }

        private static string CopyImage(OpenXmlPartContainer target, ImagePart image, string id)
        {
            var bytes = PptImages.Read(image);
            if (id == null)
            {
                foreach (var pair in target.Parts)
                {
                    if (pair.OpenXmlPart is ImagePart existing && existing.ContentType == image.ContentType && PptImages.Read(existing).AsSpan().SequenceEqual(bytes))
                        return pair.RelationshipId;
                }
            }

            var clone = (ImagePart)s_addNewPart.MakeGenericMethod(typeof(ImagePart)).Invoke(target, new object[] { image.ContentType, id });
            using (var stream = new MemoryStream(bytes, false))
                clone.FeedData(stream);

            return target.GetIdOfPart(clone);
        }

        private static (OpenXmlPart Part, string Id) CloneTree(OpenXmlPart part, OpenXmlPartContainer target, string id, bool samePackage)
        {
            var clone = part is ExtendedPart
                ? target.AddExtendedPart(part.RelationshipType, part.ContentType, Path.GetExtension(part.Uri.OriginalString), id)
                : (OpenXmlPart)s_addNewPart.MakeGenericMethod(part.GetType()).Invoke(target, new object[] { part.ContentType, id });
            if (part.RootElement is OpenXmlPartRootElement root)
                root.Save();

            using (var stream = part.GetStream(FileMode.Open, FileAccess.Read))
                clone.FeedData(stream);

            foreach (var relationship in part.HyperlinkRelationships)
                clone.AddHyperlinkRelationship(relationship.Uri, relationship.IsExternal, relationship.Id);

            foreach (var relationship in part.ExternalRelationships)
                clone.AddExternalRelationship(relationship.RelationshipType, relationship.Uri, relationship.Id);

            foreach (var relationship in part.DataPartReferenceRelationships)
                AddData(clone, relationship, relationship.Id, samePackage);

            foreach (var pair in part.Parts.ToList())
            {
                var child = pair.OpenXmlPart;
                if (child is SlidePart or SlideLayoutPart or SlideMasterPart or NotesSlidePart or NotesMasterPart)
                {
                    if (samePackage)
                        clone.AddPart(child, pair.RelationshipId);

                    continue;
                }

                if (child is ImagePart image)
                {
                    if (samePackage)
                        clone.AddPart(image, pair.RelationshipId);
                    else
                        CopyImage(clone, image, pair.RelationshipId);

                    continue;
                }

                CloneTree(child, clone, pair.RelationshipId, samePackage);
            }

            return (clone, target.GetIdOfPart(clone));
        }

        private static DataPartReferenceRelationship AddData(OpenXmlPartContainer target, DataPartReferenceRelationship relationship, string id, bool samePackage)
        {
            var data = relationship.DataPart;
            if (!samePackage)
            {
                var package = ((OpenXmlPart)target).OpenXmlPackage;
                var extension = Path.GetExtension(data.Uri.OriginalString);
                var copy = package.CreateMediaDataPart(data.ContentType, extension);
                using (var stream = data.GetStream(FileMode.Open, FileAccess.Read))
                    copy.FeedData(stream);

                data = copy;
            }

            var name = relationship switch
            {
                AudioReferenceRelationship => "AddAudioReferenceRelationship",
                VideoReferenceRelationship => "AddVideoReferenceRelationship",
                _ => "AddMediaReferenceRelationship",
            };

            var types = id == null ? new[] { typeof(MediaDataPart) } : new[] { typeof(MediaDataPart), typeof(string) };
            var method = target.GetType().GetMethod(name, types)
                ?? throw new InvalidOperationException("The target part cannot hold media references.");

            var arguments = id == null ? new object[] { data } : new object[] { data, id };
            return (DataPartReferenceRelationship)method.Invoke(target, arguments);
        }
    }
}
