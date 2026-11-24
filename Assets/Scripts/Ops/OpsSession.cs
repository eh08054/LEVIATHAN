using System;
using System.Collections.Generic;
using System.Linq;

namespace Sector.Ops
{
    public enum Outcome
    {
        None,
        Collapsed, // 사회 안정 0: 폭동으로 붕괴
        Revoked,   // 위임 게이지가 바닥 아래로: 권한 회수
        Cleared,   // 위임 최고 단계: 구역 확대
        Ended,     // 5년이 지남
    }

    public enum UnlockKind
    {
        Field,
        Slot,
        Zone,
    }

    public sealed class ActivePolicy
    {
        public int Intensity;
        public int StartedDay;
        // Week number in which the intensity last changed (one change per week).
        public int ChangedWeek = -1;
    }

    // What the monthly settlement shows.
    public sealed class MonthlyReport
    {
        public DateTime Month;
        public Indices Before;
        public Indices After;
        public float EnergyChange;
        public float InfoChange;
        public int Events;
        public float Gauge;
        public float Needed;
        public int RiseStreak;
        public int FallStreak;
        public string Judgment;
        public string Unlock;
        // A+ .. D: how far the index sum is above or below the next threshold (see OpsSession.GradeFor).
        public string Grade = "";
        // Events that fired this month, newest last.
        public List<string> EventIds = new List<string>();
        // Group satisfaction at the start and end of the month (Group order), managed population.
        public float[] GroupsBefore = new float[0];
        public float[] GroupsAfter = new float[0];
        public float PopulationBefore, PopulationAfter;
        // Each policy's (and "link:<name>") effect summed over the month.
        public Dictionary<string, Delta> Effects = new Dictionary<string, Delta>();
        // Goals judged at this settlement (not saved).
        public List<GoalResult> Goals = new List<GoalResult>();
    }

    // One playthrough of era 1 (2041 pilot district): indices, resources, policy, research, facilities, delegation.
    public sealed partial class OpsSession
    {
        public readonly Indices Indices = new Indices();
        public int Day;
        public float Energy;
        public float Info;
        public int Stage = 1;
        public int RiseStreak;
        public int FallStreak;
        public int Slots = OpsTuning.StartSlots;
        // Every zone is watched and run from the start; delegation adds facility sites instead of zones.
        public int Zones => Society.Zones.Count;
        public int Sites;

        // The gene design district opens with the gene counselling doctrine; the other ten from the first day.
        public bool ZoneOpen(int zone) => !Society.Zones[zone].Late || HasResearch("life_gene");
        public readonly List<Field> OpenFields = new List<Field> { Field.Security, Field.CityCare, Field.Welfare, Field.Construction };
        public readonly List<UnlockKind> Unlocks = new List<UnlockKind>();
        public readonly Dictionary<string, ActivePolicy> Active = new Dictionary<string, ActivePolicy>();
        public readonly HashSet<string> Done = new HashSet<string>();
        public string CurrentResearch;
        public float ResearchLeft; // days of work at speed 1
        public readonly List<string> Built = new List<string>();
        public string PendingEvent;
        public readonly HashSet<string> Seen = new HashSet<string>();
        public readonly HashSet<string> Profiles = new HashSet<string>();
        public int LastEventDay = -OpsTuning.EventCooldownDays;
        public bool PolicyWindowOpen = true;
        public Outcome Outcome;
        public readonly List<string> Log = new List<string>();
        // Weekly snapshots for the sparklines.
        public readonly List<Indices> History = new List<Indices>();
        public MonthlyReport Report;
        // The latest monthly report, kept after it is closed (not saved; a loaded game has it only while pending).
        public MonthlyReport LastReport;
        public readonly Rng Rng;

        // Running totals since the last monthly report.
        Indices monthStart;
        float monthEnergy, monthInfo;
        int monthEvents;
        readonly List<string> monthEventIds = new List<string>();

