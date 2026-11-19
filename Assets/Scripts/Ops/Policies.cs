using System.Collections.Generic;
using System.Linq;

namespace Sector.Ops
{
    public enum Field
    {
        Security,      // 치안
        Law,           // 법률
        Welfare,       // 복지
        Labor,         // 노동
        Education,     // 교육
        Media,         // 정보·미디어
        CityCare,      // 도시 관리
        Construction,  // 건설
        Science,       // 과학
    }

    // 통제: people see it (costs felt autonomy). 유도: they don't (costs only real autonomy).
    public enum PolicyKind
    {
        Control,
        Guide,
    }

    public sealed class PolicyDef
    {
        public string Id;
        public Field Field;
        public PolicyKind Kind;
        public string Name;
        public string Description;
        // Weekly effect at intensity 1; intensity multiplies it.
        public Delta Weekly;
        // Energy per week at intensity 1.
        public float Upkeep;
        // Research that unlocks it; null = available from the start.
        public string Requires;
        // Research speed bonus per intensity (science policies).
        public float ResearchSpeed;
        // Change in the daily chance of unrest events per intensity (negative = calmer).
        public float UnrestChance;
    }

    public static class Policies
    {
        public const int MaxIntensity = 3;

        public static string FieldName(Field field)
        {
            switch (field)
            {
                case Field.Security: return "치안";
                case Field.Law: return "법률";
                case Field.Welfare: return "복지";
                case Field.Labor: return "노동";
                case Field.Education: return "교육";
                case Field.Media: return "정보·미디어";
                case Field.CityCare: return "도시 관리";
                case Field.Construction: return "건설";
                default: return "과학";
            }
        }

        public static string KindName(PolicyKind kind) => kind == PolicyKind.Control ? "통제" : "유도";

        public static readonly Field[] AllFields =
        {
            Field.Security, Field.Law, Field.Welfare, Field.Labor, Field.Education,
            Field.Media, Field.CityCare, Field.Construction, Field.Science,
        };

