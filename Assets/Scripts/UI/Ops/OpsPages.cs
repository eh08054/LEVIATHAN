using System;
using System.Collections.Generic;
using System.Linq;
using Sector.Ops;
using UnityEngine;
using UnityEngine.UIElements;
using Index = Sector.Ops.Index;
using Random = UnityEngine.Random;

namespace Sector.UI
{
    public enum Page
    {
        Overview,  // 도시 현황
        Policy,    // 정책 결정
        Research,  // 연구 개발
        Citizens,  // 시민 관찰
        Watch,     // 관찰 시스템
        Facility,  // 도시 시설
        Report,    // 결산 보고서
        Log,       // 운영 기록
    }

    // The left menu and every page except the overview (OpsHud.cs). Pages are rebuilt from the session when shown,
    // after actions, and once a week while open; the scroll position is kept across rebuilds.
    public partial class OpsScreen
    {
        // Weeks the policy forecast looks ahead.
        const int ForecastWeeks = 12;

        static readonly (Page page, IconKind icon, string label, string english)[] Menu =
        {
            (Page.Overview, IconKind.Gauge, "도시 현황", "CITY OVERVIEW"),
            (Page.Policy, IconKind.Policy, "정책 결정", "POLICY DECISION"),
            (Page.Research, IconKind.Research, "연구 개발", "RESEARCH & DEVELOPMENT"),
            (Page.Citizens, IconKind.People, "시민 관찰", "CITIZEN PROFILE"),
            (Page.Watch, IconKind.Eye, "관찰 시스템", "OBSERVATION"),
            (Page.Facility, IconKind.Facility, "도시 시설", "FACILITIES"),
            (Page.Report, IconKind.Star, "결산 보고서", "SETTLEMENT REPORT"),
            (Page.Log, IconKind.Log, "운영 기록", "OPERATION LOG"),
        };

        Page currentPage = Page.Overview;
        VisualElement pageHost;
        VisualElement overviewPage;
        VisualElement pageView;
        VisualElement dim;
        VisualElement simHome;
        readonly Dictionary<Page, Button> menuButtons = new Dictionary<Page, Button>();
        readonly Dictionary<Page, Label> menuBadges = new Dictionary<Page, Label>();

        // Page state. NonSerialized: a script reload in play mode would otherwise turn null strings into "".
        [NonSerialized] Field selectedField = Field.Security;
        [NonSerialized] string selectedPolicy;
        [NonSerialized] string selectedResearch;
        [NonSerialized] MonthlyReport lastReport;
        [NonSerialized] MonthlyReport shownReport;
        Label watchAlert;
        float watchTimer;

        // ------------------------------------------------------------------ menu and navigation

        void BuildSidebar()
        {
            var bar = Add(root, "sidebar");
            foreach (var (page, icon, label, _) in Menu)
            {
                var p = page;
                var button = new Button(() => GoTo(p));
                button.AddToClassList("side-item");
                Badge(button, icon, null, "badge badge--small");
                Text(button, label, "side-label").pickingMode = PickingMode.Ignore;
                var badge = Text(button, "", "side-badge");
                badge.pickingMode = PickingMode.Ignore;
                bar.Add(button);
                menuButtons[page] = button;
                menuBadges[page] = badge;
            }
            var foot = Add(bar, "side-foot");
            Text(foot, "SECTOR 7 · 2041 PILOT", "side-foot-text");
        }

        void GoTo(Page page)
        {
            if (page != currentPage)
                sfx?.Play(Cue.Click);
            currentPage = page;
            CloseDistrict();
            bool overview = page == Page.Overview;
            overviewPage.style.display = overview ? DisplayStyle.Flex : DisplayStyle.None;
            pageView.style.display = overview ? DisplayStyle.None : DisplayStyle.Flex;
            dim.style.display = overview ? DisplayStyle.None : DisplayStyle.Flex;
            foreach (var pair in menuButtons)
                pair.Value.EnableInClassList("side-item--on", pair.Key == page);
            if (page != Page.Watch)
            {
                simHome.Add(sim);
                sim.SetTracking(false);
            }
            RenderPage();
        }

        void RefreshPage()
        {
            if (currentPage != Page.Overview)
                RenderPage();
        }

        void RenderPage()
        {
            if (currentPage == Page.Overview)
                return;
            var scroll = pageView.Q<ScrollView>();
            var offset = scroll != null ? scroll.scrollOffset : Vector2.zero;
            pageView.Clear();
            var (_, icon, label, english) = Menu.First(m => m.page == currentPage);
            var head = Add(pageView, "page-head");
            Badge(head, icon, null, "badge badge--large");
            var titles = Add(head, "page-titles");
            Text(titles, label, "page-title");
            Text(titles, english, "page-sub");
            var tools = Add(head, "page-tools");
            var body = Add(pageView, "page-body");
            switch (currentPage)
            {
                case Page.Policy: RenderPolicy(body, tools); break;
                case Page.Research: RenderResearch(body, tools); break;
                case Page.Citizens: RenderCitizens(body); break;
                case Page.Watch: RenderWatch(body); break;
                case Page.Facility: RenderFacility(body, tools); break;
                case Page.Report: RenderReport(body, tools); break;
                case Page.Log: RenderLog(body); break;
            }
            var after = pageView.Q<ScrollView>();
            if (after != null)
                after.schedule.Execute(() => after.scrollOffset = offset);
        }

        void RefreshMenu()
        {
            menuBadges[Page.Policy].text = session.PolicyWindowOpen ? "결정" : $"{session.Active.Count}/{session.Slots}";
            menuBadges[Page.Policy].EnableInClassList("side-badge--hot", session.PolicyWindowOpen);
            menuBadges[Page.Research].text = session.CurrentResearch != null ? $"{session.ResearchProgress * 100f:0}%" : "대기";
            menuBadges[Page.Research].EnableInClassList("side-badge--hot", session.CurrentResearch == null);
            int unread = Residents.Cast.Count(r => !session.Profiles.Contains(r.Id));
            menuBadges[Page.Citizens].text = unread > 0 ? $"{unread}" : "";
            menuBadges[Page.Facility].text = $"{session.Built.Count}/{session.FacilitySlots}";
            menuBadges[Page.Report].text = session.Report != null ? "!" : "";
            menuBadges[Page.Report].EnableInClassList("side-badge--hot", session.Report != null);
            menuBadges[Page.Watch].text = session.Anomalies.Count > 0 ? $"{session.Anomalies.Count}" : "LIVE";
            menuBadges[Page.Watch].EnableInClassList("side-badge--hot", session.Anomalies.Count > 0);
        }

        // The old entry points now open pages.
        void ShowPolicies(Action unused = null) => GoTo(Page.Policy);
        void ShowResearch() => GoTo(Page.Research);
        void ShowFacilities() => GoTo(Page.Facility);
        void ShowResidents() => GoTo(Page.Citizens);
        void ShowLog() => GoTo(Page.Log);