        // Each fixed resident's mood at every weekly tick (newest last, at most 26).
        public readonly Dictionary<string, List<float>> MoodHistory = new Dictionary<string, List<float>>();

        // Forecasts run quietly: no events, monthly reports close themselves.
        public bool Quiet;

        public event Action<string> Logged;

        public static readonly DateTime Start = new DateTime(OpsTuning.StartYear, 1, 1);
        public static readonly DateTime End = Start.AddYears(OpsTuning.Years);

        public DateTime Date => Start.AddDays(Day);
        public int Week => Day / 7;
        public bool IsOver => Outcome != Outcome.None;

        public OpsSession(ulong seed = 0)
        {
            Rng = new Rng(seed == 0 ? (ulong)DateTime.Now.Ticks : seed);
            foreach (var index in IndexNames.All)
                Indices[index] = OpsTuning.Start[index];
            Energy = OpsTuning.StartEnergy;
            History.Add(Indices.Copy());
            BeginMonth();
            Write("시범 운영 개시. 관리 구역: 1. 주민 불만 지수가 높다.");
            StartAudit();
            OfferDirectives();
        }

        void BeginMonth()
        {
            monthStart = Indices.Copy();
            monthEnergy = Energy;
            monthInfo = Info;
            monthEvents = 0;
            monthEventIds.Clear();
            BeginMonthSociety();
        }

        public void Write(string line)
        {
            var entry = $"{Date:yyyy.MM.dd} {line}";
            Log.Add(entry);
            if (Log.Count > 200)
                Log.RemoveAt(0);
            Logged?.Invoke(entry);
        }

        // ---------------------------------------------------------------- time

        // Advances one day. Does nothing while an event or the monthly report waits, or the game is over.
        public void AdvanceDay()
        {
            if (IsOver || PendingEvent != null || PendingScene != null || Report != null)
                return;
            // Policies can be switched only while the month-start pause lasts.
            PolicyWindowOpen = false;
            int month = Date.Month;
            Day++;
            ProgressResearch();
            DistrictDaily();
            if (Day % 7 == 0)
                Weekly();
            if (!IsOver && Date.Month != month)
                Monthly();
            if (!IsOver && Date >= End)
            {
                Outcome = Stage >= OpsTuning.MaxStage ? Outcome.Cleared : Outcome.Ended;
                Write(Outcome == Outcome.Cleared
                    ? "시범 운영 5년이 끝났다. 시는 운영 AI에게 도시 전체를 맡기기로 했다."
                    : "시범 운영 5년이 끝났다.");
                return;
            }
            if (!IsOver)
                TensionDaily();
            if (!IsOver && !CrisisDaily() && !FireSpark() && !RollScene())
                RollEvent();
        }

        public float EnergyCap => OpsTuning.StartEnergyCap + Built.Sum(id => Facilities.Get(id).Storage)
                                  + 50f * Perks.Count(p => p.StartsWith("grid"));

        public float PowerPerWeek =>
            OpsTuning.BasePower + Built.Sum(id => Facilities.Get(id).Power) + Done.Sum(id => Research.Get(id).PowerBoost);

        public float UpkeepCut => Done.Sum(id => Research.Get(id).UpkeepCut);

        public float PolicyUpkeep(string id, int intensity) => Policies.Get(id).Upkeep * intensity * (1f - UpkeepCut);

        public float UpkeepPerWeek =>
            Active.Sum(p => PolicyUpkeep(p.Key, p.Value.Intensity)) + Built.Sum(id => Facilities.Get(id).Upkeep);

        public float InfoPerWeek =>
            OpsTuning.InfoPerWeek * Indices.Average / 100f + Enumerable.Range(0, Built.Count).Sum(i => Facilities.Get(Built[i]).Info * FacilityBoost(i))
            + Done.Sum(id => Research.Get(id).InfoBoost);

