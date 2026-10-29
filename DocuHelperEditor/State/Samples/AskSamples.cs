using System.Collections.Generic;
using DocuHelper.Docus;

namespace DocuHelperEditor.State.Samples
{
    public static class AskSamples
    {
        public static AskUserRequest SameClient(string fileId)
        {
            return new AskUserRequest
            {
                Kind = AskKind.Choice,
                FileId = fileId,
                Question = "‘한빛상사’와 ‘(주)한빛상사’를 같은 거래처로 볼까요?",
                Hint = "왼쪽 표에 연필로 동그라미 친 이름들이에요.",
                Legend = "어떻게 할까요?",
                Options = new List<AskOption>
                {
                    new AskOption { Id = "same-client", Label = "네, 같은 곳이에요", Detail = "합쳐서 58건" },
                    new AskOption { Id = "other-client", Label = "아니요, 다른 곳이에요", Detail = "37건, 21건" },
                    new AskOption { Id = "explain", Label = "직접 설명할게요", IsFreeText = true },
                },
            };
        }

        public static AskUserRequest WhichFile()
        {
            return new AskUserRequest
            {
                Kind = AskKind.WhichFile,
                Question = "표를 어느 파일에 넣을까요?",
                Hint = "작업 폴더에 넣을 만한 파일이 두 개 있어요.",
                Legend = "넣을 파일",
                Options = new List<AskOption>
                {
                    new AskOption { Id = "report", Label = "7월 회의 보고서.docx", FileType = DocuType.Word },
                    new AskOption { Id = "deck", Label = "상반기 실적 발표.pptx", FileType = DocuType.Ppt },
                    new AskOption { Id = "new-word", Label = "새 워드 파일 만들기" },
                },
            };
        }

        public static AskUserRequest BigChange()
        {
            return new AskUserRequest
            {
                Kind = AskKind.BigChange,
                Eyebrow = "시작하기 전에 확인할게요",
                Question = "파일 2개에서 132곳을 바꿔요. 이대로 할까요?",
                Hint = "바꾼 뒤에도 저장하기 전에는 전부 되돌릴 수 있어요.",
                Impacts = new List<AskImpact>
                {
                    new AskImpact { FileName = "6월 매출정리.xlsx", FileType = DocuType.Excel, Count = 120 },
                    new AskImpact { FileName = "7월 회의 보고서.docx", FileType = DocuType.Word, Count = 12 },
                },
                ConfirmLabel = "이대로 해 주세요",
                AlternateLabel = "한 곳만 먼저 보여 주세요",
            };
        }

        public static AskUserRequest Conflict(string fileId)
        {
            return new AskUserRequest
            {
                Kind = AskKind.Conflict,
                FileId = fileId,
                Question = "C5는 직접 고치신 칸이에요. 도우미 계산으로 바꿀까요?",
                Legend = "어떻게 할까요?",
                MineValue = "5,610,000",
                AssistantValue = "5,620,000",
                Options = new List<AskOption>
                {
                    new AskOption { Id = "keep-mine", Label = "내가 넣은 값 그대로 두기" },
                    new AskOption { Id = "use-assistant", Label = "도우미 계산으로 바꾸기" },
                },
            };
        }
    }
}
