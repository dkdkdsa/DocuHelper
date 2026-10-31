using DocuHelper.Docus;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordProtection
    {
        public const string UnlockTool = "set_document_settings";
        public const string CommentTool = "manage_comments";

        public static WordProtectionMode Mode(WordDocu docu)
        {
            var protection = Element(docu);
            if (protection == null || protection.Enforcement == null || !protection.Enforcement.Value)
                return WordProtectionMode.None;

            var edit = protection.Edit?.Value;
            if (edit == null)
                return WordProtectionMode.None;

            if (edit == DocumentProtectionValues.ReadOnly)
                return WordProtectionMode.ReadOnly;

            if (edit == DocumentProtectionValues.Comments)
                return WordProtectionMode.CommentsOnly;

            if (edit == DocumentProtectionValues.TrackedChanges)
                return WordProtectionMode.TrackedChangesOnly;

            if (edit == DocumentProtectionValues.Forms)
                return WordProtectionMode.Forms;

            return WordProtectionMode.None;
        }

        public static bool HasPassword(WordDocu docu)
        {
            var protection = Element(docu);
            return protection != null
                && (!string.IsNullOrEmpty(protection.Hash?.Value) || !string.IsNullOrEmpty(protection.HashValue?.Value));
        }

        public static string Check(WordDocu docu, Tool tool)
        {
            if (docu == null || tool == null || tool.IsReadOnly || tool.Name == UnlockTool)
                return null;

            switch (Mode(docu))
            {
                case WordProtectionMode.ReadOnly:
                case WordProtectionMode.Forms:
                    return "이 문서는 편집이 제한돼 있어요. 편집 제한을 풀어 주세요.";
                case WordProtectionMode.CommentsOnly:
                    return tool.Name == CommentTool ? null : "이 문서는 메모만 달 수 있게 제한돼 있어요. 편집 제한을 풀어 주세요.";
                default:
                    return null;
            }
        }

        private static DocumentProtection Element(WordDocu docu) =>
            docu?.Document?.MainDocumentPart?.DocumentSettingsPart?.Settings?.GetFirstChild<DocumentProtection>();
    }
}
