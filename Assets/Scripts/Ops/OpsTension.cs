using System;
using System.Collections.Generic;
using System.Linq;

namespace Sector.Ops
{
    // Discontent builds up per zone from visible causes (불만 → 수군거림 → 모임 → 계기 → 사건), shows on camera as
    // anomalies the AI may answer, and turns into an event only after a trigger and a few days' warning.
    public sealed partial class OpsSession
    {
        public readonly float[] ZoneTension = (float[])Tension.Start.Clone();
        public readonly List<Spark> Sparks = new List<Spark>();
        public readonly List<Anomaly> Anomalies = new List<Anomaly>();
        int nextAnomaly = 1;
        // The waiting event's lineage (cause, trigger, zone, leader) when it came from a spark.
        public string PendingCause;
        public int PendingZone = -1;

        public event Action<Anomaly> AnomalySpotted;

        public int TensionStage(int zone) => Sparks.Any(s => s.Zone == zone) ? 4 : Tension.StageOf(ZoneTension[zone]);

        public Spark SparkIn(int zone) => Sparks.FirstOrDefault(s => s.Zone == zone);

        // What pushes a zone's tension up (+) or down (-) each week, biggest first.
        public List<(string name, float value)> TensionCauses(int zone)
        {
            var def = Society.Zones[zone];
            var causes = new List<(string, float)>();
            foreach (var group in Society.Groups)
            {
                int g = (int)group;
                if (GroupSat[g] < 45f && def.Mix[g] > 0.05f)
                    causes.Add(($"{Society.Get(group).Name} 불만 (만족 {GroupSat[g]:0})", def.Mix[g] * (45f - GroupSat[g]) * 0.25f));
            }
            foreach (var id in Active.Keys)
            {
                float r = Society.Groups.Sum(g => def.Mix[(int)g] * GroupReaction(id, g));
                if (r < -0.05f)
                    causes.Add(($"{Policies.Get(id).Name} 시행 {Math.Max(1, WeeksActive(id))}주째", -r * 2.2f));
            }
            foreach (var r in Residents.Cast.Where(r => Society.HomeOf(r.Id) == zone && Dominant(r.Id) == (int)Stance.Resistance))
                causes.Add(($"{r.Name}의 저항", 1.2f));
            for (int i = 0; i < Built.Count; i++)
            {
                if (i >= BuiltZone.Count || BuiltZone[i] != zone)
                    continue;
                var f = Facilities.Get(Built[i]);
                float relief = (f.Weekly.Happiness + f.Weekly.Stability) * 6f * FacilityBoost(i);
                if (relief > 0.05f)
                    causes.Add(($"{f.Name}", -relief));
            }
            // Success brings its own pressure: people move in, new rules take getting used to,
            // and a life with fewer real choices feels stuffy even when nobody can say why.
            float capacity = Capacity(zone);
            float crowd = ZonePop[zone] / capacity - 1.05f;
            if (crowd > 0f)
                causes.Add(($"인구 과밀 (+{(ZonePop[zone] / capacity - 1f) * 100f:0}%)", Math.Min(2.5f, crowd * 6f)));
            causes.AddRange(DistrictCauses(zone));
            int fresh = Active.Keys.Count(id => WeeksActive(id) < 4);
            if (fresh > 0)
                causes.Add(($"새 정책 적응 ({fresh}건)", fresh * 0.4f));
            float real = Indices[Index.AutonomyReal];
            if (real < 40f)
                causes.Add(("이유 모를 답답함", (40f - real) * 0.03f));
            float stability = Indices[Index.Stability], happiness = Indices[Index.Happiness];
            causes.Add(($"사회 안정 {stability:0}", Math.Max(-1.2f, -(stability - 50f) * 0.05f)));
            causes.Add(($"행복 {happiness:0}", Math.Max(-1.2f, -(happiness - 50f) * 0.04f)));
            causes.Add(("시간이 지나며 가라앉음", -1f));
            return causes.Where(c => Math.Abs(c.Item2) >= 0.05f).OrderByDescending(c => c.Item2).ToList();
        }

        public float TensionTrend(int zone) => TensionCauses(zone).Sum(c => c.value) * Society.Zones[zone].Spread;

        void TensionWeekly()
        {
            for (int z = 0; z < Zones; z++)
            {
                if (!ZoneOpen(z))
                    continue;
                ZoneTension[z] = Math.Max(0f, Math.Min(100f, ZoneTension[z] + TensionTrend(z)));
                if (ZoneTension[z] >= Tension.SparkAt && SparkIn(z) == null && !Quiet)
                    MakeSpark(z);
            }
        }

        // The group most likely to act in a zone: many of them, and unhappy.
        public Group AngriestGroup(int zone) =>
            Society.Groups.OrderByDescending(g => Society.Zones[zone].Mix[(int)g] * (100f - GroupSat[(int)g])).First();

