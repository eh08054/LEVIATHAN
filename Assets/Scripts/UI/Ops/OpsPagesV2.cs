using System;
using System.Collections.Generic;
using System.Linq;
using Sector.Ops;
using UnityEngine;
using UnityEngine.UIElements;
using Index = Sector.Ops.Index;

namespace Sector.UI
{
    // Pages v2 (2026-11-27), drawn after Tools/Art/proto/pages_mock.html: the pages opened from the command dock in the
    // B HUD's language — a big title, few words, pictures and icons, details for the selected item in a side panel.
    // Styles: Assets/UI/Pages.uss (classes pg2-*). Used while the main HUD v2 is on (mainHud != null); the old
    // renderers stay for their sub-tabs (stance, story, groups, goals, documents, report tabs, observation tabs).
    public partial class OpsScreen
    {
        static readonly (Page page, string title, string sub)[] PageTitles =
        {
            (Page.Policy, "정책", "무엇을 바꿀까"),
            (Page.Research, "연구", "사람의 일을 하나씩 맡는다"),
            (Page.Citizens, "시민", "지켜보는 열 사람"),
            (Page.Watch, "관찰", "지금 이 도시에서"),
            (Page.Facility, "시설", "도시에 무엇을 더할까"),
            (Page.Report, "보고서", "한 달의 결산"),
            (Page.Log, "기록", "리바이어던이 한 일"),
        };
        static readonly Dictionary<Index, IconKind> IndexIcons = new Dictionary<Index, IconKind>
        {
            [Index.Happiness] = IconKind.Smile, [Index.Stability] = IconKind.Shield, [Index.Cohesion] = IconKind.Link,
            [Index.AutonomyFelt] = IconKind.Target, [Index.AutonomyReal] = IconKind.Eye,
        };
        static string IndexShort(Index i) => i == Index.AutonomyFelt ? "자율 (느끼는)" : i == Index.AutonomyReal ? "자율 (실제)" :
            i == Index.Happiness ? "행복" : i == Index.Stability ? "안정" : "정합";
        static readonly Color[] StanceTints = { new Color32(53, 201, 232, 255), new Color32(160, 140, 255, 255), new Color32(242, 160, 61, 255), new Color32(238, 90, 90, 255) };

        // ------------------------------------------------------------------ frame

        // Builds the page head and returns the body; false when the old renderer should draw the page.
        bool RenderPage2()
        {
            if (mainHud == null)
                return false;
            pageView.AddToClassList("pg2");
            var (_, title, sub) = PageTitles.FirstOrDefault(p => p.page == currentPage);
            var head = Add(pageView, "pg2-head");
            Text(head, title ?? "", "pg2-title");
            Text(head, sub ?? "", "pg2-sub");
            var tools = Add(head, "pg2-tools");
            var body = Add(pageView, "pg2-body");
            switch (currentPage)
            {
                case Page.Policy: Policy2(body, tools); break;
                case Page.Research: Research2(body, tools); break;
                case Page.Citizens: Citizens2(body, tools); break;
                case Page.Facility: Facility2(body, tools); break;
                case Page.Report: Report2(body, tools); break;
                case Page.Log: Log2(body, tools); break;
                case Page.Watch: Watch2(body, tools); break;
            }
            ZeroLabels(pageView);
            return true;
        }

        VisualElement Glass2(VisualElement parent, string classes) => Add(parent, "pg2-glass " + classes);

        Label Pill(VisualElement parent, IconKind? icon, string text, string classes = "")
        {
            var pill = Add(parent, "pg2-pill " + classes);
            if (icon.HasValue) pill.Add(new LineIcon(icon.Value) { style = { width = 22, height = 22 } });
            return Text(pill, text, "pg2-pill-text");
        }

        // a short ▲▲ / ▼ mark for a weekly change, coloured by meaning
        void EffectPill(VisualElement parent, Index index, float weekly)
        {
            int n = Math.Abs(weekly) >= 0.6f ? 2 : 1;
            bool good = IsGood(index, weekly);
            string arrow = new string(weekly > 0 ? '▲' : '▼', n);
            var pill = Add(parent, "pg2-pill");
            pill.Add(new LineIcon(IndexIcons[index]) { style = { width = 22, height = 22 } });
            Text(pill, IndexShort(index), "pg2-pill-text");
            var a = Text(pill, arrow, "pg2-pill-arrow");
            a.style.color = good ? Good : index == Index.AutonomyReal ? Caution : Bad;
        }

        VisualElement Face(VisualElement parent, string id, string classes)
        {
            var holder = Add(parent, classes);
            var face = new CastPortrait(id) { Expression = MoodFace(Residents.Get(id)) };
            face.style.flexGrow = 1;
            holder.Add(face);
            return holder;
        }

        Button Go(VisualElement parent, string text, Action click, bool off = false)
        {
            var b = MakeButton(parent, text, click, "pg2-go" + (off ? " pg2-go--off" : ""));
            b.SetEnabled(!off);
            return b;
        }

        // ------------------------------------------------------------------ policy

