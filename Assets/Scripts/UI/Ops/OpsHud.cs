using System;
using System.Collections.Generic;
using System.Linq;
using Sector.Ops;
using UnityEngine;
using UnityEngine.UIElements;
using Index = Sector.Ops.Index;

namespace Sector.UI
{
    // The main screen: the city vista fills the screen and the panels float over it.
    // Top bar (time, resources, delegation, menu), left city status (rings, autonomy pair, trend chart),
    // right alerts and policy status, bottom simulation feed, research and one observed resident.
    public partial class OpsScreen
    {
        static readonly Color Gold = new Color32(244, 196, 92, 255);
        static readonly Color Violet = new Color32(170, 150, 245, 255);
        static readonly Color Felt = new Color32(150, 205, 250, 255);
        static readonly Color Real = new Color32(247, 147, 59, 255);
        static readonly Color Warn = new Color32(238, 92, 92, 255);

        // Each district's landmark on city_main_v5 (image fractions, 3072 x 2048), Society.Zones order.
        static readonly Vector2[] ZonePins =
        {
            new Vector2(0.515f, 0.55f), new Vector2(0.245f, 0.50f), new Vector2(0.515f, 0.38f), new Vector2(0.27f, 0.74f),
            new Vector2(0.80f, 0.78f), new Vector2(0.17f, 0.30f), new Vector2(0.20f, 0.14f), new Vector2(0.57f, 0.14f),
            new Vector2(0.78f, 0.26f), new Vector2(0.83f, 0.54f), new Vector2(0.86f, 0.13f),
        };
        const float CityAspect = 1536f / 1024f;
        VisualElement cityView;
        readonly List<VisualElement> cityLayers = new List<VisualElement>();


        // top bar
        Label dateLabel;
        Label monthLabel;
        Button pauseButton;
        readonly List<Button> speedButtons = new List<Button>();
        Label energyLabel;
        Label energyRate;
        Label infoLabel;
        Label infoRate;
        Label stageLabel;
        DelegationGauge topGauge;
        Label gaugeLabel;
        // city status: one hero number and a few rows with bars
        Label heroValue;
        Label heroDelta;
        LineChart heroChart;
        readonly List<(Index index, Label number, Label delta, Label status, VisualElement fill)> statRows =
            new List<(Index, Label, Label, Label, VisualElement)>();
        Label feltDelta, realDelta;
        Label popLabel;
        Label popRate;
        VisualElement feltFill;
        VisualElement realFill;
        Label feltValue;
        Label realValue;
        Label gapLabel;
        LineChart chart;
        Label unrestLabel;
        // vista
        VisualElement zoneLayer;
        // right column
        VisualElement logList;
        VisualElement policyList;
        Label policySlots;
        Label policyWindow;
        Button policyButton;
        // bottom row
        SimPanel sim;
        Label researchBranch;
        Label researchName;
        Label researchText;
        VisualElement researchFill;
        Label researchPercent;
        Label researchEta;
        VisualElement residentFace;
        Label residentName;
        Label residentJob;
        VisualElement residentMood;
        Label residentMoodValue;
        Label residentQuote;
        string featured = "c01";
        Label toast;

        // ------------------------------------------------------------------ layout

        void Build()
        {
            // The whole city painting lies behind translucent panels, sized to its own aspect (never cropped).
            // Drag to pan, scroll to zoom (OpsCity.cs). Edges are shaded for legibility.
            cityView = Add(root, "backdrop");
            var vista = Resources.Load<Texture2D>("Macro/city_main");
            if (vista != null)
                cityView.style.backgroundImage = new StyleBackground(vista);
            cityLayers.Add(cityView);
            BuildPanZoom();
            var vignette = Add(root, "vignette");
            vignette.pickingMode = PickingMode.Ignore;
            foreach (var side in new[] { "left", "right", "top", "bottom" })
                Add(vignette, "edge edge--" + side).pickingMode = PickingMode.Ignore;
            // Kept for GoTo; the backdrop is plain now, so it stays empty.
            dim = Add(root, "dim");
            dim.style.display = DisplayStyle.None;

            BuildTop();
            BuildSidebar();
            pageHost = Add(root, "page-host");
            pageHost.pickingMode = PickingMode.Ignore;
            overviewPage = Add(pageHost, "overview");
            overviewPage.pickingMode = PickingMode.Ignore;
            pageView = Add(pageHost, "page");
            pageView.style.display = DisplayStyle.None;
            leftColumn = Add(overviewPage, "ov-col ov-col--left");
            var center = Add(overviewPage, "ov-center");
            rightColumn = Add(overviewPage, "ov-col ov-col--right");
            leftColumn.pickingMode = PickingMode.Ignore;
            center.pickingMode = PickingMode.Ignore;
            rightColumn.pickingMode = PickingMode.Ignore;
            BuildCity(center);
            BuildStatus();
            BuildDelegation();
            BuildAlerts();
            BuildPolicy();
            BuildResearch();
            BuildResident();
            BuildSim();

            overlay = Add(root, "ops-overlay");
            overlay.style.display = DisplayStyle.None;
            toast = Text(root, "", "ops-toast");
            toast.pickingMode = PickingMode.Ignore;
            toast.style.display = DisplayStyle.None;
            BuildFx();
        }