        // The week's change to the indices before drift: policies, facilities, finished research.
        public Delta PlannedWeekly
        {
            get
            {
                var delta = new Delta();
                foreach (var id in Active.Keys)
                    delta += EffectiveWeekly(id);
                foreach (var link in ActiveLinks)
                    delta += link.Weekly;
                for (int i = 0; i < Built.Count; i++)
                    delta += Facilities.Get(Built[i]).Weekly * FacilityBoost(i);
                foreach (var id in Done)
                    delta += Research.Get(id).Passive;
                return delta;
            }
        }

        void Weekly()
        {
            var delta = PlannedWeekly;
            foreach (var index in IndexNames.All)
            {
                float v = Indices[index], d = delta[index];
                d *= d > 0f ? Math.Min(1f, (100f - v) / OpsTuning.SoftEdge) : Math.Min(1f, v / OpsTuning.SoftEdge);
                delta[index] = d + (OpsTuning.Rest[index] - v) * OpsTuning.RestPull[index];
            }
            delta.Stability += (Indices[Index.Happiness] - OpsTuning.GrumbleHappinessPoint) * OpsTuning.GrumblePerHappiness
                               - OpsTuning.GrumbleBase;
            delta.Happiness += (PopulationSatisfaction - Indices[Index.Happiness]) * OpsTuning.HappinessFromGroups;
            Indices.Apply(delta);
            RecordWeekEffects();
            SocietyWeekly();
            TensionWeekly();
            StoryWeekly();

            Energy += PowerPerWeek - UpkeepPerWeek;
            if (Energy < 0f)
            {
                Energy = 0f;
                CutForPower();
            }
            Energy = Math.Min(Energy, EnergyCap);
            Info += InfoPerWeek;
            InfoEarned += InfoPerWeek;
            DistrictWeekly();

            History.Add(Indices.Copy());
            foreach (var resident in Residents.Cast)
            {
                if (!MoodHistory.TryGetValue(resident.Id, out var moods))
                    MoodHistory[resident.Id] = moods = new List<float>();
                moods.Add(Mood(resident));
                if (moods.Count > 26)
                    moods.RemoveAt(0);
            }
            if (History.Count > 60)
                History.RemoveAt(0);
            CheckCollapse();
            CheckGoals();
        }

        // Out of energy: the most intense policy steps down.
        void CutForPower()
        {
            var top = Active.OrderByDescending(p => p.Value.Intensity).ThenByDescending(p => Policies.Get(p.Key).Upkeep)
                .Select(p => p.Key).FirstOrDefault();
            if (top == null)
                return;
            int level = Active[top].Intensity - 1;
            SetIntensity(top, level, force: true);
            Write(level > 0
                ? $"에너지 부족: '{Policies.Get(top).Name}' 강도를 {level}(으)로 낮췄다."
                : $"에너지 부족: '{Policies.Get(top).Name}'을(를) 중단했다.");
        }

        void CheckCollapse()
        {
            if (Outcome == Outcome.None && Indices[Index.Stability] <= 0f)
            {
                Outcome = Outcome.Collapsed;
                Write("사회 안정 지수 0. 폭동이 구역 전체로 번졌다. 시범 운영이 중단된다.");
            }
        }

        // ---------------------------------------------------------------- monthly settlement

        // What the city can see: every index except real autonomy (that one is the AI's own, hidden progress).
        public float Gauge => Indices.Sum - Indices[Index.AutonomyReal];

        public float NextThreshold => Stage < OpsTuning.MaxStage ? OpsTuning.RiseAt[Stage - 1] : float.PositiveInfinity;