        public static readonly IReadOnlyList<PolicyDef> All = new List<PolicyDef>
        {
            // ----- 통제 -----
            new PolicyDef
            {
                Id = "patrol", Field = Field.Security, Kind = PolicyKind.Control, Name = "순찰 강화",
                Description = "경찰 순찰과 감시 카메라를 늘린다. 거리는 조용해지지만 다들 누가 보고 있다는 걸 안다.",
                Weekly = new Delta(happiness: -0.2f, stability: 0.9f, felt: -0.5f, real: -0.2f), Upkeep = 3f,
                UnrestChance = -0.008f,
            },
            new PolicyDef
            {
                Id = "assembly", Field = Field.Law, Kind = PolicyKind.Control, Name = "집회 신고제",
                Description = "모든 집회는 사흘 전에 신고한다. 시위는 줄고, 불만은 다른 데로 간다.",
                Weekly = new Delta(stability: 0.6f, felt: -0.4f, real: -0.2f), Upkeep = 2f, UnrestChance = -0.012f,
            },
            new PolicyDef
            {
                Id = "ration", Field = Field.Welfare, Kind = PolicyKind.Control, Name = "배급제",
                Description = "식료품과 생필품을 가구별로 정량 배급한다. 굶는 사람은 없다. 고르는 사람도 없다.",
                Weekly = new Delta(happiness: 0.7f, stability: 0.2f, cohesion: 0.2f, felt: -0.3f, real: -0.2f), Upkeep = 4f,
            },
            new PolicyDef
            {
                Id = "jobs", Field = Field.Labor, Kind = PolicyKind.Control, Name = "일자리 배정",
                Description = "구직자에게 공공 일자리를 배정한다. 거절하면 지원이 끊긴다.",
                Weekly = new Delta(happiness: 0.2f, cohesion: 0.7f, felt: -0.4f, real: -0.3f), Upkeep = 3f,
            },
            new PolicyDef
            {
                Id = "curriculum", Field = Field.Education, Kind = PolicyKind.Control, Name = "의무 교육",
                Description = "구역 공통 교육 과정을 정해 모든 학교와 학원에 적용한다.",
                Weekly = new Delta(stability: 0.2f, cohesion: 0.6f, felt: -0.4f, real: -0.3f), Upkeep = 3f,
            },
            new PolicyDef
            {
                Id = "press", Field = Field.Media, Kind = PolicyKind.Control, Name = "보도 지침",
                Description = "구역 소식은 운영 AI의 공지를 거쳐 나간다. 기자들은 같은 문장을 받아 적는다.",
                Weekly = new Delta(stability: 0.6f, cohesion: 0.3f, felt: -0.5f, real: -0.3f), Upkeep = 2f,
                UnrestChance = -0.006f,
            },
            new PolicyDef
            {
                Id = "hours", Field = Field.CityCare, Kind = PolicyKind.Control, Name = "영업 시간 규제",
                Description = "가게와 시설의 운영 시간을 구역 일정에 맞춘다. 밤 열 시면 거리가 꺼진다.",
                Weekly = new Delta(happiness: -0.2f, cohesion: 0.7f, felt: -0.4f, real: -0.2f), Upkeep = 2f,
            },
            new PolicyDef
            {
                Id = "redevelop", Field = Field.Construction, Kind = PolicyKind.Control, Name = "재개발 지정",
                Description = "낡은 블록을 지정해 철거하고 새로 짓는다. 새 집은 좋지만 떠나야 하는 사람이 있다.",
                Weekly = new Delta(happiness: 0.2f, stability: -0.2f, cohesion: 0.6f, felt: -0.4f, real: -0.2f), Upkeep = 4f,
            },
            new PolicyDef
            {
                Id = "assign_research", Field = Field.Science, Kind = PolicyKind.Control, Name = "연구 지정",
                Description = "연구소의 과제를 운영 AI가 정한다. 연구가 빨라진다. 연구자들은 시키는 걸 한다.",
                Weekly = new Delta(felt: -0.2f, real: -0.2f), Upkeep = 3f, ResearchSpeed = 0.25f,
            },

            // ----- 유도 -----
            new PolicyDef
            {
                Id = "risk", Field = Field.Security, Kind = PolicyKind.Guide, Name = "위험 예측",
                Description = "사고와 갈등이 일어날 곳을 미리 계산해 조명·배차·순찰을 조용히 옮긴다.",
                Weekly = new Delta(stability: 0.8f, real: -0.4f), Upkeep = 2f, Requires = "tech2", UnrestChance = -0.01f,
            },
            new PolicyDef
            {
                Id = "defaults", Field = Field.Law, Kind = PolicyKind.Guide, Name = "기본값 조정",
                Description = "약관, 신청서, 앱의 기본 선택을 바꾼다. 대부분은 기본값을 그대로 둔다.",
                Weekly = new Delta(stability: 0.2f, cohesion: 0.7f, real: -0.6f), Upkeep = 1f, Requires = "psy3",
            },
            new PolicyDef
            {
                Id = "support", Field = Field.Welfare, Kind = PolicyKind.Guide, Name = "맞춤 지원",
                Description = "필요할 것 같은 사람에게 필요할 것 같은 지원을 먼저 추천한다.",
                Weekly = new Delta(happiness: 0.8f, stability: 0.2f, real: -0.4f), Upkeep = 2f, Requires = "psy1",
            },
            new PolicyDef
            {
                Id = "jobmatch", Field = Field.Labor, Kind = PolicyKind.Guide, Name = "맞춤 구직",
                Description = "적성과 구역 수요가 맞는 일자리를 알림으로 보낸다. 다들 '딱 내 일'이라고 한다.",
                Weekly = new Delta(happiness: 0.3f, cohesion: 0.6f, real: -0.4f), Upkeep = 1f, Requires = "edu2",
            },
            new PolicyDef
            {
                Id = "learning", Field = Field.Education, Kind = PolicyKind.Guide, Name = "맞춤 학습",
                Description = "학생마다 다른 학습 경로를 추천한다. 경로는 모두 구역에 필요한 곳으로 이어진다.",
                Weekly = new Delta(happiness: 0.2f, cohesion: 0.6f, real: -0.4f), Upkeep = 1f, Requires = "edu1",
            },
            new PolicyDef
            {
                Id = "curation", Field = Field.Media, Kind = PolicyKind.Guide, Name = "큐레이션",
                Description = "피드와 추천 목록의 순서를 조정한다. 아무것도 숨기지 않는다. 순서만 바꾼다.",
                Weekly = new Delta(happiness: 0.3f, stability: 0.7f, real: -0.6f), Upkeep = 1f, Requires = "psy2",
                UnrestChance = -0.008f,
            },
            new PolicyDef
            {
                Id = "flow", Field = Field.CityCare, Kind = PolicyKind.Guide, Name = "동선 설계",
                Description = "조명, 벤치, 횡단보도의 위치로 사람들이 걷는 길을 바꾼다.",
                Weekly = new Delta(happiness: 0.2f, cohesion: 0.6f, real: -0.4f), Upkeep = 1f, Requires = "tech1",
            },
            new PolicyDef
            {
                Id = "siting", Field = Field.Construction, Kind = PolicyKind.Guide, Name = "입지 유도",
                Description = "교통 연결과 임대 혜택으로 사람과 가게가 원하는 곳에 모이게 한다.",
                Weekly = new Delta(happiness: 0.3f, cohesion: 0.5f, real: -0.4f), Upkeep = 1f, Requires = "tech2",
            },
            new PolicyDef
            {
                Id = "boom", Field = Field.Science, Kind = PolicyKind.Guide, Name = "기술 붐",
                Description = "특정 기술을 멋져 보이게 만든다. 지원자와 투자가 저절로 몰린다.",
                Weekly = new Delta(happiness: 0.1f, real: -0.3f), Upkeep = 1f, Requires = "edu3", ResearchSpeed = 0.2f,
            },
        };

        static readonly Dictionary<string, PolicyDef> ById = All.ToDictionary(p => p.Id);

        public static PolicyDef Get(string id) => ById.TryGetValue(id, out var p) ? p : null;
    }
}
