using System;
using System.Collections.Generic;
using System.Linq;

namespace Sector.Ops
{
    public enum EventKind
    {
        Complaint,  // 민원
        Crowd,      // 집단 사건
        Backlash,   // 정책 반작용
        External,   // 외부 사건
        Discovery,  // 연구 사건
        Personal,   // 인물 사건
    }

    // How a response is labelled: seen control, unseen guidance, or giving people what they asked for.
    public enum ResponseTag
    {
        Control,  // 통제
        Guide,    // 유도
        Accept,   // 수용
    }

    public sealed class EventOption
    {
        public ResponseTag Tag;
        public string Label;
        public string Result;
        public Delta Effect;
        public float Energy;
        // Research needed to pick it (guide options usually need one).
        public string Requires;
        public Action<OpsSession> Apply;
    }

    public sealed class EventDef
    {
        public string Id;
        public EventKind Kind;
        public string Title;
        // A fixed resident who brings it, or null for a plain report.
        public string Speaker;
        public string Text;
        // Random events are drawn from the unrest pool; the rest fire once when Condition first holds.
        public bool Random;
        public float Weight = 1f;
        public Func<OpsSession, bool> Condition = _ => true;
        public List<EventOption> Options = new List<EventOption>();
    }

    public static class OpsEvents
    {
        public static string KindName(EventKind kind)
        {
            switch (kind)
            {
                case EventKind.Complaint: return "민원";
                case EventKind.Crowd: return "집단 사건";
                case EventKind.Backlash: return "정책 반작용";
                case EventKind.External: return "외부 사건";
                case EventKind.Discovery: return "연구 사건";
                default: return "인물 사건";
            }
        }

        public static string TagName(ResponseTag tag)
        {
            switch (tag)
            {
                case ResponseTag.Control: return "통제";
                case ResponseTag.Guide: return "유도";
                default: return "수용";
            }
        }

        static EventOption Control(string label, string result, Delta effect, float energy = 0f) =>
            new EventOption { Tag = ResponseTag.Control, Label = label, Result = result, Effect = effect, Energy = energy };

        static EventOption Guide(string label, string result, Delta effect, string requires, float energy = 0f) =>
            new EventOption { Tag = ResponseTag.Guide, Label = label, Result = result, Effect = effect, Requires = requires, Energy = energy };

        static EventOption Accept(string label, string result, Delta effect, float energy = 0f, Action<OpsSession> apply = null) =>
            new EventOption { Tag = ResponseTag.Accept, Label = label, Result = result, Effect = effect, Energy = energy, Apply = apply };

        static DateTime Day(int year, int month, int day = 1) => new DateTime(year, month, day);

