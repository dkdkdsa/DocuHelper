using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocuHelper.Docus;
using DocuHelper.Importers;

namespace DocuHelper.Dash.Runtime
{
    public sealed class DashDocuments : IDashDocuments
    {
        private static readonly DocuImporter[] s_importers = { new ExcelImporter(), new WordImporter(), new HwpImporter(), new PptImporter() };

        private readonly Func<IEnumerable<Docu>> _open;
        private readonly Func<string> _folder;
        private readonly Dictionary<string, (DateTime Stamp, Docu Docu)> _disk = new(StringComparer.OrdinalIgnoreCase);

        public DashDocuments(Func<IEnumerable<Docu>> open, Func<string> folder)
        {
            _open = open ?? (() => Enumerable.Empty<Docu>());
            _folder = folder ?? (() => null);
        }

        public static DashDocuments Of(Workspace workspace) => new(() => workspace.Docus, () => workspace.FolderPath);

        public Docu Open(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            var name = Path.GetFileName(fileName.Trim());
            var open = _open().FirstOrDefault(docu => string.Equals(docu.Name, name, StringComparison.OrdinalIgnoreCase));
            if (open != null)
                return open;

            var folder = _folder();
            if (string.IsNullOrEmpty(folder))
                return null;

            var path = Path.Combine(folder, name);
            if (!File.Exists(path))
                return null;

            var stamp = File.GetLastWriteTimeUtc(path);
            if (_disk.TryGetValue(path, out var hit) && hit.Stamp == stamp)
                return hit.Docu;

            var importer = s_importers.FirstOrDefault(candidate => candidate.CanImport(path));
            if (importer == null)
                return null;

            var docu = importer.Import(path);
            if (_disk.TryGetValue(path, out var old))
                old.Docu.Dispose();

            _disk[path] = (stamp, docu);
            return docu;
        }
    }
}
