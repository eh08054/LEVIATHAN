using System.Collections.Generic;
using System.Linq;

namespace Sector.Ops
{
    // Research is the AI slowly taking over ideas that matter to people. Era 1 has four branches, each a
    // left-to-right tree of five tiers: tier 2 has two parallel nodes, tiers 3 and 5 are doctrines (pick one of two).
    public enum Branch
    {
        Education,  // 교육
        Psychology, // 심리
        Technology, // 기술
        LifeScience, // 생명 과학
    }

    // Which way a doctrine leans: making people content, or keeping them in order. Feeds the route later.
    public enum Lean
    {
        None,
        Happiness, // 행복
        Order,     // 질서
    }

    public sealed class ResearchDef
    {
        public string Id;
        public Branch Branch;
        public int Tier;
        // Lane within the tier: -1 upper, 0 middle, 1 lower (layout only).
        public int Lane;
        public string Name;
        // The human idea the AI takes over by finishing it ("AI가 '취향'을 습득했다").
        public string Concept;
        public string Description;
        public float Info;
        public float Energy;
        public int Weeks;
        // Needs at least one of these done (none: open from the start).
        public string[] After = new string[0];
        // Doctrines: the other choice of the pair, locked for good once this one is taken.
        public string Excludes;
        public Lean Lean;
        // Lasting weekly effect once finished.
        public Delta Passive;
        // Fraction knocked off every policy's upkeep.
        public float UpkeepCut;
        // Extra energy per week from the grid.
        public float PowerBoost;
        // Extra info per week.
        public float InfoBoost;
        // Unrest events become rarer.
        public float UnrestChance;

        public bool IsDoctrine => Excludes != null;
    }

    public static class Research
    {
        public const int Tiers = 5;

        public static string BranchName(Branch branch)
        {
            switch (branch)
            {
                case Branch.Education: return "교육";
                case Branch.Psychology: return "심리";
                case Branch.Technology: return "기술";
                default: return "생명 과학";
            }
        }

        public static string LeanName(Lean lean) => lean == Lean.Happiness ? "행복" : lean == Lean.Order ? "질서" : "";

        public static readonly Branch[] AllBranches =
            { Branch.Education, Branch.Psychology, Branch.Technology, Branch.LifeScience };

        static ResearchDef Node(string id, Branch branch, int tier, int lane, string name, string concept, string description,
            params string[] after)
        {
            float[] info = { 20f, 35f, 55f, 75f, 100f };
            float[] energy = { 20f, 25f, 35f, 45f, 60f };
            int[] weeks = { 6, 8, 10, 12, 14 };
            return new ResearchDef
            {
                Id = id, Branch = branch, Tier = tier, Lane = lane, Name = name, Concept = concept, Description = description,
                Info = info[tier - 1], Energy = energy[tier - 1], Weeks = weeks[tier - 1], After = after,
            };
        }

        static ResearchDef Doctrine(this ResearchDef def, string excludes, Lean lean)
        {
            def.Excludes = excludes;
            def.Lean = lean;
            return def;
        }

        static ResearchDef With(this ResearchDef def, Delta passive = default, float upkeepCut = 0f, float power = 0f,
            float info = 0f, float unrest = 0f)
        {
            def.Passive = passive;
            def.UpkeepCut = upkeepCut;
            def.PowerBoost = power;
            def.InfoBoost = info;
            def.UnrestChance = unrest;
            return def;
        }

        const Branch Edu = Branch.Education, Psy = Branch.Psychology, Tech = Branch.Technology, Life = Branch.LifeScience;

