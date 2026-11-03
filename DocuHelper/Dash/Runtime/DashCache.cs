using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DocuHelper.Docus;

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
        private List<(Docu Docu, int Revision)> _sources = new();
        private string _logic;

        internal void Prepare(DashDocument document, IDashDocuments documents)
        {
            var logic = JsonSerializer.Serialize(new { document.Sources, document.Params, document.Values }, DashFile.Options);
            var sources = document.Sources
                .Where(source => source != null)
                .Select(source => Open(documents, source.File))
                .Select(docu => (docu, docu?.Revision ?? -1))
                .ToList();

            lock (_gate)
            {
                if (logic == _logic && Same(sources))
                    return;

                _tables.Clear();
                _calculations.Clear();
                _tableOrder.Clear();
                _calculationOrder.Clear();
                _logic = logic;
                _sources = sources;
            }
        }

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

        private bool Same(List<(Docu Docu, int Revision)> sources)
        {
            if (sources.Count != _sources.Count)
                return false;

            for (var i = 0; i < sources.Count; i++)
            {
                if (!ReferenceEquals(sources[i].Docu, _sources[i].Docu) || sources[i].Revision != _sources[i].Revision)
                    return false;
            }

            return true;
        }

        private static Docu Open(IDashDocuments documents, string file)
        {
            if (documents == null || string.IsNullOrWhiteSpace(file))
                return null;

            try
            {
                return documents.Open(file);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                return null;
            }
        }
    }
}
