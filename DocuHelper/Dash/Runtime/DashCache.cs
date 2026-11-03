using System;
using System.Collections.Generic;

namespace DocuHelper.Dash.Runtime
{
    public sealed class DashCache
    {
        private const int MaxTables = 64;
        private const int MaxCalculations = 64;

        private readonly object _gate = new();
        private readonly Queue<string> _tableOrder = new();
        private readonly Queue<string> _calculationOrder = new();
        private readonly Dictionary<string, DashTable> _tables = new(StringComparer.Ordinal);
        private readonly Dictionary<string, DashCalculation> _calculations = new(StringComparer.Ordinal);

        internal bool TryTable(string key, out DashTable table)
        {
            lock (_gate)
                return _tables.TryGetValue(key, out table);
        }

        internal void PutTable(string key, DashTable table)
        {
            lock (_gate)
                Put(_tables, _tableOrder, MaxTables, key, table);
        }

        internal bool TryCalculation(string key, out DashCalculation calculation)
        {
            lock (_gate)
                return _calculations.TryGetValue(key, out calculation);
        }

        internal void PutCalculation(string key, DashCalculation calculation)
        {
            lock (_gate)
                Put(_calculations, _calculationOrder, MaxCalculations, key, calculation);
        }

        private static void Put<T>(Dictionary<string, T> items, Queue<string> order, int max, string key, T item)
        {
            if (!items.ContainsKey(key))
            {
                order.Enqueue(key);
                while (order.Count > max)
                    items.Remove(order.Dequeue());
            }

            items[key] = item;
        }
    }
}
