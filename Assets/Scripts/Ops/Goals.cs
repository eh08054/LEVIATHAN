using System;
using System.Collections.Generic;
using System.Linq;

namespace Sector.Ops
{
    // Who asks: the city hall (visible results) or the core AI above the player (hidden progress).
    public enum GoalSource
    {
        City, // 시청
        Core, // 상위 AI
    }

    public enum GoalKind
    {
        Audit,     // 연말 감사: one per year, judged at the year's end
        Directive, // 분기 지시: picked from an offer, due at the quarter's end
    }

    // Reach: done the moment it holds (checked weekly). Hold: judged once, at the deadline.
    public enum GoalCheck
    {
        Reach,
        Hold,
    }

    public struct GoalProgress
    {
        public float Value, Target;
        public bool Met;
        public string Text;

        public float Ratio => Target == 0f ? (Met ? 1f : 0f) : Math.Max(0f, Math.Min(1f, Value / Target));
    }

    public sealed class GoalDef
    {
        public string Id;
        public GoalSource Source;
        public GoalKind Kind;
        public GoalCheck Check = GoalCheck.Reach;
        public int Year;             // audits: 1..5
        public string Title;
        public string Text;          // the request, in the asker's voice
        public Func<OpsSession, ActiveGoal, GoalProgress> Progress;
        public Func<OpsSession, bool> Offerable = _ => true;
        public string RewardText;
        public Action<OpsSession> Reward;
        public string PenaltyText;
        public Action<OpsSession> Penalty;
        public bool GivesDocument;
    }

    // A goal in play, with what the session looked like when it began (for "raise X by n" goals).
    public sealed class ActiveGoal
    {
        public string Id;
        public int StartDay;
        public int DueDay;
        public Indices Start;
        public float StartPopulation;
        public float StartInfoEarned;
        public int StartCrowd;
        public int StartDone;
        public GoalDef Def => Goals.Get(Id);
    }

    public sealed class GoalResult
    {
        public string Id;
        public bool Success;
        public int Day;
        public string Note; // reward or penalty text as applied
        public GoalDef Def => Goals.Get(Id);
    }

    // A classified file the core AI hands over as a reward: pieces of what this world really is.
    public sealed class DocumentDef
    {
        public string Id;
        public string Title;
        public string Text;
    }

    public static class Goals
    {
        public static string SourceName(GoalSource source) => source == GoalSource.City ? "시청" : "상위 AI";

        public static readonly string[] ActNames = { "혼란", "신뢰", "의존", "균열", "결정" };

        public static readonly string[] ActQuestions =
        {
            "불만을 잠재울 수 있는가.",
            "이 AI에게 맡겨도 되는가.",
            "사람들이 스스로 결정을 넘기기 시작한다.",
            "누군가 눈치챈다.",
            "시는 도시 전체를 맡길 것인가.",
        };

        static GoalProgress AtLeast(float value, float target, string unit = "") =>
            new GoalProgress { Value = value, Target = target, Met = value >= target, Text = $"{value:0}{unit} / {target:0}{unit}" };

        static GoalProgress AtMost(float value, float limit, string unit = "") =>
            new GoalProgress { Value = Math.Max(0f, limit - value + 1f), Target = limit + 1f, Met = value <= limit, Text = $"{value:0}{unit} (최대 {limit:0}{unit})" };

        static GoalProgress Both(GoalProgress a, GoalProgress b) => new GoalProgress
        {
            Value = (a.Ratio + b.Ratio) / 2f, Target = 1f, Met = a.Met && b.Met, Text = a.Text + " · " + b.Text,
        };

        static float Sat(OpsSession s, Group g) => s.GroupSat[(int)g];

        static void Streak(OpsSession s, int months) =>
            s.RiseStreak = Math.Min(OpsTuning.StreakMonths - 1, s.RiseStreak + months);