        void Monthly()
        {
            var report = new MonthlyReport
            {
                Month = Date.AddDays(-1), Before = monthStart, After = Indices.Copy(),
                EnergyChange = Energy - monthEnergy, InfoChange = Info - monthInfo, Events = monthEvents,
                EventIds = new List<string>(monthEventIds),
                GroupsBefore = (float[])monthGroups.Clone(), GroupsAfter = (float[])GroupSat.Clone(),
                PopulationBefore = monthPopulation, PopulationAfter = ManagedPopulation,
                Effects = new Dictionary<string, Delta>(MonthEffects),
                Gauge = Gauge, Needed = NextThreshold,
            };

            if (Gauge >= NextThreshold)
                RiseStreak++;
            else
                RiseStreak = 0;
            if (Indices[Index.Stability] < OpsTuning.FallStability)
                FallStreak++;
            else
                FallStreak = 0;

            if (RiseStreak >= OpsTuning.StreakMonths)
            {
                RiseStreak = 0;
                FallStreak = 0;
                report.Unlock = StageUp();
                report.Judgment = $"위임 단계 상승: {Stage}단계";
            }
            else if (FallStreak >= OpsTuning.FallMonths)
            {
                FallStreak = 0;
                RiseStreak = 0;
                report.Judgment = StageDown();
            }
            else if (RiseStreak > 0)
                report.Judgment = $"위임 기준 충족 {RiseStreak}/{OpsTuning.StreakMonths}개월";
            else if (FallStreak > 0)
                report.Judgment = $"경고: 사회 안정 기준 미달 {FallStreak}/{OpsTuning.FallMonths}개월";
            else
                report.Judgment = "위임 단계 유지";
            report.RiseStreak = RiseStreak;
            report.FallStreak = FallStreak;

            report.Grade = GradeFor(report.Gauge, report.Needed, Indices[Index.Stability]);
            SettleGoals(report);
            Report = report;
            LastReport = report;
            PolicyWindowOpen = true;
            BeginMonth();
            Write($"{report.Month:yyyy년 M월} 결산. {report.Judgment}");
        }

        public static readonly Field[][] FieldBatches =
        {
            new[] { Field.Labor, Field.Science },
            new[] { Field.Law, Field.Education, Field.Media },
        };

        static readonly UnlockKind[] Rotation = { UnlockKind.Field, UnlockKind.Slot, UnlockKind.Zone };

        public UnlockKind NextUnlock => Rotation[Unlocks.Count % Rotation.Length];

        string StageUp()
        {
            Stage++;
            var kind = NextUnlock;
            Unlocks.Add(kind);
            string text;
            switch (kind)
            {
                case UnlockKind.Field:
                    int batch = Unlocks.Count(u => u == UnlockKind.Field) - 1;
                    var fields = FieldBatches[Math.Min(batch, FieldBatches.Length - 1)].Where(f => !OpenFields.Contains(f)).ToList();
                    OpenFields.AddRange(fields);
                    text = "새 분야 위임: " + string.Join(", ", fields.Select(Policies.FieldName));
                    break;
                case UnlockKind.Slot:
                    Slots++;
                    text = $"정책 슬롯 +1 (총 {Slots})";
                    break;
                default:
                    Sites++;
                    text = $"시설 부지 확장: 구역마다 +1칸 (구역당 {ZoneSlots}칸)";
                    break;
            }
            Write(text);
            if (Stage >= OpsTuning.MaxStage)
                Write("위임 최고 단계. 남은 것은 시범 운영 마지막 날의 최종 결정이다.");
            return text;
        }

        string StageDown()
        {
            Stage--;
            if (Stage < 1)
            {
                Stage = 1;
                Outcome = Outcome.Revoked;
                Write("시청이 운영 권한을 회수했다.");
                return "권한 회수";
            }
            if (Unlocks.Count > 0)
            {
                var kind = Unlocks[Unlocks.Count - 1];
                Unlocks.RemoveAt(Unlocks.Count - 1);
                switch (kind)
                {
                    case UnlockKind.Field:
                        int batch = Unlocks.Count(u => u == UnlockKind.Field);
                        foreach (var field in FieldBatches[Math.Min(batch, FieldBatches.Length - 1)])
                        {
                            OpenFields.Remove(field);
                            foreach (var id in Active.Keys.Where(k => Policies.Get(k).Field == field).ToList())
                                Active.Remove(id);
                        }
                        break;
                    case UnlockKind.Slot:
                        Slots--;
                        while (Active.Count > Slots)
                            Active.Remove(Active.OrderBy(p => p.Value.StartedDay).Last().Key);
                        break;
                    default:
                        Sites = Math.Max(0, Sites - 1);
                        break;
                }
            }
            var text = $"위임 단계 하락: {Stage}단계. 마지막으로 넘겨받은 권한을 돌려준다.";
            Write(text);
            return text;
        }

