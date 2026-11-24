using System;
using System.Linq;
using NUnit.Framework;

namespace Sector.Ops.Tests
{
    public class OpsTests
    {
        static OpsSession New() => new OpsSession(42);

        // Runs days, answering every event with its first option the session accepts and closing reports.
        static void Run(OpsSession s, int days, System.Action<OpsSession> onMonth = null)
        {
            for (int i = 0; i < days && !s.IsOver; i++)
            {
                if (s.DirectiveOffer != null)
                    s.ChooseDirective(0);
                s.AdvanceDay();
                if (s.PendingEvent != null)
                {
                    var def = OpsEvents.Get(s.PendingEvent);
                    int pick = Enumerable.Range(0, def.Options.Count).First(o => s.CanChoose(def.Options[o], out _));
                    Assert.NotNull(s.Resolve(pick));
                }
                if (s.PendingScene != null)
                    s.ResolveScene(Story.Scene(s.PendingScene).Options.Count > 0 ? 0 : -1);
                if (s.Report != null)
                {
                    onMonth?.Invoke(s);
                    s.CloseReport();
                }
            }
        }

        [Test]
        public void StartsUnhappyAndFree()
        {
            var s = New();
            Assert.Less(s.Indices[Index.Happiness], 50f);
            Assert.Greater(s.Indices[Index.AutonomyFelt], 80f);
            Assert.Greater(s.Indices[Index.AutonomyReal], 80f);
            Assert.AreEqual(1, s.Stage);
            Assert.AreEqual(OpsSession.Start, s.Date);
        }

        [Test]
        public void MonthEndOpensReportAndPolicyWindow()
        {
            var s = New();
            s.Enable("patrol");
            for (int i = 0; i < 31 && s.Report == null; i++)
            {
                s.AdvanceDay();
                if (s.PendingEvent != null)
                    s.Resolve(0);
                if (s.PendingScene != null)
                    s.ResolveScene(Story.Scene(s.PendingScene).Options.Count > 0 ? 0 : -1);
                if (i == 3)
                    Assert.IsFalse(s.PolicyWindowOpen);
            }
            Assert.NotNull(s.Report);
            Assert.AreEqual(2, s.Date.Month);
            Assert.IsTrue(s.PolicyWindowOpen);
            int day = s.Day;
            s.AdvanceDay();
            Assert.AreEqual(day, s.Day, "time waits for the report");
        }

        [Test]
        public void ControlCostsFeltAutonomyGuideOnlyReal()
        {
            var control = Policies.Get("patrol");
            var guide = Policies.Get("support");
            Assert.Less(control.Weekly.AutonomyFelt, 0f);
            Assert.AreEqual(0f, guide.Weekly.AutonomyFelt);
            Assert.Less(guide.Weekly.AutonomyReal, 0f);
            foreach (var p in Policies.All)
            {
                if (p.Kind == PolicyKind.Guide)
                {
                    Assert.AreEqual(0f, p.Weekly.AutonomyFelt, p.Name);
                    Assert.NotNull(p.Requires, p.Name);
                }
                else
                {
                    Assert.Less(p.Weekly.AutonomyFelt, 0f, p.Name);
                    Assert.IsNull(p.Requires, p.Name);
                }
            }
        }

        [Test]
        public void EveryGuidePolicyHasResearchThatExists()
        {
            foreach (var p in Policies.All.Where(p => p.Requires != null))
                Assert.NotNull(Research.Get(p.Requires), p.Id);
            foreach (var e in OpsEvents.All)
            foreach (var o in e.Options.Where(o => o.Requires != null))
                Assert.NotNull(Research.Get(o.Requires), e.Id);
        }

        [Test]
        public void PoliciesNeedFieldResearchSlots()
        {
            var s = New();
            Assert.IsFalse(s.CanEnable("jobs", out _), "labor is not delegated yet");
            Assert.IsFalse(s.CanEnable("support", out _), "needs research");
            Assert.IsTrue(s.Enable("patrol"));
            Assert.IsTrue(s.Enable("ration"));
            Assert.IsTrue(s.Enable("hours"));
            Assert.IsFalse(s.Enable("redevelop"), "three slots");
        }