        void Policy2(VisualElement body, VisualElement tools)
        {
            if (session.Report != null)
                Go(tools, "결정 완료 · 운영 재개", () => { paused = false; FinishSettlement(); sfx.Play(Cue.Click); RefreshTime(); });
            Pill(tools, null, session.PolicyWindowOpen ? "결정 기간 · 시행과 중단 가능" : "시행·중단은 다음 결산 때 · 강도는 주 1회", session.PolicyWindowOpen ? "pg2-pill--hot" : "");

            var fields = Glass2(body, "pg2-rail");
            foreach (var field in Policies.AllFields)
            {
                var f = field;
                bool open = session.OpenFields.Contains(field);
                var b = new Button(() => { selectedField = f; selectedPolicy = null; sfx.Play(Cue.Click); RenderPage(); });
                b.AddToClassList("pg2-field");
                b.EnableInClassList("pg2-field--on", field == selectedField);
                b.EnableInClassList("pg2-field--locked", !open);
                b.Add(new LineIcon(FieldIcon(field)) { pickingMode = PickingMode.Ignore, style = { width = 30, height = 30 } });
                Text(b, Policies.FieldName(field), "pg2-field-name").pickingMode = PickingMode.Ignore;
                if (Policies.All.Any(p => p.Field == field && session.IsActive(p.Id)))
                    Add(b, "pg2-field-dot").pickingMode = PickingMode.Ignore;
                fields.Add(b);
            }

            var stage = Add(body, "pg2-stage");
            var hero = Art(stage, FieldArt(selectedField), "pg2-hero");
            Add(hero, "pg2-hero-shade");
            var heroText = Add(hero, "pg2-hero-text");
            Text(heroText, session.OpenFields.Contains(selectedField) ? "분야" : "아직 맡지 않은 분야", "pg2-hero-kicker");
            Text(heroText, Policies.FieldName(selectedField), "pg2-hero-title");
            var slots = Add(hero, "pg2-slots");
            Text(slots, "시행 중", "pg2-slots-label");
            for (int i = 0; i < session.Slots; i++)
                Add(slots, "pg2-slot" + (i < session.Active.Count ? " pg2-slot--on" : ""));

            var defs = Policies.All.Where(p => p.Field == selectedField).OrderBy(p => p.Kind).ToList();
            if (string.IsNullOrEmpty(selectedPolicy) || defs.All(d => d.Id != selectedPolicy))
                selectedPolicy = (defs.FirstOrDefault(d => session.IsActive(d.Id)) ?? defs.FirstOrDefault(d => session.IsUnlocked(d)) ?? defs[0]).Id;
            var row = Add(stage, "pg2-choices");
            foreach (var def in defs)
                PolicyChoice(row, def);

            Forecast2(Glass2(body, "pg2-side pg2-forecast"), Policies.Get(selectedPolicy));
        }

        static IconKind FieldIcon(Field f) => f switch
        {
            Field.Security => IconKind.Shield, Field.Law => IconKind.Policy, Field.Welfare => IconKind.Smile, Field.Labor => IconKind.Facility,
            Field.Education => IconKind.People, Field.Media => IconKind.Data, Field.CityCare => IconKind.Pin, Field.Construction => IconKind.Facility,
            _ => IconKind.Research,
        };

        void PolicyChoice(VisualElement parent, PolicyDef def)
        {
            bool active = session.IsActive(def.Id);
            var card = Glass2(parent, "pg2-choice");
            card.EnableInClassList("pg2-choice--sel", def.Id == selectedPolicy);
            card.EnableInClassList("pg2-choice--active", active);
            card.RegisterCallback<ClickEvent>(_ => { if (selectedPolicy == def.Id) return; selectedPolicy = def.Id; sfx.Play(Cue.Select); RenderPage(); });
            Text(card, Policies.KindName(def.Kind), "pg2-tag " + (def.Kind == PolicyKind.Control ? "pg2-tag--control" : "pg2-tag--guide"));
            Text(card, def.Name, "pg2-choice-name");
            // the first sentence only: the rest is detail
            int dot = def.Description.IndexOf(". ", StringComparison.Ordinal);
            Text(card, dot > 0 ? def.Description.Substring(dot + 2) : def.Description, "pg2-choice-line");

            int level = Math.Max(1, session.IntensityOf(def.Id));
            var effects = Add(card, "pg2-effects");
            foreach (var i in IndexNames.All.Where(i => Math.Abs(def.Weekly[i]) >= 0.05f).OrderByDescending(i => Math.Abs(def.Weekly[i])))
                EffectPill(effects, i, def.Weekly[i] * level);

            var likes = Residents.Cast.Where(r => r.Likes.Contains(def.Id)).ToList();
            var dislikes = Residents.Cast.Where(r => r.Dislikes.Contains(def.Id)).ToList();
            if (likes.Count + dislikes.Count > 0)
            {
                var who = Add(card, "pg2-who");
                if (likes.Count > 0) Text(who, "좋아함", "pg2-who-label");
                foreach (var r in likes) Face(who, r.Id, "pg2-face").tooltip = r.Name;
                if (dislikes.Count > 0) Text(who, "싫어함", "pg2-who-label pg2-who-label--gap");
                foreach (var r in dislikes) Face(who, r.Id, "pg2-face pg2-face--bad").tooltip = r.Name;
            }

            var foot = Add(card, "pg2-choice-foot");
            if (active)
            {
                var pips = Add(foot, "pg2-pips");
                Text(pips, "강도", "pg2-pips-label");
                bool canChange = session.CanChangeIntensity(def.Id);
                var minus = MakeButton(pips, "−", () => ChangeIntensity(def.Id, -1), "pg2-mini");
                minus.SetEnabled(canChange && level > 1);
                for (int i = 0; i < Policies.MaxIntensity; i++)
                    Add(pips, "pg2-pip" + (i < level ? " pg2-pip--on" : ""));
                var plus = MakeButton(pips, "+", () => ChangeIntensity(def.Id, +1), "pg2-mini");
                plus.SetEnabled(canChange && level < Policies.MaxIntensity);
                if (session.PolicyWindowOpen)
                    Go(foot, "중단", () => { session.Disable(def.Id); sfx.Play(Cue.Click); Refresh(); RenderPage(); }).AddToClassList("pg2-go--soft");
                else
                    Go(foot, "시행 중", null, off: true);
            }
            else
            {
                var cost = Add(foot, "pg2-pips");
                cost.Add(new LineIcon(IconKind.Bolt, Caution) { style = { width = 20, height = 20 } });
                Text(cost, $"{session.PolicyUpkeep(def.Id, 1):0.#} / 주", "pg2-pips-label");
                if (session.CanEnable(def.Id, out var reason))
                    Go(foot, "시행", () => { session.Enable(def.Id); sfx.Play(Cue.Assign); Refresh(); RenderPage(); });
                else
                    Text(foot, reason, "pg2-reason");
            }
        }

