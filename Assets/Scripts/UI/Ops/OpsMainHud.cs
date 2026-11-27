using System;
using System.Collections.Generic;
using System.Linq;
using Sector.Ops;
using UnityEngine;
using UnityEngine.UIElements;
using Index = Sector.Ops.Index;

namespace Sector.UI
{
    // Main screen HUD, option B (2026-11-27): a game HUD instead of the web-like panels. The layout is the UXML
    // Resources/UI/MainHud.uxml (styles Assets/UI/MainHud.uss, frames from Resources/UiKit2); this fills it from the
    // session, builds the repeated items (index gauges, feed cards, the command dock) and wires the buttons. The old
    // top bar, side menu and overview columns stay in the tree (their refresh code still runs) but are hidden by
    // .ops--hud2. Pages open from the dock between the top HUD and the dock.
    public partial class OpsScreen
    {
        VisualElement mainHud, feedBox, sysMenu;
        Label mhDate, mhStage, mhCrisisDd, mhCrisisTitle, mhCrisisSub, mhEnergy, mhInfo, mhPop;
        VisualElement mhCells, mhCrisis;
        HudRing mhStageRing;
        readonly List<(Index index, VisualElement fill, Label value)> mhBars = new List<(Index, VisualElement, Label)>();
        VisualElement mhEnergyBar; Label mhEnergyBarValue;
        Button mhPause;
        readonly Button[] mhSpeeds = new Button[3];
        readonly List<(Index index, HudRing ring, Label value, VisualElement item)> mhGauges = new List<(Index, HudRing, Label, VisualElement)>();
        readonly Dictionary<Page, (Button button, Label badge)> mhCommands = new Dictionary<Page, (Button, Label)>();
        readonly List<Button> mhLayers = new List<Button>(), mhViews = new List<Button>();
        string feedKey;

        static readonly (Page page, IconKind icon, string label)[] Commands =
        {
            (Page.Overview, IconKind.Gauge, "도시"),
            (Page.Policy, IconKind.Policy, "정책"),
            (Page.Research, IconKind.Research, "연구"),
            (Page.Citizens, IconKind.People, "시민"),
            (Page.Watch, IconKind.Eye, "관찰"),
            (Page.Facility, IconKind.Facility, "시설"),
            (Page.Report, IconKind.Star, "보고서"),
            (Page.Log, IconKind.Log, "기록"),
        };
        static readonly (Index index, IconKind icon, string name)[] GaugeDefs =
        {
            (Index.Happiness, IconKind.Smile, "행복"),
            (Index.Stability, IconKind.Shield, "사회 안정"),
            (Index.Cohesion, IconKind.Link, "사회 정합"),
            (Index.AutonomyFelt, IconKind.Target, "자율성 (느끼는)"),
            (Index.AutonomyReal, IconKind.Eye, "자율성 (실제)"),
        };

