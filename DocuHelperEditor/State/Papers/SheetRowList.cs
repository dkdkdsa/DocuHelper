using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace DocuHelperEditor.State.Papers
{
    public sealed class SheetRowList : IEnumerable<SheetRow>
    {
        private readonly SortedDictionary<int, SheetRow> _stored = new();

        public int Count { get; private set; }

        private static readonly SheetRow Blank = new();

        public SheetRow this[int index] => index >= 0 && index < Count && _stored.TryGetValue(index, out var row) ? row : Blank;

        public bool TryGet(int index, out SheetRow row) => _stored.TryGetValue(index, out row);

        public SheetRow Ensure(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            if (!_stored.TryGetValue(index, out var row))
            {
                row = new SheetRow();
                _stored[index] = row;
            }

            if (index >= Count)
                Count = index + 1;

            return row;
        }

        public void Add(SheetRow row)
        {
            _stored[Count] = row;
            Count++;
        }

        public IEnumerable<(int Index, SheetRow Row)> Entries => _stored.Select(pair => (pair.Key, pair.Value));

        public int FindIndex(Func<SheetRow, bool> predicate)
        {
            foreach (var pair in _stored)
            {
                if (predicate(pair.Value))
                    return pair.Key;
            }

            return -1;
        }

        public IEnumerator<SheetRow> GetEnumerator() => _stored.Values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
