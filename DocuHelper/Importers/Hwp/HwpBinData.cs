using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace DocuHelper.Importers.Hwp
{
    internal sealed class HwpBinData
    {
        private const int OlePrefixSize = 4;
        private const int SignatureSize = 8;

        private static readonly Guid[] s_chartClasses =
        {
            new("5A721580-5AF0-11CE-8384-0020AF2337F2"),
            new("00020803-0000-0000-C000-000000000046"),
        };

        private readonly HwpCompoundFile _file;
        private readonly Dictionary<int, bool> _charts = new();

        public HwpBinData(HwpCompoundFile file)
        {
            _file = file;
        }

        public bool IsChart(int id)
        {
            if (!_charts.TryGetValue(id, out var chart))
            {
                chart = Probe(id);
                _charts[id] = chart;
            }

            return chart;
        }

        private bool Probe(int id)
        {
            var prefix = "BIN" + id.ToString("X4", CultureInfo.InvariantCulture) + ".";
            foreach (var name in _file.StreamNames("BinData"))
            {
                if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    var ole = Unwrap(_file.ReadStream("BinData/" + name));
                    return ole != null && Array.IndexOf(s_chartClasses, new HwpCompoundFile(ole).RootClassId) >= 0;
                }
                catch (Exception e) when (e is InvalidDataException || e is ArgumentException || e is IndexOutOfRangeException)
                {
                    return false;
                }
            }

            return false;
        }

        private static byte[] Unwrap(byte[] data)
        {
            if (data == null)
                return null;

            if (HasOle(data))
                return data.AsSpan(OlePrefixSize).ToArray();

            var inflated = HwpDeflate.Inflate(data);
            return HasOle(inflated) ? inflated.AsSpan(OlePrefixSize).ToArray() : null;
        }

        private static bool HasOle(byte[] data) =>
            data.Length > OlePrefixSize + SignatureSize && HwpCompoundFile.IsCompoundFile(data.AsSpan(OlePrefixSize, SignatureSize).ToArray());
    }
}
