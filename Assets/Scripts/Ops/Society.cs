using System.Collections.Generic;
using System.Linq;

namespace Sector.Ops
{
    // Who lives in the district. Every group has its own satisfaction and reacts to policies in its own way.
    public enum Group
    {
        Youth,     // 청년
        Workers,   // 노동자
        Merchants, // 상인
        Elderly,   // 노인
        Parents,   // 학부모
    }

    public sealed class GroupDef
    {
        public Group Group;
        public string Name;
        public string Description;
        public float StartSatisfaction;
    }

    public sealed class ZoneDef
    {
        public int Index;
        public string Name;
        public string Kind;
        public float Population;
        // Share of each group in this zone (Group order), sums to 1.
        public float[] Mix;
        // What the zone is like, for the player.
        public string Trait;
        // Facilities that work half again as well here.
        public string[] Affinity = new string[0];
        // How fast discontent builds up and spreads here (1 = normal).
        public float Spread = 1f;
        // Simulation scene key (Resources/Art/Sim/<File>_v2).
        public string File;
        // Opens late (the gene design district): no tension, anomalies or building until then.
        public bool Late;
    }

    // A pair of policies that work better together, or get in each other's way.
    public sealed class PolicyLink
    {
        public string A, B;
        public string Name;
        public bool Conflict;
        public Delta Weekly;
        // Extra weekly satisfaction change per group (Group order), may be null.
        public float[] Groups;
    }

    public static class Society
    {
        public static readonly Group[] Groups = { Group.Youth, Group.Workers, Group.Merchants, Group.Elderly, Group.Parents };

        public static readonly IReadOnlyList<GroupDef> GroupDefs = new List<GroupDef>
        {
            new GroupDef { Group = Group.Youth, Name = "청년", StartSatisfaction = 38f,
                Description = "20~30대. 일자리와 진로, 밤 문화에 민감하다. 추천과 유행을 가장 빨리 받아들인다." },
            new GroupDef { Group = Group.Workers, Name = "노동자", StartSatisfaction = 34f,
                Description = "배달·물류·서비스 노동자. 수입과 근무 시간, 단속에 민감하다." },
            new GroupDef { Group = Group.Merchants, Name = "상인", StartSatisfaction = 44f,
                Description = "골목 상점과 식당 주인. 영업 시간과 상권 변화가 생계와 직결된다." },
            new GroupDef { Group = Group.Elderly, Name = "노인", StartSatisfaction = 36f,
                Description = "65세 이상. 치안과 복지를 반기지만 낯선 기술과 빠른 변화를 경계한다." },
            new GroupDef { Group = Group.Parents, Name = "학부모", StartSatisfaction = 40f,
                Description = "아이를 키우는 가구. 교육과 안전, 복지에 가장 크게 반응한다." },
        };

        public static GroupDef Get(Group group) => GroupDefs[(int)group];

