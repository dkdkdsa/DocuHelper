using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocuHelper.Agents;
using DocuHelperEditor.Services;

namespace DocuHelperEditor.State
{
    public partial class AssistantStore
    {
        public const int MaxAttachments = 10;
        public const int MaxImageEdge = 2000;
        public const long MaxImageBytes = 3_500_000;
        public const long MaxImageSourceBytes = 30L * 1024 * 1024;
        public const long MaxPdfBytes = 10L * 1024 * 1024;
        public const long MaxTextBytes = 200L * 1024;

        private readonly List<AttachmentModel> _attachments = new();
        private int _attachmentSeed;

        public IReadOnlyList<AttachmentModel> Attachments => _attachments;

        public string AddAttachment(IncomingFile file)
        {
            if (file == null)
                return "read";

            if (_attachments.Count >= MaxAttachments)
                return "limit";

            byte[] data;
            try
            {
                data = Convert.FromBase64String(file.Data ?? string.Empty);
            }
            catch (FormatException)
            {
                return "read";
            }

            var attachment = new AttachmentModel
            {
                Id = "attachment-" + (++_attachmentSeed).ToString(CultureInfo.InvariantCulture),
                Name = string.IsNullOrWhiteSpace(file.Name) ? "붙인 파일" : file.Name.Trim(),
                Size = data.Length,
            };

            switch (file.Kind)
            {
                case "image":
                    attachment.Kind = AttachmentKind.Image;
                    attachment.MediaType = ImageMediaType(data);
                    if (attachment.MediaType == null)
                        return "read";

                    if (data.Length > MaxImageBytes)
                        return "large";

                    attachment.Data = data;
                    attachment.Thumbnail = file.Thumbnail != null && file.Thumbnail.StartsWith("data:image/", StringComparison.Ordinal) ? file.Thumbnail : null;
                    break;
                case "pdf":
                    attachment.Kind = AttachmentKind.Pdf;
                    if (!IsPdf(data))
                        return "read";

                    if (data.Length > MaxPdfBytes)
                        return "large";

                    attachment.MediaType = "application/pdf";
                    attachment.Data = data;
                    break;
                case "text":
                    attachment.Kind = AttachmentKind.Text;
                    if (data.Length > MaxTextBytes)
                        return "large";

                    attachment.Text = TextDecoder.Decode(data);
                    if (string.IsNullOrWhiteSpace(attachment.Text))
                        return "empty";

                    attachment.MediaType = "text/plain";
                    break;
                default:
                    return "unsupported";
            }

            _attachments.Add(attachment);
            if (IsPanelCollapsed)
                SetPanelCollapsed(false);
            else
                Notify();

            return null;
        }

        public void RemoveAttachment(string id)
        {
            if (_attachments.RemoveAll(a => a.Id == id) > 0)
                Notify();
        }

        public void ReportFileProblems(IEnumerable<IncomingFile> problems)
        {
            var lines = (problems ?? Enumerable.Empty<IncomingFile>())
                .Where(p => p != null)
                .Select(p => $"‘{p.Name}’: {Explain(p)}")
                .Distinct()
                .ToList();

            if (lines.Count > 0)
                OnNotice(string.Join("\n", lines));
        }

        private static string Explain(IncomingFile problem) => (problem.Problem, problem.Kind) switch
        {
            ("limit", _) => $"한 번에 {MaxAttachments}개까지만 붙일 수 있어서 뺐어요.",
            ("large", "image") => "그림이 너무 커서 붙이지 못했어요.",
            ("large", "pdf") => $"PDF는 {MaxPdfBytes / (1024 * 1024)}MB까지 붙일 수 있어요.",
            ("large", "text") => $"글 파일은 {MaxTextBytes / 1024}KB까지 붙일 수 있어요.",
            ("read", "image") => "그림을 읽지 못했어요. PNG나 JPG로 저장해서 다시 붙여 주세요.",
            ("read", "pdf") => "PDF를 읽지 못했어요. 파일이 망가졌을 수 있어요.",
            ("empty", _) => "빈 파일이에요.",
            ("unsupported", _) => WorkspaceStore.ExplainUnsupportedDrop(problem.Name),
            _ => "파일을 읽지 못했어요.",
        };

        private static AgentAttachment ToAgentAttachment(AttachmentModel attachment) => new()
        {
            Name = attachment.Name,
            Kind = attachment.Kind switch
            {
                AttachmentKind.Image => AgentAttachmentKind.Image,
                AttachmentKind.Pdf => AgentAttachmentKind.Pdf,
                _ => AgentAttachmentKind.Text,
            },
            MediaType = attachment.MediaType,
            Data = attachment.Data,
            Text = attachment.Text,
        };

        private static string ImageMediaType(byte[] data)
        {
            if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
                return "image/png";

            if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
                return "image/jpeg";

            if (data.Length >= 6 && data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x38)
                return "image/gif";

            if (data.Length >= 12 && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46
                && data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50)
                return "image/webp";

            return null;
        }

        private static bool IsPdf(byte[] data)
        {
            var end = Math.Min(data.Length - 5, 1024);
            for (var i = 0; i <= end; i++)
            {
                if (data[i] == 0x25 && data[i + 1] == 0x50 && data[i + 2] == 0x44 && data[i + 3] == 0x46 && data[i + 4] == 0x2D)
                    return true;
            }

            return false;
        }
    }
}
