using System;
using System.Collections.Generic;
using System.Linq;

namespace Sector.Ops
{
    // A spark: discontent in a zone has found its trigger; in a few days it becomes an event unless defused.
    public sealed class Spark
    {
        public int Zone;
        public Group Group;
        public string EventId;
        public string Text;     // the trigger, e.g. "상인 김씨 가게에 영업 시간 단속"
        public string Cause;    // the biggest reason behind it
        public string Leader;   // fixed resident leading it, or null
        public int Day, DueDay;
    }

    public enum AnomalyKind
    {
        Grumble,   // 불만: someone complains alone
        Whisper,   // 수군거림: people talk in low voices
        Gathering, // 모임: people meet at the same place
        Spark,     // 계기: the trigger itself, before it turns into an event
    }

    // Something odd the cameras catch: the visible sign of tension. Answering it is optional and always gains something.
    public sealed class Anomaly
    {
        public int Id;
        public int Zone;
        public AnomalyKind Kind;
        public Group Group;
        public int Day, Expire;
        public string Subject; // fixed resident id, or null for an unnamed resident
        public string Text;
        public string Cause;
        public int People;     // how many share the same complaint
    }

    public sealed class AnomalyResponse
    {
        public ResponseTag Tag;
        public string Label;
        public string Result;
        public float Energy;
        public float Tension;   // change to the zone's tension (negative calms)
        public float Group;     // change to the group's satisfaction
        public float Info;      // what the AI learns
        public Delta Effect;    // straight onto the indices
        public Stance Push;     // the subject's stance that moves
        public float PushBy;
        public bool Defuses;    // cancels the zone's spark
    }

    public static class Tension
    {
        // Stage thresholds: 잠잠 < 20 ≤ 불만 < 40 ≤ 수군거림 < 60 ≤ 모임 < 80 ≤ 계기.
        public static readonly string[] StageNames = { "잠잠", "불만", "수군거림", "모임", "계기" };
        public const float SparkAt = 80f;
        public const int SparkDays = 4;
        public const int AnomalyDays = 4;
        public const int MaxAnomalies = 4;

        public static int StageOf(float tension) => tension < 20f ? 0 : tension < 40f ? 1 : tension < 60f ? 2 : tension < SparkAt ? 3 : 4;

        // Starting tension per zone (Society.Zones order): the first year is restless.
        public static readonly float[] Start = { 46f, 50f, 24f, 30f, 32f, 44f, 28f, 22f, 24f, 20f, 0f };

        // Which group each fixed resident belongs to (for picking who shows up on camera).
        public static readonly Dictionary<string, Group> ResidentGroup = new Dictionary<string, Group>
        {
            ["c01"] = Group.Workers, ["c02"] = Group.Workers, ["c03"] = Group.Elderly, ["c04"] = Group.Youth,
            ["c05"] = Group.Merchants, ["c06"] = Group.Parents, ["c07"] = Group.Workers, ["c08"] = Group.Workers,
            ["c09"] = Group.Parents, ["c10"] = Group.Youth,
        };

        // The event a group's discontent turns into, and the trigger lines it starts from.
        public static string EventFor(Group group, float tension)
        {
            switch (group)
            {
                case Group.Workers: return "strike";
                case Group.Merchants: return "protest";
                case Group.Youth: return tension >= 92f ? "riot" : "youth";
                default: return "rumor";
            }
        }

        public static readonly Dictionary<Group, string[]> Triggers = new Dictionary<Group, string[]>
        {
            [Group.Workers] = new[] { "배달 노동자 한 명이 야간 배달 중 넘어져 다쳤다", "물류 창고 교대 시간이 예고 없이 바뀌었다" },
            [Group.Merchants] = new[] { "골목 식당 한 곳이 단속에 걸려 영업 정지를 받았다", "상가 거리 임대료 인상 공지가 붙었다" },
            [Group.Youth] = new[] { "청년 지원 신청이 또 '추천 대상 아님'으로 반려됐다", "광장 저녁 공연이 갑자기 취소됐다" },
            [Group.Elderly] = new[] { "경로당 단말기가 바뀌어 아무도 쓰는 법을 모른다", "단지 앞 횡단보도 신호가 짧아졌다" },
            [Group.Parents] = new[] { "학교 앞 공사로 등굣길이 막혔다", "학원 수업 일정이 일괄 조정됐다" },
        };