        // Columns that hold the floating panels; they stretch with the screen so panels never overlap.
        VisualElement leftColumn, rightColumn;

        VisualElement Panel(VisualElement parent, string classes, IconKind icon, string title, string link = null, Action onLink = null)
        {
            var panel = Add(parent, "glass " + classes);
            var head = Add(panel, "glass-head");
            Badge(head, icon);
            Text(head, title, "glass-title");
            if (link != null)
            {
                var button = MakeButton(head, link + "  ›", onLink, "link");
                button.AddToClassList("glass-link");
            }
            Add(panel, "glass-line");
            return panel;
        }

        static VisualElement Badge(VisualElement parent, IconKind icon, Color? color = null, string classes = "badge")
        {
            var badge = Add(parent, classes);
            badge.Add(new LineIcon(icon, color));
            return badge;
        }

        // The city painting as the overview's centerpiece, with zone pins on it.
        void BuildCity(VisualElement parent)
        {
            // A clear window onto the backdrop: only the zone pins and a caption sit here.
            var window = Add(parent, "city-window");
            window.pickingMode = PickingMode.Ignore;
            var caption = Add(window, "city-caption");
            caption.pickingMode = PickingMode.Ignore;
            Text(caption, "SECTOR 7 시범 운영 구역", "city-title");
            Text(caption, "드래그로 이동 · 휠로 확대 · 지구를 누르면 정보", "city-sub");
            zoneLayer = Add(window, "zones");
            zoneLayer.pickingMode = PickingMode.Ignore;
            BuildLivingCity(window);
            BuildMap(window);
            BuildGuide(window);
        }

        // Pin position in the zone layer for a point of the painting (the backdrop is the painting, stretched exactly).
        Vector2 PinAt(Vector2 image, int layer = 0)
        {
            var r = cityView.worldBound;
            if (r.width <= 0f || r.height <= 0f || float.IsNaN(r.width))
                return Vector2.zero;
            return zoneLayer.WorldToLocal(new Vector2(r.x + image.x * r.width, r.y + image.y * r.height));
        }

        void BuildTop()
        {
            var top = Add(root, "topbar");
            var brand = Add(top, "brand");
            Badge(brand, IconKind.Gauge, null, "badge badge--brand");
            var brandText = Add(brand, "brand-text");
            Text(brandText, "SECTOR 7", "brand-title");
            Text(brandText, "시범 운영 구역", "brand-sub");

            var energy = Add(top, "resource");
            Badge(energy, IconKind.Bolt, Caution, "badge badge--small");
            energyLabel = Text(energy, "", "resource-value");
            energyRate = Text(energy, "", "resource-rate");
            var info = Add(top, "resource");
            Badge(info, IconKind.Data, null, "badge badge--small");
            infoLabel = Text(info, "", "resource-value");
            infoRate = Text(info, "", "resource-rate");
            var people = Add(top, "resource");
            Badge(people, IconKind.People, null, "badge badge--small");
            popLabel = Text(people, "", "resource-value");
            popRate = Text(people, "", "resource-rate");
            people.tooltip = "관리 구역의 인구. 구역 만족도가 50보다 높으면 늘고, 낮으면 줄어든다.";

            var clock = Add(top, "clock");
            var clockText = Add(clock, "clock-text");
            dateLabel = Text(clockText, "", "clock-date");
            monthLabel = Text(clockText, "", "clock-sub");
            pauseButton = MakeButton(clock, "▶", TogglePause, "time-button");
            for (int i = 0; i < Speeds.Length; i++)
            {
                int s = Speeds[i];
                speedButtons.Add(MakeButton(clock, $"{s}×", () => SetSpeed(s), "time-button"));
            }

            var gauge = Add(top, "delegation");
            gauge.tooltip = $"위임 진행: 공개 지표(실질 자율성을 뺀 네 지표)의 합이 다음 단계 기준을 넘은 달이 {OpsTuning.StreakMonths}칸을 다 채우면 단계가 오른다. " +
                            "기준 아래로 한 달을 마치면 처음부터 다시. 이번 달 칸은 기준을 넘는 동안 날짜만큼 차오른다. " +
                            $"사회 안정이 {OpsTuning.FallStability:0} 아래인 달이 {OpsTuning.FallMonths}개월 이어지면 단계가 내려간다.";
            var gaugeHead = Add(gauge, "delegation-head");
            stageLabel = Text(gaugeHead, "", "delegation-stage");
            gaugeLabel = Text(gaugeHead, "", "delegation-text");
            topGauge = new DelegationGauge();
            topGauge.AddToClassList("dgauge--top");
            gauge.Add(topGauge);

            var menu = Add(top, "menu");
            MenuButton(menu, IconKind.Info, "도움말", () =>
            {
                if (!overlayOpen && !session.IsOver)
                    ShowIntro();
            }).tooltip = "운영 브리핑과 화면 안내 다시 보기";
            MenuButton(menu, IconKind.Save, "저장", ManualSave);
            MenuButton(menu, IconKind.Reset, "초기화", ConfirmReset);
        }