        void BuildMainHud()
        {
            var tree = Resources.Load<VisualTreeAsset>("UI/MainHud");
            if (tree == null)
                return;
            tree.CloneTree(root);
            // the pages' styles (Pages.uss) live under Resources so they can sit on the root, where the pages are
            var pages = Resources.Load<StyleSheet>("UI/Pages");
            if (pages != null)
                root.styleSheets.Add(pages);
            mainHud = root.Q("main-hud");
            root.AddToClassList("ops--hud2");
            // over the city and the pages, under the overlays, toasts and effects
            mainHud.PlaceInFront(pageHost);

            mhDate = mainHud.Q<Label>("date");
            mhStage = mainHud.Q<Label>("stage");
            mhCells = mainHud.Q("stage-cells");
            mhStageRing = new HudRing(7f) { style = { flexGrow = 1 } };
            mainHud.Q("stage-ring").Add(mhStageRing);
            mainHud.Q("emblem").RegisterCallback<ClickEvent>(_ => GoTo(Page.Report));

            mhPause = mainHud.Q<Button>("pause");
            mhPause.clicked += TogglePause;
            for (int i = 0; i < Speeds.Length; i++)
            {
                int s = Speeds[i];
                mhSpeeds[i] = mainHud.Q<Button>("speed-" + s);
                mhSpeeds[i].clicked += () => SetSpeed(s);
            }

            mhCrisis = mainHud.Q("crisis");
            mhCrisisDd = mainHud.Q<Label>("crisis-dd");
            mhCrisisTitle = mainHud.Q<Label>("crisis-title");
            mhCrisisSub = mainHud.Q<Label>("crisis-sub");
            mhCrisis.RegisterCallback<ClickEvent>(_ =>
            {
                var c = session.NextCrisis;
                if (c != null) { sfx.Play(Cue.Select); ShowCrisis(c); }
            });

            mhEnergy = Chip("res-energy", IconKind.Bolt, Caution, "에너지: 정책과 시설에 쓴다. 매주 생산에서 유지비를 뺀 만큼 쌓인다.");
            mhInfo = Chip("res-info", IconKind.Data, Palette.Cyan, "정보: 연구에 쓴다.");
            mhPop = Chip("res-pop", IconKind.People, new Color(0.75f, 0.88f, 1f), "관리 구역의 인구");

            var gauges = mainHud.Q("gauges");
            foreach (var (index, icon, name) in GaugeDefs)
            {
                var item = new VisualElement(); item.AddToClassList("mh-gauge");
                var dial = new VisualElement(); dial.AddToClassList("mh-gauge-dial"); item.Add(dial);
                var ring = new HudRing(7f) { style = { flexGrow = 1 } }; dial.Add(ring);
                var inner = new VisualElement(); inner.AddToClassList("mh-gauge-in"); dial.Add(inner);
                var gi = new LineIcon(icon, Palette.Cyan); gi.AddToClassList("mh-gauge-icon"); inner.Add(gi); ring.userData = gi;
                var text = new VisualElement(); text.AddToClassList("mh-gauge-text"); item.Add(text);
                var value = new Label(); value.AddToClassList("mh-gauge-value"); value.AddToClassList("num"); text.Add(value);
                var label = new Label(name); label.AddToClassList("mh-gauge-name"); text.Add(label);
                item.tooltip = IndexNames.Name(index);
                item.RegisterCallback<ClickEvent>(_ => GoTo(Page.Report));
                gauges.Add(item);
                mhGauges.Add((index, ring, value, item));
            }

            feedBox = mainHud.Q("feed");

            // model view: index bars on both sides of the monitor (the A mock-up)
            var left = mainHud.Q("side-left"); var right = mainHud.Q("side-right");
            for (int i = 0; i < GaugeDefs.Length; i++)
            {
                var (index, _, name) = GaugeDefs[i];
                var (item, fill, value) = Bar(i < 3 ? left : right, name.Replace("자율성 ", "자율 "));
                mhBars.Add((index, fill, value));
            }
            (_, mhEnergyBar, mhEnergyBarValue) = Bar(right, "에너지");

            var mini = mainHud.Q("mini");
            var vista = Resources.Load<Texture2D>("Macro/city_main");
            if (vista != null) mini.style.backgroundImage = new StyleBackground(vista);
            mini.tooltip = "처음 위치로";
            mini.RegisterCallback<ClickEvent>(_ => { if (!modelMode) ResetView(); else modelCity?.GoHome(); });

            var dock = mainHud.Q("dock");
            foreach (var (page, icon, label) in Commands)
            {
                var p = page;
                var b = new Button(() => GoTo(currentPage == p && p != Page.Overview ? Page.Overview : p));
                b.AddToClassList("mh-cmd");
                var tile = new VisualElement { pickingMode = PickingMode.Ignore }; tile.AddToClassList("mh-cmd-hex"); b.Add(tile);
                var ic = new LineIcon(icon, new Color(0.8f, 0.93f, 1f)) { pickingMode = PickingMode.Ignore }; ic.AddToClassList("mh-cmd-icon"); tile.Add(ic);
                var lab = new Label(label) { pickingMode = PickingMode.Ignore }; lab.AddToClassList("mh-cmd-label"); b.Add(lab);
                var badge = new Label { pickingMode = PickingMode.Ignore }; badge.AddToClassList("mh-cmd-badge"); badge.AddToClassList("num"); tile.Add(badge);
                dock.Add(b);
                mhCommands[page] = (b, badge);
            }

            var layers = mainHud.Q("layers");
            for (int i = 0; i < LayerNames.Length; i++)
            {
                var layer = (MapLayer)i;
                var b = new Button(() => { mapLayer = layer; sfx.Play(Cue.Click); RefreshMapControls(); RefreshMainHudControls(); }) { text = LayerNames[i] };
                b.AddToClassList("mh-seg-item"); b.userData = layer; layers.Add(b); mhLayers.Add(b);
            }
            // map layers wash the district shapes, which are off for the current painting (OpsMap.PaintingDistricts)
            layers.style.display = PaintingDistricts ? DisplayStyle.Flex : DisplayStyle.None;
            var views = mainHud.Q("views");
            foreach (var (label, model) in new[] { ("도시 그림", false), ("입체 모형", true) })
            {
                var b = new Button(() => SetModelMode(model)) { text = label };
                b.AddToClassList("mh-seg-item"); views.Add(b); mhViews.Add(b);
            }

            // the system menu (help, save, reset) folds behind one button
            sysMenu = mainHud.Q("system-menu");
            foreach (var (label, act) in new (string, Action)[] { ("도움말", () => { if (!overlayOpen && !session.IsOver) ShowIntro(); }), ("저장", ManualSave), ("초기화", ConfirmReset) })
            {
                var b = new Button(() => { sysMenu.RemoveFromClassList("mh-sysmenu--open"); act(); }) { text = label };
                sysMenu.Add(b);
            }
            mainHud.Q<Button>("menu").clicked += () => sysMenu.ToggleInClassList("mh-sysmenu--open");
            RefreshMainHudControls();
            ZeroLabels(mainHud);
        }

