using System;
using System.Collections.Generic;
using System.Linq;

namespace Sector.Ops
{
    // Forecast crises: each is foreshadowed three times (news, report, overheard words) before it arrives,
    // and how ready the city is softens what it does.
    public sealed partial class OpsSession
    {
        public readonly HashSet<string> CrisisBeatsSeen = new HashSet<string>();
        public string PendingCrisis;

        public event Action<CrisisDef, CrisisBeat> CrisisWarned;

        public int CrisisDay(CrisisDef def) => (int)(def.Date - Start).TotalDays;

        public float CrisisReadiness(string id) => Crises.Get(id)?.Readiness(this) ?? 1f;

        // The next crisis that has been foreshadowed at least once and has not come yet.
        public CrisisDef NextCrisis => Crises.All.FirstOrDefault(c => !Seen.Contains(c.Id) && c.Beats.Select((b, i) => c.Id + ":" + i).Any(CrisisBeatsSeen.Contains));

        bool CrisisDaily()
        {
            if (Quiet)
                return false;
            foreach (var c in Crises.All)
            {
                int day = CrisisDay(c);
                for (int i = 0; i < c.Beats.Count; i++)
                {
                    var key = c.Id + ":" + i;
                    if (Day >= day - c.Beats[i].DaysBefore && Day < day && CrisisBeatsSeen.Add(key))
                    {
                        Write($"[예보 · {c.Beats[i].Channel}] {c.Beats[i].Text}");
                        CrisisWarned?.Invoke(c, c.Beats[i]);
                    }
                }
                if (Day >= day && !Seen.Contains(c.Id) && PendingEvent == null)
                {
                    var def = OpsEvents.Get(c.Id);
                    if (def == null)
                        continue;
                    float ready = c.Readiness(this);
                    PendingCrisis = c.Id;
                    PendingZone = -1;
                    PendingCause = $"예보된 위기 · 준비도 {ready * 100f:0}% · 도움이 된 것: {c.Prepare}";
                    Fire(def);
                    return true;
                }
            }
            return false;
        }

        // A crisis answer: its harms are scaled by readiness (fully ready: 30%, not ready: 130%); gains stay.
        Delta CrisisScaled(Delta effect)
        {
            if (PendingCrisis == null)
                return effect;
            float scale = 1.3f - CrisisReadiness(PendingCrisis);
            var d = effect;
            foreach (var index in IndexNames.All)
            {
                bool harm = index != Index.AutonomyReal ? effect[index] < 0f : effect[index] > 0f;
                if (harm)
                    d[index] = effect[index] * scale;
            }
            return d;
        }

        void SaveCrisis(Action<string, string> line)
        {
            line("crisisbeats", string.Join(",", CrisisBeatsSeen.OrderBy(k => k)));
            line("pendingcrisis", PendingCrisis ?? "");
        }

        void LoadCrisis(Func<string, string> get)
        {
            foreach (var key in List(get("crisisbeats")))
                CrisisBeatsSeen.Add(key);
            PendingCrisis = Crises.Get(get("pendingcrisis")) != null ? get("pendingcrisis") : null;
        }
    }
}