        Button MenuButton(VisualElement parent, IconKind icon, string label, Action onClick)
        {
            var button = new Button(onClick);
            button.AddToClassList("menu-button");
            button.Add(new LineIcon(icon));
            var text = Text(button, label, "menu-label");
            text.pickingMode = PickingMode.Ignore;
            parent.Add(button);
            return button;
        }

        void BuildStatus()
        {
            var panel = Panel(leftColumn, "status", IconKind.Gauge, "핵심 지표", "추세", ShowTrends);
            var hero = Add(panel, "hero-block");
            hero.tooltip = IndexTip(Index.Stability);
            var heroText = Add(hero, "hero-text");
            Text(heroText, "사회 안정 지수", "hero-label");
            var heroRow = Add(heroText, "hero-row");
            heroValue = Text(heroRow, "", "hero-value num");
            heroDelta = Text(heroRow, "", "delta delta--big");
            heroChart = new LineChart { AutoRange = true };
            heroChart.AddToClassList("hero-chart");
            hero.Add(heroChart);

            StatRow(panel, IconKind.Smile, Palette.Cyan, "행복 지수", Index.Happiness);
            StatRow(panel, IconKind.Link, Palette.Cyan, "사회 정합 지수", Index.Cohesion);

            var autonomy = Add(panel, "autonomy");
            var autonomyHead = Add(autonomy, "autonomy-head");
            Text(autonomyHead, "자율성", "autonomy-title");
            gapLabel = Text(autonomyHead, "", "autonomy-gap");
            AutonomyBar(autonomy, IconKind.Eye, "주민이 느끼는 자율성", Felt, "felt", out feltFill, out feltValue,
                out feltDelta, IndexTip(Index.AutonomyFelt));
            AutonomyBar(autonomy, IconKind.Target, "실제 자율성", Real, "real", out realFill, out realValue,
                out realDelta, IndexTip(Index.AutonomyReal));
            Text(autonomy, "실제 자율성이 줄어드는 것은 운영의 진척이다.", "ops-note");

            unrestLabel = Text(panel, "", "unrest");
        }

        // One index as a data row: label, change (colored by what is good for this index), value, status, bar.
        void StatRow(VisualElement parent, IconKind icon, Color color, string label, Index index)
        {
            var row = Add(parent, "stat-row");
            row.tooltip = IndexTip(index);
            Badge(row, icon, color, "badge badge--small");
            var body = Add(row, "stat-body");
            var head = Add(body, "stat-head");
            Text(head, label, "stat-label");
            var delta = Text(head, "", "delta");
            var number = Text(head, "", "stat-value num");
            var status = Text(head, "", "state-tag");
            var bar = Add(body, "mini-bar mini-bar--wide");
            var fill = Add(bar, "mini-fill");
            statRows.Add((index, number, delta, status, fill));
        }