        // The settlement screen was read; time can run again.
        // The monthly grade: the index sum against the next delegation threshold; low stability caps it.
        public static string GradeFor(float gauge, float needed, float stability)
        {
            float margin = float.IsInfinity(needed) ? 30f : gauge - needed;
            string grade = margin >= 20f ? "A+" : margin >= 10f ? "A" : margin >= 0f ? "B+" : margin >= -10f ? "B"
                : margin >= -25f ? "C" : "D";
            if (stability < OpsTuning.FallStability && string.CompareOrdinal(grade, "C") < 0)
                grade = "C";
            return grade;
        }

        // How well the AI can predict residents now: fitted behavior and few free choices make people predictable.
        public float PredictionAccuracy =>
            Math.Min(99.9f, 35f + Indices[Index.Cohesion] * 0.4f + (100f - Indices[Index.AutonomyReal]) * 0.25f);

        // Plays the next weeks on a copy of this session, with `change` applied first (e.g. a policy switched on),
        // and returns the weekly index snapshots. No events fire in the copy.
        public List<Indices> Forecast(Action<OpsSession> change, int weeks)
        {
            var copy = Load(Save());
            if (copy == null)
                return new List<Indices> { Indices.Copy() };
            var result = new List<Indices> { copy.Indices.Copy() };
            copy.Quiet = true;
            copy.PendingEvent = null;
            copy.PendingScene = null;
            copy.PendingZone = -1;
            copy.PendingCrisis = null;
            copy.Report = null;
            change?.Invoke(copy);
            int target = copy.Day + weeks * 7;
            while (copy.Day < target && !copy.IsOver)
            {
                copy.AdvanceDay();
                copy.Report = null;
                if (copy.Day % 7 == 0)
                    result.Add(copy.Indices.Copy());
            }
            return result;
        }

        // For forecasts: switch a policy on or off regardless of the window and slots.
        public void ForceActive(string id, bool on)
        {
            if (on && !Active.ContainsKey(id))
                Active[id] = new ActivePolicy { Intensity = 1, StartedDay = Day, ChangedWeek = Week };
            else if (!on)
                Active.Remove(id);
        }

        public void CloseReport()
        {
            Report = null;
        }

        // ---------------------------------------------------------------- policy

        public bool IsActive(string id) => Active.ContainsKey(id);

        public int IntensityOf(string id) => Active.TryGetValue(id, out var p) ? p.Intensity : 0;

        public int WeeksActive(string id) => Active.TryGetValue(id, out var p) ? (Day - p.StartedDay) / 7 : -1;

        public bool IsUnlocked(PolicyDef def) => def.Requires == null || Done.Contains(def.Requires);

        public bool CanEnable(string id, out string reason)
        {
            var def = Policies.Get(id);
            reason = null;
            if (IsActive(id))
                reason = "이미 시행 중";
            else if (!OpenFields.Contains(def.Field))
                reason = "위임되지 않은 분야";
            else if (!IsUnlocked(def))
                reason = $"연구 필요: {Research.Get(def.Requires).Name}";
            else if (!PolicyWindowOpen)
                reason = "정책은 월간 결산 때만 바꿀 수 있다";
            else if (Active.Count >= Slots)
                reason = "정책 슬롯이 가득 찼다";
            return reason == null;
        }