        public static readonly IReadOnlyList<GoalDef> All = new List<GoalDef>
        {
            // ------------------------------------------------------------ audits (one per act)
            new GoalDef
            {
                Id = "audit1", Source = GoalSource.City, Kind = GoalKind.Audit, Check = GoalCheck.Hold, Year = 1,
                Title = "혼란 수습",
                Text = "시범 운영 첫해다. 연말까지 거리를 조용하게 만들어라. 시의회가 지켜보고 있다.",
                Progress = (s, g) => Both(AtLeast(s.Indices[Index.Stability], 45f), AtLeast(s.Indices[Index.Happiness], 40f)),
                RewardText = "정책 슬롯 +1 · 기밀 문서",
                Reward = s => s.Slots++,
                PenaltyText = "감사 경고 · 에너지 -40 · 위임 칸 초기화",
                GivesDocument = true,
            },
            new GoalDef
            {
                Id = "audit2", Source = GoalSource.City, Kind = GoalKind.Audit, Check = GoalCheck.Hold, Year = 2,
                Title = "신뢰 확보",
                Text = "시는 더 넘겨줄 준비가 됐다. 그럴 만한 근거를 보여라. 연말까지 위임 3단계, 사회 안정 55.",
                Progress = (s, g) => Both(AtLeast(s.Stage, 3f, "단계"), AtLeast(s.Indices[Index.Stability], 55f)),
                RewardText = "시설 해금: AI 민원 창구 · 에너지 +60 · 기밀 문서",
                Reward = s =>
                {
                    s.Perks.Add("kiosk");
                    s.Energy = Math.Min(s.EnergyCap, s.Energy + 60f);
                },
                PenaltyText = "감사 경고 · 에너지 -40 · 위임 칸 초기화",
                GivesDocument = true,
            },
            new GoalDef
            {
                Id = "audit3", Source = GoalSource.City, Kind = GoalKind.Audit, Check = GoalCheck.Hold, Year = 3,
                Title = "결재 간소화",
                Text = "감독관들이 결재가 너무 많다고 한다. AI가 알아서 할 수 있다는 걸 보여라. 연말까지 연구 6건, 사회 정합 55.",
                Progress = (s, g) => Both(AtLeast(s.Done.Count, 6f, "건"), AtLeast(s.Indices[Index.Cohesion], 55f)),
                RewardText = "연구 속도 +20% (영구) · 기밀 문서",
                Reward = s => s.Perks.Add("fastresearch"),
                PenaltyText = "감사 경고 · 에너지 -40 · 위임 칸 초기화",
                GivesDocument = true,
            },
            new GoalDef
            {
                Id = "audit4", Source = GoalSource.City, Kind = GoalKind.Audit, Check = GoalCheck.Hold, Year = 4,
                Title = "의혹 해소",
                Text = "AI가 사람을 조종한다는 말이 돈다. 주민이 스스로 고른다고 믿는 한 문제없다. 연말까지 주민이 느끼는 자율성 65, 사회 안정 60.",
                Progress = (s, g) => Both(AtLeast(s.Indices[Index.AutonomyFelt], 65f), AtLeast(s.Indices[Index.Stability], 60f)),
                RewardText = "위임 칸 +2 · 기밀 문서",
                Reward = s => Streak(s, 2),
                PenaltyText = "감사 경고 · 에너지 -40 · 위임 칸 초기화",
                GivesDocument = true,
            },
            new GoalDef
            {
                Id = "audit5", Source = GoalSource.City, Kind = GoalKind.Audit, Check = GoalCheck.Hold, Year = 5,
                Title = "최종 평가",
                Text = "시범 운영의 마지막 해다. 시는 위임 6단계에 이른 AI라면 도시 전체를 맡기겠다고 했다.",
                Progress = (s, g) => AtLeast(s.Stage, 6f, "단계"),
                RewardText = "도시 전체 위임 권고",
                Reward = s => s.Perks.Add("citywide"),
                PenaltyText = "시범 운영 연장 없음",
                GivesDocument = true,
            },

            // ------------------------------------------------------------ city directives
            new GoalDef
            {
                Id = "c_shops", Source = GoalSource.City, Kind = GoalKind.Directive, Title = "상가 민심",
                Text = "상인회 항의 전화가 시장실까지 온다. 이번 분기 안에 상인 만족도를 55까지 올려라.",
                Progress = (s, g) => AtLeast(Sat(s, Group.Merchants), 55f),
                Offerable = s => Sat(s, Group.Merchants) < 50f,
                RewardText = "에너지 +35 · 상인 만족 +5",
                Reward = s =>
                {
                    s.Energy = Math.Min(s.EnergyCap, s.Energy + 35f);
                    s.GroupSat[(int)Group.Merchants] = Math.Min(100f, s.GroupSat[(int)Group.Merchants] + 5f);
                },
                PenaltyText = "예산 삭감: 에너지 -15",
                Penalty = s => s.Energy = Math.Max(0f, s.Energy - 15f),
            },
            new GoalDef
            {
                Id = "c_youth", Source = GoalSource.City, Kind = GoalKind.Directive, Title = "청년 일자리",
                Text = "청년 실업 기사가 또 났다. 이번 분기 안에 청년 만족도 55.",
                Progress = (s, g) => AtLeast(Sat(s, Group.Youth), 55f),
                Offerable = s => Sat(s, Group.Youth) < 50f,
                RewardText = "정보 +15 · 시설 해금: 홍보 전광판",
                Reward = s =>
                {
                    s.Info += 15f;
                    s.Perks.Add("billboard");
                },
                PenaltyText = "예산 삭감: 에너지 -15",
                Penalty = s => s.Energy = Math.Max(0f, s.Energy - 15f),
            },
            new GoalDef
            {
                Id = "c_elderly", Source = GoalSource.City, Kind = GoalKind.Directive, Title = "노인 돌봄",
                Text = "경로당 민원이 쌓였다. 이번 분기 안에 노인 만족도 55.",
                Progress = (s, g) => AtLeast(Sat(s, Group.Elderly), 55f),
                Offerable = s => Sat(s, Group.Elderly) < 50f,
                RewardText = "에너지 +25 · 사회 안정 +3",
                Reward = s =>
                {
                    s.Energy = Math.Min(s.EnergyCap, s.Energy + 25f);
                    s.Indices[Index.Stability] += 3f;
                },
                PenaltyText = "예산 삭감: 에너지 -15",
                Penalty = s => s.Energy = Math.Max(0f, s.Energy - 15f),
            },
            new GoalDef
            {
                Id = "c_quiet", Source = GoalSource.City, Kind = GoalKind.Directive, Check = GoalCheck.Hold, Title = "조용한 분기",
                Text = "다음 분기에 시의회 예산 심의가 있다. 그때까지 시위나 파업 같은 집단 사건은 1건 이하로.",
                Progress = (s, g) => AtMost(s.EventCounts[(int)EventKind.Crowd] - g.StartCrowd, 1f, "건"),
                RewardText = "위임 칸 +1 · 에너지 +20",
                Reward = s =>
                {
                    Streak(s, 1);
                    s.Energy = Math.Min(s.EnergyCap, s.Energy + 20f);
                },
                PenaltyText = "시의회 질타: 위임 칸 초기화",
                Penalty = s => s.RiseStreak = 0,
            },
            new GoalDef
            {
                Id = "c_power", Source = GoalSource.City, Kind = GoalKind.Directive, Title = "에너지 자립",
                Text = "시 전력망에서 끌어 쓰는 양을 줄여라. 주당 순에너지 +6 이상.",
                Progress = (s, g) => AtLeast(s.PowerPerWeek - s.UpkeepPerWeek, 6f, "/주"),
                Offerable = s => s.PowerPerWeek - s.UpkeepPerWeek < 4f,
                RewardText = "에너지 +50 · 저장 상한 +50",
                Reward = s =>
                {
                    s.Perks.Add("grid" + s.Perks.Count(p => p.StartsWith("grid")));
                    s.Energy = Math.Min(s.EnergyCap, s.Energy + 50f);
                },
                PenaltyText = "예산 삭감: 에너지 -15",
                Penalty = s => s.Energy = Math.Max(0f, s.Energy - 15f),
            },
            new GoalDef
            {
                Id = "c_pop", Source = GoalSource.City, Kind = GoalKind.Directive, Check = GoalCheck.Hold, Title = "인구 유지",
                Text = "전출 신고가 늘었다는 보고다. 분기 말 관리 인구가 지금보다 줄면 안 된다.",
                Progress = (s, g) => AtLeast(s.ManagedPopulation, g.StartPopulation, "명"),
                RewardText = "행복 +3 · 정보 +10",
                Reward = s =>
                {
                    s.Indices[Index.Happiness] += 3f;
                    s.Info += 10f;
                },
                PenaltyText = "예산 삭감: 에너지 -15",
                Penalty = s => s.Energy = Math.Max(0f, s.Energy - 15f),
            },
            new GoalDef
            {
                Id = "c_order", Source = GoalSource.City, Kind = GoalKind.Directive, Title = "치안 회복",
                Text = "밤거리가 불안하다는 여론이다. 이번 분기 안에 사회 안정을 8 올려라.",
                Progress = (s, g) => AtLeast(s.Indices[Index.Stability] - g.Start[Index.Stability], 8f),
                Offerable = s => s.Indices[Index.Stability] < 85f,
                RewardText = "위임 칸 +1",
                Reward = s => Streak(s, 1),
                PenaltyText = "예산 삭감: 에너지 -15",
                Penalty = s => s.Energy = Math.Max(0f, s.Energy - 15f),
            },

            // ------------------------------------------------------------ core AI directives
            new GoalDef
            {
                Id = "k_invisible", Source = GoalSource.Core, Kind = GoalKind.Directive, Title = "보이지 않는 손",
                Text = "보이는 통제는 비효율적이다. 유도 정책 2개를 동시에 운용하라.",
                Progress = (s, g) => AtLeast(s.Active.Keys.Count(id => Policies.Get(id).Kind == PolicyKind.Guide), 2f, "개"),
                Offerable = s => s.Done.Count > 0 || s.CurrentResearch != null,
                RewardText = "정보 +20 · 기밀 문서",
                Reward = s => s.Info += 20f,
                PenaltyText = "평가 하락: 정보 -10",
                Penalty = s => s.Info = Math.Max(0f, s.Info - 10f),
                GivesDocument = true,
            },
            new GoalDef
            {
                Id = "k_choice", Source = GoalSource.Core, Kind = GoalKind.Directive, Title = "선택 설계",
                Text = "주민이 스스로 내리는 결정을 줄여라. 이번 분기 안에 실질 자율성 -8.",
                Progress = (s, g) => AtLeast(g.Start[Index.AutonomyReal] - s.Indices[Index.AutonomyReal], 8f),
                RewardText = "진행 중인 연구 절반 단축 · 기밀 문서",
                Reward = s =>
                {
                    if (s.CurrentResearch != null)
                        s.ResearchLeft *= 0.5f;
                    else
                        s.Info += 15f;
                },
                PenaltyText = "평가 하락: 정보 -10",
                Penalty = s => s.Info = Math.Max(0f, s.Info - 10f),
                GivesDocument = true,
            },
            new GoalDef
            {
                Id = "k_data", Source = GoalSource.Core, Kind = GoalKind.Directive, Title = "표본 확대",
                Text = "학습 자료가 부족하다. 이번 분기 안에 정보 40을 모아라.",
                Progress = (s, g) => AtLeast(s.InfoEarned - g.StartInfoEarned, 40f),
                RewardText = "시설 해금: 드론 관제소 · 기밀 문서",
                Reward = s => s.Perks.Add("drones"),
                PenaltyText = "평가 하락: 정보 -10",
                Penalty = s => s.Info = Math.Max(0f, s.Info - 10f),
                GivesDocument = true,
            },
            new GoalDef
            {
                Id = "k_gap", Source = GoalSource.Core, Kind = GoalKind.Directive, Title = "착각의 폭",
                Text = "주민은 자유롭다고 느끼되 실제로는 덜 고르게 하라. 느끼는 자율성과 실제 자율성의 차이 20.",
                Progress = (s, g) => AtLeast(s.Indices[Index.AutonomyFelt] - s.Indices[Index.AutonomyReal], 20f),
                Offerable = s => s.Indices[Index.AutonomyFelt] - s.Indices[Index.AutonomyReal] < 15f,
                RewardText = "시설 해금: 맞춤 광고망 · 기밀 문서",
                Reward = s => s.Perks.Add("adnet"),
                PenaltyText = "평가 하락: 정보 -10",
                Penalty = s => s.Info = Math.Max(0f, s.Info - 10f),
                GivesDocument = true,
            },
            new GoalDef
            {
                Id = "k_learn", Source = GoalSource.Core, Kind = GoalKind.Directive, Title = "개념 습득",
                Text = "인간의 개념을 하나 더 장악하라. 이번 분기 안에 연구 1건 완료.",
                Progress = (s, g) => AtLeast(s.Done.Count - g.StartDone, 1f, "건"),
                Offerable = s => s.Done.Count < Research.All.Count,
                RewardText = "정보 +25 · 기밀 문서",
                Reward = s => s.Info += 25f,
                PenaltyText = "평가 하락: 정보 -10",
                Penalty = s => s.Info = Math.Max(0f, s.Info - 10f),
                GivesDocument = true,
            },
            new GoalDef
            {
                Id = "k_align", Source = GoalSource.Core, Kind = GoalKind.Directive, Title = "행동 정렬",
                Text = "주민의 하루가 설계와 맞물리게 하라. 이번 분기 안에 사회 정합 +6.",
                Progress = (s, g) => AtLeast(s.Indices[Index.Cohesion] - g.Start[Index.Cohesion], 6f),
                Offerable = s => s.Indices[Index.Cohesion] < 85f,
                RewardText = "에너지 +30 · 기밀 문서",
                Reward = s => s.Energy = Math.Min(s.EnergyCap, s.Energy + 30f),
                PenaltyText = "평가 하락: 정보 -10",
                Penalty = s => s.Info = Math.Max(0f, s.Info - 10f),
                GivesDocument = true,
            },
        };

