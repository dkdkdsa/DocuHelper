using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Dash.Elements;

namespace DocuHelper.Dash
{
    public static class DashKinds
    {
        public static IReadOnlyDictionary<string, Type> Elements { get; } = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["page"] = typeof(PageElement),
            ["canvas"] = typeof(CanvasElement),
        };

        public static string ElementName(Type type) => NameOf(Elements, type);

        public static string NameOf(IReadOnlyDictionary<string, Type> kinds, Type type) =>
            kinds.FirstOrDefault(pair => pair.Value == type).Key;
    }
}
