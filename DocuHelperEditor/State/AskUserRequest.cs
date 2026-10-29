using System.Collections.Generic;

namespace DocuHelperEditor.State
{
    public class AskUserRequest
    {
        public AskKind Kind { get; set; }

        public string FileId { get; set; }

        public string Eyebrow { get; set; } = "도우미가 여쭤봐요";

        public string Question { get; set; }

        public string Hint { get; set; }

        public string Legend { get; set; }

        public List<AskOption> Options { get; set; } = new();

        public List<AskImpact> Impacts { get; set; } = new();

        public string MineValue { get; set; }

        public string AssistantValue { get; set; }

        public string ConfirmLabel { get; set; } = "이렇게 해 주세요";

        public string AlternateLabel { get; set; } = "다른 말로 답할래요";
    }
}
