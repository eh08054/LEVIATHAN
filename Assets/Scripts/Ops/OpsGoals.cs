using System;
using System.Collections.Generic;
using System.Linq;

namespace Sector.Ops
{
    // Short-term goals: one audit per year from the city, and each quarter a directive picked from an offer
    // (city or core AI). Reach goals finish the moment they hold; hold goals are judged at their deadline.
    public sealed partial class OpsSession
    {
        public const int DirectivesOffered = 3;

        public readonly List<ActiveGoal> Goals = new List<ActiveGoal>();
        // Directive ids waiting for the player's pick (null when nothing is offered).
        public List<string> DirectiveOffer;
        public readonly List<GoalResult> GoalResults = new List<GoalResult>();
        public readonly HashSet<string> Perks = new HashSet<string>();
        public readonly List<string> Documents = new List<string>();
        public int CityDone, CoreDone, AuditFails;
        // Events fired so far, by kind; info earned in total (not counting rewards).
        public readonly int[] EventCounts = new int[Enum.GetValues(typeof(EventKind)).Length];
        public float InfoEarned;

        public event Action<GoalResult> GoalFinished;

        public int Year => Math.Min(OpsTuning.Years, Date.Year - OpsTuning.StartYear + 1);
        public string ActName => Ops.Goals.ActNames[Year - 1];

        public ActiveGoal Audit => Goals.FirstOrDefault(g => g.Def.Kind == GoalKind.Audit);
        public ActiveGoal Directive => Goals.FirstOrDefault(g => g.Def.Kind == GoalKind.Directive);

        public GoalProgress ProgressOf(ActiveGoal goal) => goal.Def.Progress(this, goal);

        static DateTime QuarterEnd(DateTime date) => new DateTime(date.Year, (date.Month - 1) / 3 * 3 + 1, 1).AddMonths(3);

        ActiveGoal Begin(GoalDef def, DateTime due) => new ActiveGoal
        {
            Id = def.Id, StartDay = Day, DueDay = (int)(due - Start).TotalDays, Start = Indices.Copy(),
            StartPopulation = ManagedPopulation, StartInfoEarned = InfoEarned, StartCrowd = EventCounts[(int)EventKind.Crowd],
            StartDone = Done.Count,
        };

        void StartAudit()
        {
            var def = Ops.Goals.AuditFor(Year);
            if (def == null || Goals.Any(g => g.Id == def.Id) || GoalResults.Any(r => r.Id == def.Id))
                return;
            Goals.Add(Begin(def, new DateTime(Date.Year + 1, 1, 1)));
            Write($"{Year}막 '{ActName}' 시작. 연말 감사: {def.Title}");
        }

        // Two city directives and one from the core AI, none already met and none done in the last two quarters.
        void OfferDirectives()
        {
            if (Quiet || Date >= End || QuarterEnd(Date) > End)
                return;
            var recent = new HashSet<string>(GoalResults.Skip(Math.Max(0, GoalResults.Count - 4)).Select(r => r.Id));
            List<string> Pick(GoalSource source, int count)
            {
                var pool = Ops.Goals.All.Where(g => g.Kind == GoalKind.Directive && g.Source == source && !recent.Contains(g.Id)
                                                   && g.Offerable(this) && !Met(g)).ToList();
                var picked = new List<string>();
                while (picked.Count < count && pool.Count > 0)
                {
                    var g = pool[Rng.Range(0, pool.Count)];
                    pool.Remove(g);
                    picked.Add(g.Id);
                }
                return picked;
            }
            var offer = Pick(GoalSource.City, 2).Concat(Pick(GoalSource.Core, 1)).ToList();
            DirectiveOffer = offer.Count > 0 ? offer : null;
        }

        bool Met(GoalDef def) => def.Check == GoalCheck.Reach && def.Progress(this, Begin(def, Date)).Met;

        // Takes one directive from the offer (or none with -1) until the end of this quarter.
        public bool ChooseDirective(int index)
        {
            if (DirectiveOffer == null)
                return false;
            if (index >= 0 && index < DirectiveOffer.Count)
            {
                var def = Ops.Goals.Get(DirectiveOffer[index]);
                Goals.RemoveAll(g => g.Def.Kind == GoalKind.Directive);
                Goals.Add(Begin(def, QuarterEnd(Date)));
                Write($"{Ops.Goals.SourceName(def.Source)} 지시 수락: {def.Title}");
            }
            DirectiveOffer = null;
            return true;
        }

        // Reach goals: done as soon as they hold. Called weekly and at each settlement.
        void CheckGoals()
        {
            if (Quiet)
                return;
            foreach (var goal in Goals.Where(g => g.Def.Check == GoalCheck.Reach).ToList())
                if (ProgressOf(goal).Met)
                    Finish(goal, true);
        }

