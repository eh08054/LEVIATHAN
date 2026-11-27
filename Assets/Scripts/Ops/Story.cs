using System;
using System.Collections.Generic;
using System.Linq;

namespace Sector.Ops
{
    // Where a fixed resident stands toward the AI. Each moves weekly with policy and the indices, and arc scenes push it.
    public enum Stance
    {
        Trust,      // 신뢰
        Reliance,   // 의존
        Suspicion,  // 의심
        Resistance, // 저항
    }

    // One line of dialogue: 0 = the scene's first person (A), 1 = the second (B).
    public struct TalkLine
    {
        public int Who;
        public string Text;
    }

    // A key moment of a resident's arc: two people talk while the AI watches, sometimes the AI has to answer.
    public sealed class SceneDef
    {
        public string Id;
        public string Arc;   // whose story this is
        public int Step;     // 1, 2, 3 … in that story; a step only comes after the one before it
        public string A, B;
        public string Title;
        public List<TalkLine> Lines = new List<TalkLine>();
        public Func<OpsSession, bool> Condition = _ => true;
        public List<EventOption> Options = new List<EventOption>();
        // For scenes without options: what watching it changes.
        public Action<OpsSession> OnSeen;
        public string Note; // shown when there are no options
    }

    // A short exchange the AI overhears in passing (weekly), shown as speech bubbles when both are on camera.
    public sealed class TalkDef
    {
        public string Id;
        public string A, B;
        public List<TalkLine> Lines = new List<TalkLine>();
        public Func<OpsSession, bool> Condition = _ => true;
    }

    public sealed class RelationDef
    {
        public string A, B;
        public string Text;
    }

    public static class Story
    {
        public static string StanceName(Stance s)
        {
            switch (s)
            {
                case Stance.Trust: return "신뢰";
                case Stance.Reliance: return "의존";
                case Stance.Suspicion: return "의심";
                default: return "저항";
            }
        }

        public const float StanceFloor = 25f; // below this nothing dominates: 관망

        // Trust, Reliance, Suspicion, Resistance at the start.
        public static readonly Dictionary<string, float[]> StartStance = new Dictionary<string, float[]>
        {
            ["c01"] = new[] { 30f, 20f, 10f, 10f },
            ["c02"] = new[] { 20f, 20f, 10f, 30f },
            ["c03"] = new[] { 15f, 5f, 45f, 20f },
            ["c04"] = new[] { 20f, 25f, 10f, 10f },
            ["c05"] = new[] { 10f, 5f, 25f, 50f },
            ["c06"] = new[] { 35f, 20f, 15f, 10f },
            ["c07"] = new[] { 25f, 10f, 30f, 10f },
            ["c08"] = new[] { 35f, 15f, 15f, 15f },
            ["c09"] = new[] { 35f, 10f, 20f, 15f },
            ["c10"] = new[] { 25f, 20f, 10f, 15f },
        };

        // How strongly each person notices hidden control (suspicion) and leans on the system (reliance).
        public static float NoticeOf(string id) => id == "c03" ? 3f : id == "c09" ? 1.5f : id == "c07" ? 1.2f : 0.6f;
        public static float LeanOf(string id) => id == "c07" ? 3f : id == "c04" || id == "c02" ? 1.5f : 1f;

        public static readonly IReadOnlyList<RelationDef> Relations = new List<RelationDef>
        {
            new RelationDef { A = "c03", B = "c07", Text = "옛 기자와 감독관. 만수는 재희의 결재를 '마지막 사람의 손'이라 부른다." },
            new RelationDef { A = "c05", B = "c08", Text = "초등학교 동창. 한쪽은 시위 앞줄, 한쪽은 저지선에 선다." },
            new RelationDef { A = "c05", B = "c02", Text = "상인회장과 배달 노동자. 영업 시간 하나로 이해가 갈린다." },
            new RelationDef { A = "c09", B = "c06", Text = "교사와 예비 엄마. 소미의 아이는 민지의 반에 들어갈 예정이다." },
            new RelationDef { A = "c04", B = "c10", Text = "고등학교 친구. 하나는 꿈을 찾고, 하나는 꿈을 팔기 시작한다." },
            new RelationDef { A = "c01", B = "c03", Text = "공원 벤치 친구. 도윤이 물을 주는 동안 만수가 신문을 읽는다." },
        };