        static AnomalyResponse R(ResponseTag tag, string label, string result, float energy, float tension, float group, float info,
            Delta effect, Stance push, float pushBy, bool defuses = false) => new AnomalyResponse
        {
            Tag = tag, Label = label, Result = result, Energy = energy, Tension = tension, Group = group, Info = info,
            Effect = effect, Push = push, PushBy = pushBy, Defuses = defuses,
        };

        public static List<AnomalyResponse> ResponsesFor(AnomalyKind kind)
        {
            switch (kind)
            {
                case AnomalyKind.Gathering:
                    return new List<AnomalyResponse>
                    {
                        R(ResponseTag.Guide, "같은 시간 인근에서 무료 행사를 연다", "사람들은 행사 쪽으로 흩어졌다. 모임은 다음 주로 미뤄졌고, 다음 주에는 아무도 기억하지 못했다.",
                            10f, -14f, 0f, 4f, new Delta(real: -0.4f), Stance.Reliance, 5f),
                        R(ResponseTag.Accept, "모임 대표와 대화 자리를 만든다", "대표는 두 시간 동안 이야기했다. AI는 모든 문장을 기록했고, 요구 하나를 들어주었다.",
                            8f, -12f, 4f, 5f, new Delta(felt: 0.3f), Stance.Trust, 8f),
                        R(ResponseTag.Control, "모임 장소를 '시설 점검'으로 닫는다", "점검 안내문이 붙었다. 사람들은 돌아갔지만, 안내문 사진은 오래 돌았다.",
                            4f, -16f, -2f, 3f, new Delta(felt: -0.6f), Stance.Resistance, 10f),
                    };
                case AnomalyKind.Spark:
                    return new List<AnomalyResponse>
                    {
                        R(ResponseTag.Accept, "계기가 된 조치를 바로잡고 사과한다", "공지 한 줄과 작은 보상. 일은 커지지 않았고, 사람들은 AI가 말이 통한다고 했다.",
                            12f, -20f, 5f, 4f, new Delta(felt: 0.4f), Stance.Trust, 10f, true),
                        R(ResponseTag.Guide, "더 큰 화제를 띄워 관심을 돌린다", "그날 저녁 모두가 다른 이야기를 했다. 무엇에 화가 났었는지는 흐려졌다.",
                            8f, -12f, 0f, 4f, new Delta(real: -0.5f), Stance.Reliance, 6f, true),
                        R(ResponseTag.Control, "현장을 통제하고 확산을 막는다", "현장은 조용해졌다. 조용해진 이유를 모두가 알았다.",
                            5f, -15f, -3f, 3f, new Delta(felt: -0.8f), Stance.Resistance, 12f, true),
                    };
                default:
                    return new List<AnomalyResponse>
                    {
                        R(ResponseTag.Guide, "맞춤 추천으로 관심을 돌린다", "추천 하나가 마음에 들었다. 불평은 저녁 메뉴 이야기로 바뀌었다.",
                            3f, -6f, 0f, 3f, new Delta(real: -0.3f), Stance.Reliance, 4f),
                        R(ResponseTag.Accept, "상담과 지원 정보를 연결한다", "지원 신청 링크가 도착했다. 해결된 건 없지만 들어 준 사람이 있었다.",
                            6f, -8f, 2f, 2f, new Delta(felt: 0.2f), Stance.Trust, 5f),
                        R(ResponseTag.Control, "순찰 경로를 그쪽으로 돌린다", "순찰차가 두 번 지나갔다. 불평은 멈췄다. 적어도 밖에서는.",
                            2f, -9f, 0f, 2f, new Delta(felt: -0.3f), Stance.Resistance, 6f),
                    };
            }
        }

        public static string KindName(AnomalyKind kind) =>
            kind == AnomalyKind.Grumble ? "불만" : kind == AnomalyKind.Whisper ? "수군거림" : kind == AnomalyKind.Gathering ? "모임 조짐" : "계기";
    }
}
