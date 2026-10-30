using System.Collections.Generic;
using System.Linq;

namespace DocuHelperEditor.State
{
    public static class AssistantModels
    {
        public const string DefaultModel = "sonnet";
        public const string DefaultEffort = "medium";

        public static IReadOnlyList<ModelOption> Models { get; } = new[]
        {
            new ModelOption { Id = "haiku", ApiId = "claude-haiku-5-5", Name = "Haiku 5.5", Cost = "비용 적음", Description = "가장 빨라요. 간단한 일에 좋아요" },
            new ModelOption { Id = "sonnet", ApiId = "claude-sonnet-5-5", Name = "Sonnet 5.5", Cost = "비용 보통", Description = "빠르고 똑똑해요. 대부분 이걸로 충분해요", IsDefault = true },
            new ModelOption { Id = "opus", ApiId = "claude-opus-5-5", Name = "Opus 5.5", Cost = "비용 2배", Description = "더 꼼꼼해요. 복잡한 표나 긴 문서에" },
            new ModelOption { Id = "fable", ApiId = "claude-fable-5-1", Name = "Fable 5.1", Cost = "비용 5배", Description = "가장 똑똑해요. 아주 어려운 일에만" },
        };

        public static IReadOnlyList<EffortOption> Efforts { get; } = new[]
        {
            new EffortOption { Id = "low", Name = "빠르게", Description = "짧은 질문이나 간단한 고치기" },
            new EffortOption { Id = "medium", Name = "보통", Description = "대부분의 일에 알맞아요", IsDefault = true },
            new EffortOption { Id = "high", Name = "꼼꼼하게", Description = "계산이 많거나 긴 문서. 조금 더 걸려요" },
            new EffortOption { Id = "max", Name = "최대한", Description = "아주 어려운 일. 시간과 비용이 많이 들어요" },
        };

        public static ModelOption FindModel(string id) =>
            Models.FirstOrDefault(m => m.Id == id) ?? Models.First(m => m.IsDefault);

        public static EffortOption FindEffort(string id) =>
            Efforts.FirstOrDefault(e => e.Id == id) ?? Efforts.First(e => e.IsDefault);
    }
}