        void Forecast2(VisualElement side, PolicyDef def)
        {
            bool active = session.IsActive(def.Id);
            Text(side, $"{ForecastWeeks}주 뒤 · '{def.Name}' {(active ? "중단하면" : "시행하면")}", "pg2-h4");
            var baseline = session.Forecast(null, ForecastWeeks);
            var changed = session.Forecast(s => s.ForceActive(def.Id, !active), ForecastWeeks);
            foreach (var index in IndexNames.All)
            {
                var row = Add(side, "pg2-frow");
                row.Add(new LineIcon(IndexIcons[index]) { style = { width = 26, height = 26 } });
                Text(row, IndexShort(index), "pg2-frow-name");
                float with = changed.Last()[index], keep = baseline.Last()[index], d = with - keep;
                Text(row, $"{with:0}", "pg2-frow-value num");
                var dl = Text(row, Math.Abs(d) < 0.5f ? "–" : (d > 0 ? "▲" : "▼") + $"{Math.Abs(d):0}", "pg2-frow-delta num");
                if (Math.Abs(d) >= 0.5f) dl.style.color = IsGood(index, d) ? Good : index == Index.AutonomyReal ? Caution : Bad;
            }
            var box = Add(side, "pg2-gaugebox");
            Text(box, "위임 게이지", "pg2-gaugebox-label");
            Text(box, $"{baseline.Last().Sum:0} → {changed.Last().Sum:0}", "pg2-gaugebox-value num");
        }

        // ------------------------------------------------------------------ research

        void Research2(VisualElement body, VisualElement tools)
        {
            Pill(tools, IconKind.Data, $"정보 {session.Info:0}");
            if (!subPage.ContainsKey(Page.Research) && session.CurrentResearch != null)
                subPage[Page.Research] = Array.IndexOf(Research.AllBranches, Research.Get(session.CurrentResearch).Branch);
            int tab = subPage.TryGetValue(Page.Research, out var t) ? t : 0;
            var branch = Research.AllBranches[Mathf.Clamp(tab, 0, Research.AllBranches.Length - 1)];

            var rail = Glass2(body, "pg2-rail");
            for (int i = 0; i < Research.AllBranches.Length; i++)
            {
                int at = i; var b = Research.AllBranches[i];
                var btn = new Button(() => { subPage[Page.Research] = at; selectedResearch = null; sfx.Play(Cue.Click); RenderPage(); });
                btn.AddToClassList("pg2-branch");
                btn.EnableInClassList("pg2-branch--on", b == branch);
                var orb = Art(btn, BranchArt(b), "pg2-branch-orb"); orb.pickingMode = PickingMode.Ignore;
                Text(btn, $"{Research.BranchName(b)}", "pg2-branch-name").pickingMode = PickingMode.Ignore;
                Text(btn, $"{Research.In(b).Count(r => session.HasResearch(r.Id))}/{Research.In(b).Count()}", "pg2-branch-count num").pickingMode = PickingMode.Ignore;
                rail.Add(btn);
            }

            var nodes = Research.In(branch).ToList();
            if (Research.Get(selectedResearch)?.Branch != branch)
                selectedResearch = (nodes.FirstOrDefault(r => session.CurrentResearch == r.Id) ?? nodes.FirstOrDefault(r => session.CanResearch(r.Id, out _))
                                    ?? nodes.LastOrDefault(r => session.HasResearch(r.Id)) ?? nodes[0]).Id;

            var tree = Glass2(body, "pg2-tree");
            var lines = new VisualElement { pickingMode = PickingMode.Ignore };
            lines.AddToClassList("pg2-tree-lines");
            lines.generateVisualContent += ctx => DrawTree2(ctx, lines.contentRect, nodes);
            tree.Add(lines);
            foreach (var def in nodes)
            {
                var d = def; var spot = NodeSpot2(def);
                var node = new Button(() => { selectedResearch = d.Id; sfx.Play(Cue.Select); RenderPage(); });
                node.AddToClassList("pg2-node");
                node.style.left = Length.Percent(spot.x); node.style.top = Length.Percent(spot.y);
                bool done = session.HasResearch(def.Id), running = session.CurrentResearch == def.Id, forsaken = session.IsForsaken(def);
                bool open = !done && !running && session.CanResearch(def.Id, out _);
                node.EnableInClassList("pg2-node--done", done);
                node.EnableInClassList("pg2-node--now", running);
                node.EnableInClassList("pg2-node--open", open);
                node.EnableInClassList("pg2-node--lock", !done && !running && !open);
                node.EnableInClassList("pg2-node--forsaken", forsaken);
                node.EnableInClassList("pg2-node--sel", def.Id == selectedResearch);
                var circle = Add(node, "pg2-node-c"); circle.pickingMode = PickingMode.Ignore;
                if (running)
                {
                    var ring = new HudRing(5f) { style = { position = Position.Absolute, left = -6, top = -6, right = -6, bottom = -6 } };
                    ring.Set(session.ResearchProgress, new Color32(242, 193, 78, 255)); circle.Add(ring);
                }
                circle.Add(new LineIcon(BranchIcons[branch]) { pickingMode = PickingMode.Ignore, style = { width = 30, height = 30 } });
                Text(node, def.Name, "pg2-node-name").pickingMode = PickingMode.Ignore;
                if (def.IsDoctrine) Text(node, Research.LeanName(def.Lean), "pg2-node-lean").pickingMode = PickingMode.Ignore;
                tree.Add(node);
            }

            ResearchSide(Glass2(body, "pg2-side"), Research.Get(selectedResearch));
        }

