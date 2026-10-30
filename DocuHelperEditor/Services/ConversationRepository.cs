using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using DocuHelperEditor.State;

namespace DocuHelperEditor.Services
{
    public class ConversationRepository : IConversationRepository
    {
        private const string Extension = ".json";

        private static readonly JsonSerializerOptions s_json = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() },
        };

        public async Task<IReadOnlyList<ConversationSummary>> ListRecentAsync(string folderPath, int count)
        {
            var folder = FolderOf(folderPath);
            if (folder == null || !Directory.Exists(folder))
                return Array.Empty<ConversationSummary>();

            var items = new List<ConversationSummary>();
            var files = new DirectoryInfo(folder).EnumerateFiles("*" + Extension)
                .OrderByDescending(f => f.LastWriteTimeUtc);

            foreach (var file in files)
            {
                if (items.Count >= count)
                    break;

                var conversation = await ReadAsync(file.FullName);
                if (conversation == null)
                    continue;

                items.Add(new ConversationSummary
                {
                    Id = conversation.Id,
                    Title = conversation.Title,
                    FileName = conversation.FileName,
                    FileType = conversation.FileType,
                    When = ConversationTime.Describe(conversation.UpdatedAt),
                });
            }

            return items;
        }

        public async Task<SavedConversation> LoadAsync(string folderPath, string id)
        {
            var path = PathOf(folderPath, id);
            return path == null || !File.Exists(path) ? null : await ReadAsync(path);
        }

        public async Task SaveAsync(string folderPath, SavedConversation conversation)
        {
            var path = PathOf(folderPath, conversation?.Id);
            if (path == null)
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temp = path + ".tmp";
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(conversation, s_json));
            File.Move(temp, path, true);
        }

        private static async Task<SavedConversation> ReadAsync(string path)
        {
            try
            {
                var conversation = JsonSerializer.Deserialize<SavedConversation>(await File.ReadAllTextAsync(path), s_json);
                return string.IsNullOrWhiteSpace(conversation?.Id) ? null : conversation;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException)
            {
                AppLog.Write("Could not read a conversation", e);
                return null;
            }
        }

        private static string FolderOf(string folderPath) => string.IsNullOrEmpty(folderPath)
            ? null
            : Path.Combine(folderPath, ".문서도우미", "대화");

        private static string PathOf(string folderPath, string id)
        {
            var folder = FolderOf(folderPath);
            if (folder == null || string.IsNullOrWhiteSpace(id) || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || id.Contains(".."))
                return null;

            return Path.Combine(folder, id + Extension);
        }
    }
}
