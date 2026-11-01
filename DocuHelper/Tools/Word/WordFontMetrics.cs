using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace DocuHelper.Tools.Word
{
    public sealed class WordFontMetrics
    {
        private const double FallbackLine = 1.2;
        private const double FallbackWide = 1.0;
        private const double FallbackNarrow = 0.5;
        private const double FallbackSpace = 0.25;
        private const string FontsKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts";
        private const int MaxBmp = 0xFFFF;

        private static readonly ConcurrentDictionary<string, WordFontMetrics> s_cache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Lazy<Dictionary<string, (string Path, int Index)>> s_faces = new(LoadFaces);

        private static readonly Dictionary<string, string> s_aliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["맑은 고딕"] = "Malgun Gothic",
            ["바탕"] = "Batang",
            ["바탕체"] = "BatangChe",
            ["돋움"] = "Dotum",
            ["돋움체"] = "DotumChe",
            ["굴림"] = "Gulim",
            ["굴림체"] = "GulimChe",
            ["궁서"] = "Gungsuh",
            ["궁서체"] = "GungsuhChe",
        };

        private readonly Dictionary<int, int> _glyphs;
        private readonly int[] _advances;
        private readonly double _unitsPerEm;
        private readonly double _line;
        private readonly ConcurrentDictionary<int, double> _widths = new();

        private WordFontMetrics()
        {
            _glyphs = null;
            _advances = Array.Empty<int>();
            _unitsPerEm = 1;
            _line = FallbackLine;
        }

        private WordFontMetrics(Dictionary<int, int> glyphs, int[] advances, double unitsPerEm, double line)
        {
            _glyphs = glyphs;
            _advances = advances;
            _unitsPerEm = unitsPerEm;
            _line = line;
        }

        public double LineHeight => _line;

        public static WordFontMetrics For(string family) => s_cache.GetOrAdd(Key(family), Load);

        public static bool IsEastAsia(char c) => c >= 0x1100 && !(c >= 0x2000 && c <= 0x206F);

        public double Advance(int codepoint) => _widths.GetOrAdd(codepoint, Measure);

        private static string Key(string family) => string.IsNullOrWhiteSpace(family) ? string.Empty : family.Trim();

        private static WordFontMetrics Load(string family)
        {
            var name = Key(family);
            if (s_aliases.TryGetValue(name, out var alias))
                name = alias;

            if (!s_faces.Value.TryGetValue(name, out var face))
                return new WordFontMetrics();

            try
            {
                return Parse(File.ReadAllBytes(face.Path), face.Index);
            }
            catch (Exception)
            {
                return new WordFontMetrics();
            }
        }

        private double Measure(int codepoint)
        {
            if (_glyphs != null && _glyphs.TryGetValue(codepoint, out var glyph) && _advances.Length > 0)
                return _advances[Math.Min(glyph, _advances.Length - 1)] / _unitsPerEm;

            if (codepoint == ' ')
                return FallbackSpace;

            return IsEastAsia((char)Math.Min(codepoint, MaxBmp)) ? FallbackWide : FallbackNarrow;
        }

        private static Dictionary<string, (string Path, int Index)> LoadFaces()
        {
            var faces = new Dictionary<string, (string Path, int Index)>(StringComparer.OrdinalIgnoreCase);
            if (!OperatingSystem.IsWindows())
                return faces;

            var folder = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(FontsKey);
                if (key == null)
                    return faces;

                foreach (var valueName in key.GetValueNames())
                {
                    var file = key.GetValue(valueName) as string;
                    if (string.IsNullOrEmpty(file))
                        continue;

                    var path = Path.IsPathRooted(file) ? file : Path.Combine(folder, file);
                    var label = valueName;
                    foreach (var suffix in new[] { " (TrueType)", " (OpenType)" })
                    {
                        if (label.EndsWith(suffix, StringComparison.Ordinal))
                            label = label.Substring(0, label.Length - suffix.Length);
                    }

                    var names = label.Split(" & ");
                    for (var i = 0; i < names.Length; i++)
                    {
                        var member = names[i].Trim();
                        if (member.Length > 0 && !faces.ContainsKey(member))
                            faces[member] = (path, i);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
            }

            return faces;
        }

        private static WordFontMetrics Parse(byte[] data, int index)
        {
            var offset = 0;
            if (Tag(data, 0) == "ttcf")
            {
                var count = (int)U32(data, 8);
                offset = (int)U32(data, 12 + 4 * Math.Clamp(index, 0, Math.Max(0, count - 1)));
            }

            var tables = Tables(data, offset);
            var head = tables["head"];
            var unitsPerEm = U16(data, head + 18);
            if (unitsPerEm == 0)
                return new WordFontMetrics();

            var line = LineOf(data, tables, unitsPerEm);
            var advances = Advances(data, tables);
            var glyphs = tables.TryGetValue("cmap", out var cmap) ? Glyphs(data, cmap) : null;
            return new WordFontMetrics(glyphs, advances, unitsPerEm, line);
        }

        private static double LineOf(byte[] data, Dictionary<string, int> tables, double unitsPerEm)
        {
            if (tables.TryGetValue("OS/2", out var os2) && U16(data, os2) >= 1)
            {
                var ascent = U16(data, os2 + 74);
                var descent = U16(data, os2 + 76);
                if (ascent + descent > 0)
                    return (ascent + descent) / unitsPerEm;
            }

            var hhea = tables["hhea"];
            return (I16(data, hhea + 4) - I16(data, hhea + 6)) / unitsPerEm;
        }

        private static int[] Advances(byte[] data, Dictionary<string, int> tables)
        {
            var hhea = tables["hhea"];
            var count = U16(data, hhea + 34);
            var hmtx = tables["hmtx"];
            var advances = new int[count];
            for (var i = 0; i < count; i++)
                advances[i] = U16(data, hmtx + 4 * i);

            return advances;
        }

        private static Dictionary<int, int> Glyphs(byte[] data, int cmap)
        {
            var count = U16(data, cmap + 2);
            var best = -1;
            var rank = -1;
            for (var i = 0; i < count; i++)
            {
                var record = cmap + 4 + 8 * i;
                var platform = U16(data, record);
                var encoding = U16(data, record + 2);
                var candidate = platform == 3 && encoding == 10 ? 4 : platform == 3 && encoding == 1 ? 3 : platform == 0 ? 2 : 0;
                if (candidate > rank)
                {
                    rank = candidate;
                    best = cmap + (int)U32(data, record + 4);
                }
            }

            if (best < 0)
                return null;

            var format = U16(data, best);
            return format switch
            {
                4 => Format4(data, best),
                12 => Format12(data, best),
                _ => null,
            };
        }

        private static Dictionary<int, int> Format4(byte[] data, int s)
        {
            var map = new Dictionary<int, int>();
            var segX2 = U16(data, s + 6);
            var endCodes = s + 14;
            var startCodes = endCodes + segX2 + 2;
            var deltas = startCodes + segX2;
            var ranges = deltas + segX2;
            for (var seg = 0; seg < segX2 / 2; seg++)
            {
                var end = U16(data, endCodes + 2 * seg);
                var start = U16(data, startCodes + 2 * seg);
                var delta = I16(data, deltas + 2 * seg);
                var rangeOffset = U16(data, ranges + 2 * seg);
                if (start > end)
                    continue;

                for (var c = start; c <= end; c++)
                {
                    var glyph = 0;
                    if (rangeOffset == 0)
                    {
                        glyph = (c + delta) & MaxBmp;
                    }
                    else
                    {
                        var position = ranges + 2 * seg + rangeOffset + 2 * (c - start);
                        if (position + 2 > data.Length)
                            continue;

                        glyph = U16(data, position);
                        if (glyph != 0)
                            glyph = (glyph + delta) & MaxBmp;
                    }

                    if (glyph != 0)
                        map[c] = glyph;
                }
            }

            return map;
        }

        private static Dictionary<int, int> Format12(byte[] data, int s)
        {
            var map = new Dictionary<int, int>();
            var groups = (int)U32(data, s + 12);
            for (var g = 0; g < groups; g++)
            {
                var record = s + 16 + 12 * g;
                var start = (int)U32(data, record);
                var end = (int)U32(data, record + 4);
                var glyph = (int)U32(data, record + 8);
                for (var c = start; c <= end && c <= MaxBmp; c++)
                    map[c] = glyph + (c - start);
            }

            return map;
        }

        private static Dictionary<string, int> Tables(byte[] data, int offset)
        {
            var tables = new Dictionary<string, int>(StringComparer.Ordinal);
            var count = U16(data, offset + 4);
            for (var i = 0; i < count; i++)
            {
                var record = offset + 12 + 16 * i;
                tables[Tag(data, record)] = (int)U32(data, record + 8);
            }

            return tables;
        }

        private static string Tag(byte[] data, int at) => System.Text.Encoding.ASCII.GetString(data, at, 4);

        private static int U16(byte[] data, int at) => (data[at] << 8) | data[at + 1];

        private static int I16(byte[] data, int at) => (short)U16(data, at);

        private static uint U32(byte[] data, int at) =>
            ((uint)data[at] << 24) | ((uint)data[at + 1] << 16) | ((uint)data[at + 2] << 8) | data[at + 3];
    }
}