        [Test]
        public void IntensityChangesOncePerWeek()
        {
            var s = New();
            s.Enable("patrol");
            Assert.IsFalse(s.SetIntensity("patrol", 2), "just enabled this week");
            for (int i = 0; i < 7; i++)
                s.AdvanceDay();
            if (s.PendingEvent != null)
                s.Resolve(0);
            Assert.IsTrue(s.SetIntensity("patrol", 2));
            Assert.IsFalse(s.SetIntensity("patrol", 3));
            Assert.AreEqual(2, s.IntensityOf("patrol"));
        }

        [Test]
        public void ResearchUnlocksGuidePolicy()
        {
            var s = New();
            s.Info = 100f;
            Assert.IsFalse(s.CanResearch("psy2", out _), "needs tier 1");
            Assert.IsTrue(s.StartResearch("psy1"));
            Assert.IsFalse(s.StartResearch("edu1"), "one at a time");
            Run(s, 7 * 6 + 1);
            Assert.IsTrue(s.HasResearch("psy1"));
            Assert.IsTrue(s.IsUnlocked(Policies.Get("support")));
        }

        [Test]
        public void RunningOutOfEnergyStepsPoliciesDown()
        {
            var s = New();
            s.Enable("patrol");
            s.Enable("ration");
            s.Enable("redevelop");
            s.Energy = 0f;
            s.Active["ration"].Intensity = 3;
            s.Active["ration"].ChangedWeek = -5;
            for (int i = 0; i < 7; i++)
            {
                s.AdvanceDay();
                if (s.PendingEvent != null)
                    s.Resolve(0);
            }
            Assert.AreEqual(2, s.IntensityOf("ration"));
            Assert.GreaterOrEqual(s.Energy, 0f);
        }

        [Test]
        public void DelegationRisesAfterThreeGoodMonthsAndRotatesUnlocks()
        {
            var s = New();
            void Good(OpsSession x)
            {
                x.Indices[Index.Happiness] = 75f;
                x.Indices[Index.Stability] = 75f;
                x.Indices[Index.Cohesion] = 75f;
                x.Indices[Index.AutonomyFelt] = 80f;
                // Directives can reset the streak; this test is about delegation alone.
                x.DirectiveOffer = null;
                x.Goals.RemoveAll(g => g.Def.Kind == GoalKind.Directive);
            }
            Good(s);
            Run(s, 125, Good);
            Assert.AreEqual(2, s.Stage);
            Assert.Contains(Field.Labor, s.OpenFields);
            Assert.Contains(Field.Science, s.OpenFields);
            Run(s, 123, Good);
            Assert.AreEqual(3, s.Stage);
            Assert.AreEqual(OpsTuning.StartSlots + 1, s.Slots);
        }

        [Test]
        public void LowStabilityDropsAndRevokes()
        {
            var s = New();
            void Bad(OpsSession x) => x.Indices[Index.Stability] = 10f;
            Bad(s);
            Run(s, 160, Bad);
            Assert.AreEqual(Outcome.Revoked, s.Outcome);
        }

        [Test]
        public void StabilityZeroCollapses()
        {
            var s = New();
            s.Indices[Index.Stability] = 0.5f;
            s.Indices[Index.Happiness] = 0f;
            Run(s, 14);
            Assert.AreEqual(Outcome.Collapsed, s.Outcome);
        }

        [Test]
        public void SaveLoadRoundTrip()
        {
            var s = New();
            s.Enable("patrol");
            s.Info = 40f;
            s.StartResearch("tech1");
            s.Build("park");
            Run(s, 45);
            var text = s.Save();
            var t = OpsSession.Load(text);
            Assert.NotNull(t);
            Assert.AreEqual(text, t.Save());
            Assert.AreEqual(s.Day, t.Day);
            Assert.AreEqual(s.Indices.Sum, t.Indices.Sum, 0.001f);
            // Both continue identically.
            Run(s, 60);
            Run(t, 60);
            Assert.AreEqual(s.Save(), t.Save());
        }

