using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;

namespace DocuHelper.Tools.Excel
{
    internal static class PartRelationships
    {
        public static string NextId(OpenXmlPart part)
        {
            var taken = new HashSet<string>(part.Parts.Select(p => p.RelationshipId)
                .Concat(part.ExternalRelationships.Select(r => r.Id))
                .Concat(part.HyperlinkRelationships.Select(r => r.Id))
                .Concat(part.DataPartReferenceRelationships.Select(r => r.Id)), StringComparer.Ordinal);
            var number = 1;
            while (taken.Contains("rId" + number))
                number++;

            return "rId" + number;
        }
    }
}
