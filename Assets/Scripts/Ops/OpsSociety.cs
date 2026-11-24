using System;
using System.Collections.Generic;
using System.Linq;

namespace Sector.Ops
{
    public enum AnalysisKind
    {
        Discontent, // 불만 원인 분석
        Forecast,   // 사건 예측
    }

    // Groups, zones, policy wear and links, and paid analyses: the layer under the five indices.
    public sealed partial class OpsSession
    {
        // Satisfaction of each group (Group order) and population of each zone (Society.Zones order).
        public readonly float[] GroupSat = Society.GroupDefs.Select(g => g.StartSatisfaction).ToArray();
        public readonly float[] ZonePop = Society.Zones.Select(z => z.Population).ToArray();
        // Each policy's (and link's) weekly effects summed over the current month, for the policy report.
        public readonly Dictionary<string, Delta> MonthEffects = new Dictionary<string, Delta>();
        // Analyses bought, by the month (MonthIndex) they were bought in.
        public readonly Dictionary<AnalysisKind, int> Analyzed = new Dictionary<AnalysisKind, int>();

        float[] monthGroups = Society.GroupDefs.Select(g => g.StartSatisfaction).ToArray();
        float monthPopulation = Society.Zones.Sum(z => z.Population);

        public int MonthIndex => (Date.Year - OpsTuning.StartYear) * 12 + Date.Month;

        // ---------------------------------------------------------------- policy wear and links

        // Seen control wears off as people get used to it; unseen guidance does not.
        public float Habituation(string id)
        {
            if (Policies.Get(id).Kind == PolicyKind.Guide || !IsActive(id))
                return 1f;
            return Math.Max(OpsTuning.HabitFloor, 1f - OpsTuning.HabitPerWeek * WeeksActive(id));
        }

        public Delta EffectiveWeekly(string id) => Policies.Get(id).Weekly * (Math.Max(1, IntensityOf(id)) * Habituation(id));

        public IEnumerable<PolicyLink> ActiveLinks => Society.Links.Where(l => IsActive(l.A) && IsActive(l.B));

        // ---------------------------------------------------------------- groups and zones

        public float ManagedPopulation
        {
            get
            {
                float sum = 0f;
                for (int z = 0; z < Math.Min(Zones, ZonePop.Length); z++)
                    sum += ZonePop[z];
                return sum;
            }
        }

        public float TotalPopulation => ZonePop.Sum();

        public float ZoneSatisfaction(int zone)
        {
            var mix = Society.Zones[zone].Mix;
            float sum = 0f;
            for (int g = 0; g < GroupSat.Length; g++)
                sum += mix[g] * GroupSat[g];
            return Math.Max(0f, Math.Min(100f, sum + DistrictComfort(zone)));
        }

        // Population-weighted satisfaction over the zones the AI runs.
        public float PopulationSatisfaction
        {
            get
            {
                float weight = 0f, sum = 0f;
                for (int z = 0; z < Math.Min(Zones, ZonePop.Length); z++)
                {
                    weight += ZonePop[z];
                    sum += ZonePop[z] * ZoneSatisfaction(z);
                }
                return weight > 0f ? sum / weight : GroupSat.Average();
            }
        }

        // Share of each group among managed people (Group order).
        public float[] GroupShares
        {
            get
            {
                var shares = new float[GroupSat.Length];
                for (int z = 0; z < Math.Min(Zones, ZonePop.Length); z++)
                    for (int g = 0; g < shares.Length; g++)
                        shares[g] += ZonePop[z] * Society.Zones[z].Mix[g];
                float total = shares.Sum();
                for (int g = 0; g < shares.Length; g++)
                    shares[g] = total > 0f ? shares[g] / total : 0f;
                return shares;
            }
        }

        // One policy's weekly pull on a group at its current (or first) intensity, after wear.
        public float GroupReaction(string policyId, Group group)
        {
            if (!Society.Reactions.TryGetValue(policyId, out var r))
                return 0f;
            return r[(int)group] * Math.Max(1, IntensityOf(policyId)) * Habituation(policyId);
        }

        // Everything policies, links and facilities do to a group this week (before the pull toward happiness).
        public float GroupPush(Group group)
        {
            int g = (int)group;
            float push = Active.Keys.Sum(id => GroupReaction(id, group));
            foreach (var link in ActiveLinks)
                if (link.Groups != null)
                    push += link.Groups[g];
            for (int i = 0; i < Built.Count; i++)
                push += Facilities.Get(Built[i]).Weekly.Happiness * FacilityBoost(i) * OpsTuning.FacilityGroupShare;
            return push;
        }