        void AutonomyBar(VisualElement parent, IconKind icon, string label, Color color, string kind,
            out VisualElement fill, out Label value, out Label delta, string tip)
        {
            var row = Add(parent, "autonomy-row");
            row.tooltip = tip;
            Badge(row, icon, color, "badge badge--small");
            var body = Add(row, "autonomy-body");
            var head = Add(body, "autonomy-row-head");
            Text(head, label, "autonomy-label");
            delta = Text(head, "", "delta");
            value = Text(head, "", "autonomy-value num");
            var bar = Add(body, "bar");
            fill = Add(bar, "bar-fill bar-fill--" + kind);
        }

        // Delegation summary: how close the next hand-over of power is (real numbers from the monthly judgment).
        VisualElement delegationBody;

        void BuildDelegation()
        {
            var panel = Panel(leftColumn, "delegation-panel", IconKind.Star, "위임 현황", "결산", () => GoTo(Page.Report));
            delegationBody = Add(panel, "delegation-body");
        }

        void RefreshDelegation()
        {
            delegationBody.Clear();
            var head = Add(delegationBody, "hero-row");
            Text(head, $"{session.Stage}", "hero-value num");
            Text(head, $"/ {OpsTuning.MaxStage} 단계", "hero-unit");
            float needed = session.NextThreshold;
            bool last = float.IsInfinity(needed);
            var meter = new DelegationGauge();
            meter.AddToClassList("dgauge--panel");
            SetGauge(meter);
            delegationBody.Add(meter);
            var months = Add(delegationBody, "dgauge-months");
            for (int i = 1; i <= OpsTuning.StreakMonths; i++)
                Text(months, last ? "" : $"{i}개월", "dgauge-month" + (i <= session.RiseStreak ? " dgauge-month--on" : ""));
            if (!last)
                Text(delegationBody, GaugeStateText(), "dgauge-state " + GaugeStateTone());
            void Row(string name, string value, string tone = null)
            {
                var row = Add(delegationBody, "data-row");
                Text(row, name, "data-name");
                var v = Text(row, value, "data-value num");
                if (tone != null)
                    v.AddToClassList(tone);
            }
            Row("공개 지표 합 / 기준", last ? $"{session.Gauge:0}" : $"{session.Gauge:0} / {needed:0}", !last && session.Gauge >= needed ? "tone-good" : null);
            if (!last)
                Row("기준 충족", $"{session.RiseStreak} / {OpsTuning.StreakMonths}개월");
            if (session.FallStreak > 0)
                Row("안정 기준 미달", $"{session.FallStreak} / {OpsTuning.FallMonths}개월", "tone--bad");
            string next = session.NextUnlock == UnlockKind.Field ? "새 분야 위임" : session.NextUnlock == UnlockKind.Slot ? "정책 슬롯 +1" : "시설 부지 확장";
            Row("다음 단계에서 열림", last ? "최고 단계" : next);
            Row("맡은 범위", $"분야 {session.OpenFields.Count} · 슬롯 {session.Slots} · 부지 구역당 {session.ZoneSlots}");
        }

        // The delegation meter: months met so far, this month's progress while the threshold holds.
        void SetGauge(DelegationGauge meter)
        {
            float needed = session.NextThreshold;
            var date = session.Date;
            float monthProgress = (date.Day - 1f) / DateTime.DaysInMonth(date.Year, date.Month);
            var state = float.IsInfinity(needed) ? DelegationGauge.State.Max
                : session.FallStreak > 0 ? DelegationGauge.State.Danger
                : session.Gauge >= needed ? DelegationGauge.State.Meeting
                : DelegationGauge.State.Below;
            meter.Set(session.RiseStreak, OpsTuning.StreakMonths, monthProgress, state);
        }

        string GaugeStateText()
        {
            float needed = session.NextThreshold;
            if (session.FallStreak > 0)
                return $"안정 경고 {session.FallStreak}/{OpsTuning.FallMonths}";
            return session.Gauge >= needed ? "기준 충족 중" : $"기준까지 {needed - session.Gauge:0}";
        }

        string GaugeStateTone() =>
            session.FallStreak > 0 ? "tone--bad" : session.Gauge >= session.NextThreshold ? "tone-good" : "tone--warn";

        void BuildAlerts()
        {
            var panel = Panel(rightColumn, "alerts", IconKind.Alert, "주요 알림", "전체", ShowLog);
            logList = Add(panel, "alert-list");
        }

        void BuildPolicy()
        {
            var panel = Panel(rightColumn, "policy", IconKind.Policy, "정책 요약");
            var head = Add(panel, "policy-head");
            policySlots = Text(head, "", "policy-slots");
            policyWindow = Text(head, "", "policy-window");
            policyList = Add(panel, "policy-list");
            policyButton = MakeButton(head, "정책 결정", () => ShowPolicies(), "kbtn kbtn--small");
        }

