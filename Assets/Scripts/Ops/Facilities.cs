using System.Collections.Generic;
using System.Linq;

namespace Sector.Ops
{
    // What the AI builds itself with energy. (Construction policies move what people build.)
    public sealed class FacilityDef
    {
        public string Id;
        public string Name;
        public string Description;
        public float Cost;
        public float Upkeep;
        public float Power;      // energy per week
        public float Storage;    // extra energy cap
        public float Info;       // info per week
        public int Samples;      // extra observed residents
        public Delta Weekly;
        // Unlocked by a goal reward (OpsSession.Perks), or null when always available.
        public string Perk;
    }

    public static class Facilities
    {
        public static readonly IReadOnlyList<FacilityDef> All = new List<FacilityDef>
        {
            new FacilityDef
            {
                Id = "plant", Name = "발전소", Cost = 60f, Power = 6f,
                Description = "구역 전력을 만든다. 주당 에너지 +6.",
            },
            new FacilityDef
            {
                Id = "storage", Name = "저장소", Cost = 30f, Storage = 100f,
                Description = "에너지를 더 모아 둘 수 있다. 상한 +100.",
            },
            new FacilityDef
            {
                Id = "sensors", Name = "센서망", Cost = 30f, Upkeep = 2f, Info = 3f, Samples = 2,
                Description = "거리의 움직임을 읽는다. 주당 정보 +3, 관찰 표본 주민 +2.",
            },
            new FacilityDef
            {
                Id = "park", Name = "공원", Cost = 30f, Upkeep = 1f, Weekly = new Delta(happiness: 0.3f, stability: 0.2f),
                Description = "앉을 곳과 나무. 사람들이 조금 느긋해진다.",
            },
            new FacilityDef
            {
                Id = "counsel", Name = "상담소", Cost = 40f, Upkeep = 2f, Weekly = new Delta(happiness: 0.2f, stability: 0.4f, felt: -0.1f),
                Description = "불만을 들어주는 곳. 들은 내용은 기록된다.",
            },
            new FacilityDef
            {
                Id = "learning_center", Name = "학습 센터", Cost = 35f, Upkeep = 1f,
                Weekly = new Delta(happiness: 0.2f, cohesion: 0.3f, real: -0.1f),
                Description = "방과 후 아이들이 모이는 곳. 무엇을 배울지는 센터가 먼저 안다.",
            },
            new FacilityDef
            {
                Id = "care", Name = "돌봄 센터", Cost = 40f, Upkeep = 2f,
                Weekly = new Delta(happiness: 0.3f, stability: 0.2f),
                Description = "노인과 아픈 사람을 돌본다. 돌봄 기록은 생명 과학 연구의 자료가 된다.",
            },
            new FacilityDef
            {
                Id = "billboard", Name = "홍보 전광판", Cost = 35f, Upkeep = 1f, Perk = "billboard",
                Weekly = new Delta(happiness: 0.1f, cohesion: 0.3f, felt: 0.1f),
                Description = "구역 소식과 오늘의 추천을 띄운다. 사람들은 자기가 고른 소식이라고 생각한다.",
            },
            new FacilityDef
            {
                Id = "kiosk", Name = "AI 민원 창구", Cost = 45f, Upkeep = 2f, Info = 1f, Perk = "kiosk",
                Weekly = new Delta(stability: 0.5f, felt: 0.1f),
                Description = "24시간 민원을 받는다. 답은 늘 친절하고, 결론은 늘 같다.",
            },
            new FacilityDef
            {
                Id = "drones", Name = "드론 관제소", Cost = 50f, Upkeep = 3f, Samples = 3, Info = 2f, Perk = "drones",
                Weekly = new Delta(stability: 0.4f, felt: -0.2f),
                Description = "하늘에서 거리를 본다. 관찰 표본 +3, 주당 정보 +2.",
            },
            new FacilityDef
            {
                Id = "adnet", Name = "맞춤 광고망", Cost = 40f, Upkeep = 2f, Perk = "adnet",
                Weekly = new Delta(happiness: 0.3f, cohesion: 0.2f, real: -0.4f),
                Description = "사람마다 다른 광고를 보여 준다. 다들 원래 그걸 원했다고 기억한다.",
            },
        };

        static readonly Dictionary<string, FacilityDef> ById = All.ToDictionary(f => f.Id);

        public static FacilityDef Get(string id) => ById.TryGetValue(id, out var f) ? f : null;
    }
}