        void SocietyWeekly()
        {
            float happiness = Indices[Index.Happiness];
            foreach (var group in Society.Groups)
            {
                int g = (int)group;
                float v = GroupSat[g] + GroupPush(group) + (happiness - GroupSat[g]) * OpsTuning.GroupPull;
                GroupSat[g] = Math.Max(0f, Math.Min(100f, v));
            }
            // Happy zones draw people in, up to half again their size.
            for (int z = 0; z < ZonePop.Length; z++)
            {
                float growth = (ZoneSatisfaction(z) - 50f) * OpsTuning.PopulationGrowth;
                if (growth > 0f)
                    growth *= Math.Max(0f, 1f - (ZonePop[z] / Capacity(z) - 1f) / 0.5f);
                ZonePop[z] *= 1f + growth;
            }
        }

        public float LowestGroupSatisfaction => GroupSat.Min();

        public Group LowestGroup => Society.Groups.OrderBy(g => GroupSat[(int)g]).First();

        float GroupUnrest => Math.Max(0f, OpsTuning.GroupUnrestPoint - LowestGroupSatisfaction) * OpsTuning.GroupUnrestPerPoint;

        // Unrest events led by an unhappy group come up more often.
        float EventWeight(EventDef e)
        {
            if (!Society.EventGroups.TryGetValue(e.Id, out var group))
                return e.Weight;
            return GroupSat[(int)group] < OpsTuning.GroupUnrestPoint ? e.Weight * 1.8f : e.Weight;
        }

        void ApplyEventToGroups(EventDef def, ResponseTag tag)
        {
            if (!Society.EventGroups.TryGetValue(def.Id, out var group))
                return;
            float change = tag == ResponseTag.Accept ? 4f : tag == ResponseTag.Guide ? 1.5f : -3f;
            int g = (int)group;
            GroupSat[g] = Math.Max(0f, Math.Min(100f, GroupSat[g] + change));
        }

        void BeginMonthSociety()
        {
            monthGroups = (float[])GroupSat.Clone();
            monthPopulation = ManagedPopulation;
            MonthEffects.Clear();
        }

        void RecordWeekEffects()
        {
            foreach (var id in Active.Keys)
                MonthEffects[id] = (MonthEffects.TryGetValue(id, out var d) ? d : new Delta()) + EffectiveWeekly(id);
            foreach (var link in ActiveLinks)
            {
                var key = "link:" + link.Name;
                MonthEffects[key] = (MonthEffects.TryGetValue(key, out var d) ? d : new Delta()) + link.Weekly;
            }
        }

        // ---------------------------------------------------------------- analysis

        public static float AnalysisCost(AnalysisKind kind) => kind == AnalysisKind.Discontent ? 10f : 15f;

        public static string AnalysisName(AnalysisKind kind) => kind == AnalysisKind.Discontent ? "불만 원인 분석" : "사건 예측";

        // Bought this month (results stay readable until the month turns).
        public bool HasAnalysis(AnalysisKind kind) => Analyzed.TryGetValue(kind, out var month) && month == MonthIndex;

        public bool Analyze(AnalysisKind kind)
        {
            if (HasAnalysis(kind) || Info < AnalysisCost(kind))
                return false;
            Info -= AnalysisCost(kind);
            Analyzed[kind] = MonthIndex;
            Write($"분석 실행: {AnalysisName(kind)} (정보 -{AnalysisCost(kind):0})");
            return true;
        }

        // For each group: what pushes it down and up most this week.
        public List<(Group group, float satisfaction, List<(string name, float push)> causes)> Discontent()
        {
            var result = new List<(Group, float, List<(string, float)>)>();
            foreach (var group in Society.Groups.OrderBy(g => GroupSat[(int)g]))
            {
                var causes = Active.Keys.Select(id => (Policies.Get(id).Name, GroupReaction(id, group)))
                    .Concat(ActiveLinks.Where(l => l.Groups != null).Select(l => ("[" + l.Name + "]", l.Groups[(int)group])))
                    .Where(c => Math.Abs(c.Item2) > 0.05f)
                    .OrderBy(c => c.Item2).ToList();
                result.Add((group, GroupSat[(int)group], causes));
            }
            return result;
        }

        // The events most likely in the next `days` days: story events about to fire, then the unrest pool by weight.
        public List<(EventDef def, float chance)> ForecastEvents(int days = 30)
        {
            var list = new List<(EventDef, float)>();
            foreach (var e in OpsEvents.All.Where(e => !e.Random && !Seen.Contains(e.Id) && e.Condition(this)))
                list.Add((e, 0.95f));
            foreach (var spark in Sparks)
            {
                var def = OpsEvents.Get(spark.EventId);
                if (def != null)
                    list.Add((def, 0.97f));
            }
            var pool = OpsEvents.All.Where(e => e.Random && e.Kind != EventKind.Crowd && e.Condition(this)).ToList();
            float total = pool.Sum(EventWeight);
            float any = 1f - (float)Math.Pow(1f - EventChance, days / (float)OpsTuning.EventCooldownDays);
            foreach (var e in pool.OrderByDescending(EventWeight))
                list.Add((e, total > 0f ? any * EventWeight(e) / total : 0f));
            return list;
        }
    }
}