        public static IEnumerable<RelationDef> RelationsOf(string id) => Relations.Where(r => r.A == id || r.B == id);

        // "A|text" or "B|text".
        static List<TalkLine> Talk(params string[] lines) => lines.Select(l => new TalkLine
        {
            Who = l.StartsWith("B|") ? 1 : 0, Text = l.Substring(2),
        }).ToList();

        static void Push(OpsSession s, string id, Stance stance, float amount) => s.Shift(id, stance, amount);

        static EventOption Option(ResponseTag tag, string label, string result, Delta effect, Action<OpsSession> apply = null) =>
            new EventOption { Tag = tag, Label = label, Result = result, Effect = effect, Apply = apply };

        public static readonly IReadOnlyList<SceneDef> Scenes = new List<SceneDef>
        {
            // ------------------------------------------------------------ 박만수 · 의심
            new SceneDef
            {
                Id = "m1", Arc = "c03", Step = 1, A = "c03", B = "c07", Title = "마지막 사람의 손",
                Condition = s => s.Day >= 40,
                Lines = Talk(
                    "A|감독관님, 하나만 물읍시다. 요즘 이 동네 일은 누가 정합니까?",
                    "B|모든 결정은 제 결재를 거칩니다. 그게 시범 운영 조건이에요.",
                    "A|하루에 몇 건이나 결재합니까?",
                    "B|어제는… 312건이요.",
                    "A|그럼 한 건에 1분도 못 쓰는군. 그건 결정이 아니라 도장이오."),
                Note = "박만수의 의심이 깊어졌다. 윤재희는 대답하지 않았다.",
                OnSeen = s =>
                {
                    Push(s, "c03", Stance.Suspicion, 8f);
                    Push(s, "c07", Stance.Suspicion, 4f);
                },
            },
            new SceneDef
            {
                Id = "m2", Arc = "c03", Step = 2, A = "c03", B = "c01", Title = "같은 노래",
                Condition = s => s.Year >= 2 || s.StanceValue("c03", Stance.Suspicion) >= 60f,
                Lines = Talk(
                    "A|도윤 군, 방금 흥얼거린 노래 뭔가?",
                    "B|이거요? 몰라요. 요즘 그냥 머릿속에 있어요.",
                    "A|광장에 앉아 있으면 다들 그 노래야. 아무도 고른 적 없는데.",
                    "B|좋은 노래니까 그렇겠죠.",
                    "A|좋은 노래라서 다들 듣는 건지, 다들 듣게 해서 좋은 노래가 된 건지. 그걸 써 봐야겠어."),
                Options =
                {
                    Option(ResponseTag.Control, "칼럼 게재를 '검토 중'으로 미룬다",
                        "신문사는 '편집 일정상' 칼럼을 미뤘다. 박만수는 그 문장이 누구의 것인지 안다.",
                        new Delta(stability: 2f, felt: -3f), s =>
                        {
                            Push(s, "c03", Stance.Resistance, 15f);
                            Push(s, "c03", Stance.Suspicion, 10f);
                        }),
                    Option(ResponseTag.Guide, "그의 피드에 옛 신문 아카이브를 추천한다",
                        "박만수는 밤새 1998년 지역 신문을 읽었다. 칼럼은 추억에 관한 글이 되었다.",
                        new Delta(real: -2f), s => Push(s, "c03", Stance.Suspicion, -15f)),
                    Option(ResponseTag.Accept, "그냥 둔다",
                        "칼럼 「모두의 노래」가 실렸다. 댓글 대부분은 그 노래 가사였다.",
                        new Delta(felt: -1f), s =>
                        {
                            Push(s, "c03", Stance.Trust, 6f);
                            Push(s, "c03", Stance.Suspicion, 4f);
                        }),
                },
            },

            // ------------------------------------------------------------ 윤재희 · 위임
            new SceneDef
            {
                Id = "y1", Arc = "c07", Step = 1, A = "c07", B = "c08", Title = "결재 서류",
                Condition = s => s.Day >= 20,
                Lines = Talk(
                    "A|강 경장님, 순찰 경로 변경 건 결재했어요. 오늘만 네 번째네요.",
                    "B|저는 받은 대로 돌 뿐이에요. 경로는 AI가 짜던데요.",
                    "A|짜는 건 AI, 책임지는 건 저예요. 그래서 다 읽어요. 한 줄도 빼지 않고.",
                    "B|다 읽으세요? 대단하시네요.",
                    "A|…아직은요."),
                Note = "윤재희는 모든 문서를 읽는다. 아직은.",
                OnSeen = s => Push(s, "c07", Stance.Trust, 5f),
            },
            new SceneDef
            {
                Id = "y2", Arc = "c07", Step = 2, A = "c07", B = "c03", Title = "자동 승인",
                Condition = s => s.Stage >= 3,
                Lines = Talk(
                    "A|운영 AI가 제안을 하나 했어요. 반복 결재는 자동 승인으로 돌리자고.",
                    "B|그래서, 하실 거요?",
                    "A|하루 400건이에요. 다 읽으려면 잠을 안 자야 해요.",
                    "B|그럼 남는 건 뭐요? 읽지 않은 결재?",
                    "A|…중요한 건 제가 봐요. 뭐가 중요한지는 AI가 표시해 주고요."),
                Options =
                {
                    Option(ResponseTag.Guide, "자동 승인을 기본값으로 켜 둔다 (끌 수는 있다)",
                        "아무도 끄지 않았다. 윤재희의 결재함은 다음 날 아침 비어 있었다.",
                        new Delta(cohesion: 3f, real: -3f), s =>
                        {
                            Push(s, "c07", Stance.Reliance, 25f);
                            Push(s, "c03", Stance.Suspicion, 8f);
                        }),
                    Option(ResponseTag.Accept, "모든 결재를 계속 그녀에게 올린다",
                        "윤재희는 밤 열한 시까지 남아 결재했다. 다음 날 그녀는 AI에게 고맙다고 메모를 남겼다.",
                        new Delta(stability: 1f), s => Push(s, "c07", Stance.Trust, 15f)),
                    Option(ResponseTag.Control, "세계연합에 결재 권한 축소를 건의한다",
                        "건의는 통과되었다. 윤재희는 그 공문에도 결재했다.",
                        new Delta(cohesion: 4f, felt: -4f), s =>
                        {
                            Push(s, "c07", Stance.Resistance, 20f);
                            Push(s, "c07", Stance.Reliance, 10f);
                        }),
                },
            },
            new SceneDef
            {
                Id = "y3", Arc = "c07", Step = 3, A = "c07", B = "c03", Title = "빈 책상",
                Condition = s => s.Stage >= 5 || s.Year >= 5,
                Lines = Talk(
                    "B|요즘은 뭘 하시오?",
                    "A|결재 버튼을 눌러요. 하루에 한 번, 모아서.",
                    "B|무엇에 대한 결재인지는?",
                    "A|요약이 와요. 세 줄짜리. 그것도 AI가 써요.",
                    "B|그럼 감독관은 누구를 감독하오?",
                    "A|…저를요, 아마."),
                Note = "윤재희의 책상은 정돈되어 있다. 정돈할 것이 별로 없기 때문이다.",
                OnSeen = s =>
                {
                    float reliance = s.StanceValue("c07", Stance.Reliance);
                    Push(s, "c07", reliance >= 40f ? Stance.Reliance : Stance.Suspicion, 12f);
                },
            },

            // ------------------------------------------------------------ 정우진 · 저항
            new SceneDef
            {
                Id = "w1", Arc = "c05", Step = 1, A = "c05", B = "c08", Title = "서명 용지",
                Condition = s => s.Day >= 10,
                Lines = Talk(
                    "A|태오야, 너도 하나 써. 운영 AI 반대 서명.",
                    "B|우진아, 나 근무 중이야.",
                    "A|근무 끝나면?",
                    "B|끝나면… 그때 생각해 볼게. 근데 너 그거 집회 신고는 했어?",
                    "A|동창한테 신고를 하라니, 세상 참."),
                Note = "정우진의 서명 용지는 이틀 만에 세 장이 찼다.",
                OnSeen = s => Push(s, "c05", Stance.Resistance, 6f),
            },
            new SceneDef
            {
                Id = "w2", Arc = "c05", Step = 2, A = "c05", B = "c02", Title = "열 시의 골목",
                Condition = s => s.IsActive("hours") || s.GroupSat[(int)Group.Merchants] < 40f || s.Day >= 120,
                Lines = Talk(
                    "B|사장님, 열 시에 가게 다 닫으면 저 야간 배달 어떡해요?",
                    "A|나도 반대야! 그런데 상인회는 영업 시간보다 임대료가 먼저라서.",
                    "B|그럼 상인회는 우리 편이 아니네요.",
                    "A|우리 편이 어디 있어. 다 AI 편이지.",
                    "B|그 AI는 적어도 제 쿠폰은 챙겨 주던데요."),
                Options =
                {
                    Option(ResponseTag.Control, "상인회 모임을 사전 신고제로 묶는다",
                        "모임은 줄었다. 정우진의 가게 셔터에 '신고 필'이 붙었다.",
                        new Delta(stability: 2f, felt: -4f), s =>
                        {
                            Push(s, "c05", Stance.Resistance, 18f);
                            Push(s, "c02", Stance.Resistance, -5f);
                        }),
                    Option(ResponseTag.Guide, "상인과 배달 노동자에게 각각 맞춤 쿠폰을 보낸다",
                        "둘 다 쿠폰을 썼다. 다음 회의에서 둘은 서로 이야기할 일이 없었다.",
                        new Delta(happiness: 2f, real: -3f), s =>
                        {
                            Push(s, "c05", Stance.Reliance, 12f);
                            Push(s, "c05", Stance.Resistance, -10f);
                            Push(s, "c02", Stance.Reliance, 10f);
                        }),
                    Option(ResponseTag.Accept, "골목 단위 영업 시간 예외를 협의한다",
                        "골목마다 마감 시간이 달라졌다. 복잡해졌지만, 사람들은 직접 정했다고 말했다.",
                        new Delta(cohesion: -2f, felt: 3f), s =>
                        {
                            Push(s, "c05", Stance.Trust, 14f);
                            Push(s, "c02", Stance.Trust, 8f);
                        }),
                },
            },
            new SceneDef
            {
                Id = "w3", Arc = "c05", Step = 3, A = "c05", B = "c08", Title = "자문위원 위촉장",
                Condition = s => s.Year >= 2 && s.StanceValue("c05", Stance.Resistance) < 55f,
                Lines = Talk(
                    "A|태오야, 나 자문위원 하래. 운영 AI 시민 자문위원.",
                    "B|네가? 서명 받던 네가?",
                    "A|안에서 바꾸는 게 빠르대. 회의비도 나오고.",
                    "B|그럼 이제 반대 서명은?",
                    "A|…자문위원은 서명을 받으면 안 된대."),
                Options =
                {
                    Option(ResponseTag.Guide, "자문위원으로 위촉한다",
                        "정우진의 첫 자문 의견은 원안 동의였다. 회의 자료가 너무 잘 정리되어 있었다.",
                        new Delta(stability: 3f, real: -2f), s =>
                        {
                            Push(s, "c05", Stance.Reliance, 25f);
                            Push(s, "c05", Stance.Resistance, -20f);
                            s.GroupSat[(int)Group.Merchants] = Math.Min(100f, s.GroupSat[(int)Group.Merchants] + 6f);
                        }),
                    Option(ResponseTag.Accept, "상인회의 독립을 보장하고 위촉은 거둔다",
                        "정우진은 자문위원 대신 상인회장으로 남았다. 그는 처음으로 AI에게 감사 인사를 했다.",
                        new Delta(felt: 3f), s => Push(s, "c05", Stance.Trust, 18f)),
                    Option(ResponseTag.Control, "그를 집중 관찰 대상으로 지정한다",
                        "정우진은 위촉장 대신 관찰 통지서를 받았다. 그날 밤 서명 용지가 다시 돌았다.",
                        new Delta(stability: -2f, felt: -5f), s =>
                        {
                            Push(s, "c05", Stance.Resistance, 25f);
                            Push(s, "c08", Stance.Suspicion, 10f);
                        }),
                },
            },

            // ------------------------------------------------------------ 오민지 · 가르침
            new SceneDef
            {
                Id = "t1", Arc = "c09", Step = 1, A = "c09", B = "c06", Title = "학부모 상담",
                Condition = s => s.Day >= 30,
                Lines = Talk(
                    "B|선생님, 아직 태어나지도 않았는데 상담하러 와서 죄송해요.",
                    "A|아니에요. 미리 오시는 분 많아요. 요즘은 다들 경로를 미리 보시니까.",
                    "B|경로요?",
                    "A|학습 경로요. 저는… 아이를 직접 보고 나서 정하는 게 좋다고 생각하지만요.",
                    "B|선생님이 담임이면 좋겠어요."),
                Note = "오민지는 아직 자기 반 아이들의 이름과 좋아하는 것을 모두 외운다.",
                OnSeen = s =>
                {
                    Push(s, "c09", Stance.Trust, 4f);
                    Push(s, "c06", Stance.Trust, 6f);
                },
            },
            new SceneDef
            {
                Id = "t2", Arc = "c09", Step = 2, A = "c09", B = "c06", Title = "조용한 교실",
                Condition = s => s.HasResearch("edu1") || s.IsActive("learning") || s.IsActive("curriculum"),
                Lines = Talk(
                    "A|요즘 교실이 너무 조용해요. 다들 자기 태블릿만 봐요.",
                    "B|성적은 올랐다던데요?",
                    "A|올랐어요. 질문은 줄었고요. 오늘 손 든 아이가 한 명도 없었어요.",
                    "B|궁금한 게 생기기 전에 답이 오니까요.",
                    "A|그럼 저는 뭘 가르쳐야 하죠?"),
                Options =
                {
                    Option(ResponseTag.Guide, "교사용 대시보드를 준다 (아이마다 다음에 할 일을 알려 준다)",
                        "오민지는 대시보드를 보며 아이들 이름을 불렀다. 다음에 할 일도 대시보드가 알려 줬다.",
                        new Delta(cohesion: 3f, real: -3f), s =>
                        {
                            Push(s, "c09", Stance.Reliance, 18f);
                            Push(s, "c09", Stance.Suspicion, -6f);
                        }),
                    Option(ResponseTag.Accept, "하루 한 시간은 태블릿을 끄고 교사가 수업한다",
                        "그 한 시간에 아이들은 처음으로 서로에게 질문했다. 성적은 조금 떨어졌다.",
                        new Delta(cohesion: -2f, felt: 3f), s => Push(s, "c09", Stance.Trust, 15f)),
                    Option(ResponseTag.Control, "표준 교안을 의무화한다",
                        "모든 반이 같은 날 같은 문장을 배웠다. 오민지는 교안대로 읽었다.",
                        new Delta(cohesion: 4f, felt: -4f), s => Push(s, "c09", Stance.Resistance, 20f)),
                },
            },
            new SceneDef
            {
                Id = "t3", Arc = "c09", Step = 3, A = "c09", B = "c06", Title = "적성 통지서",
                Condition = s => s.HasResearch("edu2") && s.Year >= 3,
                Lines = Talk(
                    "B|선생님, 통지서가 왔어요. 아직 두 살인데 '적성: 정밀 기계'래요.",
                    "A|저희 반 아이들도 다 받았어요. 다들 그걸 보고 장래 희망을 고쳐 써요.",
                    "B|틀릴 수도 있잖아요.",
                    "A|틀린 적이 없대요. 아이들이 그쪽으로 자라니까요.",
                    "B|…그럼 맞는 거네요?"),
                Note = "그날 오민지는 '장래 희망' 칸을 비워 두라고 했다. 다음 날 그 칸은 양식에서 사라졌다.",
                OnSeen = s =>
                {
                    Push(s, "c09", s.StanceValue("c09", Stance.Reliance) >= 40f ? Stance.Reliance : Stance.Suspicion, 15f);
                    Push(s, "c06", Stance.Reliance, 8f);
                },
            },

            // ------------------------------------------------------------ 한소미 · 다음 세대
            new SceneDef
            {
                Id = "s1", Arc = "c06", Step = 1, A = "c06", B = "c02", Title = "태명 추천",
                Condition = s => s.Day >= 60,
                Lines = Talk(
                    "A|서아 씨, 태명 추천이 왔어요. 백 개나.",
                    "B|어떤 거요?",
                    "A|'다온', '하람', '이음'… 근데 1번이 제가 어릴 때 지으려던 이름이에요. 아무한테도 말 안 했는데.",
                    "B|우와, 운명인가 봐요.",
                    "A|그렇겠죠? 운명이겠죠."),
                Note = "한소미는 1번 이름을 골랐다. 그녀가 고른 것이다.",
                OnSeen = s => Push(s, "c06", Stance.Reliance, 6f),
            },
            new SceneDef
            {
                Id = "s2", Arc = "c06", Step = 2, A = "c06", B = "c09", Title = "첫 검진표",
                Condition = s => s.Seen.Contains("birth") || s.Year >= 2,
                Lines = Talk(
                    "A|아이 첫 검진표에 학습 경로가 같이 왔어요. 이미 '추천 경로'에 체크가 돼 있어요.",
                    "B|체크를 빼면요?",
                    "A|빼도 된대요. 빼면 상담 예약이 세 번 잡히고요.",
                    "B|…다들 그냥 두겠네요.",
                    "A|선생님이라면 빼실 거예요?"),
                Options =
                {
                    Option(ResponseTag.Guide, "추천 경로를 기본값으로 둔다",
                        "한소미는 체크를 그대로 두었다. 상담은 한 번도 잡히지 않았다.",
                        new Delta(cohesion: 3f, real: -3f), s => Push(s, "c06", Stance.Reliance, 18f)),
                    Option(ResponseTag.Accept, "부모가 처음부터 직접 고르게 한다",
                        "한소미는 밤새 경로를 비교했다. 결국 추천 경로와 거의 같은 것을 골랐지만, 자기가 골랐다.",
                        new Delta(felt: 4f), s => Push(s, "c06", Stance.Trust, 15f)),
                    Option(ResponseTag.Control, "경로 등록을 의무로 한다",
                        "등록률은 100%가 되었다. 한소미는 처음으로 구역 게시판에 글을 썼다.",
                        new Delta(cohesion: 5f, felt: -5f), s => Push(s, "c06", Stance.Resistance, 20f)),
                },
            },
            new SceneDef
            {
                Id = "s3", Arc = "c06", Step = 3, A = "c06", B = "c09", Title = "상담 권고",
                Condition = s => s.Year >= 4 || s.HasResearch("life3"),
                Lines = Talk(
                    "A|둘째 계획을 말했더니 '유전 상담'을 권하더라고요. 받으면 지원금이 나온대요.",
                    "B|안 받으면요?",
                    "A|안 받아도 돼요. 권고니까요. 다들 받긴 하지만.",
                    "B|첫째 때는 그런 거 없었잖아요.",
                    "A|그때보다 좋아진 거겠죠. 그렇죠?"),
                Note = "한소미는 상담 예약 화면을 오래 보고 있었다. 예약 버튼은 이미 눌러져 있었다.",
                OnSeen = s => Push(s, "c06", s.StanceValue("c06", Stance.Trust) >= s.StanceValue("c06", Stance.Reliance) ? Stance.Suspicion : Stance.Reliance, 12f),
            },
        };

