using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DocuHelperEditor.State;

namespace DocuHelperEditor.Services
{
    public class WorkspaceRegistry
    {
        private static readonly string s_path = Path.Combine(AppPaths.DataFolder, "workspaces.json");

        private List<RecentWorkspace> _items;

        public IReadOnlyList<RecentWorkspace> Items
        {
            get
            {
                _items ??= Read();
                return _items.OrderByDescending(w => w.LastOpened).ToList();
            }
        }

        public void Touch(string path, string name)
        {
            _items ??= Read();
            var full = Path.GetFullPath(path);
            var item = Find(full);
            if (item == null)
            {
                item = new RecentWorkspace { Path = full, Name = name };
                _items.Add(item);
            }

            item.LastOpened = DateTime.Now;
            Write();
        }

        public void Remove(string path)
        {
            _items ??= Read();
            _items.RemoveAll(w => SamePath(w.Path, path));
            Write();
        }

        public void Clear()
        {
            _items = new List<RecentWorkspace>();
            Write();
        }

        public void Relocate(string path, string newPath)
        {
            _items ??= Read();
            var item = Find(path);
            if (item == null)
                return;

            _items.RemoveAll(w => w != item && SamePath(w.Path, newPath));
            item.Path = Path.GetFullPath(newPath);
            Write();
        }

        public static bool SamePath(string a, string b) => string.Equals(
            Path.TrimEndingDirectorySeparator(a ?? string.Empty),
            Path.TrimEndingDirectorySeparator(b ?? string.Empty),
            StringComparison.OrdinalIgnoreCase);

        private RecentWorkspace Find(string path) => _items.FirstOrDefault(w => SamePath(w.Path, path));

        private static List<RecentWorkspace> Read()
        {
            try
            {
                if (!File.Exists(s_path))
                    return new List<RecentWorkspace>();

                var items = JsonSerializer.Deserialize<List<RecentWorkspace>>(File.ReadAllText(s_path));
                return items == null
                    ? new List<RecentWorkspace>()
                    : items.Where(w => !string.IsNullOrWhiteSpace(w.Path)).ToList();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException)
            {
                AppLog.Write("Could not read the workspace list", e);
                return new List<RecentWorkspace>();
            }
        }

        private void Write()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(s_path));
                File.WriteAllText(s_path, JsonSerializer.Serialize(_items, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                AppLog.Write("Could not save the workspace list", e);
            }
        }
    }
}