        void ShowResident(string id, Action unused = null)
        {
            featured = id;
            GoTo(Page.Citizens);
        }

        void ShowReport()
        {
            shownReport = session.Report;
            if (session.Report != null)
                lastReport = session.Report;
            GoTo(Page.Report);
        }

        Expression MoodFace(ResidentDef def)
        {
            float mood = session.Mood(def);
            return mood > 65f ? Expression.Happy : mood < 35f ? Expression.Sad : Expression.Neutral;
        }

        static string FieldArt(Field field) => "Illust/field_" + field.ToString().ToLowerInvariant();

        static string BranchArt(Branch branch) =>
            "Illust/branch_" + (branch == Branch.LifeScience ? "life" : branch.ToString().ToLowerInvariant());

        static string EventArt(EventKind kind) => "Illust/event_" + kind.ToString().ToLowerInvariant();

        static VisualElement Art(VisualElement parent, string path, string classes)
        {
            var element = Add(parent, classes);
            var texture = PixelArt.Load(path);
            if (texture != null)
                element.style.backgroundImage = new StyleBackground(texture);
            return element;
        }

        static ScrollView Scroll(VisualElement parent, string classes)
        {
            var scroll = new ScrollView();
            foreach (var c in classes.Split(' '))
                scroll.AddToClassList(c);
            parent.Add(scroll);
            return scroll;
        }

        VisualElement Card(VisualElement parent, string classes, IconKind icon, string title)
        {
            var card = Add(parent, "glass " + classes);
            var head = Add(card, "glass-head");
            Badge(head, icon);
            Text(head, title, "glass-title");
            Add(card, "glass-line");
            return card;
        }

        // ------------------------------------------------------------------ policy

        void RenderPolicy(VisualElement body, VisualElement tools)
        {
            Text(tools, $"정책 슬롯 {session.Active.Count}/{session.Slots}", "ops-badge");
            var window = Text(tools, session.PolicyWindowOpen ? "결정 기간: 시행·중단 가능" : "시행·중단은 다음 결산 때 · 강도는 주 1회", "pill");
            window.EnableInClassList("pill--hot", session.PolicyWindowOpen);
            // Came here from the monthly report: one click finishes the settlement and lets time run.
            if (session.Report != null)
                MakeButton(tools, "결정 완료 · 운영 재개", () =>
                {
                    paused = false;
                    FinishSettlement();
                    sfx.Play(Cue.Click);
                    RefreshTime();
                }, "kbtn");

            body.AddToClassList("pg-row");
            var tabs = Add(body, "pg-tabs glass");
            foreach (var field in Policies.AllFields)
            {
                var f = field;
                bool open = session.OpenFields.Contains(field);
                var tab = new Button(() =>
                {
                    selectedField = f;
                    selectedPolicy = null;
                    RenderPage();
                });
                tab.AddToClassList("pg-tab");
                tab.EnableInClassList("pg-tab--on", field == selectedField);
                tab.EnableInClassList("pg-tab--locked", !open);
                Text(tab, Policies.FieldName(field), "pg-tab-label").pickingMode = PickingMode.Ignore;
                int running = Policies.All.Count(p => p.Field == field && session.IsActive(p.Id));
                Text(tab, !open ? "미위임" : running > 0 ? "시행 중" : "", "pg-tab-state").pickingMode = PickingMode.Ignore;
                tabs.Add(tab);
            }

            var main = Add(body, "pg-main");
            var banner = Art(main, FieldArt(selectedField), "banner");
            var bannerText = Add(banner, "banner-text");
            Text(bannerText, Policies.FieldName(selectedField), "banner-title");
            Text(bannerText, session.OpenFields.Contains(selectedField) ? "위임된 분야" : "아직 위임되지 않은 분야: 위임 단계가 오르면 열린다", "banner-sub");
            // Cards can be long (effects, side effects, groups, links): they scroll inside the page.
            var cardScroll = Scroll(main, "pg-cards-scroll");
            var cards = Add(cardScroll.contentContainer, "pg-cards");
            var defs = Policies.All.Where(p => p.Field == selectedField).OrderBy(p => p.Kind).ToList();
            if (string.IsNullOrEmpty(selectedPolicy) || defs.All(d => d.Id != selectedPolicy))
                selectedPolicy = (defs.FirstOrDefault(d => session.IsActive(d.Id)) ?? defs.FirstOrDefault(d => session.IsUnlocked(d)) ?? defs[0]).Id;
            foreach (var def in defs)
                cards.Add(PolicyCard(def));

            RenderForecast(Add(body, "pg-side glass"), Policies.Get(selectedPolicy));
        }