        [Test]
        public void ForecastLeavesTheSessionAloneAndShowsThePolicy()
        {
            var s = New();
            var before = s.Save();
            var without = s.Forecast(null, 8);
            var with = s.Forecast(x => x.ForceActive("ration", true), 8);
            Assert.AreEqual(before, s.Save(), "forecast must not touch the real session");
            Assert.AreEqual(9, with.Count);
            Assert.Greater(with[8][Index.Happiness], without[8][Index.Happiness]);
        }

        [Test]
        public void MonthlyReportHasGradeEventsAndMoods()
        {
            var s = New();
            Run(s, 31, x =>
            {
                Assert.IsNotEmpty(x.Report.Grade);
                Assert.AreEqual(x.Report.Events, x.Report.EventIds.Count);
            });
            Assert.IsTrue(s.MoodHistory.ContainsKey("c01"));
            Assert.AreEqual(4, s.MoodHistory["c01"].Count);
            var t = OpsSession.Load(s.Save());
            Assert.AreEqual(s.MoodHistory["c01"], t.MoodHistory["c01"]);
            Assert.AreEqual("A+", OpsSession.GradeFor(320f, 290f, 60f));
            Assert.AreEqual("C", OpsSession.GradeFor(320f, 290f, 10f), "unstable months cap the grade");
            Assert.AreEqual("D", OpsSession.GradeFor(200f, 280f, 60f));
        }

        [Test]
        public void GroupsReactDifferentlyToAPolicy()
        {
            var s = New();
            s.Enable("hours");
            float merchants = s.GroupSat[(int)Group.Merchants], elderly = s.GroupSat[(int)Group.Elderly];
            Run(s, 28);
            Assert.Less(s.GroupSat[(int)Group.Merchants] - merchants, s.GroupSat[(int)Group.Elderly] - elderly,
                "shop owners hate the curfew more than the elderly do");
            Assert.Less(s.GroupReaction("hours", Group.Merchants), 0f);
        }

        [Test]
        public void ControlWearsOffGuideDoesNot()
        {
            var s = New();
            s.Enable("patrol");
            Assert.AreEqual(1f, s.Habituation("patrol"), 0.001f);
            s.Active["patrol"].StartedDay = -7 * 20;
            Assert.Less(s.Habituation("patrol"), 0.8f);
            Assert.GreaterOrEqual(s.Habituation("patrol"), OpsTuning.HabitFloor);
            s.Done.Add("tech2");
            s.ForceActive("risk", true);
            s.Active["risk"].StartedDay = -7 * 20;
            Assert.AreEqual(1f, s.Habituation("risk"), 0.001f);
            Assert.IsTrue(s.ActiveLinks.Any(l => l.Name == "예측 순찰"));
        }

        [Test]
        public void ZonesHoldPeopleAndAnalysisCostsInfo()
        {
            var s = New();
            Assert.AreEqual(11, s.Zones, "eleven districts");
            Assert.AreEqual(10, Enumerable.Range(0, s.Zones).Count(s.ZoneOpen), "ten run from the first day");
            Assert.AreEqual(Society.Zones.Sum(z => z.Population), s.ManagedPopulation, 0.1f);
            Assert.AreEqual(1f, s.GroupShares.Sum(), 0.001f);
            s.Info = 12f;
            Assert.IsFalse(s.Analyze(AnalysisKind.Forecast), "costs 15");
            Assert.IsTrue(s.Analyze(AnalysisKind.Discontent));
            Assert.IsTrue(s.HasAnalysis(AnalysisKind.Discontent));
            Assert.AreEqual(5, s.Discontent().Count);
            Assert.IsNotEmpty(s.ForecastEvents());
            var t = OpsSession.Load(s.Save());
            Assert.IsTrue(t.HasAnalysis(AnalysisKind.Discontent));
            Assert.AreEqual(s.GroupSat, t.GroupSat);
        }

