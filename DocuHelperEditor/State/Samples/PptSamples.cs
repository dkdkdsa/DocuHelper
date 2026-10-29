using System.Collections.Generic;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Samples
{
    public static class PptSamples
    {
        public const string TextChangeId = "ppt-slide4-text";

        public const string NotesChangeId = "ppt-slide4-notes";

        private const string ClientBodyId = "deck-s4-body";

        public static PptPaperModel ResultsDeck(bool edited)
        {
            var model = new PptPaperModel
            {
                Footer = "영업지원팀",
                ActiveIndex = 3,
                SelectedBoxId = ClientBodyId,
            };

            model.Slides.Add(Cover());
            model.Slides.Add(Summary());
            model.Slides.Add(MonthlySales());
            model.Slides.Add(ClientSales(edited));
            model.Slides.Add(SecondHalfPlan());
            model.Slides.Add(Closing());
            return model;
        }

        public static List<ChangeItem> DeckChanges(string fileId)
        {
            return new List<ChangeItem>
            {
                new ChangeItem
                {
                    Id = TextChangeId,
                    FileId = fileId,
                    Label = "슬라이드 글을 3줄로 줄여요",
                    Location = "4번",
                    Icon = "lines",
                },
                new ChangeItem
                {
                    Id = NotesChangeId,
                    FileId = fileId,
                    Label = "뺀 내용을 발표 메모로 옮겨요",
                    Location = "메모",
                    Icon = "note",
                },
            };
        }

        private static SlideModel Cover()
        {
            var slide = new SlideModel { Id = "deck-s1", Layout = SlideLayout.Cover };
            slide.Boxes.Add(Box("deck-s1-title", SlideBoxKind.Title, 28, true, "상반기 실적 발표"));
            slide.Boxes.Add(Box("deck-s1-sub", SlideBoxKind.Subtitle, 15, false, "영업지원팀"));
            return slide;
        }

        private static SlideModel Summary()
        {
            var slide = new SlideModel { Id = "deck-s2", Layout = SlideLayout.Content };
            slide.Boxes.Add(Box("deck-s2-title", SlideBoxKind.Title, 22, true, "상반기 실적 요약"));
            slide.Boxes.Add(Box(
                "deck-s2-body",
                SlideBoxKind.Body,
                15,
                false,
                "6월 매출 4,697만 원으로 최고",
                "거래처 6곳, 6월 거래 247건",
                "5월보다 2배 넘게 늘어남"));
            return slide;
        }

        private static SlideModel MonthlySales()
        {
            var slide = new SlideModel
            {
                Id = "deck-s3",
                Layout = SlideLayout.Content,
                Chart = new SlideChartModel
                {
                    Label = "월별 매출 막대그래프",
                    Kind = SlideChartKind.Column,
                    Bars =
                    {
                        new SlideChartBar { Label = "3월", Value = 28180000 },
                        new SlideChartBar { Label = "4월", Value = 39930000 },
                        new SlideChartBar { Label = "5월", Value = 21140000 },
                        new SlideChartBar { Label = "6월", Value = 46974000 },
                    },
                },
            };

            slide.Boxes.Add(Box("deck-s3-title", SlideBoxKind.Title, 22, true, "3~6월 매출 흐름"));
            return slide;
        }

        private static SlideModel ClientSales(bool edited)
        {
            var slide = new SlideModel
            {
                Id = "deck-s4",
                Layout = SlideLayout.Content,
                Chart = new SlideChartModel
                {
                    Label = "거래처별 금액 막대그래프",
                    Kind = SlideChartKind.Bar,
                    Bars =
                    {
                        new SlideChartBar { Label = "한빛상사", Value = 14820000 },
                        new SlideChartBar { Label = "서울오피스", Value = 11356000 },
                        new SlideChartBar { Label = "대성문구", Value = 6948500 },
                        new SlideChartBar { Label = "미래사무기기", Value = 5610000 },
                        new SlideChartBar { Label = "그린유통", Value = 4327500 },
                        new SlideChartBar { Label = "동부물산", Value = 3912000 },
                    },
                },
            };

            slide.Boxes.Add(Box("deck-s4-title", SlideBoxKind.Title, 22, true, "6월 거래처별 매출"));

            if (!edited)
            {
                slide.Boxes.Add(Box(
                    ClientBodyId,
                    SlideBoxKind.Body,
                    15,
                    false,
                    "6월 매출 합계 4,697만 원",
                    "한빛상사 1,482만 원으로 1위",
                    "서울오피스 1,136만 원으로 2위",
                    "대성문구 695만 원, 미래사무기기 561만 원",
                    "그린유통 433만 원, 동부물산 391만 원",
                    "한빛상사에 ‘(주)한빛상사’ 거래 포함"));
                return slide;
            }

            var body = Box(
                ClientBodyId,
                SlideBoxKind.Body,
                15,
                false,
                "6월 매출 합계 4,697만 원",
                "한빛상사 1,482만 원으로 1위",
                "상위 2곳이 전체의 56%");
            body.ChangeId = TextChangeId;
            slide.Boxes.Add(body);

            slide.Notes.Add("서울오피스 1,136만 원(2위), 대성문구 695만 원, 미래사무기기 561만 원, 그린유통 433만 원, 동부물산 391만 원. 한빛상사 금액에는 ‘(주)한빛상사’ 거래도 합쳐져 있어요.");
            slide.NotesChangeId = NotesChangeId;
            slide.FitChecked = true;
            return slide;
        }

        private static SlideModel SecondHalfPlan()
        {
            var slide = new SlideModel { Id = "deck-s5", Layout = SlideLayout.Content };
            slide.Boxes.Add(Box("deck-s5-title", SlideBoxKind.Title, 22, true, "하반기에 할 일"));
            slide.Boxes.Add(Box(
                "deck-s5-body",
                SlideBoxKind.Body,
                15,
                false,
                "거래처별 발주 일정 미리 맞추기",
                "한빛상사 이름 하나로 맞추기",
                "모자란 품목 먼저 채우기",
                "월말 정산을 매주 점검으로",
                "새 거래처 두 곳 더 찾기"));
            return slide;
        }

        private static SlideModel Closing()
        {
            var slide = new SlideModel { Id = "deck-s6", Layout = SlideLayout.Closing };
            var title = Box("deck-s6-title", SlideBoxKind.Title, 28, true, "감사합니다");
            title.Align = SlideTextAlign.Center;
            slide.Boxes.Add(title);
            return slide;
        }

        private static SlideBoxModel Box(string id, SlideBoxKind kind, int fontSize, bool bold, params string[] lines)
        {
            var box = new SlideBoxModel
            {
                Id = id,
                Kind = kind,
                FontSize = fontSize,
                Bold = bold,
            };

            box.Lines.AddRange(lines);
            return box;
        }
    }
}
