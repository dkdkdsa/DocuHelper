namespace DocuHelper.Importers.Legacy.Xls
{
    internal static class XlsMessages
    {
        public const string Broken = "엑셀 파일을 읽지 못했어요. 파일이 망가졌을 수 있어요.";

        public const string Password = "암호가 걸린 엑셀 파일은 열 수 없어요. 엑셀에서 암호를 풀고 다시 넣어 주세요.";

        public const string TooOld = "아주 오래된 엑셀 파일(엑셀 95 이하)은 열 수 없어요. 엑셀에서 열어 새 형식(.xlsx)으로 다시 저장한 뒤 넣어 주세요.";
    }
}