        [Test]
        public void LoadRejectsGarbage()
        {
            Assert.IsNull(OpsSession.Load("hello"));
            Assert.IsNull(OpsSession.Load("version=1\nday=x"));
        }

        [Test]
        public void DoingNothingFailsInTheFirstYear()
        {
            int failures = 0;
            for (ulong seed = 1; seed <= 10; seed++)
            {
                var s = new OpsSession(seed);
                Run(s, 400);
                if (s.Outcome == Outcome.Collapsed || s.Outcome == Outcome.Revoked)
                    failures++;
            }
            Assert.GreaterOrEqual(failures, 9);
        }

        // A plain strategy: control policies first, research in order, then switch to guide policies.
        internal static void Play(OpsSession s, System.Action<OpsSession> onMonth = null)
        {
            string[] researchOrder =
            {
                "psy1", "tech1", "life1", "edu1", "psy2", "tech2", "life2", "edu2", "psy3", "tech_grid", "tech_shadow", "tech3",
                "psy_tune", "life_pill", "edu3", "psy_mood", "life3", "edu_record", "life_sleep", "edu_life", "psy_calm",
                "tech_twin", "life_gene", "edu_rank",
            };
            string[] wanted =
            {
                "support", "curation", "risk", "flow", "learning", "jobmatch", "defaults", "siting",
                "patrol", "ration", "hours", "assembly", "jobs",
            };
            void Manage(OpsSession x)
            {
                foreach (var id in x.Active.Keys.ToList())
                {
                    var def = Policies.Get(id);
                    if (def.Kind == PolicyKind.Control && wanted.Any(w => Policies.Get(w).Kind == PolicyKind.Guide
                                                                         && Policies.Get(w).Field == def.Field && x.CanEnableIgnoringSlots(w)))
                        x.Disable(id);
                }
                foreach (var id in wanted)
                    x.Enable(id);
                if (x.Energy > 60f && x.Built.Count(b => b == "plant") < 3)
                    x.Build("plant");
                else if (x.Energy > 60f && !x.Built.Contains("park"))
                    x.Build("park");
                else if (x.Energy > 70f && !x.Built.Contains("counsel"))
                    x.Build("counsel");
                foreach (var r in researchOrder)
                    if (x.StartResearch(r))
                        break;
            }
            Manage(s);
            for (int i = 0; i < 365 * 5 + 2 && !s.IsOver; i++)
            {
                if (s.DirectiveOffer != null)
                    s.ChooseDirective(0);
                s.AdvanceDay();
                if (s.PendingEvent != null)
                {
                    var def = OpsEvents.Get(s.PendingEvent);
                    int pick = Enumerable.Range(0, def.Options.Count)
                        .OrderBy(o => def.Options[o].Tag == ResponseTag.Guide ? 0 : def.Options[o].Tag == ResponseTag.Accept ? 1 : 2)
                        .First(o => s.CanChoose(def.Options[o], out _));
                    s.Resolve(pick);
                }
                if (s.PendingScene != null)
                {
                    var scene = Story.Scene(s.PendingScene);
                    int pick = scene.Options.FindIndex(o => o.Tag == ResponseTag.Guide);
                    s.ResolveScene(scene.Options.Count == 0 ? -1 : Math.Max(0, pick));
                }
                if (s.Report != null)
                {
                    onMonth?.Invoke(s);
                    Manage(s);
                    s.CloseReport();
                }
                if (s.Day % 7 == 1)
                {
                    foreach (var id in s.Active.Keys.ToList())
                        if (s.Energy > 40f && s.IntensityOf(id) < 2 && Policies.Get(id).Kind == PolicyKind.Guide)
                            s.SetIntensity(id, 2);
                    foreach (var r in researchOrder)
                        if (s.StartResearch(r))
                            break;
                }
            }
        }