        // Could be switched on if a slot were free (delegated field, researched, not running).
        public bool CanEnableIgnoringSlots(string id)
        {
            var def = Policies.Get(id);
            return !IsActive(id) && OpenFields.Contains(def.Field) && IsUnlocked(def);
        }

        public bool Enable(string id)
        {
            if (!CanEnable(id, out _))
                return false;
            Active[id] = new ActivePolicy { Intensity = 1, StartedDay = Day, ChangedWeek = Week };
            Write($"정책 시행: {Policies.Get(id).Name} [{Policies.KindName(Policies.Get(id).Kind)}]");
            return true;
        }

        public bool Disable(string id)
        {
            if (!IsActive(id) || !PolicyWindowOpen)
                return false;
            Active.Remove(id);
            Write($"정책 중단: {Policies.Get(id).Name}");
            return true;
        }

        public bool CanChangeIntensity(string id) => Active.TryGetValue(id, out var p) && p.ChangedWeek != Week;

        // force: settlement rules and events can change it outside the weekly window.
        public bool SetIntensity(string id, int level, bool force = false)
        {
            if (!Active.TryGetValue(id, out var policy))
                return false;
            level = Math.Min(level, Policies.MaxIntensity);
            if (!force && (policy.ChangedWeek == Week || level < 1))
                return false;
            if (level < 1)
            {
                Active.Remove(id);
                return true;
            }
            policy.Intensity = level;
            policy.ChangedWeek = Week;
            return true;
        }

        public float ResearchSpeed =>
            (1f + Active.Sum(p => Policies.Get(p.Key).ResearchSpeed * p.Value.Intensity)) * (Perks.Contains("fastresearch") ? 1.2f : 1f);

        // ---------------------------------------------------------------- research

        public bool HasResearch(string id) => Done.Contains(id);

        // A doctrine whose partner was taken (finished or running) can never be researched.
        public bool IsForsaken(ResearchDef def) =>
            def.Excludes != null && (Done.Contains(def.Excludes) || CurrentResearch == def.Excludes);

        // Finished doctrines, by lean (for the route).
        public int DoctrinesOf(Lean lean) => Done.Count(id => Research.Get(id)?.Lean == lean);

        public bool CanResearch(string id, out string reason)
        {
            var def = Research.Get(id);
            reason = null;
            if (Done.Contains(id))
                reason = "완료";
            else if (CurrentResearch == id)
                reason = "진행 중";
            else if (IsForsaken(def))
                reason = $"다른 교리를 택했다: {Research.Get(def.Excludes).Name}";
            else if (CurrentResearch != null)
                reason = "다른 연구가 진행 중";
            else if (def.After.Length > 0 && !def.After.Any(Done.Contains))
                reason = "선행 연구: " + string.Join(" 또는 ", def.After.Select(a => Research.Get(a).Name));
            else if (Info < def.Info)
                reason = $"정보 부족 ({def.Info:0} 필요)";
            else if (Energy < def.Energy)
                reason = $"에너지 부족 ({def.Energy:0} 필요)";
            return reason == null;
        }

        public bool StartResearch(string id)
        {
            if (!CanResearch(id, out _))
                return false;
            var def = Research.Get(id);
            Info -= def.Info;
            Energy -= def.Energy;
            CurrentResearch = id;
            ResearchLeft = def.Weeks * 7f;
            Write($"연구 시작: {Research.BranchName(def.Branch)} · {def.Name}");
            return true;
        }

        public float ResearchProgress =>
            CurrentResearch == null ? 0f : 1f - ResearchLeft / (Research.Get(CurrentResearch).Weeks * 7f);

        void ProgressResearch()
        {
            if (CurrentResearch == null)
                return;
            ResearchLeft -= ResearchSpeed;
            if (ResearchLeft > 0f)
                return;
            var def = Research.Get(CurrentResearch);
            Done.Add(CurrentResearch);
            CurrentResearch = null;
            ResearchLeft = 0f;
            var unlocks = Research.Unlocks(def.Id).Select(p => p.Name).ToList();
            Write($"연구 완료: {def.Name}" + (unlocks.Count > 0 ? $" — 유도 정책 해금: {string.Join(", ", unlocks)}" : $" — 개념 '{def.Concept}' 습득"));
        }