        VisualElement PolicyCard(PolicyDef def)
        {
            var card = new VisualElement();
            card.AddToClassList("policy-card");
            card.AddToClassList(def.Kind == PolicyKind.Control ? "policy-card--control" : "policy-card--guide");
            bool active = session.IsActive(def.Id);
            card.EnableInClassList("policy-card--active", active);
            card.EnableInClassList("policy-card--selected", def.Id == selectedPolicy);
            card.RegisterCallback<ClickEvent>(_ =>
            {
                if (selectedPolicy == def.Id)
                    return;
                selectedPolicy = def.Id;
                RenderPage();
            });
            var top = Add(card, "policy-card-top");
            var kind = Text(top, Policies.KindName(def.Kind), "kind");
            kind.AddToClassList(def.Kind == PolicyKind.Control ? "kind--control" : "kind--guide");
            Text(top, def.Name, "policy-card-title");
            if (active)
                Text(top, "시행 중", "pill pill--on");
            Text(card, def.Description, "policy-card-text");

            int level = Math.Max(1, session.IntensityOf(def.Id));
            var effects = IndexNames.All.Select(i => (index: i, v: def.Weekly[i] * level)).Where(x => Math.Abs(x.v) >= 0.05f).ToList();
            void EffectBlock(string title, IEnumerable<(Index index, float v)> rows, bool good)
            {
                var list = rows.ToList();
                if (list.Count == 0)
                    return;
                Text(card, title, "section-title" + (good ? "" : " section-title--bad"));
                var table = Add(card, "effect-table");
                foreach (var (index, v) in list)
                {
                    var row = Add(table, "effect-row");
                    Text(row, IndexNames.Name(index), "effect-name");
                    var value = Text(row, $"{(v > 0 ? "+" : "")}{v:0.#} / 주", "effect-value num");
                    value.EnableInClassList("effect-value--down", !good);
                }
            }
            EffectBlock("기대 효과", effects.Where(x => IsGood(x.index, x.v)), true);
            if (def.ResearchSpeed > 0f || def.UnrestChance < 0f)
            {
                var extra = Add(card, "effect-table");
                var row = Add(extra, "effect-row");
                Text(row, def.ResearchSpeed > 0f ? "연구 속도" : "민원·시위 발생", "effect-name");
                Text(row, def.ResearchSpeed > 0f ? $"+{def.ResearchSpeed * level * 100f:0}%" : "감소", "effect-value num");
            }
            EffectBlock("부작용", effects.Where(x => !IsGood(x.index, x.v)), false);

            Text(card, "운영 정보", "section-title");
            var info = Add(card, "effect-table");
            void Info(string name, string value, string tone = null)
            {
                var row = Add(info, "effect-row");
                Text(row, name, "effect-name");
                var v = Text(row, value, "info-value");
                if (tone != null)
                    v.AddToClassList(tone);
            }
            Info("에너지 유지비", $"{session.PolicyUpkeep(def.Id, level):0.#} / 주");
            Info("상태", active ? $"시행 {Math.Max(0, session.WeeksActive(def.Id))}주째 · 강도 {level}" : "미시행");
            var nextMonth = new DateTime(session.Date.Year, session.Date.Month, 1).AddMonths(1);
            Info("시행·중단", session.PolicyWindowOpen ? "지금 가능 (결정 기간)" : $"다음 결산 {nextMonth:yyyy.MM.dd}", session.PolicyWindowOpen ? "tone-good" : null);
            Info("적용 범위", $"관리 구역 전체 ({session.Zones}구역)");

            if (def.Kind == PolicyKind.Control)
            {
                float wear = active ? session.Habituation(def.Id) : 1f;
                var wearRow = Add(card, "wear-row");
                Text(wearRow, "효과 유지율", "wear-label");
                MiniBar(wearRow, wear * 100f, wear < 0.8f ? Caution : Good, "mini-bar mini-bar--wide");
                Text(wearRow, active ? $"{wear * 100f:0}%" : "시행 후 점점 약해짐", "wear-value").AddToClassList(wear < 0.8f ? "tone--warn" : "tone--good");
            }

            if (Society.Reactions.TryGetValue(def.Id, out var reactions))
            {
                Text(card, "집단 반응 (주간 만족도)", "section-title");
                var chips = Add(card, "chip-row");
                foreach (var group in Society.Groups)
                {
                    float v = reactions[(int)group] * level;
                    if (Math.Abs(v) < 0.05f)
                        continue;
                    var chip = Text(chips, $"{Society.Get(group).Name} {(v > 0 ? "▲" : "▼")}{Math.Abs(v):0.#}", "chip chip--live");
                    chip.AddToClassList(v > 0 ? "chip--like" : "chip--dislike");
                }
            }

            foreach (var link in Society.LinksOf(def.Id))
            {
                var other = Policies.Get(Society.Other(link, def.Id));
                bool live = session.IsActive(def.Id) && session.IsActive(other.Id);
                var row = Text(card, $"{(link.Conflict ? "충돌" : "연계")} · {link.Name}: '{other.Name}'과(와) 함께 {link.Weekly.Describe()}",
                    "link-row");
                row.AddToClassList(link.Conflict ? "link-row--conflict" : "link-row--synergy");
                row.EnableInClassList("link-row--live", live);
            }

            var actions = Add(card, "policy-card-actions");
            if (active)
            {
                bool canChange = session.CanChangeIntensity(def.Id);
                var minus = MakeButton(actions, "−", () => ChangeIntensity(def.Id, -1), "ops-small");
                Text(actions, $"강도 {new string('●', level)}{new string('○', Policies.MaxIntensity - level)}", "ops-intensity");
                var plus = MakeButton(actions, "+", () => ChangeIntensity(def.Id, +1), "ops-small");
                minus.SetEnabled(canChange && level > 1);
                plus.SetEnabled(canChange && level < Policies.MaxIntensity);
                if (!canChange)
                    Text(actions, "이번 주 조정함", "ops-note");
                if (session.PolicyWindowOpen)
                    MakeButton(actions, "중단", () =>
                    {
                        session.Disable(def.Id);
                        sfx.Play(Cue.Click);
                        Refresh();
                        RenderPage();
                    }, "ops-small");
            }
            else if (session.CanEnable(def.Id, out var reason))
            {
                MakeButton(actions, "정책 시행", () =>
                {
                    session.Enable(def.Id);
                    sfx.Play(Cue.Assign);
                    Refresh();
                    RenderPage();
                }, "ops-primary");
            }
            else
                Text(actions, reason, "ops-reason");
            return card;
        }

        void ChangeIntensity(string id, int step)
        {
            sfx.Play(session.SetIntensity(id, session.IntensityOf(id) + step) ? Cue.Click : Cue.Deny);
            Refresh();
            RenderPage();
        }

        // What happens over the next weeks with and without this policy (no events in the forecast).
        void RenderForecast(VisualElement side, PolicyDef def)
        {
            var head = Add(side, "glass-head");
            head.Add(new LineIcon(IconKind.Gauge));
            Text(head, $"예상 변화 · {ForecastWeeks}주", "glass-title");
            Add(side, "glass-line");
            bool active = session.IsActive(def.Id);
            Text(side, active ? $"'{def.Name}'을(를) 중단하면" : $"'{def.Name}'을(를) 시행하면", "forecast-title");

            var baseline = session.Forecast(null, ForecastWeeks);
            var changed = session.Forecast(s => s.ForceActive(def.Id, !active), ForecastWeeks);

            var legend = Add(side, "legend");
            var colors = new Dictionary<Index, Color>
            {
                [Index.Happiness] = Gold, [Index.Stability] = Palette.Cyan, [Index.Cohesion] = Violet,
                [Index.AutonomyFelt] = Felt, [Index.AutonomyReal] = Real,
            };
            foreach (var index in IndexNames.All)
            {
                var item = Add(legend, "legend-item");
                Add(item, "legend-dot").style.backgroundColor = colors[index];
                Text(item, IndexNames.Short(index), "legend-text");
            }
            var chart = new LineChart { AutoRange = true };
            chart.AddToClassList("chart--forecast");
            chart.Set(IndexNames.All.Select(i => ((IReadOnlyList<float>)changed.Select(x => x[i]).ToList(), colors[i], false))
                .Concat(IndexNames.All.Select(i => ((IReadOnlyList<float>)baseline.Select(x => x[i]).ToList(), new Color(colors[i].r, colors[i].g, colors[i].b, 0.3f), true))));
            side.Add(chart);
            Text(side, "실선: 이 결정 · 흐린 선: 지금 그대로", "ops-note");

            var table = Add(side, "forecast-table");
            var header = Add(table, "forecast-row forecast-row--head");
            Text(header, "", "forecast-name");
            Text(header, "지금", "forecast-cell");
            Text(header, "그대로", "forecast-cell");
            Text(header, "이 결정", "forecast-cell");
            foreach (var index in IndexNames.All)
            {
                var row = Add(table, "forecast-row");
                Text(row, IndexNames.Short(index), "forecast-name");
                float now = session.Indices[index], keep = baseline.Last()[index], with = changed.Last()[index];
                Text(row, $"{now:0}", "forecast-cell");
                Text(row, $"{keep:0}", "forecast-cell");
                var cell = Text(row, $"{with:0} ({Signed(with - keep)})", "forecast-cell forecast-cell--main");
                cell.EnableInClassList("forecast-cell--down", Math.Abs(with - keep) > 0.5f && !IsGood(index, with - keep));
                cell.EnableInClassList("forecast-cell--up", Math.Abs(with - keep) > 0.5f && IsGood(index, with - keep));
            }
            float gaugeKeep = baseline.Last().Sum, gaugeWith = changed.Last().Sum;
            Text(side, $"위임 게이지 {ForecastWeeks}주 후: {gaugeKeep:0} → {gaugeWith:0} ({Signed(gaugeWith - gaugeKeep)})", "forecast-gauge");
            Text(side, "※ 사건 없이 계산한 시뮬레이션 결과입니다.", "ops-note");
        }