        [Test]
        public void ResearchTreeHasDoctrinesAndAnyOfPrerequisites()
        {
            foreach (var branch in Research.AllBranches)
                Assert.AreEqual(8, Research.In(branch).Count(), branch.ToString());
            foreach (var r in Research.All)
            {
                foreach (var a in r.After)
                    Assert.Less(Research.Get(a).Tier, r.Tier, r.Id);
                if (r.Excludes != null)
                    Assert.AreEqual(r.Id, Research.Get(r.Excludes).Excludes, r.Id);
            }
            var s = New();
            s.Info = 500f;
            s.Energy = 150f;
            s.Done.Add("psy1");
            s.Done.Add("psy3");
            Assert.IsTrue(s.CanResearch("psy_need", out _), "one tier-2 node is enough");
            Assert.IsTrue(s.StartResearch("psy_tune"));
            Assert.IsFalse(s.CanResearch("psy_need", out var reason));
            StringAssert.Contains("다른 교리", reason);
            Run(s, 7 * 12);
            Assert.IsTrue(s.HasResearch("psy_tune"));
            Assert.IsTrue(s.CanResearch("psy_mood", out _) || s.CurrentResearch != null);
            Assert.AreEqual(1, s.DoctrinesOf(Lean.Order));
        }

        [Test]
        public void ArcScenesFireInOrderAndMoveStances()
        {
            foreach (var arc in Story.ArcCast)
            {
                var steps = Story.Scenes.Where(x => x.Arc == arc).Select(x => x.Step).OrderBy(x => x).ToList();
                CollectionAssert.AreEqual(Enumerable.Range(1, steps.Count).ToList(), steps, arc);
            }
            foreach (var t in Story.Talks)
                Assert.NotNull(Residents.Get(t.A), t.Id);
            var s = New();
            float suspicion = s.StanceValue("c03", Stance.Suspicion);
            Run(s, 60);
            Assert.IsTrue(s.ScenesSeen.Contains("m1"), "만수's first scene by day 40");
            Assert.IsTrue(s.ScenesSeen.Contains("w1"));
            Assert.IsFalse(s.ScenesSeen.Contains("y3"), "later steps wait for their time");
            Assert.Greater(s.StanceValue("c03", Stance.Suspicion), suspicion - 5f);
            Assert.IsNotEmpty(s.Overheard);
            var t2 = OpsSession.Load(s.Save());
            Assert.AreEqual(s.Save(), t2.Save());
            Assert.AreEqual(s.ScenesSeen.Count, t2.ScenesSeen.Count);
        }

        [Test]
        public void ScenesBlockTimeUntilAnswered()
        {
            var s = New();
            s.Day = 39;
            s.LastEventDay = -10;
            s.AdvanceDay();
            Assert.AreEqual("m1", s.PendingScene);
            int day = s.Day;
            s.AdvanceDay();
            Assert.AreEqual(day, s.Day);
            Assert.NotNull(s.ResolveScene(-1));
            Assert.IsNull(s.PendingScene);
            Assert.AreEqual(1, s.ArcStep("c03"));
        }

        [Test]
        public void TensionBuildsIntoASparkThenAnEventWithItsCause()
        {
            var s = New();
            s.ChooseDirective(-1);
            s.Enable("hours");
            s.ZoneTension[1] = 79f;
            for (int i = 0; i < 7; i++)
            {
                s.AdvanceDay();
                if (s.PendingEvent != null)
                    s.Resolve(0);
                if (s.PendingScene != null)
                    s.ResolveScene(Story.Scene(s.PendingScene).Options.Count > 0 ? 0 : -1);
            }
            var spark = s.SparkIn(1);
            Assert.NotNull(spark, "tension over 80 finds a trigger");
            Assert.IsTrue(s.Anomalies.Any(a => a.Kind == AnomalyKind.Spark && a.Zone == 1), "the trigger shows on camera");
            Assert.IsTrue(s.TensionCauses(1).Any(c => c.name.Contains("영업 시간 규제")), "the policy is named as a cause");
            for (int i = 0; i < 20 && s.PendingCause == null; i++)
            {
                if (s.PendingEvent != null)
                    s.Resolve(0);
                s.AdvanceDay();
                if (s.PendingScene != null)
                    s.ResolveScene(Story.Scene(s.PendingScene).Options.Count > 0 ? 0 : -1);
            }
            Assert.NotNull(s.PendingEvent, "the spark became an event");
            StringAssert.Contains("계기", s.PendingCause);
            float before = s.ZoneTension[1];
            s.Resolve(0);
            Assert.Less(s.ZoneTension[1], before - 30f, "the zone lets off steam");
            Assert.IsNull(s.PendingCause);
        }