        void BuildSim()
        {
            var panel = Add(overviewPage, "sim-holder");
            simHome = panel;
            sim = new SimPanel();
            sim.ResidentClicked += id =>
            {
                featured = id;
                ShowResident(id);
            };
            panel.Add(sim);
        }

        void BuildResearch()
        {
            var panel = Panel(rightColumn, "research", IconKind.Research, "진행 중 연구", "연구 개발", ShowResearch);
            var body = Add(panel, "research-body");
            Badge(body, IconKind.Research, null, "badge badge--large");
            var text = Add(body, "research-text");
            researchBranch = Text(text, "", "research-branch");
            researchName = Text(text, "", "research-name");
            researchText = Text(text, "", "research-desc");
            var progress = Add(panel, "research-progress");
            var progressHead = Add(progress, "research-progress-head");
            Text(progressHead, "연구 진행률", "research-label");
            researchPercent = Text(progressHead, "", "research-percent num");
            var bar = Add(progress, "bar bar--thick");
            researchFill = Add(bar, "bar-fill bar-fill--research");
            researchEta = Text(progress, "", "research-eta");
        }

        void BuildResident()
        {
            var panel = Panel(rightColumn, "observe", IconKind.Eye, "주목할 시민", "시민 관찰", ShowResidents);
            var body = Add(panel, "observe-body");
            residentFace = Add(body, "observe-face");
            residentFace.RegisterCallback<ClickEvent>(_ => ShowResident(featured));
            var text = Add(body, "observe-text");
            var nameRow = Add(text, "observe-name-row");
            residentName = Text(nameRow, "", "observe-name");
            var nav = Add(nameRow, "observe-nav");
            MakeButton(nav, "‹", () => Feature(-1), "nav-button");
            MakeButton(nav, "›", () => Feature(+1), "nav-button");
            residentJob = Text(text, "", "observe-job");
            var moodRow = Add(text, "observe-mood-row");
            Text(moodRow, "기분", "observe-mood-label");
            var bar = Add(moodRow, "bar observe-bar");
            residentMood = Add(bar, "bar-fill bar-fill--mood");
            residentMoodValue = Text(moodRow, "", "observe-mood-value");
            residentQuote = Text(text, "", "observe-quote");
        }

        void Feature(int step)
        {
            var cast = Residents.Cast.ToList();
            int at = cast.FindIndex(r => r.Id == featured);
            featured = cast[(at + step + cast.Count) % cast.Count].Id;
            sfx.Play(Cue.Click);
            RefreshResident();
        }

        // ------------------------------------------------------------------ refresh

        void Refresh()
        {
            var date = session.Date;
            dateLabel.text = $"{date:yyyy.MM.dd} ({"일월화수목금토"[(int)date.DayOfWeek]})";
            var nextMonth = new DateTime(date.Year, date.Month, 1).AddMonths(1);
            monthLabel.text = $"시범 운영 {date.Year - OpsTuning.StartYear + 1}년 차 · 결산까지 {(nextMonth - date).Days}일";
            RefreshTime();

            float net = session.PowerPerWeek - session.UpkeepPerWeek;
            energyLabel.text = $"{session.Energy:0}/{session.EnergyCap:0}";
            energyRate.text = $"{Signed(net)}/주";
            energyRate.EnableInClassList("resource-rate--down", net < 0f);
            energyLabel.parent.tooltip = $"에너지: 생산 {session.PowerPerWeek:0.#}/주, 유지비 {session.UpkeepPerWeek:0.#}/주";
            Count(infoLabel, session.Info, v => $"{v:0}");
            infoRate.text = $"+{session.InfoPerWeek:0.#}/주";
            infoLabel.parent.tooltip = "정보: 연구에 쓴다. 다섯 지표의 평균에 비례해 매주 들어오고, 사건 해결과 도감에서도 얻는다.";

            Count(popLabel, session.ManagedPopulation, Population);
            float popChange = session.ManagedPopulation - (session.Report ?? session.LastReport)?.PopulationBefore ?? 0f;
            popRate.text = session.LastReport == null ? "관리" : (popChange >= 0 ? $"+{popChange:0}" : $"{popChange:0}") + "/월";
            popRate.EnableInClassList("resource-rate--down", popChange < 0f);
            stageLabel.text = $"위임 {session.Stage}단계";
            float needed = session.NextThreshold;
            gaugeLabel.text = float.IsInfinity(needed)
                ? "최고 단계"
                : $"→ {session.Stage + 1}단계 · {session.RiseStreak}/{OpsTuning.StreakMonths}개월 · {GaugeStateText()}";
            gaugeLabel.EnableInClassList("tone--warn", !float.IsInfinity(needed) && session.Gauge < needed && session.FallStreak == 0);
            gaugeLabel.EnableInClassList("tone--bad", session.FallStreak > 0);
            SetGauge(topGauge);

            RefreshStatus();
            RefreshDelegation();
            RefreshGuide();
            RefreshZones();
            RefreshPolicy();
            RefreshResearch();
            RefreshResident();

            RefreshMenu();
            sim.SetPopulation(session.SampleCount, session.Zones);
            sim.SetZoneStages(session.TensionStage);
        }