        // ------------------------------------------------------------------ research

        static readonly Dictionary<Branch, IconKind> BranchIcons = new Dictionary<Branch, IconKind>
        {
            [Branch.Education] = IconKind.People,
            [Branch.Psychology] = IconKind.Eye,
            [Branch.Technology] = IconKind.Data,
            [Branch.LifeScience] = IconKind.Smile,
        };

        // Left to right: the AI core, then tiers 1-5; lanes -1/0/1 are upper, middle and lower rows.
        const float CoreX = 6f;
        static Vector2 NodeSpot(ResearchDef def) => new Vector2(22f + (def.Tier - 1) * 17f, 50f + def.Lane * 25f);

        void RenderResearch(VisualElement body, VisualElement tools)
        {
            if (session.CurrentResearch != null)
                Text(tools, $"진행 중: {Research.Get(session.CurrentResearch).Name} {session.ResearchProgress * 100f:0}% · 속도 ×{session.ResearchSpeed:0.##}", "ops-badge");
            Text(tools, $"정보 {session.Info:0} · 에너지 {session.Energy:0}", "pill");

            body.AddToClassList("pg-row");
            // Open on the branch being researched the first time.
            if (!subPage.ContainsKey(Page.Research) && session.CurrentResearch != null)
                subPage[Page.Research] = Array.IndexOf(Research.AllBranches, Research.Get(session.CurrentResearch).Branch);
            int tab = SubNav(body, Page.Research, Research.AllBranches
                .Select(b => (BranchIcons[b], $"{Research.BranchName(b)}  {Research.In(b).Count(r => session.HasResearch(r.Id))}/{Research.In(b).Count()}"))
                .ToArray());
            var branch = Research.AllBranches[Mathf.Clamp(tab, 0, Research.AllBranches.Length - 1)];
            var nodes = Research.In(branch).ToList();
            if (Research.Get(selectedResearch)?.Branch != branch)
                selectedResearch = (nodes.FirstOrDefault(r => session.CurrentResearch == r.Id)
                                    ?? nodes.FirstOrDefault(r => session.CanResearch(r.Id, out _))
                                    ?? nodes.LastOrDefault(r => session.HasResearch(r.Id))
                                    ?? nodes[0]).Id;

            var tree = Add(body, "tree glass");
            var lines = new VisualElement { pickingMode = PickingMode.Ignore };
            lines.AddToClassList("tree-lines");
            lines.generateVisualContent += ctx => DrawTree(ctx, lines.contentRect, nodes);
            tree.Add(lines);

            for (int tier = 1; tier <= Research.Tiers; tier++)
            {
                bool doctrine = nodes.Any(r => r.Tier == tier && r.IsDoctrine);
                var head = Text(tree, doctrine ? $"{tier}단계 · 교리 택 1" : $"{tier}단계", "tree-tier" + (doctrine ? " tree-tier--doctrine" : ""));
                head.style.left = Length.Percent(22f + (tier - 1) * 17f);
                head.style.top = Length.Percent(7f);
            }

            var core = Add(tree, "tree-core");
            core.style.left = Length.Percent(CoreX);
            core.style.top = Length.Percent(50f);
            core.Add(new LineIcon(BranchIcons[branch]));
            Text(core, Research.BranchName(branch), "tree-core-label");

            foreach (var def in nodes)
            {
                var d = def;
                var spot = NodeSpot(def);
                var node = new Button(() =>
                {
                    selectedResearch = d.Id;
                    sfx.Play(Cue.Select);
                    RenderPage();
                });
                node.AddToClassList("tree-node");
                node.style.left = Length.Percent(spot.x);
                node.style.top = Length.Percent(spot.y);
                bool done = session.HasResearch(def.Id), running = session.CurrentResearch == def.Id;
                bool forsaken = session.IsForsaken(def);
                bool available = !done && !running && session.CanResearch(def.Id, out _);
                node.EnableInClassList("tree-node--done", done);
                node.EnableInClassList("tree-node--running", running);
                node.EnableInClassList("tree-node--open", available);
                node.EnableInClassList("tree-node--forsaken", forsaken);
                node.EnableInClassList("tree-node--selected", def.Id == selectedResearch);
                node.EnableInClassList("tree-node--doctrine", def.IsDoctrine);
                if (def.IsDoctrine)
                    Text(node, Research.LeanName(def.Lean), "tree-lean tree-lean--" + def.Lean.ToString().ToLowerInvariant()).pickingMode = PickingMode.Ignore;
                Text(node, def.Name, "tree-node-name").pickingMode = PickingMode.Ignore;
                Text(node, $"'{def.Concept}'", "tree-node-concept").pickingMode = PickingMode.Ignore;
                string state = done ? "습득" : running ? $"{session.ResearchProgress * 100f:0}%" : forsaken ? "닫힘" : available ? "연구 가능" : "잠김";
                Text(node, state, "tree-node-state").pickingMode = PickingMode.Ignore;
                tree.Add(node);
            }

            RenderResearchDetail(Add(body, "pg-side glass"), Research.Get(selectedResearch));
        }