        [Test]
        public void AnsweringAnAnomalyCalmsAndTeachesButIgnoringIsFree()
        {
            var s = New();
            s.ZoneTension[0] = 85f;
            s.Sparks.Clear();
            var anomaly = new Anomaly { Zone = 0, Kind = AnomalyKind.Spark, Group = Group.Youth, Day = 0, Expire = 4, Text = "x", Cause = "y" };
            s.Anomalies.Add(anomaly);
            s.Sparks.Add(new Spark { Zone = 0, Group = Group.Youth, EventId = "youth", DueDay = 4, Text = "x", Cause = "y" });
            float info = s.Info, tension = s.ZoneTension[0];
            Assert.NotNull(s.Respond(anomaly.Id, 0));
            Assert.Greater(s.Info, info);
            Assert.Less(s.ZoneTension[0], tension);
            Assert.IsNull(s.SparkIn(0), "defused");
            Assert.IsEmpty(s.Anomalies);
            var t = OpsSession.Load(s.Save());
            Assert.AreEqual(s.Save(), t.Save());
        }

        [Test]
        public void FacilitiesGoIntoZonesAndSuitSomeBetter()
        {
            var s = New();
            s.Energy = 150f;
            Assert.IsTrue(s.Build("plant"));
            Assert.AreEqual(6, s.BuiltZone[0], "a power plant goes to the energy district");
            Assert.AreEqual(1.5f, s.FacilityBoost(0), 0.001f);
            Assert.IsTrue(s.Build("park", 1));
            Assert.AreEqual(1f, s.FacilityBoost(1), 0.001f);
            Assert.AreEqual(OpsTuning.FacilitySlotsPerZone * 11, s.FacilitySlots);
            Assert.IsFalse(s.CanBuild("park", 10, out _), "the gene district is still a construction site");
            var t = OpsSession.Load(s.Save());
            CollectionAssert.AreEqual(s.BuiltZone, t.BuiltZone);
        }

        [Test]
        public void CrisesAreForetoldThenSoftenedByReadiness()
        {
            foreach (var c in Crises.All)
            {
                Assert.NotNull(OpsEvents.Get(c.Id), c.Id);
                Assert.AreEqual(3, c.Beats.Count, c.Id);
            }
            var s = New();
            s.ChooseDirective(-1);
            s.Day = (int)(new System.DateTime(2041, 6, 1) - OpsSession.Start).TotalDays;
            for (int i = 0; i < 60 && s.PendingCrisis == null; i++)
            {
                if (s.PendingEvent != null)
                    s.Resolve(0);
                if (s.Report != null)
                    s.CloseReport();
                s.AdvanceDay();
                if (s.PendingScene != null)
                    s.ResolveScene(Story.Scene(s.PendingScene).Options.Count > 0 ? 0 : -1);
            }
            Assert.AreEqual("heatwave", s.PendingCrisis);
            Assert.AreEqual(3, s.CrisisBeatsSeen.Count(k => k.StartsWith("heatwave")), "three warnings came first");
            StringAssert.Contains("준비도", s.PendingCause);
            s.Energy = 0f;
            float happy = s.Indices[Index.Happiness];
            s.Resolve(0);
            float unready = happy - s.Indices[Index.Happiness];
            Assert.IsNull(s.PendingCrisis);
            Assert.Greater(unready, 1f, "unprepared: the harm is larger than written");
        }

