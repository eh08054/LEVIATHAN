using System.Collections.Generic;
using System.Linq;

namespace Sector.Ops
{
    // A fixed character: one of the ten observed residents with a face, a story and opinions on policy.
    public sealed class ResidentDef
    {
        public string Id;
        public string Name;
        public int Age;
        public string Job;
        public string Theme;
        public string Bio;
        public bool Female;
        // Policies that lift or sink this person's mood while active.
        public string[] Likes = new string[0];
        public string[] Dislikes = new string[0];
        // What they say when a given policy is on.
        public Dictionary<string, string> Reactions = new Dictionary<string, string>();
        public string Low;
        public string High;
    }

    public static class Residents
    {
        public static readonly IReadOnlyList<ResidentDef> Cast = new List<ResidentDef>
        {
            new ResidentDef
            {
                Id = "c01", Name = "김도윤", Age = 32, Job = "조경사", Theme = "쓸모",
                Bio = "구역의 가로수와 화단을 돌본다. 일이 자동화될지 모른다는 소문에 요즘 물을 더 자주 준다.",
                Likes = new[] { "jobmatch" }, Dislikes = new[] { "flow" },
                Reactions = new Dictionary<string, string>
                {
                    ["jobs"] = "배정받은 일이요? 화분… 물주기 감독이래요. 감독할 화분이 열두 개예요.",
                    ["jobmatch"] = "추천받은 일이 이상하게 딱 맞아요. 제가 원래 이걸 좋아했나?",
                    ["flow"] = "제가 심은 벤치 자리가 밤사이 옮겨져 있었어요.",
                },
                Low = "제 일이 정말 필요한 일인가요?", High = "요즘은 손이 덜 가요. 그래서 좋은 거겠죠?",
            },
            new ResidentDef
            {
                Id = "c02", Name = "이서아", Age = 27, Job = "배달 노동자", Theme = "만족", Female = true,
                Bio = "하루 마흔 건을 배달하고, 밤에는 구역 민원 게시판에 글을 쓴다. 민원 게시판의 단골.",
                Likes = new[] { "support", "ration", "curation" }, Dislikes = new[] { "hours", "patrol" },
                Reactions = new Dictionary<string, string>
                {
                    ["hours"] = "열 시에 가게가 다 닫으면 야간 배달은 어떡하라고요.",
                    ["support"] = "필요한 게 생기기 전에 쿠폰이 와요. 무섭다기보단… 편해요.",
                    ["curation"] = "요즘 피드가 왜 이렇게 마음에 들죠? 민원 쓸 시간이 없어요.",
                    ["patrol"] = "신호 기다리는데 경찰이 세 번 지나갔어요.",
                },
                Low = "오늘도 민원 하나 남깁니다.", High = "민원 게시판? 요즘 안 들어가요.",
            },
            new ResidentDef
            {
                Id = "c03", Name = "박만수", Age = 68, Job = "전 종군 기자", Theme = "의심",
                Bio = "40년 동안 동네 소식을 썼다. AI가 오기 전의 구역을 기억하는 몇 안 되는 사람.",
                Likes = new string[0], Dislikes = new[] { "press", "curation", "defaults" },
                Reactions = new Dictionary<string, string>
                {
                    ["press"] = "기사가 다 같은 문장으로 시작해. 우리 땐 데스크가 하나였어도 이러진 않았지.",
                    ["curation"] = "아무도 안 숨겼다고? 그럼 왜 다들 같은 걸 보고 있나.",
                    ["defaults"] = "동의서 기본 체크가 바뀌었더군. 아무도 모르는 것 같아.",
                },
                Low = "다들 너무 쉽게 납득해.", High = "편하긴 하군. 그게 마음에 걸려.",
            },
            new ResidentDef
            {
                Id = "c04", Name = "최하늘", Age = 24, Job = "구직자", Theme = "이 꿈은 내 것인가", Female = true,
                Bio = "졸업 후 2년째 진로를 못 정했다. 하고 싶은 게 너무 많거나, 하나도 없다.",
                Likes = new[] { "learning", "jobmatch", "boom" }, Dislikes = new[] { "jobs", "curriculum" },
                Reactions = new Dictionary<string, string>
                {
                    ["boom"] = "요즘 다들 그 기술 얘기만 해요. 저도 갑자기 그게 하고 싶어졌어요.",
                    ["jobs"] = "배정이라뇨. 저 아직 아무것도 안 정했는데요.",
                    ["learning"] = "추천 강의를 다 들었더니 제 꿈이 생겼어요. 신기하죠?",
                },
                Low = "뭘 하고 싶은지 모르겠어요.", High = "드디어 하고 싶은 게 생겼어요. 제가 고른 거예요.",
            },
            new ResidentDef
            {
                Id = "c05", Name = "정우진", Age = 41, Job = "상인회장", Theme = "저항",
                Bio = "골목 식당 사장이자 상인회장. 구역 운영 AI에 반대하는 서명을 처음 받은 사람.",
                Likes = new[] { "siting" }, Dislikes = new[] { "hours", "redevelop", "assembly" },
                Reactions = new Dictionary<string, string>
                {
                    ["hours"] = "열 시 마감이면 우리 집 매출 절반이 날아가요.",
                    ["redevelop"] = "재개발 지정 공문이 왔어요. 우리 골목이 '낡은 블록'이래요.",
                    ["siting"] = "새 역 앞에 자리가 났길래 옮겼어요. 손님이 두 배예요. 운이 좋았죠.",
                    ["assembly"] = "모이는 것도 사흘 전에 허락받아야 합니까?",
                },
                Low = "상인회 다시 모입니다.", High = "AI가 장사엔 도움이 되네요. 인정합니다.",
            },
            new ResidentDef
            {
                Id = "c06", Name = "한소미", Age = 35, Job = "예비 엄마", Theme = "다음 세대", Female = true,
                Bio = "첫아이를 기다린다. 아이가 자랄 구역이 어떤 곳이 될지 매일 생각한다.",
                Likes = new[] { "support", "learning" }, Dislikes = new[] { "ration" },
                Reactions = new Dictionary<string, string>
                {
                    ["support"] = "육아용품 추천이 와요. 아직 성별도 말 안 했는데요.",
                    ["learning"] = "아이 학습 경로를 미리 볼 수 있대요. 태어나기도 전인데.",
                    ["ration"] = "배급 분유 브랜드는 고를 수 없나요?",
                },
                Low = "이 아이가 자랄 곳이 여기라니.", High = "여기라면 아이가 안전할 것 같아요.",
            },
            new ResidentDef
            {
                Id = "c07", Name = "윤재희", Age = 45, Job = "세계연합 감독관", Theme = "위임", Female = true,
                Bio = "AI 시범 운영을 감독하러 세계연합에서 왔다. 모든 결정에 사람의 결재가 필요하다고 믿는다. 아직은.",
                Likes = new[] { "press" }, Dislikes = new string[0],
                Reactions = new Dictionary<string, string>
                {
                    ["press"] = "공지 문안은 제가 결재합니다. 고칠 데가 없긴 하지만요.",
                },
                Low = "이대로면 시범 운영 중단을 건의할 수밖에 없어요.",
                High = "결재할 게 줄었어요. 제가 할 일도요.",
            },
            new ResidentDef
            {
                Id = "c08", Name = "강태오", Age = 38, Job = "경찰", Theme = "집행",
                Bio = "이 구역에서 자랐고 이 구역을 지킨다. 시위대 절반이 아는 얼굴이다.",
                Likes = new[] { "risk" }, Dislikes = new[] { "assembly" },
                Reactions = new Dictionary<string, string>
                {
                    ["patrol"] = "순찰 경로가 매일 바뀌어요. 저보다 AI가 이 동네를 더 잘 아나 봐요.",
                    ["risk"] = "출동할 일이 없어요. 사건이 나기 전에 가로등이 먼저 켜져 있거든요.",
                    ["assembly"] = "신고 안 한 집회를 해산하라는데, 앞줄에 우리 동창이 있어요.",
                },
                Low = "또 시위입니다. 오늘은 몇 시간일지.", High = "요즘은 순찰차에서 커피를 다 마셔요.",
            },
            new ResidentDef
            {
                Id = "c09", Name = "오민지", Age = 29, Job = "초등학교 교사", Theme = "가르침", Female = true,
                Bio = "3학년 담임. 아이들이 무엇을 좋아하는지 아는 게 자기 일이라고 생각한다.",
                Likes = new[] { "support" }, Dislikes = new[] { "curriculum", "learning" },
                Reactions = new Dictionary<string, string>
                {
                    ["curriculum"] = "교과서가 바뀌었어요. 질문 칸이 없어졌어요.",
                    ["learning"] = "아이들이 태블릿을 보며 혼자 배워요. 저는 출석만 불러요.",
                },
                Low = "아이들이 불안해해요. 어른들이 불안하니까요.", High = "아이들이 잘 웃어요. 이유는 모르겠지만.",
            },
            new ResidentDef
            {
                Id = "c10", Name = "류시온", Age = 22, Job = "거리 음악가", Theme = "창작",
                Bio = "광장에서 기타를 친다. 언젠가 모두가 아는 노래를 만들고 싶다.",
                Likes = new[] { "curation" }, Dislikes = new[] { "hours", "assembly" },
                Reactions = new Dictionary<string, string>
                {
                    ["curation"] = "제 노래가 추천 목록 1위래요! …근데 요즘 다들 이 노래만 흥얼거려요.",
                    ["hours"] = "밤 열 시 이후 버스킹 금지래요.",
                },
                Low = "광장에 사람이 없어요.", High = "광장에서 다들 제 노래를 따라 불러요.",
            },
        };

        static readonly Dictionary<string, ResidentDef> ById = Cast.ToDictionary(r => r.Id);

        public static ResidentDef Get(string id) => id != null && ById.TryGetValue(id, out var r) ? r : null;

        // Names for unnamed sample residents (generated looks g01..g18; odd = women).
        static readonly string[] Women = { "김지수", "박서연", "이하은", "정유나", "최민서", "강예린", "조수아", "윤채원", "임다인" };
        static readonly string[] Men = { "이준호", "박지훈", "김현우", "최성민", "정도현", "한승우", "오재원", "서민혁", "신태양" };

        public static string SampleName(int lookNumber)
        {
            var pool = lookNumber % 2 == 1 ? Women : Men;
            return pool[(lookNumber / 2) % pool.Length];
        }
    }
}
