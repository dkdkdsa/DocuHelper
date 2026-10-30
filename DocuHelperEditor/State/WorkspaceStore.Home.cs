using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DocuHelper;
using DocuHelper.Docus;
using DocuHelperEditor.Services;

namespace DocuHelperEditor.State
{
    public partial class WorkspaceStore
    {
        private const string HelperFolder = ".문서도우미";

        private readonly List<RecentFolderItem> _recents = new();

        public IReadOnlyList<RecentFolderItem> RecentFolders => _recents;

        public bool IsNewFileOpen { get; private set; }

        public string NewFileReturnFocus { get; private set; }

        public DocuType LastNewFileType { get; private set; } = DocuType.Excel;

        public void OpenNewFile(string returnFocus)
        {
            if (!IsReady)
                return;

            NewFileReturnFocus = returnFocus;
            IsNewFileOpen = true;
            Notify();
        }

        public void CloseNewFile()
        {
            IsNewFileOpen = false;
            Notify();
        }

        public void RememberNewFileType(DocuType type) => LastNewFileType = type;

        public void RefreshRecents()
        {
            _recents.Clear();
            var items = _registry.Items;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var folder = new RecentFolderItem
                {
                    Key = "recent-" + i.ToString(CultureInfo.InvariantCulture),
                    Path = item.Path,
                    Name = string.IsNullOrWhiteSpace(item.Name) ? Path.GetFileName(Path.TrimEndingDirectorySeparator(item.Path)) : item.Name,
                    DisplayPath = AppPaths.Display(item.Path),
                    When = FormatRecent(item.LastOpened),
                    IsMissing = !Directory.Exists(item.Path),
                };

                if (!folder.IsMissing)
                    CountFiles(folder);

                _recents.Add(folder);
            }

            Notify();
        }

        public bool OpenRecent(string path)
        {
            if (!Directory.Exists(path))
            {
                RefreshRecents();
                return false;
            }

            if (IsReady && WorkspaceRegistry.SamePath(path, FolderPath))
            {
                _registry.Touch(FolderPath, FolderName);
                return true;
            }

            OpenWorkspace(path);
            return true;
        }

        public void ForgetRecent(string path)
        {
            _registry.Remove(path);
            RefreshRecents();
        }

        public async Task RelocateRecentAsync(string path)
        {
            var picked = await _dialogs.PickFolderAsync();
            if (string.IsNullOrEmpty(picked))
                return;

            _registry.Relocate(path, picked);
            RefreshRecents();
        }

        public void OpenInOriginalApp(string fileId)
        {
            var file = FindFile(fileId);
            if (file?.Path == null || !File.Exists(file.Path))
                return;

            if (file.IsDirty)
                RaiseNotice($"‘{file.Name}’은 마지막으로 저장한 모습으로 열려요. 고친 걸 보려면 먼저 저장해 주세요.");

            try
            {
                _shell?.OpenFile(file.Path);
            }
            catch (Exception e)
            {
                AppLog.Write($"Could not open {file.Path}", e);
                RaiseNotice($"‘{file.Name}’을 열 프로그램을 찾지 못했어요.");
            }
        }

        public void RevealFolder(string path)
        {
            try
            {
                _shell?.OpenFile(path);
            }
            catch (Exception e)
            {
                AppLog.Write($"Could not show {path}", e);
            }
        }

        public string WorkspaceNameProblem(string name, out bool exists)
        {
            exists = false;
            var problem = FileNames.Problem(name);
            if (problem != null)
                return problem;

            var trimmed = name.Trim();
            if (Directory.Exists(NewWorkspacePath(trimmed)))
            {
                exists = true;
                return $"‘{trimmed}’ 폴더가 이미 있어요. 새로 만들지 않고 그 폴더를 열 수 있어요.";
            }

            return null;
        }

        public string NewWorkspacePath(string name) => Path.Combine(_settings.RootPath, (name ?? string.Empty).Trim());

        public string CreateWorkspace(string name)
        {
            var problem = WorkspaceNameProblem(name, out _);
            if (problem != null)
                return problem;

            var path = NewWorkspacePath(name);
            try
            {
                Directory.CreateDirectory(path);
                var helper = Directory.CreateDirectory(Path.Combine(path, HelperFolder));
                helper.Attributes |= FileAttributes.Hidden;
                Directory.CreateDirectory(Path.Combine(helper.FullName, "기록"));
                Directory.CreateDirectory(Path.Combine(helper.FullName, "대화"));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                AppLog.Write($"Could not create {path}", e);
                return "폴더를 만들지 못했어요. 만들 곳을 바꾸거나, 그 폴더에 쓸 수 있는지 봐 주세요.";
            }

            OpenWorkspace(path);
            return null;
        }

