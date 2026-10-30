using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace DocuHelper
{
    public class DocuHistory
    {
        private const string StampFormat = "yyyyMMdd-HHmmss-fff";

        private readonly string _root;

        public DocuHistory(string root)
        {
            _root = root;
        }

        public HistoryEntry Record(string filePath, string what)
        {
            var fileName = Path.GetFileName(filePath);
            var folder = EnsureFolder(fileName);
            var savedAt = DateTime.Now;
            var id = savedAt.ToString(StampFormat, CultureInfo.InvariantCulture);
            var copy = Path.Combine(folder, id + Path.GetExtension(fileName));

            File.Copy(filePath, copy, true);
            File.WriteAllText(Path.Combine(folder, id + ".txt"), what ?? string.Empty);

            return new HistoryEntry { Id = id, FileName = fileName, SavedAt = savedAt, What = what, Path = copy };
        }

        public IReadOnlyList<HistoryEntry> List(string fileName)
        {
            var folder = Path.Combine(_root, fileName);
            if (!Directory.Exists(folder))
                return Array.Empty<HistoryEntry>();

            var extension = Path.GetExtension(fileName);
            return Directory.EnumerateFiles(folder, "*.txt")
                .Select(note => Path.GetFileNameWithoutExtension(note))
                .Where(id => DateTime.TryParseExact(id, StampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                .Where(id => File.Exists(Path.Combine(folder, id + extension)))
                .OrderByDescending(id => id, StringComparer.Ordinal)
                .Select(id => new HistoryEntry
                {
                    Id = id,
                    FileName = fileName,
                    SavedAt = DateTime.ParseExact(id, StampFormat, CultureInfo.InvariantCulture),
                    What = File.ReadAllText(Path.Combine(folder, id + ".txt")),
                    Path = Path.Combine(folder, id + extension),
                })
                .ToList();
        }

        public byte[] Read(HistoryEntry entry) => File.ReadAllBytes(entry.Path);

        public void Rename(string oldName, string newName)
        {
            var source = Path.Combine(_root, oldName);
            var target = Path.Combine(_root, newName);
            if (!Directory.Exists(source) || string.Equals(source, target, StringComparison.Ordinal))
                return;

            try
            {
                if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
                {
                    var step = source + "." + Guid.NewGuid().ToString("N");
                    Directory.Move(source, step);
                    Directory.Move(step, target);
                    return;
                }

                if (!Directory.Exists(target))
                {
                    Directory.Move(source, target);
                    return;
                }

                foreach (var file in Directory.EnumerateFiles(source))
                {
                    var moved = Path.Combine(target, Path.GetFileName(file));
                    if (!File.Exists(moved))
                        File.Move(file, moved);
                }

                Directory.Delete(source, true);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
            }
        }

        public void Forget(string fileName)
        {
            var folder = Path.Combine(_root, fileName);
            try
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
            }
        }

        private string EnsureFolder(string fileName)
        {
            if (!Directory.Exists(_root))
            {
                var hidden = Directory.CreateDirectory(Path.GetDirectoryName(_root));
                hidden.Attributes |= FileAttributes.Hidden;
                Directory.CreateDirectory(_root);
            }

            var folder = Path.Combine(_root, fileName);
            Directory.CreateDirectory(folder);
            return folder;
        }
    }
}