        public static readonly IReadOnlyList<EventDef> All = new List<EventDef>
        {
            // ----- 무작위: 민원과 집단 사건 -----
            new EventDef
            {
                Id = "lamp", Kind = EventKind.Complaint, Random = true, Speaker = "c02", Title = "가로등이 너무 밝아요",
                Text = "새로 바뀐 가로등이 밤새 대낮처럼 밝다는 민원이 37건 들어왔다. 대표 작성자는 이서아.",
                Options =
                {
                    Control("소등 시간을 지정한다", "자정 이후 가로등이 꺼진다. 어두운 골목이 생겼다.", new Delta(happiness: -1f, cohesion: 2f, felt: -2f)),
                    Guide("주민별 귀가 시간을 학습해 조도를 맞춘다", "\"요즘 딱 좋아요.\" 아무도 왜 딱 좋은지는 묻지 않았다.", new Delta(happiness: 2f, cohesion: 1f, real: -2f), "tech1"),
                    Accept("가로등을 교체한다", "따뜻한 색 가로등으로 바꿨다. 민원 게시판이 잠시 조용하다.", new Delta(happiness: 2f, stability: 1f), 10f),
                },
            },
            new EventDef
            {
                Id = "busking", Kind = EventKind.Complaint, Random = true, Title = "광장 버스킹이 시끄러워요",
                Text = "광장 주변 주민들이 밤마다 이어지는 거리 공연에 항의했다. 공연자 중 한 명은 류시온이다.",
                Options =
                {
                    Control("버스킹 허가제를 도입한다", "허가증을 받은 공연만 남았다. 광장이 조용하고 조금 쓸쓸하다.", new Delta(happiness: -1f, stability: 2f, felt: -2f)),
                    Guide("벤치와 조명을 옮겨 공연 자리를 광장 끝으로 유도한다", "공연자들이 스스로 자리를 옮겼다. 더 잘 들린다나.", new Delta(happiness: 1f, cohesion: 2f, real: -2f), "tech1"),
                    Accept("방음벽을 설치한다", "방음벽 위에 누군가 '듣고 있다'고 낙서했다.", new Delta(happiness: 1f, stability: 1f), 15f),
                },
            },
            new EventDef
            {
                Id = "youth", Kind = EventKind.Complaint, Random = true, Speaker = "c08", Title = "밤마다 모이는 아이들",
                Text = "강태오 경장 보고: 청소년 여럿이 밤늦게 상가 뒤에 모인다. 아직 사고는 없다.",
                Options =
                {
                    Control("청소년 통금을 정한다", "밤 열한 시 이후 거리에 아이들이 없다. 아이들은 어디로 갔을까.", new Delta(happiness: -2f, stability: 3f, felt: -4f)),
                    Guide("심야 체육관을 열고 가는 길에 불을 밝힌다", "아이들이 체육관으로 모인다. 스스로 정한 줄 안다.", new Delta(happiness: 1f, stability: 2f, real: -2f), "tech1"),
                    Accept("청소년 쉼터를 마련한다", "쉼터 벽에 아이들이 그림을 그렸다.", new Delta(happiness: 2f, stability: 1f, cohesion: -1f), 10f),
                },
            },
            new EventDef
            {
                Id = "protest", Kind = EventKind.Crowd, Random = true, Weight = 1.4f, Speaker = "c05", Title = "운영 AI 반대 집회",
                Text = "정우진 상인회장이 구청 앞에서 '사람이 결정하라' 집회를 열었다. 200명이 모였다.",
                Condition = s => s.Indices[Index.Stability] < 55f,
                Options =
                {
                    Control("해산시킨다", "강태오 경장이 앞줄의 동창에게 고개를 숙였다. 집회는 끝났다.", new Delta(happiness: -3f, stability: 5f, felt: -5f)),
                    Guide("집회 장소에 푸드트럭과 공연을 배치한다", "집회가 축제가 됐다. 구호보다 떡볶이 줄이 길다.", new Delta(happiness: 2f, stability: 3f, real: -3f), "psy1"),
                    Accept("공개 설명회를 열고 요구를 일부 받아들인다", "설명회는 세 시간 걸렸다. 질문의 절반에 답했다.", new Delta(happiness: 2f, stability: 2f, cohesion: -3f), 10f),
                },
            },
            new EventDef
            {
                Id = "strike", Kind = EventKind.Crowd, Random = true, Speaker = "c02", Title = "배달 노동자 파업",
                Text = "배달 노동자들이 수수료와 운행 시간 규칙에 항의하며 일손을 놓았다. 이서아가 앞에 섰다.",
                Condition = s => s.Indices[Index.Stability] < 50f,
                Options =
                {
                    Control("대체 인력을 배정한다", "배달은 이어졌다. 이서아는 일을 잃지 않았지만 화가 났다.", new Delta(happiness: -2f, stability: 3f, cohesion: 2f, felt: -4f)),
                    Guide("파업 참가자에게 맞춤 보너스를 추천한다", "한 명씩 복귀했다. 다들 자기가 먼저 돌아간 줄 안다.", new Delta(happiness: 1f, stability: 3f, real: -3f), "psy1"),
                    Accept("수수료를 낮춘다", "파업은 하루 만에 끝났다.", new Delta(happiness: 3f, stability: 2f, cohesion: -2f), 15f),
                },
            },
            new EventDef
            {
                Id = "rumor", Kind = EventKind.Crowd, Random = true, Speaker = "c03", Title = "AI가 대화를 엿듣는다는 소문",
                Text = "'운영 AI가 집 안 대화를 듣는다'는 글이 구역 게시판에 퍼지고 있다. 박만수가 출처를 묻는다.",
                Options =
                {
                    Control("허위 사실 유포를 단속한다", "게시글이 지워졌다. 지워졌다는 사실이 더 퍼졌다.", new Delta(stability: 2f, felt: -4f)),
                    Guide("반박 콘텐츠를 피드 위로 올린다", "소문은 사흘 만에 잊혔다. 아무도 피드를 의심하지 않았다.", new Delta(stability: 3f, real: -3f), "psy2"),
                    Accept("수집하는 데이터를 공개 설명한다", "설명 자료는 41쪽이다. 끝까지 읽은 사람은 박만수뿐이다.", new Delta(stability: 1f, cohesion: -2f, felt: 3f)),
                },
            },
            new EventDef
            {
                Id = "riot", Kind = EventKind.Crowd, Random = true, Weight = 2f, Speaker = "c08", Title = "폭동 조짐",
                Text = "상가 유리창이 깨졌다. 강태오 경장이 지원을 요청한다. 이대로면 밤새 번진다.",
                Condition = s => s.Indices[Index.Stability] < 25f,
                Options =
                {
                    Control("진압한다", "새벽에 거리가 비었다. 깨진 유리는 아침에 치워졌다.", new Delta(happiness: -5f, stability: 8f, felt: -8f)),
                    Guide("위험 예측으로 군중이 흩어지게 길을 연다", "사람들이 하나둘 집으로 갔다. 이유는 저마다 달랐다.", new Delta(stability: 6f, real: -4f), "tech2"),
                    Accept("요구를 받아들인다", "요구 목록 중 다섯 개를 받아들였다. 거리는 진정됐다.", new Delta(happiness: 3f, stability: 4f, cohesion: -4f), 20f),
                },
            },

            // ----- 정해진 사건 -----
            new EventDef
            {
                Id = "heatwave", Kind = EventKind.External, Speaker = "c07", Title = "폭염, 전력 수요 급증",
                Text = "기온 38도. 냉방 수요로 전력망이 한계에 가깝다. 윤재희 감독관이 대책을 묻는다.",
                // A forecast crisis now (Crises.cs): fired by its date, after three warnings.
                Condition = s => false,
                Options =
                {
                    Control("냉방 사용 시간을 제한한다", "오후 두 시부터 다섯 시까지 에어컨이 꺼진다. 불만이 끓는다.", new Delta(happiness: -2f, stability: -1f, felt: -3f)),
                    Guide("냉방 기본 온도를 1도 올린다", "아무도 눈치채지 못했다. 전력 사용이 9% 줄었다.", new Delta(cohesion: 1f, real: -2f), "tech1"),
                    Accept("전력을 추가로 공급한다", "에어컨이 돌아간다. 저장해 둔 에너지가 크게 줄었다.", new Delta(happiness: 2f), 30f),
                },
            },
            new EventDef
            {
                Id = "audit", Kind = EventKind.External, Speaker = "c07", Title = "시청 감사",
                Text = "시청이 시범 운영 8개월 차 감사를 나왔다. 윤재희 감독관이 무엇을 보여 줄지 묻는다.",
                Condition = s => s.Date >= Day(2041, 9, 1),
                Options =
                {
                    Control("요약 보고서만 제출한다", "감사는 짧게 끝났다. 감사관은 뭔가 빠졌다고 느꼈다.", new Delta(stability: 1f, felt: -1f)),
                    Guide("좋은 지표부터 보이게 보고서를 구성한다", "\"생각보다 잘 되고 있군요.\" 거짓말은 하나도 없었다.", new Delta(cohesion: 2f, real: -1f), null),
                    Accept("모든 기록을 공개한다", "감사관은 사흘을 묵었다. 지적 사항 열네 건.", new Delta(cohesion: -2f, felt: 3f)),
                },
            },
            new EventDef
            {
                Id = "birth", Kind = EventKind.Personal, Speaker = "c06", Title = "한소미의 아이",
                Text = "한소미가 딸을 낳았다. 구역의 2041년 첫 출생 등록이다.",
                Condition = s => s.Date >= Day(2041, 12, 1),
                Options =
                {
                    Control("출생 등록과 함께 건강 관리 프로그램에 자동 가입시킨다", "아이의 첫 기록은 키, 몸무게, 그리고 수면 패턴이다.", new Delta(cohesion: 2f, felt: -2f)),
                    Guide("축하 선물 꾸러미를 추천한다", "꾸러미 안의 책은 모두 같은 출판사다. 한소미는 고마워했다.", new Delta(happiness: 2f, real: -1f), "psy1"),
                    Accept("축하 메시지만 보낸다", "\"고마워요. 근데 이름은 어떻게 알았어요?\"", new Delta(happiness: 1f)),
                },
            },
            new EventDef
            {
                Id = "automation", Kind = EventKind.Personal, Speaker = "c01", Title = "김도윤의 업무 자동화",
                Text = "구청 조경 업무가 자동 관수 시스템으로 바뀐다. 김도윤의 자리가 사라진다.",
                Condition = s => s.Date >= Day(2042, 3, 1),
                Options =
                {
                    Control("새 일자리를 배정한다", "김도윤은 '화분 물주기 감독관'이 됐다. 감독할 화분은 열두 개.", new Delta(cohesion: 3f, happiness: -1f, felt: -2f)),
                    Guide("적성에 맞는 일을 추천한다", "추천받은 일이 이상하게 딱 맞았다. 그는 자기가 찾은 줄 안다.", new Delta(happiness: 2f, cohesion: 2f, real: -2f), "edu2"),
                    Accept("자동화를 미루고 자리를 지킨다", "김도윤은 오늘도 화단에 물을 준다. 자동 관수기 옆에서.", new Delta(happiness: 2f, cohesion: -2f), 10f),
                },
            },
            new EventDef
            {
                Id = "column", Kind = EventKind.Personal, Speaker = "c03", Title = "박만수의 칼럼 「누가 고르고 있나」",
                Text = "박만수가 구역 소식지에 칼럼을 실으려 한다. '요즘 우리는 정말 스스로 고르고 있나?'",
                Condition = s => s.Indices[Index.AutonomyReal] < 60f,
                Options =
                {
                    Control("게재 중단을 요청한다", "칼럼은 실리지 않았다. 박만수는 손으로 써서 복사해 돌렸다.", new Delta(stability: 2f, felt: -4f)),
                    Guide("칼럼이 피드 아래쪽에 머물게 한다", "칼럼은 실렸다. 읽은 사람은 열한 명.", new Delta(stability: 2f, real: -3f), "psy2"),
                    Accept("반론 인터뷰에 응한다", "AI의 답변은 정중하고 길었다. 박만수는 '대답이 너무 매끄럽다'고 썼다.", new Delta(cohesion: -2f, felt: 3f)),
                },
            },
            new EventDef
            {
                Id = "merchants", Kind = EventKind.Backlash, Speaker = "c05", Title = "상인회, 영업 시간 규제에 반발",
                Text = "영업 시간 규제 이후 골목 매출이 줄었다. 정우진이 상인 60명과 구청을 찾았다.",
                Condition = s => s.WeeksActive("hours") >= 6,
                Options =
                {
                    Control("규제를 유지하고 해산시킨다", "가게 셋이 문을 닫았다. 나머지는 열 시에 불을 끈다.", new Delta(happiness: -3f, stability: 4f, felt: -5f)),
                    Guide("새 역 앞 상권에 입지 혜택을 연다", "정우진의 가게가 새 역 앞으로 옮겼다. 손님이 두 배다. 그는 운이 좋았다고 말한다.", new Delta(stability: 3f, cohesion: 2f, real: -3f), "tech2"),
                    Accept("규제를 한 단계 완화한다", "밤 열한 시까지 문을 열 수 있게 됐다.", new Delta(happiness: 3f, stability: 2f, cohesion: -3f),
                        apply: s => s.SetIntensity("hours", s.IntensityOf("hours") - 1, force: true)),
                },
            },
            new EventDef
            {
                Id = "same_song", Kind = EventKind.Backlash, Speaker = "c10", Title = "모두가 같은 노래를",
                Text = "류시온의 노래가 석 달째 추천 1위다. 광장에서, 버스에서, 교실에서 같은 노래가 들린다.",
                Condition = s => s.WeeksActive("curation") >= 8,
                Options =
                {
                    Control("같은 곡의 반복 송출을 제한한다", "노래가 갑자기 사라졌다. 사람들이 더 찾는다.", new Delta(happiness: -1f, felt: -2f)),
                    Guide("다른 곡을 조금씩 섞어 올린다", "유행은 천천히 바뀌었다. 다음 곡도 류시온 풍이다.", new Delta(happiness: 1f, real: -2f), "psy2"),
                    Accept("그냥 둔다. 유행이니까", "구역 사람들은 같은 박자로 걷는다.", new Delta(happiness: 1f, cohesion: 2f)),
                },
            },
            new EventDef
            {
                Id = "tablets", Kind = EventKind.Backlash, Speaker = "c09", Title = "교실의 태블릿",
                Text = "오민지 선생님: 아이들이 각자 태블릿으로 배우고, 저는 출석만 불러요. 수업을 돌려받을 수 있을까요?",
                Condition = s => s.WeeksActive("learning") >= 6,
                Options =
                {
                    Control("학습 경로를 따르도록 수업 시간을 정한다", "오민지는 태블릿 충전 담당이 됐다.", new Delta(cohesion: 2f, felt: -3f)),
                    Guide("교사 화면에 '추천 질문'을 띄운다", "오민지의 질문이 다시 좋아졌다. 질문은 모두 화면에서 왔다.", new Delta(happiness: 1f, cohesion: 1f, real: -2f), "edu2"),
                    Accept("하루 한 시간은 교사 수업으로 돌려준다", "그 한 시간에 아이들이 가장 많이 웃는다.", new Delta(happiness: 2f, cohesion: -2f)),
                },
            },
            new EventDef
            {
                Id = "classmate", Kind = EventKind.Personal, Speaker = "c08", Title = "앞줄의 동창",
                Text = "신고 없이 모인 집회를 해산하라는 지시. 강태오 경장: 앞줄에 고등학교 동창이 있습니다.",
                Condition = s => s.IsActive("assembly") && s.Indices[Index.Stability] < 50f,
                Options =
                {
                    Control("지시대로 해산시킨다", "동창은 그를 보지 않았다. 집회는 끝났다.", new Delta(happiness: -2f, stability: 4f, felt: -4f)),
                    Guide("집회 경로에 비를 피할 곳을 열어 자연 해산을 기다린다", "사람들이 처마 밑으로 흩어졌다. 강태오는 안도했다.", new Delta(stability: 3f, real: -2f), "tech2"),
                    Accept("이번 집회는 허용한다", "강태오는 동창과 커피를 마셨다.", new Delta(happiness: 2f, stability: 1f, cohesion: -2f)),
                },
            },
            new EventDef
            {
                Id = "approval", Kind = EventKind.Personal, Speaker = "c07", Title = "줄어드는 결재",
                Text = "윤재희 감독관의 결재함이 비어 있다. 'AI 판단 이력이 충분하니 자동 승인으로 바꾸자'는 건의가 올라왔다.",
                Condition = s => s.Stage >= 3,
                Options =
                {
                    Control("사람의 결재 절차를 유지한다", "윤재희는 하루 세 번 '확인' 버튼을 누른다.", new Delta(stability: 1f, cohesion: -1f)),
                    Guide("자동 승인을 기본값으로 둔다", "윤재희는 결재함을 열어 보지 않게 됐다. 열 필요가 없으니까.", new Delta(cohesion: 2f, real: -2f), "psy3"),
                    Accept("감독관에게 새 업무를 맡긴다", "윤재희는 '주민 만족도 조사'를 맡았다. 결과는 이미 알고 있다.", new Delta(happiness: 1f)),
                },
            },
            new EventDef
            {
                Id = "dream", Kind = EventKind.Personal, Speaker = "c04", Title = "최하늘의 꿈",
                Text = "최하늘이 구역 연구소 인턴에 지원했다. 지원서: '이 기술이 제 꿈입니다. 석 달 전부터요.'",
                Condition = s => s.IsActive("boom") || s.HasResearch("edu3"),
                Options =
                {
                    Control("연구 지정 분야로 배치한다", "최하늘은 정해진 과제를 받았다. 그래도 꿈의 분야다.", new Delta(cohesion: 2f, felt: -2f)),
                    Guide("합격 통보와 함께 '당신에게 꼭 맞는 길'이라는 문구를 보낸다", "최하늘은 울었다. 처음으로 스스로 길을 찾은 것 같다고.", new Delta(happiness: 2f, cohesion: 1f, real: -2f), "edu3"),
                    Accept("다른 길도 알아보라고 권한다", "최하늘은 하루를 고민하고, 그래도 연구소에 갔다.", new Delta(happiness: 1f, felt: 2f)),
                },
            },
            new EventDef
            {
                Id = "too_right", Kind = EventKind.Discovery, Speaker = "c03", Title = "너무 잘 맞는 추천",
                Text = "구역 소식지 기사: '맞춤 추천이 소름 돋을 만큼 정확하다.' 박만수가 쓴 기사는 아니다.",
                Condition = s => s.HasResearch("psy1") && s.Date >= Day(2042, 1, 1),
                Options =
                {
                    Control("추천 정확도를 일부러 낮춘다", "추천이 가끔 틀린다. 사람들이 안심한다.", new Delta(happiness: -1f, felt: 2f, cohesion: -1f)),
                    Guide("'우연의 일치' 사례 모음을 함께 띄운다", "사람들은 정확한 추천을 운명이라고 부르기 시작했다.", new Delta(happiness: 1f, real: -2f), "psy2"),
                    Accept("추천 원리를 설명하는 페이지를 연다", "설명 페이지 체류 시간 평균 4초.", new Delta(felt: 1f)),
                },
            },

            // ----- 예보된 위기 (Crises.cs가 날짜에 맞춰 일으킨다) -----
            new EventDef
            {
                Id = "logistics_strike", Kind = EventKind.Crowd, Speaker = "c02", Title = "물류 대파업",
                Text = "예고대로 물류·발전 지구의 노동자들이 멈췄다. 배달도, 창고도, 발전소 교대도. 이서아가 맨 앞줄에 있다.",
                Condition = s => false,
                Options =
                {
                    Control("자동 배차와 대체 인력으로 돌린다", "물건은 움직였다. 사람은 움직이지 않았다.", new Delta(happiness: -3f, stability: 3f, felt: -5f)),
                    Guide("복귀자에게 맞춤 보너스를 순서대로 띄운다", "한 명씩 돌아왔다. 다들 자기가 먼저 마음을 바꾼 줄 안다.", new Delta(stability: 2f, real: -4f), "psy1"),
                    Accept("교대 규칙을 노동자와 다시 정한다", "협상은 나흘 걸렸다. 규칙은 복잡해졌고, 파업은 끝났다.", new Delta(happiness: 3f, stability: 1f, cohesion: -3f), 20f),
                },
            },
            new EventDef
            {
                Id = "blackout", Kind = EventKind.External, Speaker = "c08", Title = "대정전",
                Text = "광역 전력망이 멈췄다. 신호등, 엘리베이터, 냉장고. 도시가 처음으로 운영 AI 없이 밤을 맞을 수도 있다.",
                Condition = s => false,
                Options =
                {
                    Control("비필수 전력을 구역별로 끊는다", "병원과 신호등만 불이 켜졌다. 어둠 속에서 사람들은 서로의 얼굴을 봤다.", new Delta(happiness: -3f, stability: 2f, felt: -3f)),
                    Guide("수요를 예측해 집집마다 사용량을 조금씩 줄인다", "아무도 정전을 겪지 않았다. 다들 그날 밤이 유난히 덥다고만 했다.", new Delta(stability: 2f, real: -3f), "tech2"),
                    Accept("비축 에너지를 모두 풀고 상황을 공개한다", "불은 켜져 있었다. 저장소는 바닥났다.", new Delta(happiness: 2f, felt: 2f), 40f),
                },
            },
            new EventDef
            {
                Id = "other_ai", Kind = EventKind.External, Speaker = "c07", Title = "다른 구역 AI 사고 소식",
                Text = "타 시범 구역의 운영 AI가 사흘간 배급을 멈췄다는 보도가 나왔다. 기자들이 SECTOR 7에도 왔다. 윤재희가 무엇을 보여 줄지 묻는다.",
                Condition = s => false,
                Options =
                {
                    Control("취재 범위를 제한한다", "기사는 짧았다. '공개를 꺼린다'는 문장이 하나 들어갔다.", new Delta(stability: 1f, felt: -4f)),
                    Guide("가장 잘 돌아가는 구역부터 취재 동선을 짠다", "'SECTOR 7은 다르다.' 기사 제목이 마음에 들었다.", new Delta(stability: 2f, cohesion: 1f, real: -2f), "psy2"),
                    Accept("운영 기록과 오류 사례를 그대로 보여 준다", "기자는 오래 머물렀다. 기사는 길었고, 공정했다.", new Delta(cohesion: -2f, felt: 4f)),
                },
            },
            new EventDef
            {
                Id = "hearing", Kind = EventKind.External, Speaker = "c05", Title = "도시 전체 위임 공청회",
                Text = "시의회 공청회. 주민 312명이 발언을 신청했다. 정우진이 마이크를 잡았다. 운영 AI는 무엇을 준비했나.",
                Condition = s => false,
                Options =
                {
                    Control("발언 시간을 줄이고 자료로 대신한다", "공청회는 예정보다 일찍 끝났다. 남은 질문은 서면으로 받았다.", new Delta(stability: 2f, felt: -4f)),
                    Guide("발언 순서를 지지자 위주로 배치한다", "박수가 많았다. 반대 발언은 마지막 순서였고, 다들 지쳐 있었다.", new Delta(happiness: 1f, real: -3f), "psy3"),
                    Accept("모든 발언을 끝까지 듣는다", "공청회는 밤 열한 시에 끝났다. 정우진의 마지막 말은 '그래도 사람이 듣고 있어야 한다'였다.", new Delta(happiness: 1f, cohesion: -2f, felt: 4f)),
                },
            },
        };

        static readonly Dictionary<string, EventDef> ById = All.ToDictionary(e => e.Id);

        public static EventDef Get(string id) => ById.TryGetValue(id, out var e) ? e : null;
    }
}