        (VisualElement item, VisualElement fill, Label value) Bar(VisualElement parent, string name)
        {
            var item = new VisualElement(); item.AddToClassList("mh-bar-item");
            var bar = new VisualElement(); bar.AddToClassList("mh-bar"); item.Add(bar);
            var fill = new VisualElement(); fill.AddToClassList("mh-bar-fill"); bar.Add(fill);
            var value = new Label(); value.AddToClassList("mh-bar-value"); value.AddToClassList("num"); item.Add(value);
            var label = new Label(name); label.AddToClassList("mh-bar-name"); item.Add(label);
            parent.Add(item);
            return (item, fill, value);
        }

        // Unity's default label margins off (MainHud.uss .mh-l); each label's own class then sets what it needs
        static void ZeroLabels(VisualElement e) => e.Query<Label>().ForEach(l => l.AddToClassList("mh-l"));

        Label Chip(string name, IconKind icon, Color color, string tip)
        {
            var chip = mainHud.Q(name);
            chip.tooltip = tip;
            chip.Q("icon").Add(new LineIcon(icon, color) { style = { flexGrow = 1 } });
            return chip.Q<Label>("value");
        }

        void RefreshMainHudControls()
        {
            if (mainHud == null)
                return;
            foreach (var b in mhLayers)
                b.EnableInClassList("mh-seg-item--on", (MapLayer)b.userData == mapLayer);
            for (int i = 0; i < mhViews.Count; i++)
                mhViews[i].EnableInClassList("mh-seg-item--on", (i == 1) == modelMode);
            foreach (var pair in mhCommands)
                pair.Value.button.EnableInClassList("mh-cmd--on", pair.Key == currentPage);
            mhPause.text = paused ? "▶" : "Ⅱ";
            mhPause.EnableInClassList("mh-round--on", paused);
            for (int i = 0; i < Speeds.Length; i++)
                mhSpeeds[i].EnableInClassList("mh-round--on", !paused && speed == Speeds[i]);
        }