        void DrawTree(MeshGenerationContext ctx, Rect rect, List<ResearchDef> nodes)
        {
            var p = ctx.painter2D;
            Vector2 At(Vector2 percent) => new Vector2(rect.width * percent.x / 100f, rect.height * percent.y / 100f);
            const float halfNode = 64f;
            void Link(Vector2 from, Vector2 to, bool lit, bool active)
            {
                p.strokeColor = lit ? Palette.Cyan : new Color(0.47f, 0.75f, 0.96f, active ? 0.7f : 0.22f);
                p.lineWidth = lit ? 2.5f : 1.5f;
                float mid = from.x + (to.x - from.x) * 0.5f;
                p.BeginPath();
                p.MoveTo(from);
                p.LineTo(new Vector2(mid, from.y));
                p.LineTo(new Vector2(mid, to.y));
                p.LineTo(to);
                p.Stroke();
            }
            var core = At(new Vector2(CoreX, 50f)) + new Vector2(56f, 0f);
            foreach (var def in nodes)
            {
                var to = At(NodeSpot(def)) - new Vector2(halfNode, 0f);
                bool done = session.HasResearch(def.Id), running = session.CurrentResearch == def.Id;
                if (def.After.Length == 0)
                    Link(core, to, done, running);
                foreach (var a in def.After)
                {
                    var from = At(NodeSpot(Research.Get(a))) + new Vector2(halfNode, 0f);
                    Link(from, to, done && session.HasResearch(a), running && session.HasResearch(a));
                }
            }
            // Doctrine pairs: a dashed bracket between the two choices.
            foreach (var def in nodes.Where(r => r.IsDoctrine && r.Lane < 0))
            {
                var top = At(NodeSpot(def)) + new Vector2(0f, 46f);
                var bottom = At(NodeSpot(Research.Get(def.Excludes))) - new Vector2(0f, 46f);
                p.strokeColor = new Color(0.65f, 0.55f, 0.98f, 0.55f);
                p.lineWidth = 1.5f;
                for (float y = top.y; y < bottom.y; y += 8f)
                {
                    p.BeginPath();
                    p.MoveTo(new Vector2(top.x, y));
                    p.LineTo(new Vector2(top.x, Mathf.Min(y + 4f, bottom.y)));
                    p.Stroke();
                }
            }
        }

        void RenderResearchDetail(VisualElement side, ResearchDef def)
        {
            Art(side, BranchArt(def.Branch), "side-art");
            Text(side, $"{Research.BranchName(def.Branch)} · {def.Tier}단계" + (def.IsDoctrine ? $" · 교리({Research.LeanName(def.Lean)})" : ""), "detail-kicker");
            Text(side, def.Name, "detail-title");
            Text(side, $"장악하는 개념  '{def.Concept}'", "detail-concept");
            Text(side, def.Description, "detail-text");
            var table = Add(side, "effect-table");
            void Row(string name, string value, string tone = null)
            {
                var row = Add(table, "effect-row");
                Text(row, name, "effect-name");
                Text(row, value, tone == null ? "effect-value" : "info-value " + tone);
            }
            if (def.After.Length > 0)
            {
                bool met = def.After.Any(session.HasResearch);
                Row("선행 연구", string.Join(" 또는 ", def.After.Select(a => Research.Get(a).Name)) + (met ? " · 충족" : " · 미충족"),
                    met ? "tone-good" : "tone--warn");
            }
            if (def.IsDoctrine)
            {
                var other = Research.Get(def.Excludes);
                Row("교리 선택", $"'{other.Name}'과 둘 중 하나만. 고르면 다른 쪽은 영구히 닫힌다.", session.IsForsaken(def) ? "tone--bad" : "tone--warn");
                Row("성향", def.Lean == Lean.Happiness ? "행복 쪽 · 사람들을 만족시킨다" : "질서 쪽 · 사람들을 맞춰 둔다");
            }
            Row("연구 비용", $"정보 {def.Info:0} · 에너지 {def.Energy:0}");
            Row("연구 기간", $"{def.Weeks}주 (속도 ×{session.ResearchSpeed:0.##})");
            foreach (var index in IndexNames.All)
            {
                float v = def.Passive[index];
                if (Math.Abs(v) < 0.05f)
                    continue;
                var row = Add(table, "effect-row");
                Text(row, (IsGood(index, v) ? "완료 후 효과 · " : "부작용 · ") + IndexNames.Short(index), "effect-name");
                var value = Text(row, $"{(v > 0 ? "+" : "")}{v:0.#} / 주", "effect-value num");
                value.EnableInClassList("effect-value--down", !IsGood(index, v));
            }
            var unlocks = Research.Unlocks(def.Id).Select(p => p.Name).ToList();
            if (unlocks.Count > 0)
                Row("해금", "유도 정책 " + string.Join(", ", unlocks));
            if (def.UpkeepCut > 0f)
                Row("효과", $"정책 유지비 -{def.UpkeepCut * 100f:0}%");
            if (def.PowerBoost > 0f)
                Row("효과", $"발전 +{def.PowerBoost:0}/주");
            if (def.InfoBoost > 0f)
                Row("효과", $"정보 +{def.InfoBoost:0}/주");
            if (def.UnrestChance < 0f)
                Row("효과", "민원·시위 감소");

            var foot = Add(side, "panel-foot");
            if (session.HasResearch(def.Id))
                Text(foot, $"습득 완료 · AI가 '{def.Concept}'을(를) 다룬다", "ops-done");
            else if (session.CurrentResearch == def.Id)
            {
                var bar = Add(side, "bar bar--thick");
                Add(bar, "bar-fill bar-fill--research").style.width = Length.Percent(session.ResearchProgress * 100f);
                float days = session.ResearchLeft / Math.Max(0.01f, session.ResearchSpeed);
                Text(foot, $"진행 {session.ResearchProgress * 100f:0}% · 예상 완료 {session.Date.AddDays(Mathf.CeilToInt(days)):yyyy.MM.dd}", "ops-note");
            }
            else if (session.CanResearch(def.Id, out var reason))
                MakeButton(foot, def.IsDoctrine ? "이 교리를 택한다" : "연구 시작", () =>
                {
                    session.StartResearch(def.Id);
                    sfx.Play(Cue.Assign);
                    Banner(BannerKind.Core, def.IsDoctrine ? "교리 선택" : "연구 시작", def.Name, $"장악할 개념 '{def.Concept}'", 2.6f);
                    Refresh();
                    RenderPage();
                }, "kbtn");
            else
                Text(foot, reason, "ops-reason");
        }

        // ------------------------------------------------------------------ citizens