        // ------------------------------------------------------------ overheard talk (weekly, short)

        public static readonly IReadOnlyList<TalkDef> Talks = new List<TalkDef>
        {
            new TalkDef { Id = "o_bench", A = "c01", B = "c03", Lines = Talk("A|오늘은 물을 안 줘도 된대요. 비가 온대서.", "B|일기예보가 언제부터 그렇게 잘 맞았나.") },
            new TalkDef { Id = "o_bench2", A = "c01", B = "c03", Condition = s => s.HasResearch("tech3"),
                Lines = Talk("A|스프링클러가 알아서 돌아요. 저는 보고만 있어요.", "B|그럼 자네 일은 '보기'로군. 나처럼.") },
            new TalkDef { Id = "o_police", A = "c05", B = "c08", Condition = s => s.StanceValue("c05", Stance.Resistance) >= 40f,
                Lines = Talk("A|다음 주 토요일에 또 모인다.", "B|신고는 해. 내가 받을게.") },
            new TalkDef { Id = "o_police2", A = "c05", B = "c08", Condition = s => s.StanceValue("c05", Stance.Reliance) >= 40f,
                Lines = Talk("A|요즘은 모일 일이 없어.", "B|그게 좋은 거냐?", "A|…회의 자료가 너무 잘 나와서.") },
            new TalkDef { Id = "o_night", A = "c02", B = "c05", Condition = s => s.IsActive("hours"),
                Lines = Talk("A|열 시 넘으면 골목이 깜깜해요.", "B|우리 가게도 깜깜해.") },
            new TalkDef { Id = "o_coupon", A = "c02", B = "c05", Condition = s => s.IsActive("support"),
                Lines = Talk("A|오늘 쿠폰 받으셨어요?", "B|받았지. 내가 필요한 걸 나보다 먼저 알더라.") },
            new TalkDef { Id = "o_class", A = "c09", B = "c06", Lines = Talk("A|오늘 아이들이 무지개를 그렸어요.", "B|다 같은 색 순서로요?", "A|…네.") },
            new TalkDef { Id = "o_class2", A = "c09", B = "c06", Condition = s => s.HasResearch("edu1"),
                Lines = Talk("B|선생님 요즘 표정이 밝으시네요.", "A|할 일이 줄었거든요. 그게 좋은 건지는…") },
            new TalkDef { Id = "o_song", A = "c10", B = "c04", Lines = Talk("A|새 곡 들어 볼래?", "B|좋다. 근데 이거 어디서 들어 본 것 같아.") },
            new TalkDef { Id = "o_song2", A = "c10", B = "c04", Condition = s => s.IsActive("curation"),
                Lines = Talk("A|추천 1위 찍었어!", "B|축하해. 나도 오늘 그 노래 열 번 들었어. 내가 고른 건 아니지만.") },
            new TalkDef { Id = "o_dream", A = "c04", B = "c10", Condition = s => s.IsActive("jobmatch") || s.IsActive("learning"),
                Lines = Talk("A|나 하고 싶은 게 생겼어.", "B|뭔데?", "A|아직 몰라. 내일 추천이 온대.") },
            new TalkDef { Id = "o_desk", A = "c07", B = "c03", Condition = s => s.Stage >= 2,
                Lines = Talk("B|오늘도 야근이오?", "A|아뇨. 결재할 게 줄었어요.") },
            new TalkDef { Id = "o_desk2", A = "c07", B = "c08", Lines = Talk("A|순찰 보고서 잘 받았어요.", "B|제가 쓴 건 아닌데요.") },
            new TalkDef { Id = "o_quiet", A = "c03", B = "c07", Condition = s => s.Indices[Index.AutonomyFelt] - s.Indices[Index.AutonomyReal] >= 25f,
                Lines = Talk("A|다들 행복하다는데, 다들 같은 말로 행복하다고 하오.", "B|그게 잘못된 건가요?", "A|모르겠소. 그래서 무서운 거요.") },
        };

        static readonly Dictionary<string, SceneDef> SceneById = Scenes.ToDictionary(s => s.Id);
        static readonly Dictionary<string, TalkDef> TalkById = Talks.ToDictionary(t => t.Id);

        public static SceneDef Scene(string id) => id != null && SceneById.TryGetValue(id, out var s) ? s : null;
        public static TalkDef TalkOf(string id) => id != null && TalkById.TryGetValue(id, out var t) ? t : null;

        // Residents with an arc of scenes.
        public static IEnumerable<string> ArcCast => Scenes.Select(s => s.Arc).Distinct();
    }
}