        // At a settlement: judge whatever is due, then hand out the next audit and directives.
        void SettleGoals(MonthlyReport report)
        {
            if (Quiet)
                return;
            CheckGoals();
            foreach (var goal in Goals.Where(g => g.DueDay <= Day).ToList())
                report.Goals.Add(Finish(goal, ProgressOf(goal).Met));
            if (Date >= End)
                return;
            if (Date.Month == 1)
                StartAudit();
            if ((Date.Month - 1) % 3 == 0)
                OfferDirectives();
        }

        GoalResult Finish(ActiveGoal goal, bool success)
        {
            var def = goal.Def;
            Goals.Remove(goal);
            var result = new GoalResult { Id = def.Id, Success = success, Day = Day };
            if (success)
            {
                def.Reward?.Invoke(this);
                var note = def.RewardText;
                if (def.GivesDocument)
                {
                    var doc = GiveDocument();
                    if (doc == null)
                        note = note.Replace(" · 기밀 문서", "");
                }
                if (def.Source == GoalSource.City)
                    CityDone++;
                else
                    CoreDone++;
                if (def.Kind == GoalKind.Audit)
                    AuditFails = 0;
                result.Note = note;
                Write($"목표 달성: {def.Title} — {note}");
            }
            else
            {
                if (def.Kind == GoalKind.Audit)
                    FailAudit();
                else
                    def.Penalty?.Invoke(this);
                result.Note = def.PenaltyText;
                Write($"목표 실패: {def.Title} — {def.PenaltyText}");
            }
            GoalResults.Add(result);
            GoalFinished?.Invoke(result);
            return result;
        }

        // A failed audit: a warning, less energy, the delegation streak reset; two in a row cost a stage.
        void FailAudit()
        {
            AuditFails++;
            Energy = Math.Max(0f, Energy - 40f);
            RiseStreak = 0;
            if (AuditFails >= 2 && Year < OpsTuning.Years)
            {
                AuditFails = 0;
                StageDown();
            }
        }

        DocumentDef GiveDocument()
        {
            var doc = Ops.Goals.Documents.FirstOrDefault(d => !Documents.Contains(d.Id));
            if (doc != null)
                Documents.Add(doc.Id);
            return doc;
        }

        // ---------------------------------------------------------------- save

        void SaveGoals(Action<string, string> line)
        {
            foreach (var g in Goals)
                line("goal", $"{g.Id},{g.StartDay},{g.DueDay},{F(g.StartPopulation)},{F(g.StartInfoEarned)},{g.StartCrowd},{g.StartDone};" +
                             string.Join(",", IndexNames.All.Select(i => F(g.Start[i]))));
            line("offer", DirectiveOffer == null ? "" : string.Join(",", DirectiveOffer));
            line("goalresults", string.Join(";", GoalResults.Select(r => $"{r.Id},{(r.Success ? 1 : 0)},{r.Day}")));
            line("perks", string.Join(",", Perks.OrderBy(p => p)));
            line("documents", string.Join(",", Documents));
            line("goalcounts", $"{CityDone},{CoreDone},{AuditFails},{F(InfoEarned)};{string.Join(",", EventCounts)}");
        }

        void LoadGoals(Func<string, string> get, IEnumerable<string> goalLines)
        {
            foreach (var value in goalLines)
            {
                var halves = value.Split(';');
                var p = halves[0].Split(',');
                if (Ops.Goals.Get(p[0]) == null)
                    continue;
                Goals.Add(new ActiveGoal
                {
                    Id = p[0], StartDay = int.Parse(p[1], Inv), DueDay = int.Parse(p[2], Inv), StartPopulation = P(p[3]),
                    StartInfoEarned = P(p[4]), StartCrowd = int.Parse(p[5], Inv), StartDone = int.Parse(p[6], Inv),
                    Start = ParseIndices(halves[1]),
                });
            }
            var offer = List(get("offer")).Where(id => Ops.Goals.Get(id) != null).ToList();
            DirectiveOffer = offer.Count > 0 ? offer : null;
            foreach (var part in get("goalresults").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var r = part.Split(',');
                if (Ops.Goals.Get(r[0]) != null)
                    GoalResults.Add(new GoalResult { Id = r[0], Success = r[1] == "1", Day = int.Parse(r[2], Inv) });
            }
            foreach (var perk in List(get("perks")))
                Perks.Add(perk);
            Documents.AddRange(List(get("documents")));
            var counts = get("goalcounts").Split(';');
            if (counts.Length == 2)
            {
                var c = counts[0].Split(',');
                CityDone = int.Parse(c[0], Inv);
                CoreDone = int.Parse(c[1], Inv);
                AuditFails = int.Parse(c[2], Inv);
                InfoEarned = P(c[3]);
                var kinds = List(counts[1]).Select(v => int.Parse(v, Inv)).ToArray();
                for (int i = 0; i < Math.Min(kinds.Length, EventCounts.Length); i++)
                    EventCounts[i] = kinds[i];
            }
        }
    }
}