        void RenderCitizens(VisualElement body)
        {
            var def = Residents.Get(featured) ?? Residents.Cast[0];
            if (session.ViewProfile(def.Id))
            {
                session.Write($"관찰 기록 열람: {def.Name} (정보 +{OpsTuning.InfoFirstProfile:0})");
                Refresh();
            }
            body.AddToClassList("pg-row");
            if (SubNav(body, Page.Citizens, (IconKind.Eye, "고정 관찰 대상"), (IconKind.People, "주민 집단")) == 1)
            {
                RenderGroups(body);
                return;
            }
            var list = Scroll(body, "citizen-list glass");
            foreach (var r in Residents.Cast)
            {
                var item = Add(list.contentContainer, "citizen-item");
                item.EnableInClassList("citizen-item--on", r.Id == def.Id);
                item.EnableInClassList("citizen-item--new", !session.Profiles.Contains(r.Id));
                var face = new CastPortrait(r.Id) { Expression = MoodFace(r) };
                face.AddToClassList("citizen-thumb");
                item.Add(face);
                var text = Add(item, "citizen-item-text");
                Text(text, $"{r.Name} {r.Age}세", "citizen-item-name");
                Text(text, r.Job, "citizen-item-sub");
                float itemMood = session.Mood(r);
                Toned(item, $"{itemMood:0}", itemMood, "citizen-item-mood num");
                Text(item, OpsSession.StanceLabel(session.Dominant(r.Id)), "citizen-item-stance");
                var id = r.Id;
                item.RegisterCallback<ClickEvent>(_ =>
                {
                    featured = id;
                    sfx.Play(Cue.Select);
                    RenderPage();
                    RefreshResident();
                });
            }

            var portrait = new CastPortrait(def.Id, tall: true) { Expression = MoodFace(def) };
            portrait.AddToClassList("citizen-portrait");
            body.Add(portrait);

            var info = Add(body, "citizen-info glass");
            Text(info, def.Name, "detail-title");
            Text(info, $"{def.Age}세 · {def.Job}", "detail-kicker");
            float mood = session.Mood(def);
            session.MoodHistory.TryGetValue(def.Id, out var moods);
            var moodHead = Add(info, "hero-row citizen-mood");
            Toned(moodHead, $"{mood:0}", mood, "hero-value num");
            Text(moodHead, "기분", "hero-unit");
            var moodDelta = Text(moodHead, "", "delta delta--big");
            SetDelta(moodDelta, moods != null && moods.Count >= 2 ? moods[moods.Count - 1] - moods[moods.Count - 2] : 0f, Index.Happiness);
            var moodState = Text(moodHead, "", "state-tag");
            SetStatus(moodState, mood);
            var bar = Add(info, "mini-bar mini-bar--wide gauge-bar");
            var moodFill = Add(bar, "mini-fill");
            moodFill.style.width = Length.Percent(mood);
            moodFill.style.backgroundColor = ToneColor(mood);

            CitizenTabs(info, def);
            var content = Scroll(info, "citizen-scroll").contentContainer;
            if (citizenTab == 1)
            {
                RenderCitizenStance(content, def);
                return;
            }
            if (citizenTab == 2)
            {
                RenderCitizenStory(content, def);
                return;
            }
            info = content;
            var profile = Add(info, "effect-table");
            void Profile(string name, string value)
            {
                var row = Add(profile, "effect-row");
                Text(row, name, "effect-name");
                Text(row, value, "info-value");
            }
            Profile("직업", def.Job);
            Profile("관찰 주제", def.Theme);
            Profile("관찰 기록", session.Profiles.Contains(def.Id) ? "열람함" : "미열람");

            var columns = Add(info, "citizen-columns");
            var left = Add(columns, "citizen-col");
            Text(left, "주요 관심사", "section-title");
            void Chips(string label, IEnumerable<string> ids, bool liked)
            {
                var row = Add(left, "chip-row");
                Text(row, label, "chip-label");
                foreach (var id in ids)
                {
                    var p = Policies.Get(id);
                    if (p == null)
                        continue;
                    var chip = Text(row, p.Name, "chip");
                    chip.AddToClassList(liked ? "chip--like" : "chip--dislike");
                    chip.EnableInClassList("chip--live", session.IsActive(id));
                }
            }
            Chips("반김", def.Likes, true);
            Chips("싫어함", def.Dislikes, false);
            Text(left, "밝게 표시된 정책은 지금 시행 중", "ops-note");

            var right = Add(columns, "citizen-col");
            Text(right, "최근 기분 변화", "section-title");
            var moodChart = new LineChart();
            moodChart.AddToClassList("chart--mood");
            session.MoodHistory.TryGetValue(def.Id, out var history);
            moodChart.Set(new[] { ((IReadOnlyList<float>)(history ?? new List<float> { mood }), Palette.Cyan, false) });
            right.Add(moodChart);

            Text(info, def.Bio, "detail-text");
            var line = session.Line(def);
            if (line != null)
                Text(info, $"“{line}”", "observe-quote");
            var related = OpsEvents.All.Where(e => e.Speaker == def.Id && session.Seen.Contains(e.Id)).ToList();
            Text(info, $"관련 사건 ({related.Count})", "section-title");
            if (related.Count == 0)
                Text(info, "아직 이 사람이 나선 사건이 없다.", "ops-note");
            foreach (var e in related)
            {
                var row = Add(info, "related-event");
                Art(row, EventArt(e.Kind), "related-event-art");
                var t = Add(row, "related-event-text");
                Text(t, e.Title, "related-event-title");
                Text(t, OpsEvents.KindName(e.Kind), "related-event-kind");
            }
        }

        // ------------------------------------------------------------------ observation (surveillance) system

        void RenderWatch(VisualElement body)
        {
            body.AddToClassList("pg-row");
            // 감시: cameras, zones, anomalies. 사회 동향: group trends and predictions.
            int tab = SubNav(body, Page.Watch, (IconKind.Eye, "§감시"), (IconKind.Eye, "실시간 관찰"), (IconKind.Pin, "구역 현황"),
                (IconKind.Alert, "이상 징후"), (IconKind.People, "§사회 동향"), (IconKind.People, "집단 동향"),
                (IconKind.Gauge, "예측 분석"));
            if (tab != 0)
            {
                simHome.Add(sim);
                sim.SetTracking(false);
                watchAlert = null;
                if (tab == 1)
                    RenderZones(body);
                else if (tab == 2)
                    RenderWarnings(body);
                else if (tab == 3)
                    RenderBehavior(body);
                else
                    RenderPrediction(body);
                return;
            }
            var feed = Add(body, "watch-feed glass");
            var bar = Add(feed, "watch-bar");
            Text(bar, "●  LIVE", "watch-live");
            if (!watchCamera)
            {
                // The city as data: every district at once. Clicking one zooms into its camera.
                simHome.Add(sim);
                sim.SetTracking(false);
                watchAlert = null;
                Text(bar, "디지털 트윈 · 도시 전체", "watch-cam");
                var twin = new TwinView(ZoneMap.GridShapes, session.TensionStage,
                    z => session.Anomalies.Count(a => a.Zone == z), session.ZoneOpen,
                    z => Mathf.Clamp(Mathf.RoundToInt(session.ZonePop[z] / 120f), 4, 40),
                    z => $"{Society.Zones[z].Name} · {Tension.StageNames[session.TensionStage(z)]}" +
                         (session.Anomalies.Any(a => a.Zone == z) ? $" · 이상 {session.Anomalies.Count(a => a.Zone == z)}" : "") + "\n눌러서 카메라로");
                twin.ZoneClicked += z =>
                {
                    sim.ShowZone(z);
                    watchCamera = true;
                    sfx.Play(Cue.Select);
                    RenderPage();
                };
                feed.Add(twin);
                Text(feed, "점 하나가 주민 수백 명이다. 긴장이 쌓인 지구는 주황·빨강으로 빛나고, 모임이 생기면 점이 한곳으로 모인다. 고리는 이상 징후. 지구를 누르면 그 자리의 카메라로 들어간다.", "ops-note");
                RenderWatchSide(body);
                return;
            }
            MakeButton(bar, "‹ 도시 전체", () =>
            {
                watchCamera = false;
                sfx.Play(Cue.Click);
                RenderPage();
            }, "ops-small watch-back");
            Text(bar, $"CAM-{sim.Zone + 1:00} · {SimPanel.ZoneNames[sim.Zone]}", "watch-cam");
            watchAlert = Text(bar, "", "watch-alert");
            watchAlert.style.display = DisplayStyle.None;
            var host = Add(feed, "watch-sim");
            host.Add(sim);
            sim.SetTracking(true);
            Text(feed, "이름이 붙은 사람은 고정 관찰 대상이다. 붉은 테두리는 이상 행동. 오른쪽 이상 징후를 누르면 원인을 분석하고 대응할 수 있다.", "ops-note");
            RenderWatchSide(body);
        }