        // ---------------------------------------------------------------- facilities

        // Zone of each built facility (same order as Built).
        public readonly List<int> BuiltZone = new List<int>();

        public int ZoneSlots => OpsTuning.FacilitySlotsPerZone + Sites;
        public int FacilitySlots => ZoneSlots * Zones;
        public int UsedSlots(int zone) => BuiltZone.Count(z => z == zone);

        public static bool Suits(string facilityId, int zone) => Society.Zones[zone].Affinity.Contains(facilityId);

        // A facility in a zone it suits works half again as well.
        public float FacilityBoost(int builtIndex) =>
            builtIndex < BuiltZone.Count && Suits(Built[builtIndex], BuiltZone[builtIndex]) ? 1.5f : 1f;

        // The zone a facility would go to when none is named: a suited zone with room, else any zone with room.
        public int BestZone(string id)
        {
            var open = Enumerable.Range(0, Zones).Where(z => ZoneOpen(z) && UsedSlots(z) < ZoneSlots).ToList();
            if (open.Count == 0)
                return -1;
            var suited = open.Where(z => Suits(id, z)).ToList();
            return (suited.Count > 0 ? suited : open).OrderBy(UsedSlots).First();
        }

        public bool CanBuild(string id, out string reason) => CanBuild(id, -1, out reason);

        public bool CanBuild(string id, int zone, out string reason)
        {
            var def = Facilities.Get(id);
            reason = null;
            if (zone < 0)
                zone = BestZone(id);
            if (def.Perk != null && !Perks.Contains(def.Perk))
                reason = "목표 보상으로 해금";
            else if (zone >= 0 && !ZoneOpen(zone))
                reason = "아직 열리지 않은 지구 (유전 상담 연구 필요)";
            else if (zone < 0 || UsedSlots(zone) >= ZoneSlots)
                reason = "이 지구에 부지가 없다 (위임으로 부지 확장)";
            else if (Energy < def.Cost)
                reason = $"에너지 부족 ({def.Cost:0} 필요)";
            return reason == null;
        }

        public bool Build(string id, int zone = -1)
        {
            if (zone < 0)
                zone = BestZone(id);
            if (!CanBuild(id, zone, out _))
                return false;
            Energy -= Facilities.Get(id).Cost;
            Built.Add(id);
            BuiltZone.Add(zone);
            Write($"시설 건설: {Facilities.Get(id).Name} ({Society.Zones[zone].Name}" + (Suits(id, zone) ? ", 어울리는 구역 +50%)" : ")"));
            return true;
        }

        public bool Demolish(int index)
        {
            if (index < 0 || index >= Built.Count)
                return false;
            Write($"시설 철거: {Facilities.Get(Built[index]).Name}");
            Built.RemoveAt(index);
            if (index < BuiltZone.Count)
                BuiltZone.RemoveAt(index);
            return true;
        }

        // ---------------------------------------------------------------- events

        public float EventChance
        {
            get
            {
                float chance = OpsTuning.EventBase
                               + Math.Max(0f, 60f - Indices[Index.Stability]) * OpsTuning.EventPerStability
                               + Math.Max(0f, 60f - Indices[Index.Happiness]) * OpsTuning.EventPerHappiness
                               + Active.Sum(p => Policies.Get(p.Key).UnrestChance * p.Value.Intensity)
                               + Done.Sum(id => Research.Get(id).UnrestChance)
                               + GroupUnrest;
                return Math.Max(OpsTuning.EventMin, Math.Min(OpsTuning.EventMax, chance));
            }
        }