        static string Signed(float v) => v >= 0f ? $"+{v:0.#}" : $"{v:0.#}";

        void RefreshStatus()
        {
            var last = session.History.Count >= 2 ? session.History[session.History.Count - 2] : null;
            float stability = session.Indices[Index.Stability];
            Count(heroValue, stability, v => $"{v:0.0}");
            SetTone(heroValue, stability);
            SetDelta(heroDelta, last == null ? 0f : stability - last[Index.Stability], Index.Stability);
            var recentStability = session.History.Skip(Math.Max(0, session.History.Count - 26)).Select(h => h[Index.Stability]).ToList();
            heroChart.Set(new[] { ((IReadOnlyList<float>)recentStability, ToneColor(stability), false) });
            foreach (var (index, number, delta, status, fill) in statRows)
            {
                float v = session.Indices[index];
                Count(number, v, x => $"{x:0.0}");
                SetTone(number, v);
                SetDelta(delta, last == null ? 0f : v - last[index], index);
                SetStatus(status, v);
                fill.style.width = Length.Percent(v);
                fill.style.backgroundColor = ToneColor(v);
            }
            float felt = session.Indices[Index.AutonomyFelt], real = session.Indices[Index.AutonomyReal];
            feltFill.style.width = Length.Percent(felt);
            realFill.style.width = Length.Percent(real);
            Count(feltValue, felt, v => $"{v:0}");
            Count(realValue, real, v => $"{v:0}");
            SetDelta(feltDelta, last == null ? 0f : felt - last[Index.AutonomyFelt], Index.AutonomyFelt);
            SetDelta(realDelta, last == null ? 0f : real - last[Index.AutonomyReal], Index.AutonomyReal);
            float gap = felt - real;
            gapLabel.text = gap > 5f ? $"격차 {gap:0}" : "격차 없음";
            gapLabel.EnableInClassList("autonomy-gap--wide", gap > 30f);
            gapLabel.tooltip = "주민이 느끼는 자율성과 실제 자율성의 차이. 주민은 스스로 고른다고 믿는다.";

            unrestLabel.text = $"민원·시위 발생률 {session.EventChance * 100f:0.#}% / 일";
            unrestLabel.EnableInClassList("unrest--high", session.EventChance > 0.1f);
        }

        static void SetTone(Label label, float value)
        {
            foreach (var c in new[] { "tone--bad", "tone--warn", "tone--mid", "tone--good" })
                label.RemoveFromClassList(c);
            label.AddToClassList(ToneClass(value));
        }

        // ▲/▼ with the number; green when the change is good for this index, red when it is bad.
        static void SetDelta(Label label, float change, Index index)
        {
            bool tiny = Math.Abs(change) < 0.05f;
            label.text = tiny ? "–" : change > 0 ? $"▲{change:0.#}" : $"▼{-change:0.#}";
            bool good = HigherIsBetter(index) ? change > 0 : change < 0;
            label.EnableInClassList("delta--up", !tiny && good);
            label.EnableInClassList("delta--down", !tiny && !good);
        }

        // A word next to a colored value, so the state never rests on color alone.
        static void SetStatus(Label label, float value)
        {
            label.text = value < 30f ? "위험" : value < 45f ? "주의" : "";
            // Hidden, not removed: rows keep the same width whether or not they carry a word.
            label.style.visibility = label.text.Length > 0 ? Visibility.Visible : Visibility.Hidden;
            label.EnableInClassList("state-tag--bad", value < 30f);
            label.EnableInClassList("state-tag--warn", value >= 30f && value < 45f);
        }