        static Vector2 NodeSpot2(ResearchDef def) => new Vector2(12f + (def.Tier - 1) * 19f, 50f + def.Lane * 28f);

        void DrawTree2(MeshGenerationContext ctx, Rect rect, List<ResearchDef> nodes)
        {
            var p = ctx.painter2D;
            Vector2 At(Vector2 pc) => new Vector2(rect.width * pc.x / 100f, rect.height * pc.y / 100f);
            foreach (var def in nodes)
                foreach (var a in def.After)
                {
                    var from = At(NodeSpot2(Research.Get(a))) + new Vector2(42, 0); var to = At(NodeSpot2(def)) - new Vector2(42, 0);
                    bool lit = session.HasResearch(a) && (session.HasResearch(def.Id) || session.CurrentResearch == def.Id);
                    p.strokeColor = lit ? new Color32(53, 201, 232, 255) : new Color(0.55f, 0.7f, 0.86f, 0.3f);
                    p.lineWidth = lit ? 3f : 2f;
                    float mid = (from.x + to.x) / 2;
                    p.BeginPath(); p.MoveTo(from); p.BezierCurveTo(new Vector2(mid, from.y), new Vector2(mid, to.y), to); p.Stroke();
                }
        }

        void ResearchSide(VisualElement side, ResearchDef def)
        {
            bool done = session.HasResearch(def.Id), running = session.CurrentResearch == def.Id;
            string kicker = done ? "습득함" : running ? $"연구 중 · {session.ResearchProgress * 100f:0}%" : $"{def.Tier}단계" + (def.IsDoctrine ? $" · 교리 ({Research.LeanName(def.Lean)})" : "");
            Text(side, kicker, "pg2-h4");
            Text(side, def.Name, "pg2-side-title");
            Text(side, def.Description, "pg2-quote");
            Text(side, "맡게 되는 일", "pg2-h4");
            Text(side, def.Concept, "pg2-side-big");
            var opens = Add(side, "pg2-effects");
            foreach (var p in Research.Unlocks(def.Id))
                Pill(opens, IconKind.Policy, "정책 · " + p.Name);
            foreach (var i in IndexNames.All.Where(i => Math.Abs(def.Passive[i]) >= 0.05f))
                EffectPill(opens, i, def.Passive[i]);
            if (def.IsDoctrine)
                Text(side, $"'{Research.Get(def.Excludes).Name}'과 둘 중 하나만 고를 수 있다.", "pg2-note pg2-note--warn");
            var foot = Add(side, "pg2-side-foot");
            if (done) Text(foot, "이제 AI가 맡는다", "pg2-note");
            else if (running) Text(foot, $"완료 예정 {session.Date.AddDays(Mathf.CeilToInt(session.ResearchLeft / Math.Max(0.01f, session.ResearchSpeed))):M월 d일}", "pg2-note");
            else if (session.CanResearch(def.Id, out var reason))
            {
                var row = Add(foot, "pg2-effects");
                Pill(row, IconKind.Data, $"{def.Info:0}"); Pill(row, IconKind.Bolt, $"{def.Energy:0}"); Pill(row, null, $"{def.Weeks}주");
                Go(foot, def.IsDoctrine ? "이 교리를 택한다" : "연구 시작", () =>
                {
                    session.StartResearch(def.Id); sfx.Play(Cue.Assign);
                    Banner(BannerKind.Core, def.IsDoctrine ? "교리 선택" : "연구 시작", def.Name, def.Concept, 2.6f);
                    Refresh(); RenderPage();
                }).AddToClassList("pg2-go--wide");
            }
            else Text(foot, reason, "pg2-reason");
        }

        // ------------------------------------------------------------------ citizens