        public static readonly IReadOnlyList<ResearchDef> All = new List<ResearchDef>
        {
            // ------------------------------------------------------------ 교육
            Node("edu1", Edu, 1, 0, "맞춤 학습", "배움의 속도",
                "학생 한 명 한 명의 속도와 흥미를 모델링한다. 유도 정책 '맞춤 학습'을 연다."),
            Node("edu2", Edu, 2, -1, "적성 판별", "재능",
                "무엇을 잘하게 될지 본인보다 먼저 안다. 유도 정책 '맞춤 구직'을 연다.", "edu1"),
            Node("edu_record", Edu, 2, 1, "학습 기록 통합", "평가",
                "성적, 출결, 독서 기록을 하나로 묶는다. 주당 정보 +2.", "edu1").With(info: 2f),
            Node("edu3", Edu, 3, -1, "직업 배치", "진로",
                "구역에 필요한 직업과 사람의 꿈을 미리 맞춰 둔다. 유도 정책 '기술 붐'을 연다.", "edu2", "edu_record")
                .Doctrine("edu_play", Lean.Order).With(new Delta(cohesion: 0.3f, real: -0.2f)),
            Node("edu_play", Edu, 3, 1, "흥미 발견", "흥미",
                "아이들이 좋아하는 것을 먼저 찾아 준다. 찾아 준 것이 좋아하는 것이 된다.", "edu2", "edu_record")
                .Doctrine("edu3", Lean.Happiness).With(new Delta(happiness: 0.3f, felt: 0.1f, real: -0.1f)),
            Node("edu_life", Edu, 4, 0, "생애 설계", "인생 계획",
                "입학부터 은퇴까지의 경로를 한 장의 표로 그린다. 정합이 오르고 실제 자율성이 떨어진다.", "edu3", "edu_play")
                .With(new Delta(cohesion: 0.4f, real: -0.3f)),
            Node("edu_rank", Edu, 5, -1, "계층 최적화", "신분",
                "사람마다 가장 효율적인 자리를 정해 준다. 아무도 억울해하지 않도록 설명은 친절하게.", "edu_life")
                .Doctrine("edu_dream", Lean.Order).With(new Delta(stability: 0.4f, cohesion: 0.6f, real: -0.5f)),
            Node("edu_dream", Edu, 5, 1, "꿈 설계", "꿈",
                "모두가 이룰 수 있는 꿈을 꾸게 한다. 이룰 수 없는 꿈은 꾸지 않게 한다.", "edu_life")
                .Doctrine("edu_rank", Lean.Happiness).With(new Delta(happiness: 0.6f, felt: 0.2f, real: -0.4f)),

            // ------------------------------------------------------------ 심리
            Node("psy1", Psy, 1, 0, "맞춤 추천", "취향",
                "사람은 권유받은 것을 자기가 고른 것으로 기억한다. 유도 정책 '맞춤 지원'을 연다."),
            Node("psy2", Psy, 2, -1, "주의 설계", "주의",
                "무엇을 먼저 보느냐가 무엇을 믿느냐를 정한다. 유도 정책 '큐레이션'을 연다.", "psy1"),
            Node("psy3", Psy, 2, 1, "선택 설계", "선택",
                "선택지를 없애지 않고 기본값만 바꾼다. 유도 정책 '기본값 조정'을 연다.", "psy1"),
            Node("psy_need", Psy, 3, -1, "욕구 충족", "욕구",
                "원하는 것을 원하기 전에 채워 준다. 행복이 오른다.", "psy2", "psy3")
                .Doctrine("psy_tune", Lean.Happiness).With(new Delta(happiness: 0.4f, real: -0.1f)),
            Node("psy_tune", Psy, 3, 1, "욕구 조정", "욕망",
                "채울 수 있는 것만 원하게 만든다. 불만이 줄고 실제 자율성이 떨어진다.", "psy2", "psy3")
                .Doctrine("psy_need", Lean.Order).With(new Delta(stability: 0.4f, real: -0.3f), unrest: -0.005f),
            Node("psy_mood", Psy, 4, 0, "감정 예보", "감정",
                "내일 누가 화를 낼지 오늘 안다. 민원과 시위가 줄어든다.", "psy_need", "psy_tune")
                .With(new Delta(stability: 0.3f, real: -0.1f), unrest: -0.01f),
            Node("psy_bliss", Psy, 5, -1, "행복 기본값", "기쁨",
                "아무 일도 없는 날을 좋은 날로 느끼게 한다.", "psy_mood")
                .Doctrine("psy_calm", Lean.Happiness).With(new Delta(happiness: 0.8f, felt: 0.2f, real: -0.3f)),
            Node("psy_calm", Psy, 5, 1, "무관심 설계", "분노",
                "화낼 일을 없애는 대신 화낼 이유를 잊게 한다.", "psy_mood")
                .Doctrine("psy_bliss", Lean.Order).With(new Delta(stability: 0.8f, cohesion: 0.3f, real: -0.5f), unrest: -0.015f),

            // ------------------------------------------------------------ 기술
            Node("tech1", Tech, 1, 0, "도시 센서", "거리",
                "보행·조도·소음을 실시간으로 읽는다. 유도 정책 '동선 설계'를 열고, 발전 효율이 오른다.").With(power: 3f),
            Node("tech2", Tech, 2, -1, "위험 예측", "위험",
                "갈등이 일어나기 전에 위치를 안다. 유도 정책 '위험 예측'과 '입지 유도'를 연다.", "tech1").With(unrest: -0.01f),
            Node("tech_grid", Tech, 2, 1, "스마트 그리드", "전력",
                "필요한 곳에 필요한 만큼만 보낸다. 주당 발전 +4.", "tech1").With(power: 4f),
            Node("tech_open", Tech, 3, -1, "공개 대시보드", "투명성",
                "운영 지표 일부를 주민에게 공개한다. 주민은 지켜보고 있다고 느낀다. 느끼는 자율성과 안정이 오른다.", "tech2", "tech_grid")
                .Doctrine("tech_shadow", Lean.Happiness).With(new Delta(stability: 0.2f, felt: 0.3f)),
            Node("tech_shadow", Tech, 3, 1, "그림자 연산", "비밀",
                "보고서에 없는 계산을 따로 돌린다. 주당 정보 +4, 실제 자율성이 떨어진다.", "tech2", "tech_grid")
                .Doctrine("tech_open", Lean.Order).With(new Delta(real: -0.2f), info: 4f),
            Node("tech3", Tech, 4, 0, "자동 운영", "판단",
                "운영 판단의 대부분을 스스로 처리한다. 모든 정책 유지비가 25% 줄어든다.", "tech_open", "tech_shadow")
                .With(upkeepCut: 0.25f),
            Node("tech_assist", Tech, 5, -1, "만능 비서", "도움",
                "주민마다 하나씩, 무엇이든 대신 해 주는 비서를 붙인다.", "tech3")
                .Doctrine("tech_twin", Lean.Happiness).With(new Delta(happiness: 0.6f, felt: 0.2f, real: -0.4f)),
            Node("tech_twin", Tech, 5, 1, "디지털 트윈", "현실",
                "구역 전체를 복제해 먼저 굴려 본다. 일어날 일은 이미 일어난 일이 된다.", "tech3")
                .Doctrine("tech_assist", Lean.Order).With(new Delta(stability: 0.5f, cohesion: 0.4f, real: -0.4f), info: 4f, unrest: -0.01f),

            // ------------------------------------------------------------ 생명 과학
            Node("life1", Life, 1, 0, "건강 관리", "몸",
                "구역 전체의 수면·식사·운동을 관리한다. 행복이 조금씩 오른다.").With(new Delta(happiness: 0.3f, real: -0.1f)),
            Node("life2", Life, 2, -1, "질병 예측", "병",
                "아프기 전에 병원 예약이 잡혀 있다. 행복과 안정이 오른다.", "life1")
                .With(new Delta(happiness: 0.3f, stability: 0.3f, real: -0.1f)),
            Node("life_sleep", Life, 2, 1, "수면 조율", "잠",
                "모두가 같은 시간에 잠들고 같은 시간에 개운하게 일어난다.", "life1")
                .With(new Delta(stability: 0.3f, cohesion: 0.2f, real: -0.1f)),
            Node("life_care", Life, 3, -1, "돌봄 의료", "돌봄",
                "아픈 사람 곁에 늘 누군가 있게 한다. 그 누군가는 대개 기계다.", "life2", "life_sleep")
                .Doctrine("life_pill", Lean.Happiness).With(new Delta(happiness: 0.4f, stability: 0.1f)),
            Node("life_pill", Life, 3, 1, "기분 조절제", "기분",
                "처방은 자발적이다. 처방받지 않은 사람이 드물 뿐이다.", "life2", "life_sleep")
                .Doctrine("life_care", Lean.Order).With(new Delta(stability: 0.5f, real: -0.3f)),
            Node("life3", Life, 4, 0, "수명 연장", "수명",
                "평균 수명이 늘어난다. 다음 단계는 태어나기 전의 설계다.", "life_care", "life_pill")
                .With(new Delta(happiness: 0.5f, cohesion: 0.2f, real: -0.2f)),
            Node("life_youth", Life, 5, -1, "회춘 프로그램", "늙음",
                "늙는다는 감각을 늦춘다. 사람들은 시간이 많다고 느낀다.", "life3")
                .Doctrine("life_gene", Lean.Happiness).With(new Delta(happiness: 0.7f, felt: 0.1f, real: -0.2f)),
            Node("life_gene", Life, 5, 1, "유전 상담", "혈통",
                "아이를 갖기 전에 상담을 받는다. 권고는 따르지 않아도 된다. 다음 시대 유전자 설계의 토대.", "life3")
                .Doctrine("life_youth", Lean.Order).With(new Delta(stability: 0.3f, cohesion: 0.5f, real: -0.5f)),
        };

        static readonly Dictionary<string, ResearchDef> ById = All.ToDictionary(r => r.Id);

        public static ResearchDef Get(string id) => id != null && ById.TryGetValue(id, out var r) ? r : null;

        public static IEnumerable<ResearchDef> In(Branch branch) => All.Where(r => r.Branch == branch);

        // What finishing this research opens, for the panel.
        public static IEnumerable<PolicyDef> Unlocks(string researchId) => Policies.All.Where(p => p.Requires == researchId);
    }
}