        // Zone list and map (observation page): satisfaction per zone; clicking switches the camera.
        void ZoneStatus(VisualElement parent)
        {
            var panel = Card(parent, "zones-panel", IconKind.Pin, "구역 감시 현황");
            var row = Add(panel, "zones-body");
            var list = Add(row, "zone-list");
            var map = new ZoneMap(z => StageColor(session.TensionStage(z)), z => true);
            map.ZoneClicked += z =>
            {
                sim.ShowZone(z);
                sfx.Play(Cue.Click);
                RefreshZones();
                RenderPage();
            };
            row.Add(map);
            for (int z = 0; z < Society.Zones.Count; z++)
            {
                int zone = z;
                var line = Add(list, "zone-row");
                line.EnableInClassList("zone-row--on", sim.Zone == z);
                Add(line, "zone-dot").style.backgroundColor = StageColor(session.TensionStage(z));
                var text = Add(line, "zone-text");
                Text(text, Society.Zones[z].Name, "zone-name");
                Text(text, $"{Society.Zones[z].Kind} · {Population(session.ZonePop[z])}", "zone-sub");
                Text(line, $"{session.ZoneTension[z]:0}", "zone-value num");
                var status = Text(line, "", "state-tag");
                SetStage(status, z);
                line.RegisterCallback<ClickEvent>(_ =>
                {
                    sim.ShowZone(zone);
                    sfx.Play(Cue.Click);
                    RefreshZones();
                    RenderPage();
                });
            }
            map.Select(sim.Zone);
        }

        static string IndexTip(Index index)
        {
            switch (index)
            {
                case Index.Happiness: return "관리하는 주민들의 행복. 낮으면 사회 안정이 매주 깎인다.";
                case Index.Stability: return "불만, 시위, 폭동에 따라 오르내린다. 0이 되면 사회가 무너진다.";
                case Index.Cohesion: return "주민의 행동·역할·욕구가 운영 설계와 맞물리는 정도. 100은 완전한 통제.";
                case Index.AutonomyFelt: return "주민이 스스로 선택한다고 느끼는 정도. 통제 정책이 깎는다.";
                default: return "간섭 없이 내려진 선택의 비율. 유도 정책은 이것만 깎는다.";
            }
        }

        void RefreshPolicy()
        {
            policySlots.text = $"슬롯 {session.Active.Count}/{session.Slots}";
            policyWindow.text = session.PolicyWindowOpen ? "결정 기간" : "다음 결산까지 고정";
            policyWindow.EnableInClassList("policy-window--open", session.PolicyWindowOpen);
            policyButton.EnableInClassList("kbtn--hot", session.PolicyWindowOpen);
            policyList.Clear();
            if (session.Active.Count == 0)
                Text(policyList, "시행 중인 정책이 없다.", "empty");
            int worn = session.Active.Keys.Count(id => session.Habituation(id) < 0.8f);
            int conflicts = session.ActiveLinks.Count(l => l.Conflict);
            if (worn + conflicts > 0)
            {
                var warn = Text(policyList, (conflicts > 0 ? $"정책 충돌 {conflicts}건" : "") + (conflicts > 0 && worn > 0 ? " · " : "") +
                    (worn > 0 ? $"효과 약화 {worn}건" : ""), "summary-warn");
                warn.RegisterCallback<ClickEvent>(_ =>
                {
                    subPage[Page.Watch] = 2;
                    GoTo(Page.Watch);
                });
            }
            foreach (var pair in session.Active)
            {
                var def = Policies.Get(pair.Key);
                var row = Add(policyList, "policy-row");
                row.RegisterCallback<ClickEvent>(_ => ShowPolicies());
                var tag = Text(row, Policies.KindName(def.Kind), "kind");
                tag.AddToClassList(def.Kind == PolicyKind.Control ? "kind--control" : "kind--guide");
                var names = Add(row, "policy-names");
                Text(names, def.Name, "policy-name");
                Text(names, Policies.FieldName(def.Field), "policy-effect");
                var pips = Add(row, "pips");
                for (int i = 0; i < Policies.MaxIntensity; i++)
                    Add(pips, i < pair.Value.Intensity ? "pip pip--on" : "pip");
            }
        }