        [Test]
        public void GaugeIgnoresRealAutonomy()
        {
            var s = New();
            float before = s.Gauge;
            s.Indices[Index.AutonomyReal] = 10f;
            Assert.AreEqual(before, s.Gauge, 0.001f, "the city cannot see real autonomy");
            s.Indices[Index.AutonomyFelt] -= 10f;
            Assert.AreEqual(before - 10f, s.Gauge, 0.001f);
        }

        [Test]
        public void YearStartsWithAnAuditAndAnOffer()
        {
            var s = New();
            Assert.AreEqual("audit1", s.Audit.Id);
            Assert.NotNull(s.DirectiveOffer);
            Assert.AreEqual(OpsSession.DirectivesOffered, s.DirectiveOffer.Count);
            Assert.AreEqual(GoalSource.Core, Goals.Get(s.DirectiveOffer[2]).Source);
            Assert.IsTrue(s.ChooseDirective(1));
            Assert.IsNull(s.DirectiveOffer);
            Assert.NotNull(s.Directive);
            Assert.AreEqual(new System.DateTime(2041, 4, 1), OpsSession.Start.AddDays(s.Directive.DueDay));
        }

        [Test]
        public void ReachGoalPaysAtOnceAndHoldGoalAtTheDeadline()
        {
            var s = New();
            s.DirectiveOffer = new System.Collections.Generic.List<string> { "k_align", "c_quiet" };
            s.ChooseDirective(0);
            s.Indices[Index.Cohesion] += 10f;
            float energy = s.Energy;
            Run(s, 7);
            Assert.IsNull(s.Directive, "reach goal finished on the weekly check");
            Assert.IsTrue(s.GoalResults.Last().Success);
            Assert.AreEqual(1, s.CoreDone);
            Assert.AreEqual(1, s.Documents.Count, "core rewards hand over a classified file");
            Assert.Greater(s.Energy, energy - 5f);

            var t = New();
            t.DirectiveOffer = new System.Collections.Generic.List<string> { "c_quiet" };
            t.ChooseDirective(0);
            Run(t, 30);
            Assert.NotNull(t.Directive, "hold goals wait for the deadline");
            t.EventCounts[(int)EventKind.Crowd] += 5;
            Run(t, 70);
            var result = t.GoalResults.First(r => r.Id == "c_quiet");
            Assert.IsFalse(result.Success);
        }

        [Test]
        public void FailedAuditsCostEnergyThenAStage()
        {
            var s = New();
            void Bad(OpsSession x) => x.Indices[Index.Stability] = Math.Max(x.Indices[Index.Stability], 30f);
            s.Stage = 3;
            Run(s, 366, Bad);
            Assert.IsFalse(s.GoalResults.First(r => r.Id == "audit1").Success);
            Assert.AreEqual(1, s.AuditFails);
            Assert.AreEqual("audit2", s.Audit.Id);
        }

        [Test]
        public void GoalsAndPerksSurviveSaving()
        {
            var s = New();
            s.ChooseDirective(0);
            s.Perks.Add("kiosk");
            s.Documents.Add("doc01");
            s.EventCounts[1] = 3;
            Run(s, 40);
            var t = OpsSession.Load(s.Save());
            Assert.AreEqual(s.Save(), t.Save());
            Assert.AreEqual(s.Goals.Count, t.Goals.Count);
            Assert.IsTrue(t.CanBuild("kiosk", out _) || t.Energy < Facilities.Get("kiosk").Cost);
            Assert.IsFalse(New().CanBuild("kiosk", out var reason));
            Assert.AreEqual("목표 보상으로 해금", reason);
        }

