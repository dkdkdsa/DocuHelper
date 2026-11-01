using System.Collections.Generic;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Importers.Legacy.Xls
{
    internal sealed class XlsStringTable
    {
        private readonly Dictionary<string, int> _indexes = new();
        private readonly List<string> _items = new();
        private int _references;

        public int Add(string text)
        {
            _references++;
            if (_indexes.TryGetValue(text, out var existing))
                return existing;

            var index = _items.Count;
            _items.Add(text);
            _indexes[text] = index;
            return index;
        }

        public SharedStringTable Build()
        {
            var table = new SharedStringTable { Count = (uint)_references, UniqueCount = (uint)_items.Count };
            foreach (var item in _items)
            {
                var text = new Text(XlsXmlText.Escape(item));
                if (item.Length > 0 && (char.IsWhiteSpace(item[0]) || char.IsWhiteSpace(item[item.Length - 1]) || item.IndexOf('\n') >= 0))
                    text.Space = SpaceProcessingModeValues.Preserve;

                table.Append(new SharedStringItem(text));
            }

            return table;
        }
    }
}
