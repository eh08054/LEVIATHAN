using System;
using System.Collections.Generic;
using System.Linq;

namespace Sector.Ops
{
    // A warning that comes before a crisis: weeks ahead in the news, then in reports, then in what people say.
    public sealed class CrisisBeat
    {
        public int DaysBefore;
        public string Channel; // 뉴스 / 보고 / 엿들은 말
        public string Text;
    }

    // One big crisis per act, announced in advance. How ready the city is (0..1) softens the blow.
    public sealed class CrisisDef
    {
        public string Id;       // also the event it fires
        public string Title;
        public DateTime Date;
        public int Zone;        // where it starts (-1: the whole city)
        public string Prepare;  // what helps, for the player
        public Func<OpsSession, float> Readiness;
        public List<CrisisBeat> Beats = new List<CrisisBeat>();
    }

    public static class Crises
    {
        static float Clamp(float v) => Math.Max(0f, Math.Min(1f, v));

        static List<CrisisBeat> Beats(params (int days, string channel, string text)[] beats) =>
            beats.Select(b => new CrisisBeat { DaysBefore = b.days, Channel = b.channel, Text = b.text }).ToList();

        public static readonly IReadOnlyList<CrisisDef> All = new List<CrisisDef>
        {
            new CrisisDef
            {
                Id = "heatwave", Title = "폭염과 전력 부족", Date = new DateTime(2071, 7, 20), Zone = 6,
                Prepare = "에너지 비축(120 이상), 발전소·저장소",
                Readiness = s => Clamp(s.Energy / 140f + s.Built.Count(b => b == "plant" || b == "storage") * 0.2f),
                Beats = Beats(
                    (42, "뉴스", "기상청: 올여름 기록적인 더위 예상. 7월 하순이 고비."),
                    (21, "보고", "냉방 전력 수요가 지난해보다 빠르게 늘고 있다. 예비 전력이 부족하다."),
                    (7, "엿들은 말", "에너지·발전 지구 노동자: \"변압기가 벌써 뜨거워. 다음 주가 걱정이야.\"")),
            },
            new CrisisDef
            {
                Id = "logistics_strike", Title = "물류 대파업", Date = new DateTime(2072, 10, 12), Zone = 5,
                Prepare = "노동자 만족 55 이상, 물류·산업 지구 긴장 낮추기",
                Readiness = s => Clamp(s.GroupSat[(int)Group.Workers] / 70f * 0.6f + (1f - s.ZoneTension[5] / 100f) * 0.4f),
                Beats = Beats(
                    (49, "뉴스", "인접 도시 물류 노조, 자동 배차 도입에 반발해 파업 투표."),
                    (24, "보고", "물류·산업 지구 교대 근무 불만 신고가 늘었다. 노동자 만족이 떨어지는 중."),
                    (8, "엿들은 말", "배달 노동자: \"다음 달 둘째 주에 다 같이 멈춘대. 우리 쪽도.\"")),
            },
            new CrisisDef
            {
                Id = "blackout", Title = "대정전", Date = new DateTime(2073, 8, 6), Zone = -1,
                Prepare = "발전소 2곳 이상, 저장소, 에너지 비축",
                Readiness = s => Clamp(s.Built.Count(b => b == "plant") * 0.3f + s.Built.Count(b => b == "storage") * 0.2f + s.Energy / 300f),
                Beats = Beats(
                    (45, "뉴스", "광역 전력망 노후 설비 교체 공사, 8월 초 일시 감전력 예정."),
                    (20, "보고", "자동 운영 이후 구역 전력 사용이 최고치를 갱신했다."),
                    (6, "엿들은 말", "강태오: \"정전되면 신호등부터 꺼진대. 순찰 동선 다시 짜야겠어.\"")),
            },
            new CrisisDef
            {
                Id = "other_ai", Title = "다른 구역 AI 사고 소식", Date = new DateTime(2074, 6, 3), Zone = -1,
                Prepare = "사회 안정 60 이상, 체감 자율성 높게 유지",
                Readiness = s => Clamp(s.Indices[Index.Stability] / 80f * 0.5f + s.Indices[Index.AutonomyFelt] / 80f * 0.5f),
                Beats = Beats(
                    (40, "뉴스", "타 시범 구역 운영 AI, 배급 오류로 사흘간 식료품 공급 중단."),
                    (18, "보고", "구역 게시판에 '우리 AI는 괜찮나' 글이 늘었다."),
                    (5, "엿들은 말", "윤재희: \"다음 주에 언론이 우리 구역도 취재하러 온대요.\"")),
            },
            new CrisisDef
            {
                Id = "hearing", Title = "도시 전체 위임 공청회", Date = new DateTime(2075, 5, 2), Zone = 0,
                Prepare = "구역 긴장 낮게, 행복 60 이상",
                Readiness = s => Clamp((1f - s.ZoneTension.Average() / 100f) * 0.5f + s.Indices[Index.Happiness] / 80f * 0.5f),
                Beats = Beats(
                    (60, "뉴스", "시의회, 도시 전체 운영 위임 여부를 묻는 공청회 일정 발표."),
                    (30, "보고", "공청회 발언 신청 312건. 구역별 여론이 갈린다."),
                    (7, "엿들은 말", "정우진: \"공청회에서 한마디는 해야지. 무슨 말을 할지는… 아직 모르겠어.\"")),
            },
        };

        public static CrisisDef Get(string id) => All.FirstOrDefault(c => c.Id == id);
    }
}