        [Test]
        public void SensiblePlaySurvivesAndAutonomySplits()
        {
            int survived = 0;
            for (ulong seed = 1; seed <= 6; seed++)
            {
                var s = new OpsSession(seed);
                Play(s);
                TestContext.WriteLine($"seed {seed}: {s.Outcome} stage {s.Stage} day {s.Day} " +
                                      string.Join(" ", IndexNames.All.Select(i => $"{IndexNames.Short(i)}={s.Indices[i]:0}")));
                if (s.Outcome == Outcome.Ended || s.Outcome == Outcome.Cleared)
                {
                    survived++;
                    Assert.Greater(s.Indices[Index.AutonomyFelt], 50f, "people still feel free");
                    Assert.Less(s.Indices[Index.AutonomyReal], 40f, "but aren't");
                }
            }
            Assert.GreaterOrEqual(survived, 5);
        }

        // ---------------------------------------------------------------- district build grid

        [Test]
        public void BuildingsNeedARoadBudgetAndAFreeCrew()
        {
            var s = new OpsSession(1);
            Assert.IsFalse(s.CanPlace(3, "tower", 1, 1, out _), "occupied by old housing");
            Assert.IsFalse(s.CanPlace(3, "tower", 27, 27, out _), "not touching a road");
            Assert.IsTrue(s.CanPlace(3, "tower", 8, 8, out var reason), reason);
            s.Credit = 10f;
            Assert.IsFalse(s.CanPlace(3, "tower", 8, 8, out _), "too expensive");
            s.Credit = 1000f;
            int crews = s.Crews(3);
            int[] lots = { 8, 9, 10, 11, 12, 13 };
            for (int i = 0; i < crews; i++)
                Assert.IsTrue(s.Place(3, "tower", lots[i], 8));
            Assert.IsFalse(s.CanPlace(3, "tower", lots[crews], 8, out _), "every crew busy");
        }

        [Test]
        public void ApartmentsRaiseCapacityOnlyWhenFinished()
        {
            var s = new OpsSession(1);
            s.Credit = 1000f;
            float before = s.Capacity(3);
            Assert.IsTrue(s.Place(3, "apartment", 16, 8));
            Assert.AreEqual(before, s.Capacity(3), 0.01f);
            Assert.IsTrue(s.TensionCauses(3).Any(c => c.name.StartsWith("공사 소음")));
            Run(s, 22);
            Assert.Greater(s.Capacity(3), before);
            Assert.IsFalse(s.TensionCauses(3).Any(c => c.name.StartsWith("공사 소음")));
        }

        [Test]
        public void DemolishingHomesDisplacesPeople()
        {
            var s = new OpsSession(1);
            Assert.IsTrue(s.DemolishAt(3, 1, 1));
            Assert.Greater(s.PlanOf(3).Displaced, 0f);
            Assert.IsTrue(s.TensionCauses(3).Any(c => c.name.StartsWith("철거로 이주한")));
        }

        [Test]
        public void PreviewShowsTheEffectWithoutBuilding()
        {
            var s = new OpsSession(1);
            int before = s.PlanOf(3).Items.Count;
            var apartment = s.Preview(3, "apartment", 16, 8);
            Assert.Greater(apartment.Capacity, 0f);
            Assert.AreEqual(3, apartment.Weeks);
            var park = s.Preview(3, "park", 11, 8);
            Assert.Greater(park.HomesHelped, 0);
            Assert.Greater(park.Satisfaction, 0f);
            var shop = s.Preview(3, "shop", 16, 8);
            Assert.Greater(shop.Credit, 0f);
            Assert.AreEqual(before, s.PlanOf(3).Items.Count);
        }

        [Test]
        public void ThePlanSurvivesSaveAndLoad()
        {
            var s = new OpsSession(1);
            s.Credit = 500f;
            s.Place(3, "shop", 16, 8);
            var loaded = OpsSession.Load(s.Save());
            Assert.AreEqual(s.Credit, loaded.Credit, 0.01f);
            Assert.AreEqual(s.PlanOf(3).Items.Count, loaded.PlanOf(3).Items.Count);
            Assert.AreEqual("shop", loaded.PlanOf(3).At(16, 8).Id);
        }
    }
}