        void RefreshMainHud()
        {
            if (mainHud == null)
                return;
            RefreshMainHudControls();
            mhStage.text = $"위임 {session.Stage}단계";
            mhCells.Clear();
            for (int i = 0; i < OpsTuning.StreakMonths; i++)
            {
                var c = new VisualElement(); c.AddToClassList("mh-cell");
                if (i < session.RiseStreak) c.AddToClassList("mh-cell--on");
                else if (i == session.RiseStreak && session.Gauge >= session.NextThreshold) c.AddToClassList("mh-cell--part");
                mhCells.Add(c);
            }
            mhStageRing.Set(Mathf.Clamp01(session.Stage / (float)Math.Max(1, OpsTuning.MaxStage)), Palette.Cyan);

            mhEnergy.text = $"{session.Energy:0}";
            mhInfo.text = $"{session.Info:0}";
            mhPop.text = Population(session.ManagedPopulation);

            foreach (var (index, ring, value, item) in mhGauges)
            {
                float v = session.Indices[index];
                var color = index == Index.AutonomyReal ? (v < 45 ? Caution : Palette.Cyan) : ToneColor(v);
                ring.Set(v / 100f, color);
                ((LineIcon)ring.userData).Set(GaugeDefs.First(g => g.index == index).icon, color);
                value.text = $"{v:0}";
                item.EnableInClassList("mh-gauge--warn", v < 45 && v >= 30);
                item.EnableInClassList("mh-gauge--bad", v < 30);
            }

            foreach (var (index, fill, value) in mhBars)
            {
                float v = session.Indices[index];
                fill.style.height = Length.Percent(v);
                fill.EnableInClassList("mh-bar-fill--warn", v < 45);
                value.text = $"{v:0}";
            }
            mhEnergyBar.style.height = Length.Percent(100f * session.Energy / Math.Max(1f, session.EnergyCap));
            mhEnergyBar.AddToClassList("mh-bar-fill--warn");
            mhEnergyBarValue.text = $"{session.Energy:0}";

            var crisis = session.NextCrisis;
            mhCrisis.style.display = crisis == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (crisis != null)
            {
                mhCrisisDd.text = $"D-{Math.Max(0, session.CrisisDay(crisis) - session.Day)}";
                mhCrisisTitle.text = crisis.Title;
                mhCrisisSub.text = $"준비 {crisis.Readiness(session) * 100f:0}% · 눌러서 예보 보기";
            }

            foreach (var pair in mhCommands)
            {
                string text = pair.Key == Page.Watch ? (session.Anomalies.Count > 0 ? session.Anomalies.Count.ToString() : "")
                    : pair.Key == Page.Report ? (session.Report != null ? "!" : "")
                    : "";
                pair.Value.badge.text = text;
                pair.Value.badge.style.display = text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            RefreshFeed();
        }

        // Up to three cards: the tensest district, the latest log line, a time-limited chance. Rebuilt only on change.
        void RefreshFeed()
        {
            var cards = new List<(IconKind icon, Color color, string title, string sub, bool bad, Action click)>();
            var hot = Enumerable.Range(0, Society.Zones.Count).Where(session.ZoneOpen).OrderByDescending(z => session.ZoneTension[z]).FirstOrDefault();
            int stage = session.TensionStage(hot);
            if (stage >= 2)
                cards.Add((IconKind.Alert, Bad, $"{Society.Zones[hot].Name} · {Tension.StageNames[stage]}", $"긴장 {session.ZoneTension[hot]:0}" + (session.Anomalies.Any(a => a.Zone == hot) ? $" · 이상 징후 {session.Anomalies.Count(a => a.Zone == hot)}" : ""), stage >= 3,
                    () => { sim.ShowZone(hot); watchCamera = true; subPage[Page.Watch] = 0; GoTo(Page.Watch); }));
            if (session.Log.Count > 0)
            {
                var line = session.Log[session.Log.Count - 1];
                int sp = line.IndexOf(' ');
                cards.Add((IconKind.Log, Palette.Cyan, sp > 0 ? line.Substring(sp + 1) : line, sp > 0 ? line.Substring(0, sp) : "", false, () => GoTo(Page.Log)));
            }
            if (session.PolicyWindowOpen)
                cards.Add((IconKind.Policy, Palette.Cyan, "정책을 바꿀 수 있다", "시간이 흐르기 전까지", false, () => GoTo(Page.Policy)));

            string key = string.Join("|", cards.Select(c => c.title + c.sub));
            if (key == feedKey)
                return;
            feedKey = key;
            feedBox.Clear();
            int n = 0;
            foreach (var (icon, color, title, sub, bad, click) in cards.Take(3))
            {
                var card = new VisualElement(); card.AddToClassList("mh-card"); card.EnableInClassList("mh-card--bad", bad);
                card.pickingMode = PickingMode.Position;
                var pic = new VisualElement(); pic.AddToClassList("mh-card-pic"); pic.Add(new LineIcon(icon, color) { style = { width = 30, height = 30 } }); card.Add(pic);
                var text = new VisualElement(); text.AddToClassList("mh-card-text"); card.Add(text);
                var t = new Label(title); t.AddToClassList("mh-card-title"); text.Add(t);
                var s = new Label(sub); s.AddToClassList("mh-card-sub"); text.Add(s);
                card.RegisterCallback<ClickEvent>(_ => { sfx.Play(Cue.Select); click(); });
                feedBox.Add(card);
                // slide in one after another (USS transition on .mh-card--in)
                card.schedule.Execute(() => card.AddToClassList("mh-card--in")).StartingIn(60 + 90 * n++);
            }
            ZeroLabels(feedBox);
        }

        // the clock text follows the day's progress (a game day is 8 seconds)
        void TickMainHud()
        {
            if (mainHud == null || session == null)
                return;
            float h = Mathf.Clamp01(dayClock / DaySeconds) * 24f;
            var d = session.Date;
            mhDate.text = $"{d.Year}. {d.Month}. {d.Day}   {(int)h:00}:{(int)(h % 1 * 60) / 10 * 10:00}";
        }
    }

    // The mock-up's ring: a faint track and a round-capped arc from the top, clockwise.
    public class HudRing : VisualElement
    {
        float value; Color color = Palette.Cyan; readonly float width;
        public HudRing(float width) { this.width = width; pickingMode = PickingMode.Ignore; generateVisualContent += Draw; }
        public void Set(float v, Color c) { value = Mathf.Clamp01(v); color = c; MarkDirtyRepaint(); }
        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect; float rad = Mathf.Min(r.width, r.height) / 2 - width / 2 - 1; if (rad <= 0) return;
            var p = ctx.painter2D; p.lineWidth = width; p.lineCap = LineCap.Round;
            p.strokeColor = new Color(0.55f, 0.7f, 0.86f, 0.2f); p.BeginPath(); p.Arc(r.center, rad, 0, 360); p.Stroke();
            if (value <= 0.001f) return;
            p.strokeColor = color; p.BeginPath(); p.Arc(r.center, rad, -90, -90 + 360 * value); p.Stroke();
        }
    }
}
