using System;
using System.Collections.Generic;
using System.Linq;

namespace Sector.Ops
{
    public sealed class StanceChange
    {
        public int Day;
        public string Id;
        public int From; // -1 = 관망
        public int To;
        public string Reason;
    }

    public sealed class OverheardTalk
    {
        public int Day;
        public string Id;
    }

    // The fixed residents' side of the story: where each stands (trust, reliance, suspicion, resistance), the arc
    // scenes that fire one step at a time, and the short talks the AI overhears each week.
    public sealed partial class OpsSession
    {
        public readonly Dictionary<string, float[]> Stances = Story.StartStance.ToDictionary(p => p.Key, p => (float[])p.Value.Clone());
        public readonly List<StanceChange> StanceLog = new List<StanceChange>();
        public readonly HashSet<string> ScenesSeen = new HashSet<string>();
        public string PendingScene;
        public readonly List<OverheardTalk> Overheard = new List<OverheardTalk>();

        // A talk was overheard this week (the screen plays it as speech bubbles if both are on camera).
        public event Action<TalkDef> TalkOverheard;

        public float StanceValue(string id, Stance stance) =>
            Stances.TryGetValue(id, out var v) ? v[(int)stance] : 0f;

        // The strongest stance, or -1 (관망) when none reaches Story.StanceFloor.
        public int Dominant(string id)
        {
            if (!Stances.TryGetValue(id, out var v))
                return -1;
            int best = 0;
            for (int i = 1; i < v.Length; i++)
                if (v[i] > v[best])
                    best = i;
            return v[best] >= Story.StanceFloor ? best : -1;
        }

        public static string StanceLabel(int dominant) => dominant < 0 ? "관망" : Story.StanceName((Stance)dominant);

        public int ArcStep(string id) => ScenesSeen.Count(s => Story.Scene(s)?.Arc == id);

        public void Shift(string id, Stance stance, float amount, string reason = null)
        {
            if (!Stances.TryGetValue(id, out var v))
                return;
            int before = Dominant(id);
            v[(int)stance] = Math.Max(0f, Math.Min(100f, v[(int)stance] + amount));
            NoteStance(id, before, reason);
        }

        void NoteStance(string id, int before, string reason)
        {
            int after = Dominant(id);
            if (after == before || Quiet)
                return;
            StanceLog.Add(new StanceChange { Day = Day, Id = id, From = before, To = after, Reason = reason ?? "" });
            if (StanceLog.Count > 200)
                StanceLog.RemoveAt(0);
            var r = Residents.Get(id);
            Write($"입장 변화: {r?.Name} {StanceLabel(before)} → {StanceLabel(after)}");
        }

        // Weekly: moods, policies and hidden control move every fixed resident a little.
        void StoryWeekly()
        {
            int control = Active.Keys.Count(k => Policies.Get(k).Kind == PolicyKind.Control);
            int guide = Active.Count - control;
            float gap = Math.Max(0f, Indices[Index.AutonomyFelt] - Indices[Index.AutonomyReal]);
            foreach (var r in Residents.Cast)
            {
                if (!Stances.TryGetValue(r.Id, out var v))
                    continue;
                int before = Dominant(r.Id);
                float mood = Mood(r);
                int likes = r.Likes.Count(IsActive), dislikes = r.Dislikes.Count(IsActive);
                float lean = Story.LeanOf(r.Id), notice = Story.NoticeOf(r.Id);
                v[(int)Stance.Trust] += (mood - 50f) * 0.03f + likes * 0.4f;
                v[(int)Stance.Reliance] += guide * 0.25f * lean + (Stage - 1) * 0.05f * lean + Done.Count * 0.01f;
                v[(int)Stance.Suspicion] += gap * 0.012f * notice - (mood - 50f) * 0.005f;
                v[(int)Stance.Resistance] += dislikes * 0.6f + control * 0.1f - (mood - 50f) * 0.02f;
                for (int i = 0; i < v.Length; i++)
                    v[i] = Math.Max(0f, Math.Min(100f, v[i] * 0.985f));
                NoteStance(r.Id, before, "한 주의 변화");
            }
            OverhearTalk();
        }

