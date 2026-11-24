using System;
using System.Collections.Generic;
using System.Linq;

namespace Sector.Ops
{
    // A building that can stand on a district's build grid (the district screen, 2026-11-20 trial: residential only).
    public sealed class BuildingDef
    {
        public string Id;
        public string Name;
        public string Desc;
        // What a resident says when it opens (shown as a speech line on the district screen).
        public string Say;
        public int W = 1, H = 1;
        public float Cost;          // budget
        public int Weeks;           // construction time
        public float Housing;       // people it can house
        public float Income;        // budget per week
        public float Comfort;       // satisfaction it gives homes within Reach tiles
        public int Reach;
        public float Noise;         // tension it adds to homes right next to it
        public int PowerReach;      // a substation powers buildings this many tiles away
        public float Watch;         // calms the district each week, at the cost of felt autonomy
        public float Info;          // info per week
        public float Height = 1f;   // drawing only
        public bool Existing;       // the old housing the district starts with; cannot be built again
        public bool NeedsPower = true;  // parks work without the grid
        public bool IsHome => Housing > 0f || Existing;
    }

    // One building on a grid. Done once Day >= DoneDay.
    public sealed class Placed
    {
        public string Id;
        public int X, Y;
        public int DoneDay;
        public BuildingDef Def => Buildings.Get(Id);
        public bool Covers(int x, int y) => x >= X && y >= Y && x < X + Def.W && y < Y + Def.H;

        // Tiles between the two footprints (0 when touching or overlapping).
        public int Gap(Placed other)
        {
            int dx = Math.Max(0, Math.Max(other.X - (X + Def.W - 1), X - (other.X + other.Def.W - 1)) - 1);
            int dy = Math.Max(0, Math.Max(other.Y - (Y + Def.H - 1), Y - (other.Y + other.Def.H - 1)) - 1);
            return Math.Max(dx, dy);
        }
    }

    // A district's build grid: fixed roads, the buildings on it, people moved out by demolition.
    public sealed class DistrictPlan
    {
        public const int Size = 32;
        public int Zone;
        public readonly bool[] Road = new bool[Size * Size];
        public readonly List<Placed> Items = new List<Placed>();
        // Homes the district started with (old housing blocks): its base population lives in them.
        public int OldHomes;
        // People who lost their homes to demolition and have not settled yet (fades week by week).
        public float Displaced;

        public bool IsRoad(int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size && Road[y * Size + x];

        public Placed At(int x, int y) => Items.FirstOrDefault(p => p.Covers(x, y));
    }

    public static class Buildings
    {
        public static readonly IReadOnlyList<BuildingDef> All = new List<BuildingDef>
        {
            new BuildingDef
            {
                Id = "old_homes", Name = "기존 아파트", W = 2, H = 2, Height = 2.4f, Existing = true,
                Desc = "시범 지구 지정 전부터 있던 아파트 단지. 헐면 주민이 이주해야 한다.",
            },
            new BuildingDef
            {
                Id = "apartment", Name = "신축 아파트", W = 2, H = 2, Height = 3.4f, Cost = 60f, Weeks = 3, Housing = 450f,
                Desc = "450명이 살 수 있다. 과밀을 풀고 인구가 늘 여지를 만든다.",
                Say = "새 아파트 입주 공고 봤어? 이번엔 우리도 넣어 보자.",
            },
            new BuildingDef
            {
                Id = "park", Name = "공원", NeedsPower = false, W = 2, H = 2, Cost = 25f, Weeks = 2, Comfort = 3f, Reach = 3, Height = 0.2f,
                Desc = "2×2 블록 공원. 주변 3칸 안의 집이 만족한다.",
                Say = "집 앞에 공원이 생겼네. 저녁에 좀 걸어야겠다.",
            },
            new BuildingDef
            {
                Id = "shop", Name = "상점가", Cost = 25f, Weeks = 2, Income = 4f, Noise = 0.5f, Height = 1f,
                Desc = "매주 예산 +4. 바로 옆 집은 시끄러워한다.",
                Say = "가게가 생긴 건 좋은데, 밤늦게까지 시끄럽네.",
            },
            new BuildingDef
            {
                Id = "substation", Name = "변전소", Cost = 30f, Weeks = 2, PowerReach = 4, Noise = 0.3f, Height = 1.2f,
                Desc = "4칸 안의 새 건물에 전력을 댄다. 전력이 없는 새 건물은 절반만 일한다.",
                Say = "변전소 웅웅거리는 소리, 너만 들려?",
            },
            new BuildingDef
            {
                Id = "tower", Name = "감시탑", Cost = 20f, Weeks = 1, Watch = 0.8f, Height = 3f,
                Desc = "지구 긴장을 매주 낮춘다. 대신 자율성 인지가 조금씩 떨어진다.",
                Say = "저 탑 생기고 나서 골목이 조용해졌어. 너무 조용해.",
            },
            new BuildingDef
            {
                Id = "center", Name = "주민 센터", W = 2, H = 1, Cost = 35f, Weeks = 2, Comfort = 2f, Reach = 3, Info = 1.5f, Height = 1.3f,
                Desc = "주변 3칸 안의 집이 만족하고, 주민 이야기가 정보로 들어온다(매주 +1.5).",
                Say = "주민 센터에서 상담해 준대. AI가 받는다던데.",
            },
        };

        public static readonly IReadOnlyList<BuildingDef> Buildable = All.Where(b => !b.Existing).ToList();

        static readonly Dictionary<string, BuildingDef> ById = All.ToDictionary(b => b.Id);

        public static BuildingDef Get(string id) => id != null && ById.TryGetValue(id, out var def) ? def : null;
    }

    public static class DistrictLayouts
    {
        // Districts that have a build grid so far.
        public static readonly int[] Zones = { 3 };

        public static readonly int[] Streets = { 7, 15, 23 };

        public static DistrictPlan For(int zone)
        {
            var plan = new DistrictPlan { Zone = zone };
            // Streets every eighth row and column cut the district into 16 blocks of about 7 x 7 lots; the edges are
            // the boulevards around it (not on the grid).
            foreach (int line in Streets)
                for (int i = 0; i < DistrictPlan.Size; i++)
                {
                    plan.Road[line * DistrictPlan.Size + i] = true;
                    plan.Road[i * DistrictPlan.Size + line] = true;
                }
            void Put(string id, int x, int y) => plan.Items.Add(new Placed { Id = id, X = x, Y = y, DoneDay = 0 });
            // The old housing estate fills the north-west blocks and thins out to the east and south; the rest of the
            // district is empty land waiting for development.
            foreach (var (x, y) in new[]
                     {
                         (1, 1), (4, 1), (1, 4), (4, 4),      // block 0,0: full
                         (9, 1), (12, 1), (9, 4),             // block 1,0
                         (1, 9), (4, 9), (1, 12),             // block 0,1
                         (17, 1), (20, 1),                    // block 2,0
                         (9, 9),                              // block 1,1
                     })
                Put("old_homes", x, y);
            Put("substation", 12, 5);
            Put("substation", 20, 5);
            plan.OldHomes = plan.Items.Count(p => p.Id == "old_homes");
            return plan;
        }
    }
}