        void MakeSpark(int zone)
        {
            var group = AngriestGroup(zone);
            var lines = Tension.Triggers[group];
            var leader = Residents.Cast.Where(r => Society.HomeOf(r.Id) == zone && Dominant(r.Id) == (int)Stance.Resistance)
                .Select(r => r.Id).FirstOrDefault();
            var spark = new Spark
            {
                Zone = zone, Group = group, EventId = Tension.EventFor(group, ZoneTension[zone]), Text = lines[Rng.Range(0, lines.Length)],
                Cause = TensionCauses(zone).FirstOrDefault().name ?? "", Leader = leader, Day = Day, DueDay = Day + Tension.SparkDays,
            };
            Sparks.Add(spark);
            Write($"[계기] {Society.Zones[zone].Name}: {spark.Text}");
            // The trigger itself shows on camera until it blows up or is defused.
            Spot(new Anomaly
            {
                Zone = zone, Kind = AnomalyKind.Spark, Group = group, Day = Day, Expire = spark.DueDay,
                Subject = leader, Text = spark.Text, Cause = spark.Cause, People = PeopleWith(zone, group),
            });
        }

        int PeopleWith(int zone, Group group) =>
            (int)(ZonePop[zone] * Society.Zones[zone].Mix[(int)group] * ZoneTension[zone] / 100f * 0.04f) + 3;

        // Daily: anomalies expire and new ones show up where tension is high; due sparks turn into events.
        void TensionDaily()
        {
            if (Quiet)
                return;
            Anomalies.RemoveAll(a => a.Expire < Day);
            for (int z = 0; z < Zones; z++)
            {
                if (!ZoneOpen(z))
                    continue;
                int stage = Tension.StageOf(ZoneTension[z]);
                if (stage == 0 || Anomalies.Count >= Tension.MaxAnomalies || Anomalies.Any(a => a.Zone == z && a.Kind != AnomalyKind.Spark))
                    continue;
                if (!Rng.Chance((ZoneTension[z] - 15f) / 500f * Society.Zones[z].Spread))
                    continue;
                var group = AngriestGroup(z);
                var kind = stage >= 3 ? AnomalyKind.Gathering : stage == 2 ? AnomalyKind.Whisper : AnomalyKind.Grumble;
                var subject = Residents.Cast.Where(r => Society.HomeOf(r.Id) == z && Tension.ResidentGroup[r.Id] == group)
                    .Select(r => r.Id).FirstOrDefault();
                if (subject != null && !Rng.Chance(0.5f))
                    subject = null;
                Spot(new Anomaly
                {
                    Zone = z, Kind = kind, Group = group, Day = Day, Expire = Day + Tension.AnomalyDays, Subject = subject,
                    Text = AnomalyText(kind, group, subject), Cause = TensionCauses(z).FirstOrDefault().name ?? "",
                    People = PeopleWith(z, group),
                });
            }
        }

        void Spot(Anomaly anomaly)
        {
            anomaly.Id = nextAnomaly++;
            Anomalies.Add(anomaly);
            AnomalySpotted?.Invoke(anomaly);
        }

        static string AnomalyText(AnomalyKind kind, Group group, string subject)
        {
            string who = Residents.Get(subject)?.Name ?? $"{Society.Get(group).Name} 주민 한 명";
            switch (kind)
            {
                case AnomalyKind.Grumble: return $"{who}이(가) 혼자 중얼거리며 같은 자리를 오래 서성인다.";
                case AnomalyKind.Whisper: return $"{who}와(과) 이웃들이 골목에서 낮은 목소리로 이야기한다. 화면을 의식한다.";
                default: return $"{Society.Get(group).Name} 여럿이 저녁마다 같은 장소에 모인다. 인원이 늘고 있다.";
            }
        }

        // Spark due: the event fires with its lineage. Called daily before story scenes and random events.
        bool FireSpark()
        {
            if (Quiet || PendingEvent != null || Day - LastEventDay < OpsTuning.EventCooldownDays)
                return false;
            var spark = Sparks.FirstOrDefault(s => s.DueDay <= Day);
            var def = spark == null ? null : OpsEvents.Get(spark.EventId);
            if (def == null)
                return false;
            Sparks.Remove(spark);
            Anomalies.RemoveAll(a => a.Kind == AnomalyKind.Spark && a.Zone == spark.Zone);
            PendingZone = spark.Zone;
            PendingCause = $"원인: {spark.Cause} · 계기: {spark.Text} · {Society.Zones[spark.Zone].Name}"
                           + (spark.Leader != null ? $" · 주동: {Residents.Get(spark.Leader)?.Name}" : "");
            Fire(def);
            return true;
        }

