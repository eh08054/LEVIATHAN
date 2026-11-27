// Trial (2026-11-27): the model view's HUD (option C, the AI's view), ported from Tools/Art/proto/city.html to UI
// Toolkit. Top bar (view tabs, clock, speed, delegation cells), bracketed corner panels (key indices, recent alerts or
// the district card), the sky band (the AI's broadcast line and the city pulse: tension, movement, power over 24 h),
// a data tag standing on each district, a bottom dock (layers, hour) and a minimap. Numbers are made up for the trial.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sector.ModelCity
{
    // What the game feeds the HUD (OpsScreen implements it); without it the trial numbers stay.
    public interface IModelData
    {
        (string name, float v, float d)[] Indices();
        List<(string k, Color c, string a, string b)> Alerts();
        (float tension, float pop, float lit) District(string k);
        string DateText();
        void OpenCamera(string k);
    }

    [RequireComponent(typeof(UIDocument))]
    public class ModelHud : MonoBehaviour
    {
        public ModelCity city;
        public StyleSheet styleSheet;
        // inside the game: the game's top bar and menu stay, so the HUD drops its own top bar and moves aside
        public bool embedded;
        public IModelData data;
        // inside the game: the monitor's opening in panel coordinates; the HUD lays itself out inside it
        [System.NonSerialized] public Rect? frame;
        VisualElement idxPanel, dockEl; Label helpEl;
        VisualElement idxBox; Label dateLabel; float refresh;
        public (float tension, float pop, float lit) D(string k) => data != null ? data.District(k) : city.State[k];
        public void SetShown(bool on) { if (hud != null) hud.style.display = on ? DisplayStyle.Flex : DisplayStyle.None; }

        VisualElement root, hud, sky, tagLayer, alerts, card, toast, mini;
        Label clock;
        readonly Dictionary<string, (VisualElement tag, VisualElement fill)> tags = new Dictionary<string, (VisualElement, VisualElement)>();
        readonly List<(Vector2 a, Vector2 b, Color c)> leaders = new List<(Vector2, Vector2, Color)>();
        readonly List<Label> skyLabels = new List<Label>();
        string focus;

        static readonly (string name, float v, float d)[] Idx = { ("행복", 62, 1.2f), ("사회 안정", 71, -0.4f), ("사회 정합", 58, 0.8f), ("자율성 (체감)", 66, 0.3f), ("자율성 (실질)", 41, -1.1f) };
        static readonly (string k, Color c, string a, string b)[] Alerts =
        {
            ("U", new Color32(238, 90, 90, 255), "문화·여가 · 모임", "광장에 사람이 모이고 있다 · 긴장 82"),
            ("K", new Color32(242, 160, 61, 255), "치안·보안 · 게이트", "바깥 대기열 1,240명 · 어제보다 +180"),
            ("E", new Color32(242, 160, 61, 255), "에너지·발전 · 예비율 12%", "오후 2시 냉방 수요 최고치 예상"),
            ("H", new Color32(53, 201, 232, 255), "주거 · 주목 시민", "한소미가 3일째 집 밖에 나오지 않았다"),
        };
        static readonly (string n, Color c, System.Func<float, float> f)[] Pulse =
        {
            ("도시 긴장", new Color32(238, 90, 90, 255), h => 38 + 10 * Mathf.Sin(h * 0.9f) + (h > 17 && h < 22 ? 14 : 0)),
            ("이동 인구", new Color32(53, 201, 232, 255), h => 20 + 55 * Mathf.Exp(-(h - 8.5f) * (h - 8.5f) / 1.5f) + 50 * Mathf.Exp(-(h - 18.5f) * (h - 18.5f) / 2) + 12 * Mathf.Exp(-(h - 13) * (h - 13) / 3)),
            ("전력 부하", new Color32(242, 160, 61, 255), h => 40 + 45 * Mathf.Exp(-(h - 14.5f) * (h - 14.5f) / 8) + 15 * Mathf.Exp(-(h - 20) * (h - 20) / 3)),
        };
        static readonly Color Cyan = new Color32(53, 201, 232, 255);

        // ------------------------------------------------------------------ building
        static VisualElement Bracket(string name)
        {
            var e = new VisualElement { name = name }; e.AddToClassList("bracket");
            foreach (var c in new[] { "tl", "tr", "bl", "br" }) { var k = new VisualElement { pickingMode = PickingMode.Ignore }; k.AddToClassList("corner"); k.AddToClassList(c); e.Add(k); }
            return e;
        }
        static Label L(string text, params string[] cls) { var l = new Label(text); foreach (var c in cls) l.AddToClassList(c); return l; }
        static VisualElement Tabs(string[] labels, int on, System.Action<int> pick)
        {
            var t = new VisualElement(); t.AddToClassList("tabs");
            var bs = new List<Button>();
            for (int i = 0; i < labels.Length; i++)
            {
                int j = i; var b = new Button(() => { foreach (var o in bs) o.RemoveFromClassList("on"); bs[j].AddToClassList("on"); pick(j); }) { text = labels[i] };
                b.AddToClassList("tab"); if (i == on) b.AddToClassList("on"); bs.Add(b); t.Add(b);
            }
            return t;
        }

        void OnEnable()
        {
            tags.Clear(); skyLabels.Clear(); leaders.Clear();
            root = GetComponent<UIDocument>().rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            if (styleSheet != null) root.styleSheets.Add(styleSheet);
            hud = new VisualElement { pickingMode = PickingMode.Ignore }; hud.AddToClassList("hud-root"); root.Add(hud);

            // under everything: leader lines and tags, then the sky band
            tagLayer = new VisualElement { pickingMode = PickingMode.Ignore }; tagLayer.style.position = Position.Absolute;
            tagLayer.style.left = 0; tagLayer.style.top = 0; tagLayer.style.right = 0; tagLayer.style.bottom = 0;
            tagLayer.generateVisualContent += DrawLeaders; hud.Add(tagLayer);
            foreach (var k in ModelCity.Keys)
            {
                var t = new VisualElement { pickingMode = PickingMode.Ignore }; t.AddToClassList("tag");
                t.Add(L(ModelCity.Names[k], "tag-name")); var pop = L("", "tag-pop", "num"); pop.name = "pop"; t.Add(pop);
                var bar = new VisualElement(); bar.AddToClassList("tag-bar"); var fill = new VisualElement(); fill.AddToClassList("tag-bar-fill"); bar.Add(fill); t.Add(bar);
                foreach (var c in t.Children()) c.pickingMode = PickingMode.Ignore;
                tagLayer.Add(t); tags[k] = (t, fill);
            }
            sky = new VisualElement { pickingMode = PickingMode.Ignore }; sky.style.position = Position.Absolute;
            sky.generateVisualContent += DrawSky; hud.Add(sky);
            var head1 = L("송출 중인 문구 · 전 구역 412개 화면", "sky-head"); head1.style.top = 2; head1.style.fontSize = 11; head1.style.color = Cyan; sky.Add(head1);
            var head2 = L("오늘도 힘찬 하루", "sky-head"); head2.style.top = 16; head2.style.fontSize = 24; head2.style.unityFontStyleAndWeight = FontStyle.Bold;
            head2.style.textShadow = new TextShadow { color = new Color(0.2f, 0.8f, 0.9f, 0.8f), blurRadius = 8 }; sky.Add(head2);
            foreach (var p in Pulse) { var a = L(p.n, "sky-label"); var b = L("", "sky-val", "num"); b.style.color = p.c; sky.Add(a); sky.Add(b); skyLabels.Add(a); skyLabels.Add(b); }
            for (int h = 0; h <= 24; h += 6) { var t = L($"{h:00}:00", "sky-tick"); sky.Add(t); skyLabels.Add(t); }
            foreach (var c in sky.Children()) c.pickingMode = PickingMode.Ignore;

            // top bar
            float y0 = embedded ? 96 : 64, xl = embedded ? 240 : 16;
            var top = new VisualElement(); top.AddToClassList("top"); hud.Add(top); if (embedded) top.style.display = DisplayStyle.None;
            top.Add(L("LEVIATHAN", "title")); top.Add(L("섹터 알파", "dim"));
            top.Add(Tabs(new[] { "도시 이미지", "입체 모형" }, 1, i => ShowToast(i == 0 ? "도시 이미지 보기\n모형을 바탕으로 그린 도시 그림 위에 A안 틀(지금 게임의 메인 화면)을 씌우는 모드입니다. 이 시험 장면에는 아직 없습니다." : null)));
            var grow = new VisualElement(); grow.AddToClassList("grow"); top.Add(grow);
            dateLabel = L("2071.03.14", "num", "clock"); top.Add(dateLabel); clock = L("", "num", "clock"); clock.style.marginLeft = -8; top.Add(clock);
            top.Add(Tabs(new[] { "Ⅱ", "1×", "2×", "4×" }, 0, i => city.speed = new[] { 0f, 1, 2, 4 }[i]));
            top.Add(L("위임 2단계", "dim")); var cells = new VisualElement(); cells.AddToClassList("cells");
            for (int i = 0; i < 6; i++) { var c = new VisualElement(); c.AddToClassList("cell"); if (i < 3) c.AddToClassList("f"); cells.Add(c); }
            top.Add(cells);

            // left: key indices
            var idx = Bracket("idx"); idx.style.left = xl; idx.style.top = y0; idx.style.width = 220; hud.Add(idx); idxPanel = idx;
            idxBox = new VisualElement(); idx.Add(idxBox); FillIndices(Idx);

            // right: alerts (the district card takes their place)
            alerts = Bracket("alerts"); alerts.style.right = 16; alerts.style.top = y0; alerts.style.width = 270; hud.Add(alerts);
            FillAlerts(new List<(string, Color, string, string)>(Alerts));
            card = Bracket("card"); card.style.right = 16; card.style.top = y0; card.style.width = 270; card.style.display = DisplayStyle.None; hud.Add(card);
            BuildBottom();
        }

        void FillIndices((string name, float v, float d)[] list)
        {
            idxBox.Clear();
            idxBox.Add(L("핵심 지수", "h3"));
            foreach (var (n, v, d) in list)
            {
                var idx = idxBox;
                bool real = n == "자율성 (실질)";
                var col = real && d < 0 ? new Color32(242, 160, 61, 255) : d > 0 ? new Color32(79, 209, 139, 255) : new Color32(238, 90, 90, 255);
                var ix = new VisualElement(); ix.AddToClassList("ix");
                var row = new VisualElement(); row.AddToClassList("ix-row"); row.Add(L(n, "ix-name"));
                var right = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.FlexEnd } };
                right.Add(L(v.ToString("0"), "ix-val", "num")); bool flat = Mathf.Abs(d) < 0.05f;
                var dl = L(flat ? "–" : (d > 0 ? "▲" : "▼") + Mathf.Abs(d).ToString("0.0"), "ix-d", "num"); dl.style.color = flat ? new Color(0.62f, 0.7f, 0.82f) : (Color)col; right.Add(dl);
                row.Add(right); ix.Add(row);
                var bar = new VisualElement(); bar.AddToClassList("bar"); var fill = new VisualElement(); fill.AddToClassList("bar-fill");
                fill.style.width = Length.Percent(v); if (v < 45) fill.style.backgroundColor = (Color)new Color32(242, 160, 61, 255); bar.Add(fill); ix.Add(bar);
                idx.Add(ix);
            }
        }
        void FillAlerts(List<(string k, Color c, string a, string b)> list)
        {
            alerts.Clear();
            foreach (var cn in new[] { "tl", "tr", "bl", "br" }) { var e = new VisualElement { pickingMode = PickingMode.Ignore }; e.AddToClassList("corner"); e.AddToClassList(cn); alerts.Add(e); }
            alerts.Add(L(embedded ? "지금 살펴볼 곳" : "최근 경보", "h3"));
            if (list.Count == 0) alerts.Add(L("눈에 띄는 움직임이 없다.", "al-sub"));
            foreach (var (k, c, a, b) in list)
            {
                var al = new VisualElement(); al.AddToClassList("al");
                var dot = new VisualElement(); dot.AddToClassList("al-dot"); dot.style.backgroundColor = c; al.Add(dot);
                var txt = new VisualElement { style = { flexShrink = 1 } }; txt.Add(L(a, "al-title")); txt.Add(L(b, "al-sub")); al.Add(txt);
                string kk = k; al.RegisterCallback<ClickEvent>(_ => { Pick(kk); city.FlyTo(kk); });
                alerts.Add(al);
            }
        }
        void BuildBottom()
        {
            // bottom: dock and minimap
            var dock = Bracket("dock"); dock.AddToClassList("dock"); dock.style.bottom = 14; dock.style.left = Length.Percent(50); dock.style.translate = new Translate(Length.Percent(-50), 0); hud.Add(dock); dockEl = dock;
            var g1 = new VisualElement(); g1.AddToClassList("grp"); g1.Add(L("겹", "lbl")); g1.Add(Tabs(new[] { "없음", "긴장", "인구" }, 0, i => city.layer = new[] { "none", "tension", "pop" }[i])); dock.Add(g1);
            var g2 = new VisualElement(); g2.AddToClassList("grp"); g2.Add(L("시각", "lbl"));
            var slider = new Slider(0, 24) { value = city != null ? city.hour : 16 }; slider.style.width = 140; slider.RegisterValueChangedCallback(e => city.hour = e.newValue); slider.name = "hour"; g2.Add(slider); dock.Add(g2);
            mini = Bracket("mini"); mini.style.right = 16; mini.style.bottom = 14; mini.style.width = 236; mini.style.height = 162; hud.Add(mini);
            var map = new VisualElement(); map.style.flexGrow = 1; map.style.overflow = Overflow.Hidden; map.generateVisualContent += DrawMini; map.RegisterCallback<ClickEvent>(e => MiniClick(map, e.localPosition)); mini.Add(map); mini.userData = map;
            var help = helpEl = L("드래그 이동 · 오른쪽 드래그 회전 · Alt+드래그 각도 · 휠 확대 · 구역 클릭 = 카드 · Esc 전체 보기 · T 시간 흐름", "help"); help.pickingMode = PickingMode.Ignore; hud.Add(help);
            if (embedded) { help.text = "드래그 이동 · 오른쪽 드래그 회전 · Alt+드래그 각도 · 휠 확대 · 구역 클릭 = 카드 · Esc 전체 보기 · 시각은 아래 슬라이더"; help.style.left = 240; }
            toast = Bracket("toast"); toast.AddToClassList("toast"); toast.style.display = DisplayStyle.None; hud.Add(toast);
        }

        // ------------------------------------------------------------------ the city side
        public bool Over(Vector2 screen)
        {
            if (root?.panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
            var e = root.panel.Pick(p);
            return e != null && e != root;
        }
        public void Pick(string k)
        {
            focus = k;
            ShowToast(null);
            if (k == null) { card.style.display = DisplayStyle.None; alerts.style.display = DisplayStyle.Flex; return; }
            var st = D(k);
            string stage = st.tension >= 80 ? "계기" : st.tension >= 60 ? "모임" : st.tension >= 40 ? "수군거림" : st.tension >= 20 ? "불만" : "잠잠";
            card.Clear();
            foreach (var c in new[] { "tl", "tr", "bl", "br" }) { var e = new VisualElement { pickingMode = PickingMode.Ignore }; e.AddToClassList("corner"); e.AddToClassList(c); card.Add(e); }
            var h = L(ModelCity.Names[k], "h3"); h.style.fontSize = 15; h.style.letterSpacing = 0; card.Add(h);
            void Row(string a, string b, Color? c = null) { var r = new VisualElement(); r.AddToClassList("card-row"); r.Add(L(a)); var v = L(b, "num"); if (c != null) v.style.color = c.Value; r.Add(v); card.Add(r); }
            Row("인구", st.pop.ToString("#,0") + "명"); Row("긴장", $"{st.tension:0} · {stage}", ModelCity.TensionColor(st.tension)); Row("입주·불빛", $"{st.lit * 100:0}%");
            var b1 = new Button(() => { if (data != null) data.OpenCamera(k); else ShowToast("블록 관찰\n게임에서는 이 구역의 블록 그림(사람이 걷는 관찰 화면)으로 들어갑니다."); }) { text = "카메라 보기 (블록 관찰)" }; b1.AddToClassList("btn"); card.Add(b1);
            var b2 = new Button(() => city.FlyTo(k, 70)) { text = "위에서 내려다보기" }; b2.AddToClassList("btn"); b2.AddToClassList("soft"); card.Add(b2);
            card.style.display = DisplayStyle.Flex; alerts.style.display = DisplayStyle.None;
        }
        void ShowToast(string text)
        {
            if (string.IsNullOrEmpty(text)) { toast.style.display = DisplayStyle.None; return; }
            toast.Clear();
            var parts = text.Split('\n');
            toast.Add(L(parts[0], "h3"));
            if (parts.Length > 1) { var l = L(parts[1]); l.style.whiteSpace = WhiteSpace.Normal; toast.Add(l); }
            var ok = new Button(() => toast.style.display = DisplayStyle.None) { text = "닫기" }; ok.AddToClassList("btn"); toast.Add(ok);
            toast.style.display = DisplayStyle.Flex;
            toast.style.left = Length.Percent(50); toast.style.top = Length.Percent(50); toast.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
        }

        // ------------------------------------------------------------------ per frame
        void Update()
        {
            if (city == null || root?.panel == null || city.Cam == null) return;
            clock.text = $"{(int)city.hour:00}:{(int)(city.hour % 1 * 60):00}";
            if (data != null && (refresh -= Time.deltaTime) <= 0)
            {
                refresh = 1f;
                FillIndices(data.Indices()); dateLabel.text = data.DateText();
                if (alerts.style.display != DisplayStyle.None) FillAlerts(data.Alerts());
                if (focus != null && card.style.display == DisplayStyle.Flex) Pick(focus);
            }
            var slider = root.Q<Slider>("hour"); if (slider != null && Mathf.Abs(slider.value - city.hour) > 0.05f) slider.SetValueWithoutNotify(city.hour);

            // the sky band sits under the top bar between the side panels
            float W = root.layout.width, H = root.layout.height;
            if (float.IsNaN(W) || W <= 0) return;
            float x0 = embedded ? Mathf.Max(600, W * 0.33f) : Mathf.Max(300, W * 0.24f), x1 = Mathf.Min(W - 330, W * 0.76f);
            float skyTop = embedded ? 90 : 58, skyH = Mathf.Clamp(H * 0.24f, 160, 230);
            if (embedded && frame is Rect f)
            {
                // inside the monitor: panels hug its inner edges, the sky band sits between them at the top
                idxPanel.style.display = DisplayStyle.None;   // the game shows the indices as bars beside the monitor
                alerts.style.right = W - f.xMax + 16; alerts.style.top = f.y + 16;
                card.style.right = W - f.xMax + 16; card.style.top = f.y + 16;
                dockEl.style.bottom = H - f.yMax + 14;
                mini.style.right = W - f.xMax + 16; mini.style.bottom = H - f.yMax + 14;
                helpEl.style.display = DisplayStyle.None;
                x0 = f.x + 160; x1 = f.xMax - 400; skyTop = f.y + 6; skyH = Mathf.Clamp(f.height * 0.26f, 140, 190);
            }
            sky.style.left = x0 - 120; sky.style.top = skyTop; sky.style.width = x1 - x0 + 200; sky.style.height = skyH;
            sky.MarkDirtyRepaint();
            PlaceSkyLabels();

            // tags: a district's data on a stick, fading as the camera comes close
            float near = 1 - Mathf.Clamp01((city.Dist - 60) / (240 - 60));
            float a = Mathf.Clamp01(1 - near / 0.7f);
            leaders.Clear();
            var placed = new List<Rect>();
            var order = new List<string> { "A" }; foreach (var k in ModelCity.Keys) if (k != "A") order.Add(k);
            foreach (var k in order)
            {
                var (tag, fill) = tags[k];
                var c = city.CentreOf(k);
                var bp = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, c, city.Cam);
                var tp = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, c + Vector3.up * (k == "A" ? 33 : 12), city.Cam);
                bool behind = city.Cam.WorldToViewportPoint(c).z < 0;
                if (a <= 0.01f || behind || tp.x < -100 || tp.x > W + 100) { tag.style.display = DisplayStyle.None; continue; }
                var st = D(k); bool hot = st.tension >= 60; var col = hot ? ModelCity.TensionColor(st.tension) : Cyan;
                var r = new Rect(tp.x - 48, tp.y - 36, 96, 36);
                for (int n = 0; n < 6; n++) { int hit = placed.FindIndex(q => q.Overlaps(new Rect(r.x - 4, r.y - 2, r.width + 8, r.height + 4))); if (hit < 0) break; r.y = placed[hit].y - 40; }
                placed.Add(r);
                tag.style.display = DisplayStyle.Flex; tag.style.left = r.x; tag.style.top = r.y; tag.style.opacity = a;
                tag.style.borderLeftColor = col;
                tag.Q<Label>("pop").text = st.pop <= 0 ? "—" : st.pop >= 10000 ? (st.pop / 10000f).ToString("0.0") + "만" : st.pop.ToString("#,0");
                fill.style.width = 32 * st.tension / 100f; fill.style.backgroundColor = ModelCity.TensionColor(st.tension);
                leaders.Add((bp, new Vector2(tp.x, r.y + 36), new Color(col.r, col.g, col.b, 0.75f * a)));
            }
            tagLayer.MarkDirtyRepaint();
            ((VisualElement)mini.userData).MarkDirtyRepaint();
        }

        // the sky band's labels follow the same geometry as DrawSky (styles can't change while drawing)
        void PlaceSkyLabels()
        {
            var r = sky.layout; if (float.IsNaN(r.width) || r.width <= 0) return;
            float x0 = 120, x1 = r.width - 80, top = 56, bot = r.height - 14, rh = (bot - top) / Pulse.Length;
            for (int i = 0; i < Pulse.Length; i++)
            {
                float yb = top + (i + 1) * rh - 6;
                var lab = skyLabels[i * 2]; lab.style.left = x0 - 90; lab.style.top = yb - 16;
                var val = skyLabels[i * 2 + 1]; val.style.left = x1 + 10; val.style.top = yb - 18; val.text = Pulse[i].f(city.hour).ToString("0");
            }
            for (int j = 0; j < 5; j++) { var t = skyLabels[Pulse.Length * 2 + j]; t.style.left = Mathf.Lerp(x0, x1, j / 4f) - 20; t.style.top = bot - 2; }
        }
        void DrawLeaders(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D; p.lineWidth = 1;
            foreach (var (a, b, c) in leaders)
            {
                p.strokeColor = c; p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.Stroke();
                p.fillColor = c; p.BeginPath(); p.Arc(a, 2.5f, 0, 360); p.Fill();
            }
        }
        void DrawSky(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D; var r = sky.contentRect; if (r.width <= 0) return;
            float x0 = 120, x1 = r.width - 80, top = 56, bot = r.height - 14, hour = city.hour;
            // a soft shade so the band reads over the city
            p.fillColor = new Color(0.024f, 0.055f, 0.118f, 0.32f); p.BeginPath(); p.MoveTo(new Vector2(0, 0)); p.LineTo(new Vector2(r.width, 0)); p.LineTo(new Vector2(r.width, r.height)); p.LineTo(new Vector2(0, r.height)); p.ClosePath(); p.Fill();
            float rh = (bot - top) / Pulse.Length;
            for (int i = 0; i < Pulse.Length; i++)
            {
                var (n, c, f) = Pulse[i];
                float yb = top + (i + 1) * rh - 6;
                p.strokeColor = new Color(0.2f, 0.79f, 0.91f, 0.12f); p.lineWidth = 1; p.BeginPath(); p.MoveTo(new Vector2(x0, yb)); p.LineTo(new Vector2(x1, yb)); p.Stroke();
                foreach (bool past in new[] { true, false })
                {
                    p.strokeColor = new Color(c.r, c.g, c.b, past ? 0.95f : 0.25f); p.lineWidth = past ? 1.6f : 1f; p.BeginPath(); bool first = true;
                    for (int s = 0; s <= 240; s++)
                    {
                        float h = s / 10f; if (past ? h > hour : h < hour) continue;
                        float v = f(h) + Mathf.Sin(h * 13 + i) * 2.5f, x = Mathf.Lerp(x0, x1, h / 24), y = yb - (rh - 12) * Mathf.Min(1, v / 100);
                        if (first) { p.MoveTo(new Vector2(x, y)); first = false; } else p.LineTo(new Vector2(x, y));
                    }
                    p.Stroke();
                }
                float nv = f(hour), nx = Mathf.Lerp(x0, x1, hour / 24), ny = yb - (rh - 12) * Mathf.Min(1, nv / 100);
                p.fillColor = c; p.BeginPath(); p.Arc(new Vector2(nx, ny), 3, 0, 360); p.Fill();
            }
            float hx = Mathf.Lerp(x0, x1, hour / 24);
            p.strokeColor = new Color(0.9f, 0.96f, 1f, 0.35f); p.lineWidth = 1; p.BeginPath(); p.MoveTo(new Vector2(hx, top - 4)); p.LineTo(new Vector2(hx, bot)); p.Stroke();
        }
        void DrawMini(MeshGenerationContext ctx)
        {
            var el = (VisualElement)mini.userData; var r = el.contentRect; if (r.width <= 0 || city == null) return;
            var pr = city.PlanRect; float s = Mathf.Min(r.width / pr.width, r.height / pr.height), ox = (r.width - pr.width * s) / 2, oy = (r.height - pr.height * s) / 2;
            Vector2 M(float x, float z) => new Vector2(ox + x * s, oy + (pr.height - z) * s);
            var p = ctx.painter2D;
            foreach (var (lr, k) in city.LotRects())
            {
                var st = D(k);
                Color c = city.layer == "tension" ? ModelCity.TensionColor(st.tension) : new Color(0.55f, 0.65f, 0.78f);
                c.a = focus == k ? 1 : city.layer == "tension" ? 0.85f : 0.45f;
                var a = M(lr.xMin, lr.yMax); p.fillColor = c; p.BeginPath(); p.MoveTo(a); p.LineTo(a + new Vector2(lr.width * s, 0)); p.LineTo(a + new Vector2(lr.width * s, lr.height * s)); p.LineTo(a + new Vector2(0, lr.height * s)); p.ClosePath(); p.Fill();
            }
            // what the camera sees: the ground under the screen's corners (the top edge a little below the horizon)
            var pts = new List<Vector2>();
            var pr2 = city.Cam.pixelRect;
            foreach (var sp in new[] { new Vector2(pr2.xMin, pr2.yMin + pr2.height * 0.62f), new Vector2(pr2.xMax, pr2.yMin + pr2.height * 0.62f), new Vector2(pr2.xMax, pr2.yMin), new Vector2(pr2.xMin, pr2.yMin) })
                if (city.GroundAt(sp, out var g)) pts.Add(M(Mathf.Clamp(g.x, -40, pr.width + 40), Mathf.Clamp(g.z, -40, pr.height + 40)));
            if (pts.Count == 4) { p.strokeColor = new Color32(76, 141, 255, 255); p.lineWidth = 2; p.BeginPath(); p.MoveTo(pts[0]); for (int i = 1; i < 4; i++) p.LineTo(pts[i]); p.ClosePath(); p.Stroke(); }
        }
        void MiniClick(VisualElement el, Vector2 local)
        {
            var r = el.contentRect; var pr = city.PlanRect; float s = Mathf.Min(r.width / pr.width, r.height / pr.height), ox = (r.width - pr.width * s) / 2, oy = (r.height - pr.height * s) / 2;
            var g = new Vector3((local.x - ox) / s, 0, pr.height - (local.y - oy) / s);
            var k = city.DistrictAt(g); if (k != null) { Pick(k); city.FlyTo(k); }
        }
    }
}
