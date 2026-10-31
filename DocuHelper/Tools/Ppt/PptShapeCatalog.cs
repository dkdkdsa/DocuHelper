using System;
using System.Collections.Generic;
using System.Linq;

namespace DocuHelper.Tools.Ppt
{
    public static class PptShapeCatalog
    {
        public const double LineLengthCm = 5;

        public const double TextBoxWidthCm = 8;

        private static readonly PptShapeSpec[] Specs =
        {
            new("textbox", "rect", "텍스트 상자", PptShapeForm.TextBox, TextBoxWidthCm, 1.2),
            new("rect", "rect", "직사각형", PptShapeForm.Auto, 4, 3),
            new("round_rect", "roundRect", "사각형: 둥근 모서리", PptShapeForm.Auto, 4, 3),
            new("ellipse", "ellipse", "타원", PptShapeForm.Auto, 4, 3),
            new("triangle", "triangle", "이등변 삼각형", PptShapeForm.Auto, 4, 3),
            new("right_triangle", "rtTriangle", "직각 삼각형", PptShapeForm.Auto, 4, 3),
            new("diamond", "diamond", "다이아몬드", PptShapeForm.Auto, 4, 3),
            new("pentagon", "pentagon", "오각형", PptShapeForm.Auto, 3, 3),
            new("hexagon", "hexagon", "육각형", PptShapeForm.Auto, 4, 3),
            new("parallelogram", "parallelogram", "평행 사변형", PptShapeForm.Auto, 4, 3),
            new("trapezoid", "trapezoid", "사다리꼴", PptShapeForm.Auto, 4, 3),
            new("cross", "plus", "십자형", PptShapeForm.Auto, 3, 3),
            new("right_arrow", "rightArrow", "화살표: 오른쪽", PptShapeForm.Auto, 4, 3),
            new("left_arrow", "leftArrow", "화살표: 왼쪽", PptShapeForm.Auto, 4, 3),
            new("up_arrow", "upArrow", "화살표: 위쪽", PptShapeForm.Auto, 3, 4),
            new("down_arrow", "downArrow", "화살표: 아래쪽", PptShapeForm.Auto, 3, 4),
            new("left_right_arrow", "leftRightArrow", "화살표: 좌우", PptShapeForm.Auto, 4, 3),
            new("chevron", "chevron", "화살표: 갈매기형 수장", PptShapeForm.Auto, 4, 3),
            new("home_plate", "homePlate", "화살표: 오각형", PptShapeForm.Auto, 4, 3),
            new("star5", "star5", "별: 꼭짓점 5개", PptShapeForm.Auto, 3, 3),
            new("star6", "star6", "별: 꼭짓점 6개", PptShapeForm.Auto, 3, 3),
            new("wedge_rect_callout", "wedgeRectCallout", "설명선: 사각형", PptShapeForm.Auto, 4, 3),
            new("wedge_round_callout", "wedgeRoundRectCallout", "설명선: 둥근 사각형", PptShapeForm.Auto, 4, 3),
            new("wedge_ellipse_callout", "wedgeEllipseCallout", "설명선: 타원형", PptShapeForm.Auto, 4, 3),
            new("line", "line", "직선 연결선", PptShapeForm.Connector, LineLengthCm, 0),
            new("arrow_line", "straightConnector1", "직선 화살표 연결선", PptShapeForm.Connector, LineLengthCm, 0, tailEnd: true),
            new("double_arrow_line", "straightConnector1", "직선 화살표 연결선", PptShapeForm.Connector, LineLengthCm, 0, headEnd: true, tailEnd: true),
            new("elbow_arrow", "bentConnector3", "연결선: 꺾임", PptShapeForm.Connector, LineLengthCm, 2, tailEnd: true),
            new("curved_arrow", "curvedConnector3", "연결선: 구부러진", PptShapeForm.Connector, LineLengthCm, 2, tailEnd: true),
            new("flow_process", "flowChartProcess", "순서도: 처리", PptShapeForm.Auto, 4, 3),
            new("flow_decision", "flowChartDecision", "순서도: 판단", PptShapeForm.Auto, 4, 3),
            new("flow_terminator", "flowChartTerminator", "순서도: 터미네이터", PptShapeForm.Auto, 4, 3),
            new("flow_document", "flowChartDocument", "순서도: 문서", PptShapeForm.Auto, 4, 3),
            new("heart", "heart", "하트", PptShapeForm.Auto, 3, 3),
            new("sun", "sun", "해", PptShapeForm.Auto, 3, 3),
            new("moon", "moon", "달", PptShapeForm.Auto, 3, 3),
            new("cloud", "cloud", "구름", PptShapeForm.Auto, 4, 3),
            new("donut", "donut", "도넛", PptShapeForm.Auto, 3, 3),
            new("left_brace", "leftBrace", "중괄호 열기", PptShapeForm.Auto, 1.2, 4),
            new("right_brace", "rightBrace", "중괄호 닫기", PptShapeForm.Auto, 1.2, 4),
            new("left_bracket", "leftBracket", "대괄호 열기", PptShapeForm.Auto, 1.2, 4),
            new("right_bracket", "rightBracket", "대괄호 닫기", PptShapeForm.Auto, 1.2, 4),
        };

        public static IReadOnlyList<PptShapeSpec> All => Specs;

        public static string Tokens => string.Join(", ", Specs.Select(s => s.Token));

        public static PptShapeSpec Find(string token)
        {
            var key = PptInputs.Normalize(token);
            var found = Specs.FirstOrDefault(s => string.Equals(s.Token, key, StringComparison.Ordinal));
            return found ?? throw new ToolInputException($"'shape_type' 값을 알 수 없어요. {Tokens} 중 하나로 알려 주세요.");
        }
    }
}