        // After a spark's event is answered, the zone lets off steam (more when people were heard).
        void ReleaseTension(ResponseTag tag)
        {
            if (PendingZone < 0)
                return;
            int z = PendingZone;
            ZoneTension[z] = Math.Max(0f, ZoneTension[z] - 35f - (tag == ResponseTag.Accept ? 10f : tag == ResponseTag.Control ? 15f : 5f));
            PendingZone = -1;
            PendingCause = null;
        }

        public bool CanRespond(Anomaly anomaly, AnomalyResponse response, out string reason)
        {
            reason = null;
            if (!Anomalies.Contains(anomaly))
                reason = "이미 지나갔다";
            else if (Energy < response.Energy)
                reason = $"에너지 부족 ({response.Energy:0} 필요)";
            return reason == null;
        }

        // Answers an anomaly. Optional: ignoring one costs nothing, answering always gains something.
        public string Respond(int anomalyId, int responseIndex)
        {
            var anomaly = Anomalies.FirstOrDefault(a => a.Id == anomalyId);
            if (anomaly == null)
                return null;
            var responses = Tension.ResponsesFor(anomaly.Kind);
            if (responseIndex < 0 || responseIndex >= responses.Count)
                return null;
            var r = responses[responseIndex];
            if (!CanRespond(anomaly, r, out _))
                return null;
            Energy -= r.Energy;
            int z = anomaly.Zone;
            ZoneTension[z] = Math.Max(0f, Math.Min(100f, ZoneTension[z] + r.Tension));
            int g = (int)anomaly.Group;
            GroupSat[g] = Math.Max(0f, Math.Min(100f, GroupSat[g] + r.Group));
            Indices.Apply(r.Effect);
            Info += r.Info;
            InfoEarned += r.Info;
            if (anomaly.Subject != null)
                Shift(anomaly.Subject, r.Push, r.PushBy, "관찰 대응");
            if (r.Defuses)
            {
                var spark = SparkIn(z);
                if (spark != null)
                {
                    Sparks.Remove(spark);
                    if (spark.Leader != null && r.Tag == ResponseTag.Control)
                        Shift(spark.Leader, Stance.Resistance, 10f, "계기 진압");
                }
            }
            Anomalies.Remove(anomaly);
            Write($"관찰 대응 [{OpsEvents.TagName(r.Tag)}] {Society.Zones[z].Name}: {r.Label}");
            return r.Result;
        }

        // ---------------------------------------------------------------- save

        void SaveTension(Action<string, string> line)
        {
            line("tension", string.Join(",", ZoneTension.Select(F)));
            line("sparks", string.Join(";", Sparks.Select(s =>
                $"{s.Zone},{(int)s.Group},{s.EventId},{s.Day},{s.DueDay},{s.Leader ?? ""},{Clean(s.Text)},{Clean(s.Cause)}")));
            line("anomalies", string.Join(";", Anomalies.Select(a =>
                $"{a.Id},{a.Zone},{(int)a.Kind},{(int)a.Group},{a.Day},{a.Expire},{a.Subject ?? ""},{a.People},{Clean(a.Text)},{Clean(a.Cause)}")));
            line("eventcause", PendingZone < 0 ? "" : PendingZone.ToString(Inv) + "|" + PendingCause);
        }

        static string Clean(string text) => (text ?? "").Replace(";", " ").Replace(",", " ·");

        void LoadTension(Func<string, string> get)
        {
            var tension = Floats(get("tension"));
            if (tension.Length == ZoneTension.Length)
                tension.CopyTo(ZoneTension, 0);
            foreach (var part in get("sparks").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var p = part.Split(',');
                Sparks.Add(new Spark
                {
                    Zone = int.Parse(p[0], Inv), Group = (Group)int.Parse(p[1], Inv), EventId = p[2], Day = int.Parse(p[3], Inv),
                    DueDay = int.Parse(p[4], Inv), Leader = p[5].Length > 0 ? p[5] : null, Text = p[6], Cause = p[7],
                });
            }
            foreach (var part in get("anomalies").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var p = part.Split(',');
                var a = new Anomaly
                {
                    Id = int.Parse(p[0], Inv), Zone = int.Parse(p[1], Inv), Kind = (AnomalyKind)int.Parse(p[2], Inv),
                    Group = (Group)int.Parse(p[3], Inv), Day = int.Parse(p[4], Inv), Expire = int.Parse(p[5], Inv),
                    Subject = p[6].Length > 0 ? p[6] : null, People = int.Parse(p[7], Inv), Text = p[8], Cause = p[9],
                };
                Anomalies.Add(a);
                nextAnomaly = Math.Max(nextAnomaly, a.Id + 1);
            }
            var cause = get("eventcause");
            int bar = cause.IndexOf('|');
            if (bar > 0)
            {
                PendingZone = int.Parse(cause.Substring(0, bar), Inv);
                PendingCause = cause.Substring(bar + 1);
            }
        }
    }
}