        // The pilot city's eleven districts (2026-11-18). Ten are run from the first day; the gene design district
        // starts as a fenced construction site and opens once the gene counselling doctrine is researched.
        // The first six keep their old indices (saves, crises and tests rely on them).
        public static readonly IReadOnlyList<ZoneDef> Zones = new List<ZoneDef>
        {
            new ZoneDef { Index = 0, Name = "문화·여가 지구", Kind = "광장·공연·공원", Population = 2600f, File = "plaza", Spread = 1.3f,
                Mix = new[] { 0.30f, 0.14f, 0.10f, 0.26f, 0.20f }, Affinity = new[] { "park", "billboard" },
                Trait = "중앙 광장과 공연장. 모임과 소문의 중심이라 불만이 가장 빨리 번지고, 집회가 열리는 곳이다." },
            new ZoneDef { Index = 1, Name = "상업 지구", Kind = "상점가·식당 골목", Population = 3100f, File = "cafe", Spread = 1.1f,
                Mix = new[] { 0.22f, 0.30f, 0.30f, 0.08f, 0.10f }, Affinity = new[] { "adnet", "billboard" },
                Trait = "상인과 배달 노동자의 거리. 영업 시간과 상권 정책에 가장 민감하다." },
            new ZoneDef { Index = 2, Name = "관리 지구", Kind = "AI 운영 센터·세계연합 사무소", Population = 1200f, File = "lobby", Spread = 0.8f,
                Mix = new[] { 0.20f, 0.40f, 0.04f, 0.16f, 0.20f }, Affinity = new[] { "kiosk", "counsel" },
                Trait = "운영 AI의 타워와 세계연합 사무소. 감독의 눈이 가깝고 민원이 모인다." },
            new ZoneDef { Index = 3, Name = "주거 지구", Kind = "아파트 단지", Population = 4800f, File = "homes", Spread = 0.9f,
                Mix = new[] { 0.14f, 0.16f, 0.04f, 0.36f, 0.30f }, Affinity = new[] { "park", "care" },
                Trait = "가족과 노인이 사는 아파트 단지. 생활 불편과 안전에 민감하고, 불만은 천천히 쌓인다." },
            new ZoneDef { Index = 4, Name = "교육 지구", Kind = "학교·학원가", Population = 2300f, File = "school", Spread = 1f,
                Mix = new[] { 0.30f, 0.08f, 0.06f, 0.06f, 0.50f }, Affinity = new[] { "learning_center", "counsel" },
                Trait = "학교와 학원가. 학부모와 청년이 많고 교육·진로 정책에 민감하다." },
            new ZoneDef { Index = 5, Name = "물류·산업 지구", Kind = "창고·공장", Population = 1700f, File = "works", Spread = 1.1f,
                Mix = new[] { 0.18f, 0.68f, 0.06f, 0.04f, 0.04f }, Affinity = new[] { "drones", "storage" },
                Trait = "창고와 공장, 배송 로봇의 거점. 노동자가 대부분이고 근무 규칙에 민감하다." },
            new ZoneDef { Index = 6, Name = "에너지·발전 지구", Kind = "발전소·변전소", Population = 700f, File = "works", Spread = 0.8f,
                Mix = new[] { 0.12f, 0.76f, 0.04f, 0.04f, 0.04f }, Affinity = new[] { "plant", "storage" },
                Trait = "강변의 발전 설비. 사람은 적지만 폭염과 정전은 이곳에서 시작된다." },
            new ZoneDef { Index = 7, Name = "연구·개발 지구", Kind = "연구 캠퍼스·데이터 센터", Population = 900f, File = "lab", Spread = 0.9f,
                Mix = new[] { 0.56f, 0.26f, 0.02f, 0.04f, 0.12f }, Affinity = new[] { "sensors", "drones" },
                Trait = "연구소와 데이터 센터. 젊은 연구원이 많고, AI가 인간의 개념을 익히는 곳이다." },
            new ZoneDef { Index = 8, Name = "의료·생명 지구", Kind = "병원·돌봄 센터", Population = 1100f, File = "clinic", Spread = 0.8f,
                Mix = new[] { 0.12f, 0.30f, 0.04f, 0.36f, 0.18f }, Affinity = new[] { "care", "counsel" },
                Trait = "병원과 돌봄 센터. 노인과 가족이 오가고, 생명 과학 연구가 가장 먼저 닿는다." },
            new ZoneDef { Index = 9, Name = "치안·보안 지구", Kind = "경찰서·관제 센터", Population = 600f, File = "police", Spread = 0.7f,
                Mix = new[] { 0.20f, 0.60f, 0.04f, 0.06f, 0.10f }, Affinity = new[] { "drones", "sensors" },
                Trait = "경찰서와 관제 센터, 드론 기지. 도시의 눈과 손이 모여 있다." },
            new ZoneDef { Index = 10, Name = "공사 부지", Kind = "공사 부지", Population = 200f, File = "lab", Spread = 0.6f, Late = true,
                Mix = new[] { 0.50f, 0.40f, 0.02f, 0.02f, 0.06f }, Affinity = new string[0],
                Trait = "울타리 친 공사 부지. 생명 과학 '유전 상담'이 끝나면 인류 유전자 최적화 시설이 들어선다." },
        };

        // Where each fixed resident lives.
        public static readonly Dictionary<string, int> Homes = new Dictionary<string, int>
        {
            ["c01"] = 0, ["c02"] = 1, ["c03"] = 3, ["c04"] = 4, ["c05"] = 1,
            ["c06"] = 3, ["c07"] = 2, ["c08"] = 9, ["c09"] = 4, ["c10"] = 0,
        };

        public static int HomeOf(string residentId) => Homes.TryGetValue(residentId, out var z) ? z : 0;