        void RefreshResearch()
        {
            if (session.CurrentResearch == null)
            {
                researchBranch.text = "대기";
                researchName.text = "진행 중인 연구 없음";
                researchText.text = "연구 목록에서 다음 과제를 고른다. 정보와 에너지가 든다.";
                researchFill.style.width = 0f;
                researchPercent.text = "";
                researchEta.text = "";
                return;
            }
            var def = Research.Get(session.CurrentResearch);
            researchBranch.text = $"{Research.BranchName(def.Branch)} · {def.Tier}단계";
            researchName.text = def.Name;
            researchText.text = def.Description;
            researchFill.style.width = Length.Percent(session.ResearchProgress * 100f);
            researchPercent.text = $"{session.ResearchProgress * 100f:0}%";
            float daysLeft = session.ResearchLeft / Math.Max(0.01f, session.ResearchSpeed);
            researchEta.text = $"예상 완료 {session.Date.AddDays(Mathf.CeilToInt(daysLeft)):yyyy.MM.dd} · 속도 ×{session.ResearchSpeed:0.##}";
        }

        void RefreshResident()
        {
            var def = Residents.Get(featured);
            if (def == null)
                return;
            if (residentFace.childCount == 0 || ((CastPortrait)residentFace[0]).ResidentId != def.Id)
            {
                residentFace.Clear();
                var portrait = new CastPortrait(def.Id);
                portrait.AddToClassList("observe-portrait");
                residentFace.Add(portrait);
            }
            ((CastPortrait)residentFace[0]).Expression = MoodFace(def);
            residentName.text = $"{def.Name} ({def.Age})";
            residentJob.text = $"{def.Job} · 주제: {def.Theme}";
            float mood = session.Mood(def);
            residentMood.style.width = Length.Percent(mood);
            residentMoodValue.text = $"{mood:0}";
            var line = session.Line(def);
            residentQuote.text = line != null ? $"“{line}”" : $"“{def.Bio}”";
        }

        void OnLogged(string line)
        {
            var row = AddLogLine(line);
            row.AddToClassList("alert--enter");
            row.AddToClassList("alert--fresh");
            row.schedule.Execute(() => row.RemoveFromClassList("alert--enter")).StartingIn(30);
            row.schedule.Execute(() => row.RemoveFromClassList("alert--fresh")).StartingIn(2200);
            BannerForLog(line);
        }

        // The alert list keeps the newest few log lines, each with an icon for its kind.
        VisualElement AddLogLine(string line)
        {
            var row = Add(null, "alert");
            var (icon, color) = AlertIcon(line);
            Badge(row, icon, color, "badge badge--small");
            int space = line.IndexOf(' ');
            var body = Add(row, "alert-body");
            Text(body, space > 0 ? line.Substring(space + 1) : line, "alert-text");
            Text(body, space > 0 ? line.Substring(0, space) : "", "alert-date");
            logList.Insert(0, row);
            while (logList.childCount > 3)
                logList.RemoveAt(logList.childCount - 1);
            return row;
        }

        static (IconKind, Color) AlertIcon(string line)
        {
            if (line.Contains("[") && (line.Contains("민원") || line.Contains("집단") || line.Contains("반작용") || line.Contains("외부")))
                return (IconKind.Alert, new Color32(247, 147, 59, 255));
            if (line.Contains("붕괴") || line.Contains("회수") || line.Contains("하락") || line.Contains("부족"))
                return (IconKind.Alert, Warn);
            if (line.Contains("연구"))
                return (IconKind.Research, Felt);
            if (line.Contains("결산") || line.Contains("상승") || line.Contains("위임"))
                return (IconKind.Star, Gold);
            if (line.Contains("정책"))
                return (IconKind.Policy, Palette.Cyan);
            return (IconKind.Info, Felt);
        }

        // ----- trends (the chart, larger)

        void ShowTrends()
        {
            var body = OpenOverlay("지표 추세 (최근 60주)", "ops-modal--narrow");
            var big = new LineChart();
            big.AddToClassList("chart--big");
            var all = session.History.ToList();
            IReadOnlyList<float> Series(Index i) => all.Select(h => h[i]).ToList();
            big.Set(new[]
            {
                (Series(Index.Happiness), Gold, false), (Series(Index.Stability), Palette.Cyan, false),
                (Series(Index.Cohesion), Violet, false), (Series(Index.AutonomyFelt), Felt, true),
                (Series(Index.AutonomyReal), Real, true),
            });
            body.Add(big);
            foreach (var index in IndexNames.All)
                Text(body, $"{IndexNames.Name(index)} {session.Indices[index]:0} — {IndexTip(index)}", "ops-note");
        }
    }
}
