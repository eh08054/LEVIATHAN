using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Sector.Ops
{
    // Save and load as plain "key=value" lines. The monthly report and the waiting event are kept too.
    public sealed partial class OpsSession
    {
        const int SaveVersion = 1;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static string F(float v) => v.ToString("R", Inv);

        static float P(string s) => float.Parse(s, Inv);

        public string Save()
        {
            var sb = new StringBuilder();
            void Line(string key, string value) => sb.Append(key).Append('=').Append(value).Append('\n');

            Line("version", SaveVersion.ToString(Inv));
            Line("rng", Rng.State.ToString(Inv));
            Line("day", Day.ToString(Inv));
            Line("indices", string.Join(",", IndexNames.All.Select(i => F(Indices[i]))));
            Line("energy", F(Energy));
            Line("info", F(Info));
            Line("stage", $"{Stage},{RiseStreak},{FallStreak},{Slots},{Zones}");
            Line("fields", string.Join(",", OpenFields.Select(f => f.ToString())));
            Line("unlocks", string.Join(",", Unlocks.Select(u => u.ToString())));
            foreach (var pair in Active)
                Line("policy", $"{pair.Key},{pair.Value.Intensity},{pair.Value.StartedDay},{pair.Value.ChangedWeek}");
            Line("done", string.Join(",", Done));
            Line("research", CurrentResearch == null ? "" : $"{CurrentResearch},{F(ResearchLeft)}");
            Line("built", string.Join(",", Built.Select((id, i) => id + "@" + (i < BuiltZone.Count ? BuiltZone[i] : 0).ToString(Inv))));
            Line("sites", Sites.ToString(Inv));
            Line("pending", PendingEvent ?? "");
            Line("seen", string.Join(",", Seen));
            Line("profiles", string.Join(",", Profiles));
            Line("lastevent", LastEventDay.ToString(Inv));
            Line("window", PolicyWindowOpen ? "1" : "0");
            Line("outcome", Outcome.ToString());
            Line("month", $"{string.Join(",", IndexNames.All.Select(i => F(monthStart[i])))};{F(monthEnergy)};{F(monthInfo)};{monthEvents}");
            foreach (var snapshot in History)
                Line("history", string.Join(",", IndexNames.All.Select(i => F(snapshot[i]))));
            foreach (var pair in MoodHistory)
                Line("mood", pair.Key + ":" + string.Join(",", pair.Value.Select(F)));
            Line("monthevents", string.Join(",", monthEventIds));
            Line("groups", string.Join(",", GroupSat.Select(F)));
            Line("zonepop", string.Join(",", ZonePop.Select(F)));
            Line("monthgroups", string.Join(",", monthGroups.Select(F)) + ";" + F(monthPopulation));
            Line("montheffects", Effects(MonthEffects));
            Line("analyzed", string.Join(",", Analyzed.Select(p => p.Key + ":" + p.Value.ToString(Inv))));
            SaveGoals(Line);
            SaveStory(Line);
            SaveTension(Line);
            SaveCrisis(Line);
            foreach (var entry in Log.Skip(Math.Max(0, Log.Count - 60)))
                Line("log", entry.Replace("\n", " "));
            if (Report != null)
            {
                var r = Report;
                Line("report", string.Join("|", new[]
                {
                    r.Month.ToString("yyyy-MM-dd", Inv),
                    string.Join(",", IndexNames.All.Select(i => F(r.Before[i]))),
                    string.Join(",", IndexNames.All.Select(i => F(r.After[i]))),
                    F(r.EnergyChange), F(r.InfoChange), r.Events.ToString(Inv), F(r.Gauge),
                    float.IsInfinity(r.Needed) ? "inf" : F(r.Needed), r.RiseStreak.ToString(Inv), r.FallStreak.ToString(Inv),
                    r.Judgment ?? "", r.Unlock ?? "", r.Grade ?? "", string.Join(",", r.EventIds),
                    string.Join(",", r.GroupsBefore.Select(F)), string.Join(",", r.GroupsAfter.Select(F)),
                    F(r.PopulationBefore) + "," + F(r.PopulationAfter), Effects(r.Effects),
                }));
            }
            return sb.ToString();
        }

        static string Effects(Dictionary<string, Delta> effects) =>
            string.Join(";", effects.Select(p => p.Key + "=" + string.Join(",", IndexNames.All.Select(i => F(p.Value[i])))));

        static Dictionary<string, Delta> ParseEffects(string text)
        {
            var result = new Dictionary<string, Delta>();
            foreach (var part in text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int at = part.LastIndexOf('=');
                var values = part.Substring(at + 1).Split(',');
                var d = new Delta();
                foreach (var index in IndexNames.All)
                    d[index] = P(values[(int)index]);
                result[part.Substring(0, at)] = d;
            }
            return result;
        }

        static float[] Floats(string text) => List(text).Select(P).ToArray();

        static Indices ParseIndices(string text)
        {
            var parts = text.Split(',');
            var indices = new Indices();
            foreach (var index in IndexNames.All)
                indices[index] = P(parts[(int)index]);
            return indices;
        }

        static IEnumerable<string> List(string text) => text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        // Returns null when the text isn't a save this version understands.
        public static OpsSession Load(string text)
        {
            try
            {
                var lines = text.Split('\n').Where(l => l.Contains("=")).Select(l =>
                {
                    int at = l.IndexOf('=');
                    return (key: l.Substring(0, at), value: l.Substring(at + 1).TrimEnd('\r'));
                }).ToList();
                string Get(string key) => lines.FirstOrDefault(l => l.key == key).value ?? "";
                if (Get("version") != SaveVersion.ToString(Inv))
                    return null;

                var s = new OpsSession(ulong.Parse(Get("rng"), Inv));
                s.Log.Clear();
                s.History.Clear();
                s.Rng.State = ulong.Parse(Get("rng"), Inv);
                s.Day = int.Parse(Get("day"), Inv);
                var indices = ParseIndices(Get("indices"));
                foreach (var index in IndexNames.All)
                    s.Indices[index] = indices[index];
                s.Energy = P(Get("energy"));
                s.Info = P(Get("info"));
                var stage = Get("stage").Split(',').Select(v => int.Parse(v, Inv)).ToArray();
                s.Stage = stage[0];
                s.RiseStreak = stage[1];
                s.FallStreak = stage[2];
                s.Slots = stage[3];
                int.TryParse(Get("sites"), NumberStyles.Integer, Inv, out s.Sites);
                s.OpenFields.Clear();
                s.OpenFields.AddRange(List(Get("fields")).Select(f => (Field)Enum.Parse(typeof(Field), f)));
                s.Unlocks.AddRange(List(Get("unlocks")).Select(u => (UnlockKind)Enum.Parse(typeof(UnlockKind), u)));
                foreach (var line in lines.Where(l => l.key == "policy"))
                {
                    var p = line.value.Split(',');
                    s.Active[p[0]] = new ActivePolicy
                    {
                        Intensity = int.Parse(p[1], Inv), StartedDay = int.Parse(p[2], Inv), ChangedWeek = int.Parse(p[3], Inv),
                    };
                }
                foreach (var id in List(Get("done")))
                    s.Done.Add(id);
                var research = Get("research");
                if (research.Length > 0)
                {
                    var r = research.Split(',');
                    s.CurrentResearch = r[0];
                    s.ResearchLeft = P(r[1]);
                }
                foreach (var entry in List(Get("built")))
                {
                    int at = entry.IndexOf('@');
                    s.Built.Add(at < 0 ? entry : entry.Substring(0, at));
                    s.BuiltZone.Add(at < 0 ? 0 : Math.Min(Society.Zones.Count - 1, int.Parse(entry.Substring(at + 1), Inv)));
                }
                s.PendingEvent = Get("pending").Length > 0 ? Get("pending") : null;
                foreach (var id in List(Get("seen")))
                    s.Seen.Add(id);
                foreach (var id in List(Get("profiles")))
                    s.Profiles.Add(id);
                s.LastEventDay = int.Parse(Get("lastevent"), Inv);
                s.PolicyWindowOpen = Get("window") == "1";
                s.Outcome = (Outcome)Enum.Parse(typeof(Outcome), Get("outcome"));
                var month = Get("month").Split(';');
                s.monthStart = ParseIndices(month[0]);
                s.monthEnergy = P(month[1]);
                s.monthInfo = P(month[2]);
                s.monthEvents = int.Parse(month[3], Inv);
                foreach (var line in lines.Where(l => l.key == "mood"))
                {
                    int colon = line.value.IndexOf(':');
                    s.MoodHistory[line.value.Substring(0, colon)] = List(line.value.Substring(colon + 1)).Select(P).ToList();
                }
                s.monthEventIds.AddRange(List(Get("monthevents")));
                var groups = Floats(Get("groups"));
                if (groups.Length == s.GroupSat.Length)
                    groups.CopyTo(s.GroupSat, 0);
                var pops = Floats(Get("zonepop"));
                if (pops.Length == s.ZonePop.Length)
                    pops.CopyTo(s.ZonePop, 0);
                var mg = Get("monthgroups").Split(';');
                if (mg.Length == 2)
                {
                    s.monthGroups = Floats(mg[0]);
                    s.monthPopulation = P(mg[1]);
                }
                foreach (var pair in ParseEffects(Get("montheffects")))
                    s.MonthEffects[pair.Key] = pair.Value;
                foreach (var part in List(Get("analyzed")))
                {
                    var kv = part.Split(':');
                    s.Analyzed[(AnalysisKind)Enum.Parse(typeof(AnalysisKind), kv[0])] = int.Parse(kv[1], Inv);
                }
                s.Goals.Clear();
                s.DirectiveOffer = null;
                s.LoadGoals(Get, lines.Where(l => l.key == "goal").Select(l => l.value));
                s.LoadStory(Get);
                s.LoadTension(Get);
                s.LoadCrisis(Get);
                foreach (var line in lines.Where(l => l.key == "history"))
                    s.History.Add(ParseIndices(line.value));
                foreach (var line in lines.Where(l => l.key == "log"))
                    s.Log.Add(line.value);
                var report = Get("report");
                if (report.Length > 0)
                {
                    var r = report.Split('|');
                    s.Report = new MonthlyReport
                    {
                        Month = DateTime.ParseExact(r[0], "yyyy-MM-dd", Inv), Before = ParseIndices(r[1]), After = ParseIndices(r[2]),
                        EnergyChange = P(r[3]), InfoChange = P(r[4]), Events = int.Parse(r[5], Inv), Gauge = P(r[6]),
                        Needed = r[7] == "inf" ? float.PositiveInfinity : P(r[7]), RiseStreak = int.Parse(r[8], Inv),
                        FallStreak = int.Parse(r[9], Inv), Judgment = r[10], Unlock = r[11].Length > 0 ? r[11] : null,
                        Grade = r.Length > 12 ? r[12] : "",
                        EventIds = r.Length > 13 ? List(r[13]).ToList() : new List<string>(),
                        GroupsBefore = r.Length > 14 ? Floats(r[14]) : new float[0],
                        GroupsAfter = r.Length > 15 ? Floats(r[15]) : new float[0],
                        Effects = r.Length > 17 ? ParseEffects(r[17]) : new Dictionary<string, Delta>(),
                    };
                    if (r.Length > 16)
                    {
                        var pop = Floats(r[16]);
                        s.Report.PopulationBefore = pop[0];
                        s.Report.PopulationAfter = pop[1];
                    }
                }
                s.LastReport = s.Report;
                return s;
            }
            catch (Exception exception) when (exception is FormatException || exception is IndexOutOfRangeException
                                              || exception is ArgumentException || exception is OverflowException)
            {
                return null;
            }
        }
    }
}