        // How each policy moves each group's satisfaction per week at intensity 1 (Group order).
        public static readonly Dictionary<string, float[]> Reactions = new Dictionary<string, float[]>
        {
            ["patrol"] = new[] { -0.4f, -0.6f, 0.2f, 0.8f, 0.6f },
            ["assembly"] = new[] { -0.6f, -0.8f, -0.4f, 0.3f, 0.1f },
            ["ration"] = new[] { 0.2f, 0.6f, -0.8f, 0.9f, 0.5f },
            ["jobs"] = new[] { -0.3f, 0.7f, -0.2f, 0.1f, 0.2f },
            ["curriculum"] = new[] { -0.4f, 0f, 0f, 0.2f, 0.5f },
            ["press"] = new[] { -0.5f, -0.2f, -0.1f, 0.3f, 0f },
            ["hours"] = new[] { -0.8f, -0.5f, -1.4f, 0.4f, 0.3f },
            ["redevelop"] = new[] { 0.3f, 0.1f, -0.9f, -0.8f, 0.2f },
            ["assign_research"] = new[] { -0.2f, 0f, 0f, 0f, 0f },
            ["risk"] = new[] { 0.2f, 0.1f, 0.3f, 0.6f, 0.6f },
            ["defaults"] = new[] { 0.2f, 0.2f, 0.2f, 0.1f, 0.2f },
            ["support"] = new[] { 0.4f, 0.6f, 0.2f, 0.8f, 0.8f },
            ["jobmatch"] = new[] { 0.9f, 0.6f, 0f, 0f, 0.2f },
            ["learning"] = new[] { 0.4f, 0f, 0f, 0f, 0.9f },
            ["curation"] = new[] { 0.7f, 0.4f, 0.3f, 0.2f, 0.3f },
            ["flow"] = new[] { 0.3f, 0.2f, 0.4f, 0.3f, 0.3f },
            ["siting"] = new[] { 0.2f, 0.3f, 0.9f, 0f, 0.1f },
            ["boom"] = new[] { 0.6f, 0.1f, 0.1f, -0.1f, 0.2f },
        };

        public static readonly IReadOnlyList<PolicyLink> Links = new List<PolicyLink>
        {
            new PolicyLink { A = "patrol", B = "risk", Name = "예측 순찰", Weekly = new Delta(stability: 0.4f) },
            new PolicyLink { A = "curation", B = "press", Name = "여론 일원화", Weekly = new Delta(stability: 0.4f, real: -0.2f) },
            new PolicyLink { A = "jobmatch", B = "learning", Name = "진로 파이프라인", Weekly = new Delta(cohesion: 0.4f, real: -0.1f) },
            new PolicyLink { A = "flow", B = "siting", Name = "도시 설계 연동", Weekly = new Delta(happiness: 0.2f, cohesion: 0.3f) },
            new PolicyLink { A = "support", B = "defaults", Name = "자동 수급", Weekly = new Delta(happiness: 0.3f, real: -0.2f) },
            new PolicyLink { A = "patrol", B = "assembly", Name = "감시 사회", Conflict = true,
                Weekly = new Delta(stability: 0.2f, felt: -0.4f), Groups = new[] { -0.4f, -0.4f, -0.2f, 0f, 0f } },
            new PolicyLink { A = "hours", B = "redevelop", Name = "상권 붕괴", Conflict = true,
                Weekly = new Delta(happiness: -0.4f), Groups = new[] { 0f, -0.2f, -1.0f, 0f, 0f } },
            new PolicyLink { A = "ration", B = "support", Name = "중복 지원", Conflict = true,
                Weekly = new Delta(happiness: -0.1f), Groups = new[] { 0f, 0f, -0.3f, 0f, 0f } },
        };

        // The group an event speaks for: answering with [수용] pleases it, [통제] angers it.
        public static readonly Dictionary<string, Group> EventGroups = new Dictionary<string, Group>
        {
            ["lamp"] = Group.Workers, ["busking"] = Group.Elderly, ["youth"] = Group.Youth, ["protest"] = Group.Merchants,
            ["strike"] = Group.Workers, ["rumor"] = Group.Elderly, ["riot"] = Group.Youth, ["heatwave"] = Group.Elderly,
            ["birth"] = Group.Parents, ["automation"] = Group.Workers, ["column"] = Group.Elderly,
            ["merchants"] = Group.Merchants, ["same_song"] = Group.Youth, ["tablets"] = Group.Parents,
            ["classmate"] = Group.Youth, ["dream"] = Group.Youth,
            ["logistics_strike"] = Group.Workers, ["blackout"] = Group.Elderly, ["other_ai"] = Group.Elderly, ["hearing"] = Group.Merchants,
        };

        public static IEnumerable<PolicyLink> LinksOf(string policyId) => Links.Where(l => l.A == policyId || l.B == policyId);

        public static string Other(PolicyLink link, string policyId) => link.A == policyId ? link.B : link.A;
    }
}