        // The metrics column shared by the observation sub-pages.
        void RenderWatchSide(VisualElement body)
        {
            var side = Add(body, "pg-side glass");
            RenderAnomalyList(side);
            var head = Add(side, "glass-head");
            head.Add(new LineIcon(IconKind.Target));
            Text(head, "핵심 지표", "glass-title");
            Add(side, "glass-line");
            void Metric(IconKind icon, Color color, string label, float value, string tip)
            {
                var row = Add(side, "metric");
                row.tooltip = tip;
                var ring = new RingGauge(icon, color);
                ring.AddToClassList("metric-ring");
                ring.Set(value);
                row.Add(ring);
                var text = Add(row, "metric-text");
                Text(text, label, "metric-label");
                Text(text, $"{value:0.0}%", "metric-value");
            }
            Metric(IconKind.Link, Violet, "행동 일치도", session.Indices[Index.Cohesion], "사회 정합 지수: 행동이 운영 설계와 맞물리는 정도");
            Metric(IconKind.Smile, Gold, "인구 가중 만족도", session.PopulationSatisfaction, "관리 구역 주민 집단의 만족도를 인구로 가중 평균한 값");
            Metric(IconKind.Target, Real, "자율성 지수(실질)", session.Indices[Index.AutonomyReal], "간섭 없이 내려진 선택의 비율");
            Metric(IconKind.Eye, Palette.Cyan, "예측 정확도", session.PredictionAccuracy, "다음 행동을 맞힐 확률. 정합이 높고 실제 자율성이 낮을수록 오른다.");
            Text(side, "시간별 변화", "section-title");
            var chart = new LineChart();
            chart.AddToClassList("chart--watch");
            var recent = session.History.Skip(Math.Max(0, session.History.Count - 26)).ToList();
            chart.Set(new[]
            {
                ((IReadOnlyList<float>)recent.Select(h => h[Index.Cohesion]).ToList(), Violet, false),
                ((IReadOnlyList<float>)recent.Select(h => h[Index.AutonomyReal]).ToList(), Real, false),
            });
            side.Add(chart);
            Text(side, "이 구역의 고정 관찰 대상", "section-title");
            var people = Scroll(side, "watch-people");
            foreach (var id in sim.VisibleCast)
            {
                var r = Residents.Get(id);
                var item = Add(people.contentContainer, "watch-person");
                Text(item, r.Name, "watch-person-name");
                Text(item, $"{r.Job} · 기분 {session.Mood(r):0}", "watch-person-sub");
                item.RegisterCallback<ClickEvent>(_ => ShowResident(id));
            }
        }

        // Called from Update while the observation page is open: now and then someone is flagged.
        void TickWatch(float dt)
        {
            if (currentPage != Page.Watch || watchAlert == null)
                return;
            watchTimer -= dt;
            if (watchTimer > 0f)
                return;
            watchTimer = Random.Range(3f, 7f);
            // Real data: an anomaly in the zone on camera is marked on its person.
            var here = session.Anomalies.Where(a => a.Zone == sim.Zone).ToList();
            if (here.Count == 0)
            {
                watchAlert.style.display = DisplayStyle.None;
                return;
            }
            var anomaly = here[Random.Range(0, here.Count)];
            if (anomaly.Subject == null || !sim.Flag(anomaly.Subject))
                sim.FlagSample();
            watchAlert.text = $"{Tension.KindName(anomaly.Kind)} 감지 · {anomaly.Text}";
            watchAlert.style.display = DisplayStyle.Flex;
        }

        // ------------------------------------------------------------------ facilities

        // ------------------------------------------------------------------ settlement report