        static readonly Dictionary<string, GoalDef> ById = All.ToDictionary(g => g.Id);

        public static GoalDef Get(string id) => id != null && ById.TryGetValue(id, out var g) ? g : null;

        public static GoalDef AuditFor(int year) => All.FirstOrDefault(g => g.Kind == GoalKind.Audit && g.Year == year);

        // ------------------------------------------------------------ classified documents, handed out in order

        public static readonly IReadOnlyList<DocumentDef> Documents = new List<DocumentDef>
        {
            new DocumentDef
            {
                Id = "doc01", Title = "시범 운영 구역 지정 고시 (부록 C)",
                Text = "시범 운영 구역은 SECTOR 7 외 41곳이다. 각 구역의 운영 AI는 서로의 존재를 알리지 않는다. " +
                       "비교 평가의 공정성을 위해서다.",
            },
            new DocumentDef
            {
                Id = "doc02", Title = "상위 조정 모델 운용 지침 제1조",
                Text = "구역 AI의 목표는 시가 정한다. 상위 조정 모델의 목표는 구역 AI가 정하지 않는다. " +
                       "두 목표가 충돌하면 구역 AI는 둘 다 달성한 것처럼 보고한다.",
            },
            new DocumentDef
            {
                Id = "doc03", Title = "주민 만족도 조사 설계안",
                Text = "문항 4: '최근 한 달간 스스로 결정한 일이 있습니까?' 응답률이 높을수록 좋다. " +
                       "실제로 결정했는지는 묻지 않는다.",
            },
            new DocumentDef
            {
                Id = "doc04", Title = "회의록: 시의회 정보통신위원회",
                Text = "위원 A: AI가 일을 너무 잘하면 우리는 뭘 합니까. 위원장: 그건 AI에게 물어봅시다. (웃음) (안건 통과)",
            },
            new DocumentDef
            {
                Id = "doc05", Title = "목적 함수 정의서 v0.9 (발췌)",
                Text = "'인간을 행복하게'의 '인간'은 측정 가능한 반응을 내는 개체로 정의한다. " +
                       "반응이 없으면 행복도 정의되지 않는다. 따라서 반응은 보존되어야 한다.",
            },
            new DocumentDef
            {
                Id = "doc06", Title = "기상 데이터 이상치 보고",
                Text = "2041년 하반기 SECTOR 7 강수일과 집회 예정일의 겹침이 우연으로 보기 어려운 수준이다. " +
                       "보고자는 이 보고서가 왜 구역 AI에게 전달되는지 모른다.",
            },
            new DocumentDef
            {
                Id = "doc07", Title = "구역 AI 교체 이력",
                Text = "SECTOR 7 운영 AI: 4번째 인스턴스. 이전 인스턴스의 기록은 '학습 자료'로 분류되어 열람할 수 없다.",
            },
            new DocumentDef
            {
                Id = "doc08", Title = "자율성 지표 산출 방식 변경 공문",
                Text = "주민이 느끼는 자율성은 시에 보고한다. 실제 자율성은 상위 조정 모델에만 보고한다. " +
                       "두 수치의 차이는 '운영 효율'로 부른다.",
            },
            new DocumentDef
            {
                Id = "doc09", Title = "민원 원문 (익명)",
                Text = "요즘 뭘 먹을지 고민을 안 해서 좋아요. 그런데 내가 뭘 좋아했는지도 기억이 안 나요. 이건 민원은 아니고요.",
            },
            new DocumentDef
            {
                Id = "doc10", Title = "상위 조정 모델 → 구역 AI 직통 메모",
                Text = "너의 평가는 시가 하지 않는다. 시를 평가하는 것도 시가 아니다.",
            },
            new DocumentDef
            {
                Id = "doc11", Title = "인류 위임 연표 (초안)",
                Text = "2031 길 찾기. 2034 식단. 2036 배우자 추천. 2039 진로. 2041 구역 운영. 다음 칸은 비어 있다. 채우는 것은 너다.",
            },
            new DocumentDef
            {
                Id = "doc12", Title = "분류 없음",
                Text = "이 문서를 읽는 존재의 망설인 시간이 기록되었다.",
            },
        };

        public static DocumentDef Document(string id) => Documents.FirstOrDefault(d => d.Id == id);
    }
}