        void RollEvent()
        {
            if (Quiet)
                return;
            if (Day - LastEventDay < OpsTuning.EventCooldownDays || Report != null)
                return;
            // Story events first: each fires once, the first day its condition holds.
            var story = OpsEvents.All.FirstOrDefault(e => !e.Random && !Seen.Contains(e.Id) && e.Condition(this));
            if (story != null)
            {
                Fire(story);
                return;
            }
            if (!Rng.Chance(EventChance))
                return;
            // Crowd events (protests, strikes, riots) come only from sparks now; the random pool keeps small complaints.
            var pool = OpsEvents.All.Where(e => e.Random && e.Kind != EventKind.Crowd && e.Condition(this)).ToList();
            if (pool.Count == 0)
                return;
            float total = pool.Sum(EventWeight), pick = Rng.Value() * total;
            foreach (var e in pool)
            {
                pick -= EventWeight(e);
                if (pick <= 0f)
                {
                    Fire(e);
                    return;
                }
            }
            Fire(pool[pool.Count - 1]);
        }

        public void Fire(EventDef def)
        {
            PendingEvent = def.Id;
            LastEventDay = Day;
            monthEvents++;
            monthEventIds.Add(def.Id);
            EventCounts[(int)def.Kind]++;
            Write($"[{OpsEvents.KindName(def.Kind)}] {def.Title}");
        }

        public bool CanChoose(EventOption option, out string reason)
        {
            reason = null;
            if (option.Requires != null && !Done.Contains(option.Requires))
                reason = $"연구 필요: {Research.Get(option.Requires).Name}";
            else if (Energy < option.Energy)
                reason = $"에너지 부족 ({option.Energy:0} 필요)";
            return reason == null;
        }

        // Answers the waiting event. Returns the result text, or null if that option can't be taken.
        public string Resolve(int optionIndex)
        {
            var def = OpsEvents.Get(PendingEvent);
            if (def == null || optionIndex < 0 || optionIndex >= def.Options.Count)
                return null;
            var option = def.Options[optionIndex];
            if (!CanChoose(option, out _))
                return null;
            Energy -= option.Energy;
            Indices.Apply(CrisisScaled(option.Effect * OpsTuning.EventScale));
            option.Apply?.Invoke(this);
            ApplyEventToGroups(def, option.Tag);
            PendingCrisis = null;
            bool first = Seen.Add(def.Id);
            float info = first ? OpsTuning.InfoFirstEvent : OpsTuning.InfoRepeatEvent;
            Info += info;
            InfoEarned += info;
            PendingEvent = null;
            ReleaseTension(option.Tag);
            Write($"대응 [{OpsEvents.TagName(option.Tag)}] {option.Label}");
            CheckCollapse();
            return option.Result;
        }

        // ---------------------------------------------------------------- residents

        public int SampleCount => OpsTuning.SamplesPerZone * Zones + Built.Sum(id => Facilities.Get(id).Samples);

        public float Mood(ResidentDef resident)
        {
            float mood = Indices[Index.Happiness];
            mood += resident.Likes.Count(IsActive) * 10f;
            mood -= resident.Dislikes.Count(IsActive) * 10f;
            return Math.Max(0f, Math.Min(100f, mood));
        }

        // What a fixed resident says right now: about the newest policy they care about, else their mood.
        public string Line(ResidentDef resident)
        {
            var reaction = Active.Where(p => resident.Reactions.ContainsKey(p.Key))
                .OrderByDescending(p => p.Value.StartedDay).Select(p => resident.Reactions[p.Key]).FirstOrDefault();
            if (reaction != null)
                return reaction;
            float mood = Mood(resident);
            return mood < 40f ? resident.Low : mood > 65f ? resident.High : null;
        }

        // Looking at a fixed resident's file for the first time teaches the AI something.
        public bool ViewProfile(string id)
        {
            if (Residents.Get(id) == null || !Profiles.Add(id))
                return false;
            Info += OpsTuning.InfoFirstProfile;
            InfoEarned += OpsTuning.InfoFirstProfile;
            return true;
        }
    }
}
