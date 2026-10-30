using System;
using System.Globalization;

namespace DocuHelperEditor.State
{
    public static class ConversationTime
    {
        private static readonly CultureInfo s_korean = CultureInfo.GetCultureInfo("ko-KR");

        public static string Describe(DateTime time)
        {
            var today = DateTime.Today;
            if (time.Date == today)
                return "오늘 " + time.ToString("tt h:mm", s_korean);

            if (time.Date == today.AddDays(-1))
                return "어제 " + time.ToString("tt h:mm", s_korean);

            return time.Year == today.Year
                ? time.ToString("M월 d일 tt h:mm", s_korean)
                : time.ToString("yyyy년 M월 d일", s_korean);
        }
    }
}