        void Citizens2(VisualElement body, VisualElement tools)
        {
            var def = Residents.Get(featured) ?? Residents.Cast[0];
            if (session.ViewProfile(def.Id)) { session.Write($"관찰 기록 열람: {def.Name} (정보 +{OpsTuning.InfoFirstProfile:0})"); Refresh(); }
            var seg = Add(tools, "pg2-seg");
            foreach (var (label, at) in new[] { ("열 사람", 0), ("주민 집단", 1) })
            {
                var b = MakeButton(seg, label, () => { subPage[Page.Citizens] = at; RenderPage(); }, "pg2-seg-item");
                b.EnableInClassList("pg2-seg-item--on", (subPage.TryGetValue(Page.Citizens, out var s) ? s : 0) == at);
            }
            if ((subPage.TryGetValue(Page.Citizens, out var sp) ? sp : 0) == 1)
            {
                body.AddToClassList("pg2-legacy");
                RenderGroups(body);
                return;
            }

            var roster = Add(body, "pg2-roster");
            foreach (var r in Residents.Cast)
            {
                var id = r.Id;
                var card = Add(roster, "pg2-person");
                card.EnableInClassList("pg2-person--sel", r.Id == def.Id);
                card.EnableInClassList("pg2-person--new", !session.Profiles.Contains(r.Id));
                var face = new CastPortrait(r.Id, tall: true) { Expression = MoodFace(r) };
                face.AddToClassList("pg2-person-face"); card.Add(face);
                Add(card, "pg2-person-shade").pickingMode = PickingMode.Ignore;
                int dom = session.Dominant(r.Id);
                var st = Text(card, OpsSession.StanceLabel(dom), "pg2-stance");
                st.style.backgroundColor = dom < 0 ? new Color(0.55f, 0.63f, 0.75f, 0.75f) : StanceTints[dom];
                var mood = Add(card, "pg2-person-mood"); var fill = Add(mood, "pg2-person-mood-fill");
                float m = session.Mood(r); fill.style.width = Length.Percent(m); fill.style.backgroundColor = ToneColor(m);
                var info = Add(card, "pg2-person-info");
                Text(info, r.Name, "pg2-person-name");
                Text(info, r.Job, "pg2-person-job");
                card.RegisterCallback<ClickEvent>(_ => { featured = id; sfx.Play(Cue.Select); RenderPage(); RefreshResident(); });
            }

            var side = Glass2(body, "pg2-side");
            Text(side, $"{def.Name} · {def.Age}", "pg2-h4");
            var line = session.Line(def);
            Text(side, $"“{line ?? def.Theme}”", "pg2-quote pg2-quote--big");
            var tabs = Add(side, "pg2-seg pg2-seg--side");
            string[] names = { "마음", "입장과 관계", "이야기" };
            for (int i = 0; i < names.Length; i++)
            {
                int at = i;
                MakeButton(tabs, names[i], () => { citizenTab = at; sfx.Play(Cue.Click); RenderPage(); }, "pg2-seg-item" + (citizenTab == i ? " pg2-seg-item--on" : ""));
            }
            var content = Scroll(side, "pg2-side-scroll").contentContainer;
            if (citizenTab == 1) { RenderCitizenStance(content, def); }
            else if (citizenTab == 2) { RenderCitizenStory(content, def); }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    float v = session.StanceValue(def.Id, (Stance)i);
                    var row = Add(content, "pg2-bar-row");
                    Text(row, Story.StanceName((Stance)i), "pg2-bar-name");
                    var bar = Add(row, "pg2-bar"); var f = Add(bar, "pg2-bar-fill");
                    f.style.width = Length.Percent(v); f.style.backgroundColor = StanceTints[i];
                    Text(row, $"{v:0}", "pg2-bar-value num");
                }
                Text(content, "요즘", "pg2-h4");
                string Names(IEnumerable<string> ids) => string.Join(", ", ids.Select(Policies.Get).Where(p => p != null).Select(p => p.Name + (session.IsActive(p.Id) ? " ●" : "")));
                if (def.Likes.Length > 0) Text(content, "좋아한다 · " + Names(def.Likes), "pg2-line");
                if (def.Dislikes.Length > 0) Text(content, "싫어한다 · " + Names(def.Dislikes), "pg2-line");
                Text(content, def.Bio, "pg2-line pg2-line--dim");
            }
            var foot = Add(side, "pg2-side-foot pg2-btnrow");
            Go(foot, "카메라로", () =>
            {
                int zone = Society.HomeOf(def.Id);
                if (zone >= 0) sim.ShowZone(zone);
                watchCamera = true; subPage[Page.Watch] = 0; GoTo(Page.Watch);
            }).AddToClassList("pg2-go--wide");
        }

        // ------------------------------------------------------------------ observation

        void Watch2(VisualElement body, VisualElement tools)
        {
            int tab = subPage.TryGetValue(Page.Watch, out var t) ? t : 0;
            var seg = Add(tools, "pg2-seg");
            string[] names = { "실시간", "구역 현황", "이상 징후", "집단 동향", "예측" };
            for (int i = 0; i < names.Length; i++)
            {
                int at = i;
                MakeButton(seg, names[i], () => { subPage[Page.Watch] = at; RenderPage(); }, "pg2-seg-item" + (tab == i ? " pg2-seg-item--on" : ""));
            }
            if (tab != 0)
            {
                simHome.Add(sim); sim.SetTracking(false); watchAlert = null;
                body.AddToClassList("pg2-legacy");
                if (tab == 1) RenderZones(body); else if (tab == 2) RenderWarnings(body); else if (tab == 3) RenderBehavior(body); else RenderPrediction(body);
                return;
            }

            var feed = Glass2(body, "pg2-feed");
            var bar = Add(feed, "pg2-feed-bar");
            Text(bar, "● LIVE", "pg2-live");
            if (!watchCamera)
            {
                simHome.Add(sim); sim.SetTracking(false); watchAlert = null;
                Text(bar, "도시 전체 · 지구를 누르면 그 자리의 카메라로", "pg2-cam");
                var twin = new TwinView(ZoneMap.GridShapes, session.TensionStage,
                    z => session.Anomalies.Count(a => a.Zone == z), session.ZoneOpen,
                    z => Mathf.Clamp(Mathf.RoundToInt(session.ZonePop[z] / 120f), 4, 40),
                    z => $"{Society.Zones[z].Name} · {Tension.StageNames[session.TensionStage(z)]}");
                twin.ZoneClicked += z => { sim.ShowZone(z); watchCamera = true; sfx.Play(Cue.Select); RenderPage(); };
                twin.AddToClassList("pg2-twin");
                feed.Add(twin);
            }
            else
            {
                Text(bar, $"CAM-{sim.Zone + 1:00} · {SimPanel.ZoneNames[sim.Zone]}", "pg2-cam");
                watchAlert = Text(bar, "", "pg2-alert");
                watchAlert.style.display = DisplayStyle.None;
                var host = Add(feed, "pg2-sim");
                host.Add(sim);
                host.SendToBack();   // the LIVE bar and the chips stay on top of the camera
                sim.SetTracking(true);
            }
            // zone chips: the whole city, then every open district
            var chips = Add(feed, "pg2-feed-chips");
            var all = MakeButton(chips, "도시 전체", () => { watchCamera = false; sfx.Play(Cue.Click); RenderPage(); }, "pg2-seg-item");
            all.EnableInClassList("pg2-seg-item--on", !watchCamera);
            for (int z = 0; z < Society.Zones.Count; z++)
            {
                if (!session.ZoneOpen(z)) continue;
                int zone = z;
                var b = MakeButton(chips, Society.Zones[z].Name.Replace(" 지구", ""), () => { sim.ShowZone(zone); watchCamera = true; sfx.Play(Cue.Click); RenderPage(); }, "pg2-seg-item");
                b.EnableInClassList("pg2-seg-item--on", watchCamera && sim.Zone == z);
                int stage = session.TensionStage(z);
                if (stage >= 2) b.style.color = StageColor(stage);
            }

            var side = Glass2(body, "pg2-side");
            Text(side, $"이상 징후 · {session.Anomalies.Count}", "pg2-h4");
            var list = Scroll(side, "pg2-side-scroll").contentContainer;
            if (session.Anomalies.Count == 0)
                Text(list, "지금은 카메라에 잡힌 이상 행동이 없다.", "pg2-line pg2-line--dim");
            foreach (var a in session.Anomalies.OrderByDescending(a => a.Kind).ThenBy(a => a.Expire).Take(8))
            {
                var anomaly = a;
                var card = Add(list, "pg2-acard" + (a.Kind >= AnomalyKind.Gathering ? " pg2-acard--bad" : ""));
                if (a.Subject != null && Residents.Get(a.Subject) != null) Face(card, a.Subject, "pg2-acard-pic");
                else { var pic = Add(card, "pg2-acard-pic"); pic.Add(new LineIcon(IconKind.Alert, a.Kind >= AnomalyKind.Gathering ? Bad : Caution) { style = { width = 30, height = 30 } }); }
                var tx = Add(card, "pg2-acard-text");
                Text(tx, a.Text, "pg2-acard-title");
                Text(tx, $"{Society.Zones[a.Zone].Name} · {Tension.KindName(a.Kind)} · D-{Math.Max(0, a.Expire - session.Day)}", "pg2-acard-sub");
                card.RegisterCallback<ClickEvent>(_ => { sfx.Play(Cue.Select); OpenAnomaly(anomaly); });
            }
            Text(side, "사회 동향", "pg2-h4");
            foreach (var g in Society.Groups)
            {
                float v = session.GroupSat[(int)g];
                var row = Add(side, "pg2-bar-row");
                Text(row, Society.Get(g).Name, "pg2-bar-name");
                var b = Add(row, "pg2-bar"); var f = Add(b, "pg2-bar-fill"); f.style.width = Length.Percent(v); f.style.backgroundColor = ToneColor(v);
                Text(row, $"{v:0}", "pg2-bar-value num");
            }
            if (watchCamera && sim.VisibleCast.Any())
            {
                Text(side, "이 카메라의 고정 관찰 대상", "pg2-h4");
                var who = Add(side, "pg2-who");
                foreach (var id in sim.VisibleCast)
                {
                    var f = Face(who, id, "pg2-face"); f.tooltip = Residents.Get(id).Name;
                    f.RegisterCallback<ClickEvent>(_ => ShowResident(id));
                }
            }
        }

        // ------------------------------------------------------------------ facilities

        void Facility2(VisualElement body, VisualElement tools)
        {
            var seg = Add(tools, "pg2-seg");
            for (int z = 0; z < session.Zones; z++)
            {
                int zone = z;
                var b = MakeButton(seg, $"{Society.Zones[z].Name}", () => { buildZone = zone; sfx.Play(Cue.Click); RenderPage(); }, "pg2-seg-item");
                b.EnableInClassList("pg2-seg-item--on", z == buildZone);
                b.tooltip = $"부지 {session.UsedSlots(z)}/{session.ZoneSlots}";
            }
            var grid = Add(Scroll(body, "pg2-fscroll").contentContainer, "pg2-fgrid");
            foreach (var def in Facilities.All)
            {
                int owned = session.Built.Count(b => b == def.Id);
                bool can = session.CanBuild(def.Id, buildZone, out var reason);
                bool suits = OpsSession.Suits(def.Id, buildZone);
                var card = Glass2(grid, "pg2-fac");
                card.EnableInClassList("pg2-fac--built", owned > 0);
                if (owned > 0) Text(card, $"{owned}곳", "pg2-fac-count");
                var big = Add(card, "pg2-fac-icon");
                big.Add(new LineIcon(FacilityIcon(def)) { style = { width = 34, height = 34 } });
                Text(card, def.Name, "pg2-fac-name");
                var fx = Add(card, "pg2-effects");
                if (def.Power > 0) Pill(fx, IconKind.Bolt, $"+{def.Power:0}/주");
                if (def.Info > 0) Pill(fx, IconKind.Data, $"+{def.Info:0}/주");
                foreach (var i in IndexNames.All.Where(i => Math.Abs(def.Weekly[i]) >= 0.05f))
                    EffectPill(fx, i, def.Weekly[i] * (suits ? 1.5f : 1f));
                if (suits) Text(card, "이 구역에 어울림 · ×1.5", "pg2-note pg2-note--good");
                var foot = Add(card, "pg2-fac-foot");
                var cost = Add(foot, "pg2-pips");
                cost.Add(new LineIcon(IconKind.Bolt, Caution) { style = { width = 20, height = 20 } });
                Text(cost, $"{def.Cost:0}", "pg2-fac-cost num");
                if (can) Go(foot, "건설", () => { session.Build(def.Id, buildZone); sfx.Play(Cue.Assign); Refresh(); RenderPage(); });
                else Text(foot, reason, "pg2-reason");
                card.tooltip = def.Description;
            }

            var side = Glass2(body, "pg2-side");
            Text(side, "부지", "pg2-h4");
            Text(side, $"{session.Built.Count} / {session.FacilitySlots}", "pg2-side-title num");
            Text(side, Society.Zones[buildZone].Name, "pg2-h4");
            Text(side, Society.Zones[buildZone].Trait, "pg2-line");
            if (session.Built.Count > 0)
            {
                Text(side, "지은 시설", "pg2-h4");
                var list = Scroll(side, "pg2-side-scroll").contentContainer;
                for (int i = 0; i < session.Built.Count; i++)
                {
                    int at = i;
                    var row = Add(list, "pg2-built");
                    Text(row, $"{Facilities.Get(session.Built[i]).Name} · {Society.Zones[session.BuiltZone[i]].Name}", "pg2-line");
                    MakeButton(row, "철거", () => { session.Demolish(at); sfx.Play(Cue.Click); Refresh(); RenderPage(); }, "pg2-mini pg2-mini--text");
                }
            }
        }

        static IconKind FacilityIcon(FacilityDef def) => def.Power > 0 ? IconKind.Bolt : def.Info > 0 ? IconKind.Eye : def.Storage > 0 ? IconKind.Data
            : def.Weekly[Index.Happiness] > 0 ? IconKind.Smile : def.Weekly[Index.Stability] > 0 ? IconKind.Shield : IconKind.Facility;

        // ------------------------------------------------------------------ report

        void Report2(VisualElement body, VisualElement tools)
        {
            var r = session.Report ?? session.LastReport ?? lastReport;
            if (r == null) { Text(body, "아직 결산이 없다. 매달 1일에 지난달의 결산이 나온다.", "pg2-empty"); return; }
            bool pending = session.Report != null;
            var seg = Add(tools, "pg2-seg");
            string[] names = { "요약", "시민", "정책", "사건" };
            int tab = subPage.TryGetValue(Page.Report, out var t) ? t : 0;
            for (int i = 0; i < names.Length; i++)
            {
                int at = i;
                MakeButton(seg, names[i], () => { subPage[Page.Report] = at; RenderPage(); }, "pg2-seg-item" + (tab == i ? " pg2-seg-item--on" : ""));
            }
            if (tab != 0)
            {
                body.AddToClassList("pg2-legacy");
                if (tab == 1) RenderReportCitizens(body, r);
                else if (tab == 2) RenderReportPolicies(body, r);
                else foreach (var id in Enumerable.Reverse(r.EventIds))
                    {
                        var e = OpsEvents.Get(id); if (e == null) continue;
                        var row = Glass2(body, "pg2-event");
                        Art(row, EventArt(e.Kind), "pg2-event-art");
                        var tx = Add(row, "pg2-event-text"); Text(tx, e.Title, "pg2-line pg2-line--b"); Text(tx, e.Text, "pg2-line pg2-line--dim");
                    }
                return;
            }

            var grade = Glass2(body, "pg2-grade");
            var ring = Add(grade, "pg2-grade-ring");
            ring.style.borderTopColor = ring.style.borderBottomColor = ring.style.borderLeftColor = ring.style.borderRightColor = GradeColor(r.Grade);
            Text(ring, r.Grade, "pg2-grade-letter num");
            Text(grade, $"{r.Month:yyyy년 M월}", "pg2-h4");
            Text(grade, GradeLine(r), "pg2-line pg2-line--center");
            var j = Text(grade, r.Judgment, "pg2-note pg2-line--center");
            if (r.FallStreak > 0) j.AddToClassList("pg2-note--warn");
            if (r.Unlock != null) Text(grade, r.Unlock, "pg2-note pg2-note--good pg2-line--center");
            var foot = Add(grade, "pg2-side-foot pg2-btnrow");
            if (pending)
            {
                Go(foot, "정책 조정", () => GoTo(Page.Policy)).AddToClassList("pg2-go--soft");
                Go(foot, "다음 달로", ResumeFromReport);
            }

            var mid = Add(body, "pg2-rmid");
            var ich = Glass2(mid, "pg2-ichanges");
            foreach (var index in IndexNames.All)
            {
                var c = Add(ich, "pg2-ich");
                c.Add(new LineIcon(IndexIcons[index]) { style = { width = 34, height = 34 } });
                float a = r.After[index], d = a - r.Before[index];
                Text(c, $"{a:0}", "pg2-ich-value num");
                var dl = Text(c, Math.Abs(d) < 0.05f ? "–" : (d > 0 ? "▲" : "▼") + $"{Math.Abs(d):0.#}", "pg2-ich-delta num");
                if (Math.Abs(d) >= 0.05f) dl.style.color = IsGood(index, d) ? Good : index == Index.AutonomyReal ? Caution : Bad;
                Text(c, IndexShort(index), "pg2-ich-name");
            }
            var rows = Add(mid, "pg2-rrow");
            var groups = Glass2(rows, "pg2-rbox");
            Text(groups, "집단", "pg2-h4");
            for (int g = 0; g < r.GroupsAfter.Length && g < Society.Groups.Length; g++)
            {
                float a = r.GroupsAfter[g], d = g < r.GroupsBefore.Length ? a - r.GroupsBefore[g] : 0f;
                var row = Add(groups, "pg2-bar-row");
                Text(row, Society.Get(Society.Groups[g]).Name, "pg2-bar-name");
                var bar = Add(row, "pg2-bar"); var f = Add(bar, "pg2-bar-fill"); f.style.width = Length.Percent(a); f.style.backgroundColor = ToneColor(a);
                Text(row, $"{a:0}", "pg2-bar-value num");
                var dl = Text(row, Math.Abs(d) < 0.5f ? "–" : (d > 0 ? "▲" : "▼") + $"{Math.Abs(d):0}", "pg2-bar-value num");
                if (Math.Abs(d) >= 0.5f) dl.style.color = d > 0 ? Good : Bad;
            }
            var why = Glass2(rows, "pg2-rbox");
            Text(why, "이번 달", "pg2-h4");
            foreach (var line in Causes(r)) TimelineItem(why, null, line, null, "");
            foreach (var id in Enumerable.Reverse(r.EventIds).Take(3))
            {
                var e = OpsEvents.Get(id); if (e != null) TimelineItem(why, null, e.Title, OpsEvents.KindName(e.Kind), "warn");
            }
            TimelineItem(why, null, $"인구 {Signed(r.PopulationAfter - r.PopulationBefore)} · {Population(r.PopulationAfter)}", $"에너지 {Signed(r.EnergyChange)} · 정보 {Signed(r.InfoChange)}", "");
        }

        void TimelineItem(VisualElement parent, string date, string title, string sub, string tone)
        {
            var row = Add(parent, "pg2-tli" + (string.IsNullOrEmpty(tone) ? "" : " pg2-tli--" + tone));
            if (date != null) Text(row, date, "pg2-tli-date num");
            Add(row, "pg2-tli-dot");
            var tx = Add(row, "pg2-tli-text");
            Text(tx, title, "pg2-tli-title");
            if (!string.IsNullOrEmpty(sub)) Text(tx, sub, "pg2-tli-sub");
        }

        // ------------------------------------------------------------------ log

        void Log2(VisualElement body, VisualElement tools)
        {
            int tab = subPage.TryGetValue(Page.Log, out var t) ? t : 0;
            var seg = Add(tools, "pg2-seg");
            string[] names = { "운영", "목표", $"기밀 문서 {session.Documents.Count}" };
            for (int i = 0; i < names.Length; i++)
            {
                int at = i;
                MakeButton(seg, names[i], () => { subPage[Page.Log] = at; RenderPage(); }, "pg2-seg-item" + (tab == i ? " pg2-seg-item--on" : ""));
            }
            if (tab != 0)
            {
                body.AddToClassList("pg2-legacy");
                var main = Add(body, "pg-main");
                if (tab == 1) RenderGoalLog(main); else RenderDocuments(main);
                return;
            }
            var list = Scroll(Glass2(body, "pg2-timeline"), "pg2-side-scroll").contentContainer;
            foreach (var line in Enumerable.Reverse(session.Log).Take(80))
            {
                int sp = line.IndexOf(' ');
                string date = sp > 0 ? line.Substring(5, Math.Min(5, sp - 5)).Replace('.', '.') : "";
                string text = sp > 0 ? line.Substring(sp + 1) : line;
                var (icon, color) = AlertIcon(line);
                string tone = color == Bad ? "bad" : color == Caution ? "warn" : color == Good ? "good" : "";
                TimelineItem(list, date, text, null, tone);
            }
            var side = Glass2(body, "pg2-side");
            RenderGoalTracker(side);
        }
    }
}