        public bool OpenExistingWorkspace(string name)
        {
            var path = NewWorkspacePath(name);
            return Directory.Exists(path) && OpenRecent(path);
        }

        public async Task<bool> OpenFolderFromDiskAsync()
        {
            var path = await _dialogs.PickFolderAsync();
            if (string.IsNullOrEmpty(path))
                return false;

            return OpenRecent(path);
        }

        public bool CanLeaveWorkspace()
        {
            if (_workspace.IsAgentRunning)
            {
                RaiseNotice("도우미가 일하는 중이에요. 끝나거나 멈춘 다음 다른 작업 폴더를 열어 주세요.");
                return false;
            }

            if (UnsavedCount > 0)
            {
                RaiseNotice("저장 안 한 파일이 있어요. 먼저 저장하거나 닫은 다음 다른 작업 폴더를 열어 주세요.");
                return false;
            }

            return true;
        }

        public string ExtensionOf(DocuType type) => type switch
        {
            DocuType.Excel => ".xlsx",
            DocuType.Word => ".docx",
            DocuType.Hwp => _workspace.Editors.TryGetValue(DocuType.Hwp, out var editor) ? editor.Extension : ".hwpx",
            _ => ".pptx",
        };

        public string SuggestFileName(DocuType type)
        {
            var stem = type switch
            {
                DocuType.Excel => "새 엑셀 파일",
                DocuType.Word => "새 워드 파일",
                DocuType.Hwp => "새 한글 파일",
                _ => "새 파워포인트 파일",
            };

            var extension = ExtensionOf(type);
            if (!_workspace.IsTakenName(stem + extension))
                return stem;

            for (var i = 2; ; i++)
            {
                var candidate = $"{stem} ({i})";
                if (!_workspace.IsTakenName(candidate + extension))
                    return candidate;
            }
        }

        public string NewFileProblem(DocuType type, string name)
        {
            var problem = FileNames.Problem(name);
            if (problem != null)
                return problem;

            var full = name.Trim() + ExtensionOf(type);
            return _workspace.IsTakenName(full) ? $"‘{full}’ 파일이 이미 있어요. 다른 이름을 적어 주세요." : null;
        }

        public WorkspaceFile CreateFile(DocuType type, string name, out string error)
        {
            error = IsReady ? NewFileProblem(type, name) : "작업 폴더를 먼저 열어 주세요.";
            if (error != null)
                return null;

            try
            {
                var docu = _workspace.CreateDocu(name.Trim() + ExtensionOf(type));
                return _files.FirstOrDefault(f => f.Docu == docu);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException)
            {
                AppLog.Write($"Could not create {name}", e);
                error = e is InvalidOperationException ? e.Message : "파일을 만들지 못했어요. 작업 폴더에 쓸 수 있는지 봐 주세요.";
                return null;
            }
        }

        private void OpenWorkspace(string path)
        {
            OpenFolder(path);
            _registry.Touch(FolderPath, FolderName);
        }

        private void OnDocuCreated(Docu docu)
        {
            var file = _files.FirstOrDefault(f => f.Docu == docu);
            if (file == null)
                return;

            file.IsJustCreated = true;
            _files.Remove(file);
            _files.Insert(0, file);
            Open(file.Id);
        }

        private static void CountFiles(RecentFolderItem folder)
        {
            try
            {
                foreach (var path in Directory.EnumerateFiles(folder.Path))
                {
                    if (Path.GetFileName(path).StartsWith("~$", StringComparison.Ordinal))
                        continue;

                    switch (Path.GetExtension(path).ToLowerInvariant())
                    {
                        case ".xlsx":
                            folder.ExcelCount++;
                            break;
                        case ".docx":
                            folder.WordCount++;
                            break;
                        case ".pptx":
                            folder.PptCount++;
                            break;
                        case ".hwp":
                        case ".hwpx":
                            folder.HwpCount++;
                            break;
                    }
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                AppLog.Write($"Could not count files in {folder.Path}", e);
            }
        }

        private static string FormatRecent(DateTime time)
        {
            var days = (DateTime.Today - time.Date).Days;
            if (days == 0)
                return "오늘 " + time.ToString("tt h:mm", s_korean);

            if (days == 1)
                return "어제 " + time.ToString("tt h:mm", s_korean);

            return time.Year == DateTime.Today.Year
                ? $"{time.Month}월 {time.Day}일"
                : $"{time.Year}년 {time.Month}월 {time.Day}일";
        }
    }
}
