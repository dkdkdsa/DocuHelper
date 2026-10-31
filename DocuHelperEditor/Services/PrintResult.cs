namespace DocuHelperEditor.Services
{
    public sealed record PrintResult(PrintStatus Status, string Message = null)
    {
        public bool IsDone => Status == PrintStatus.Done;

        public static PrintResult Done(string message = null) => new PrintResult(PrintStatus.Done, message);

        public static PrintResult Cancelled() => new PrintResult(PrintStatus.Cancelled);

        public static PrintResult Unsupported(string message = null) => new PrintResult(PrintStatus.Unsupported, message ?? "이 컴퓨터에서는 아직 쓸 수 없어요.");

        public static PrintResult Failed(string message = null) => new PrintResult(PrintStatus.Failed, message ?? "끝내지 못했어요. 한 번 더 해 보세요.");
    }
}
