using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DocuHelper.Docus;
using DocuHelper.Importers;

namespace DocuHelperEditor.State
{
    public partial class WorkspaceStore
    {
        private static readonly string[] s_explainedExtensions = { ".hwp", ".hwpx", ".xls", ".doc", ".ppt" };

        private readonly List<DropCandidate> _dropCandidates = new();

        public bool IsDragOver { get; private set; }

        public IReadOnlyList<DropCandidate> DropCandidates => _dropCandidates;

        public bool CanTakeFiles => IsReady && PendingClose == null && !IsNewFileOpen;

        public void ShowDrop(IEnumerable<DropCandidate> candidates)
        {
            var items = candidates?.Where(c => c != null).ToList() ?? new List<DropCandidate>();
            _dropCandidates.Clear();
            _dropCandidates.AddRange(items);
            IsDragOver = true;
            Notify();
        }

        public void ShowDrag(IEnumerable<string> kinds)
        {
            var counts = (kinds ?? Enumerable.Empty<string>())
                .GroupBy(k => k ?? string.Empty)
                .ToDictionary(g => g.Key, g => g.Count());

            var candidates = new List<DropCandidate>();
            AddDragRow(candidates, counts, "excel", "엑셀 파일", DocuType.Excel, DropDestination.Folder, null);
            AddDragRow(candidates, counts, "word", "워드 파일", DocuType.Word, DropDestination.Folder, null);
            AddDragRow(candidates, counts, "ppt", "파워포인트 파일", DocuType.Ppt, DropDestination.Folder, null);
            AddDragRow(candidates, counts, "image", "그림", null, DropDestination.Assistant, "image");
            AddDragRow(candidates, counts, "pdf", "PDF", null, DropDestination.Assistant, "file-text");
            AddDragRow(candidates, counts, "text", "글 파일", null, DropDestination.Assistant, "file-lines");
            AddDragRow(candidates, counts, "other", "다른 파일", null, DropDestination.Unknown, "file");
            ShowDrop(candidates);
        }

        public void ClearDrop()
        {
            if (!IsDragOver && _dropCandidates.Count == 0)
                return;

            _dropCandidates.Clear();
            IsDragOver = false;
            Notify();
        }

        public async Task ImportDroppedAsync(IEnumerable<string> paths)
        {
            ClearDrop();
            if (!CanTakeFiles || paths == null)
                return;

            var all = paths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var problems = all
                .Where(p => !Directory.Exists(p) && FindImporter(p) == null && !s_explainedExtensions.Contains(Path.GetExtension(p).ToLowerInvariant()))
                .Select(p => $"‘{Path.GetFileName(p)}’: {ExplainUnsupportedDrop(p)}")
                .ToList();

            var importable = all.Where(p => Directory.Exists(p) || FindImporter(p) != null || s_explainedExtensions.Contains(Path.GetExtension(p).ToLowerInvariant()));
            await ImportAsync(importable, problems);
        }

        public static string ExplainUnsupportedDrop(string name)
        {
            var extension = Path.GetExtension(name ?? string.Empty).ToLowerInvariant();
            if (s_explainedExtensions.Contains(extension))
                return DocuImporter.ExplainUnsupported(name);

            return "넣을 수 없는 파일이에요. 엑셀, 워드, 파워포인트 파일은 작업 폴더에 넣고, 그림, PDF, 글 파일(.txt, .csv, .md)은 도우미에게 붙일 수 있어요.";
        }

        private static void AddDragRow(List<DropCandidate> rows, Dictionary<string, int> counts, string kind, string label, DocuType? type, DropDestination destination, string glyph)
        {
            if (!counts.TryGetValue(kind, out var count) || count == 0)
                return;

            rows.Add(new DropCandidate
            {
                Name = $"{label} {count}개",
                Type = type,
                CanOpen = true,
                Destination = destination,
                Glyph = glyph,
            });
        }
    }
}