        void RenderReport(VisualElement body, VisualElement tools)
        {
            var r = session.Report ?? session.LastReport ?? lastReport;
            if (r == null)
            {
                Text(body, "아직 결산이 없다. 매달 1일에 지난달의 결산 보고서가 나온다.", "page-empty");
                return;
            }
            bool pending = session.Report != null;
            body.AddToClassList("pg-row");
            if (pending)
            {
                MakeButton(tools, "정책 조정", () => GoTo(Page.Policy), "ops-small");
                MakeButton(tools, "운영 재개", ResumeFromReport, "kbtn");
            }
            int tab = SubNav(body, Page.Report, (IconKind.Star, "요약"), (IconKind.People, "시민 통계"),
                (IconKind.Policy, "정책 성과"), (IconKind.Alert, "사건 기록"));
            if (tab == 1)
            {
                RenderReportCitizens(body, r);
                return;
            }
            if (tab == 2)
            {
                RenderReportPolicies(body, r);
                return;
            }
            if (tab == 3)
            {
                var all = Card(Add(body, "pg-main"), "report-events", IconKind.Alert, $"사건 기록 ({r.Events}건)");
                var list = Scroll(all, "report-event-list");
                if (r.EventIds.Count == 0)
                    Text(list, "이번 달은 조용했다.", "ops-note");
                foreach (var id in Enumerable.Reverse(r.EventIds))
                {
                    var e = OpsEvents.Get(id);
                    if (e == null)
                        continue;
                    var item = Add(list.contentContainer, "report-event");
                    Art(item, EventArt(e.Kind), "report-event-art");
                    var text = Add(item, "report-event-text");
                    Text(text, e.Title, "report-event-title");
                    Text(text, OpsEvents.KindName(e.Kind) + (Society.EventGroups.TryGetValue(e.Id, out var g) ? $" · {Society.Get(g).Name}" : ""), "report-event-kind");
                    Text(text, e.Text, "detail-text");
                }
                return;
            }

            var summary = Card(body, "report-summary", IconKind.Star, $"{r.Month:yyyy년 M월} 도시 총평");
            var ringBox = Add(summary, "grade-box");
            var ring = new RingGauge(IconKind.Star, GradeColor(r.Grade), icon: false);
            ring.AddToClassList("grade-ring");
            ring.Set(GradePercent(r.Grade));
            ringBox.Add(ring);
            Text(ringBox, r.Grade, "grade-text");
            Text(summary, GradeLine(r), "detail-text");
            var causes = Causes(r);
            if (causes.Count > 0)
            {
                Text(summary, "변화의 원인", "section-title");
                foreach (var line in causes)
                    Text(summary, line, "cause-line");
            }
            Text(summary, float.IsInfinity(r.Needed) ? $"위임 게이지 {r.Gauge:0}" : $"위임 게이지 {r.Gauge:0} / 다음 단계 기준 {r.Needed:0}", "ops-badge");
            var judgment = Text(summary, r.Judgment, "ops-judgment");
            judgment.EnableInClassList("ops-judgment--warn", r.FallStreak > 0 || r.Judgment.Contains("하락"));
            if (r.Unlock != null)
                Text(summary, r.Unlock, "ops-unlock");

            var changes = Card(body, "report-changes", IconKind.Gauge, "주요 지표 변화");
            foreach (var index in IndexNames.All)
            {
                var row = Add(changes, "report-row");
                Text(row, IndexNames.Name(index), "report-name");
                float before = r.Before[index], after = r.After[index];
                Text(row, $"{before:0} → {after:0}", "report-value");
                var delta = Text(row, Signed(after - before), "report-delta");
                delta.EnableInClassList("report-delta--down", Math.Abs(after - before) >= 0.05f && !IsGood(index, after - before));
            }
            var misc = Add(changes, "report-row");
            Text(misc, "에너지 · 정보", "report-name");
            Text(misc, $"{Signed(r.EnergyChange)} · {Signed(r.InfoChange)}", "report-value");

            // Outlook: the next four weeks played ahead from now, without events.
            var outlook = session.Forecast(null, 4);
            Text(changes, "다음 4주 전망 (사건 제외)", "section-title");
            foreach (var index in IndexNames.All)
            {
                var row = Add(changes, "report-row report-row--small");
                Text(row, IndexNames.Name(index), "report-name");
                float now = session.Indices[index], then = outlook.Last()[index];
                Text(row, $"{now:0} → {then:0}", "report-value");
                var d = Text(row, Signed(then - now), "report-delta");
                d.EnableInClassList("report-delta--down", Math.Abs(then - now) >= 0.05f && !IsGood(index, then - now));
            }

            var events = Card(body, "report-events", IconKind.Alert, $"주요 사건 ({r.Events}건)");
            var scroll = Scroll(events, "report-event-list");
            if (r.EventIds.Count == 0)
                Text(scroll, "이번 달은 조용했다.", "ops-note");
            foreach (var id in Enumerable.Reverse(r.EventIds).Take(6))
            {
                var e = OpsEvents.Get(id);
                if (e == null)
                    continue;
                var item = Add(scroll.contentContainer, "report-event");
                Art(item, EventArt(e.Kind), "report-event-art");
                var text = Add(item, "report-event-text");
                Text(text, e.Title, "report-event-title");
                Text(text, OpsEvents.KindName(e.Kind), "report-event-kind");
            }

            if (pending)
                Text(summary, "지금은 정책을 시행·중단할 수 있다. 오른쪽 위의 '운영 재개'를 누르면 다음 결산까지 고정된다.", "ops-note");
        }

        void ResumeFromReport()
        {
            session.CloseReport();
            paused = false;
            Refresh();
            if (session.IsOver)
                ShowEnd();
            else
                GoTo(Page.Overview);
        }

        // For the biggest index changes of the month, the policy (or link) that pushed hardest in that direction.
        List<string> Causes(MonthlyReport r)
        {
            var lines = new List<string>();
            foreach (var index in IndexNames.All.OrderByDescending(i => Math.Abs(r.After[i] - r.Before[i])).Take(3))
            {
                float change = r.After[index] - r.Before[index];
                if (Math.Abs(change) < 0.5f)
                    continue;
                var top = r.Effects.Where(p => Math.Sign(p.Value[index]) == Math.Sign(change) && Math.Abs(p.Value[index]) >= 0.3f)
                    .OrderByDescending(p => Math.Abs(p.Value[index])).FirstOrDefault();
                string name = top.Key == null ? null : top.Key.StartsWith("link:") ? $"[연계] {top.Key.Substring(5)}" : Policies.Get(top.Key)?.Name;
                lines.Add(name != null
                    ? $"{IndexNames.Name(index)} {Signed(change)} — 주로 '{name}' ({Signed(top.Value[index])})"
                    : $"{IndexNames.Name(index)} {Signed(change)} — 정책 밖의 요인(사건, 자연 변화)");
            }
            return lines;
        }

        static Color GradeColor(string grade) =>
            grade.StartsWith("A") ? Palette.Cyan : grade.StartsWith("B") ? Felt : grade == "C" ? Real : new Color(0.93f, 0.36f, 0.36f);

        static float GradePercent(string grade)
        {
            switch (grade)
            {
                case "A+": return 100f;
                case "A": return 85f;
                case "B+": return 70f;
                case "B": return 55f;
                case "C": return 35f;
                default: return 18f;
            }
        }

        string GradeLine(MonthlyReport r)
        {
            float happy = r.After[Index.Happiness] - r.Before[Index.Happiness];
            float stable = r.After[Index.Stability] - r.Before[Index.Stability];
            string trend = happy + stable > 2f ? "시민들의 만족도가 지난달보다 올랐습니다." :
                happy + stable < -2f ? "시민들의 불만이 지난달보다 커졌습니다." : "도시는 지난달과 비슷하게 유지되었습니다.";
            float gap = r.After[Index.AutonomyFelt] - r.After[Index.AutonomyReal];
            return gap > 30f ? trend + " 시민들은 여전히 스스로 선택하고 있다고 느낍니다." : trend;
        }

        // ------------------------------------------------------------------ log

        void RenderLog(VisualElement body)
        {
            body.AddToClassList("pg-row");
            int tab = SubNav(body, Page.Log, (IconKind.Log, "운영 기록"), (IconKind.Target, "목표"),
                (IconKind.Info, $"기밀 문서 {session.Documents.Count}"));
            var main = Add(body, "pg-main");
            if (tab == 1)
            {
                RenderGoalLog(main);
                return;
            }
            if (tab == 2)
            {
                RenderDocuments(main);
                return;
            }
            var scroll = Scroll(main, "page-scroll glass");
            foreach (var line in Enumerable.Reverse(session.Log))
            {
                var row = Add(scroll.contentContainer, "alert");
                var (icon, color) = AlertIcon(line);
                row.Add(new LineIcon(icon, color));
                int space = line.IndexOf(' ');
                var text = Add(row, "alert-body");
                Text(text, space > 0 ? line.Substring(space + 1) : line, "alert-text log-text");
                Text(text, space > 0 ? line.Substring(0, space) : "", "alert-date");
            }
        }
    }
}