        void OverhearTalk()
        {
            if (Quiet || !Rng.Chance(0.6f))
                return;
            var recent = new HashSet<string>(Overheard.Skip(Math.Max(0, Overheard.Count - 6)).Select(o => o.Id));
            var pool = Story.Talks.Where(t => !recent.Contains(t.Id) && t.Condition(this)).ToList();
            if (pool.Count == 0)
                return;
            var talk = pool[Rng.Range(0, pool.Count)];
            Overheard.Add(new OverheardTalk { Day = Day, Id = talk.Id });
            if (Overheard.Count > 60)
                Overheard.RemoveAt(0);
            TalkOverheard?.Invoke(talk);
        }

        // Daily, before events: the next step of someone's arc, when its time has come.
        bool RollScene()
        {
            if (Quiet || Day - LastEventDay < OpsTuning.EventCooldownDays)
                return false;
            var next = Story.Scenes.FirstOrDefault(s => !ScenesSeen.Contains(s.Id) && ArcStep(s.Arc) == s.Step - 1 && s.Condition(this));
            if (next == null)
                return false;
            PendingScene = next.Id;
            LastEventDay = Day;
            Write($"[관찰 기록] {next.Title}");
            return true;
        }

        public bool CanChooseScene(EventOption option, out string reason) => CanChoose(option, out reason);

        // Closes the waiting scene: the picked option (or just watching, -1, when it has none). Returns the result text.
        public string ResolveScene(int optionIndex)
        {
            var def = Story.Scene(PendingScene);
            if (def == null)
                return null;
            string result;
            if (def.Options.Count > 0)
            {
                if (optionIndex < 0 || optionIndex >= def.Options.Count)
                    return null;
                var option = def.Options[optionIndex];
                if (!CanChoose(option, out _))
                    return null;
                Energy -= option.Energy;
                Indices.Apply(option.Effect * OpsTuning.EventScale);
                option.Apply?.Invoke(this);
                result = option.Result;
                Write($"대응 [{OpsEvents.TagName(option.Tag)}] {option.Label}");
            }
            else
            {
                def.OnSeen?.Invoke(this);
                result = def.Note;
            }
            ScenesSeen.Add(def.Id);
            Info += OpsTuning.InfoFirstEvent;
            InfoEarned += OpsTuning.InfoFirstEvent;
            PendingScene = null;
            CheckCollapse();
            return result;
        }

        // ---------------------------------------------------------------- save

        void SaveStory(Action<string, string> line)
        {
            line("stances", string.Join(";", Stances.Select(p => p.Key + ":" + string.Join(",", p.Value.Select(F)))));
            line("stancelog", string.Join(";", StanceLog.Select(c => $"{c.Day},{c.Id},{c.From},{c.To},{c.Reason.Replace(",", " ").Replace(";", " ")}")));
            line("scenes", string.Join(",", ScenesSeen));
            line("pendingscene", PendingScene ?? "");
            line("overheard", string.Join(";", Overheard.Select(o => $"{o.Day},{o.Id}")));
        }

        void LoadStory(Func<string, string> get)
        {
            foreach (var part in get("stances").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int colon = part.IndexOf(':');
                var values = List(part.Substring(colon + 1)).Select(P).ToArray();
                if (values.Length == 4 && Stances.ContainsKey(part.Substring(0, colon)))
                    Stances[part.Substring(0, colon)] = values;
            }
            foreach (var part in get("stancelog").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var c = part.Split(',');
                StanceLog.Add(new StanceChange
                {
                    Day = int.Parse(c[0], Inv), Id = c[1], From = int.Parse(c[2], Inv), To = int.Parse(c[3], Inv), Reason = c.Length > 4 ? c[4] : "",
                });
            }
            foreach (var id in List(get("scenes")))
                ScenesSeen.Add(id);
            PendingScene = Story.Scene(get("pendingscene")) != null ? get("pendingscene") : null;
            foreach (var part in get("overheard").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var o = part.Split(',');
                if (Story.TalkOf(o[1]) != null)
                    Overheard.Add(new OverheardTalk { Day = int.Parse(o[0], Inv), Id = o[1] });
            }
        }
    }
}
