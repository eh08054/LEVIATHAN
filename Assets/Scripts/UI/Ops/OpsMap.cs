using System;
using System.Collections.Generic;
using System.Linq;
using Sector.Ops;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sector.UI
{
    // The city painting as a working map: district outlines on hover, a district card on click, information layers
    // (tension, satisfaction, population, energy), anomaly and crisis badges at the districts, facility marks when
    // zoomed in, and a full-map mode that folds the side panels away. Quiet by default: details appear on demand.
    public partial class OpsScreen
    {
        // District outlines on the painting (image fractions), traced from Tools/Art/painted/ui/city_main_v5.png
        // (laid out from district_layout_v2.png). Society.Zones order.
        internal static Vector2[][] ZoneOutlines => ZoneShapes;

        static readonly Vector2[][] ZoneShapes =
        {
            new[] { V(0.44f, 0.45f), V(0.60f, 0.45f), V(0.61f, 0.62f), V(0.43f, 0.62f) },   // 0 문화·여가
            new[] { V(0.12f, 0.40f), V(0.36f, 0.40f), V(0.37f, 0.57f), V(0.10f, 0.58f) },   // 1 상업
            new[] { V(0.47f, 0.20f), V(0.56f, 0.20f), V(0.58f, 0.43f), V(0.44f, 0.43f) },   // 2 관리
            new[] { V(0.12f, 0.62f), V(0.40f, 0.62f), V(0.43f, 0.86f), V(0.10f, 0.86f) },   // 3 주거
            new[] { V(0.67f, 0.67f), V(0.94f, 0.67f), V(0.95f, 0.87f), V(0.66f, 0.87f) },   // 4 교육
            new[] { V(0.07f, 0.24f), V(0.28f, 0.24f), V(0.28f, 0.37f), V(0.03f, 0.37f) },   // 5 물류·산업
            new[] { V(0.10f, 0.05f), V(0.30f, 0.05f), V(0.31f, 0.21f), V(0.08f, 0.21f) },   // 6 에너지·발전
            new[] { V(0.47f, 0.03f), V(0.64f, 0.03f), V(0.65f, 0.19f), V(0.46f, 0.19f) },   // 7 연구·개발
            new[] { V(0.69f, 0.18f), V(0.86f, 0.18f), V(0.88f, 0.34f), V(0.67f, 0.34f) },   // 8 의료·생명
            new[] { V(0.69f, 0.40f), V(0.97f, 0.40f), V(0.98f, 0.60f), V(0.68f, 0.60f) },   // 9 치안·보안
            new[] { V(0.77f, 0.05f), V(0.94f, 0.05f), V(0.95f, 0.18f), V(0.76f, 0.18f) },   // 10 유전 설계
        };

        static Vector2 V(float x, float y) => new Vector2(x, y);

        public enum MapLayer { Map, Tension, Satisfaction, Population, Energy }

        static readonly string[] LayerNames = { "지도", "긴장", "만족", "인구", "에너지" };

        [NonSerialized] MapLayer mapLayer;
        [NonSerialized] int hoverZone = -1;
        [NonSerialized] int selectedZone = -1;
        [NonSerialized] bool mapMode;
        VisualElement mapCanvas, mapTip, zoneCard, layerBar;
        Button mapButton;
        readonly List<VisualElement> zoneBadges = new List<VisualElement>();
        Label mapTipTitle, mapTipText;

        static bool Inside(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        // Which district is under a screen position (world coordinates), or -1.
        int ZoneAt(Vector2 world)
        {
            var r = cityView.worldBound;
            if (r.width <= 0f)
                return -1;
            var f = new Vector2((world.x - r.x) / r.width, (world.y - r.y) / r.height);
            for (int z = 0; z < ZoneShapes.Length; z++)
                if (Inside(ZoneShapes[z], f))
                    return z;
            return -1;
        }

        void BuildMap(VisualElement window)
        {
            mapCanvas = new VisualElement { pickingMode = PickingMode.Ignore };
            mapCanvas.AddToClassList("map-canvas");
            mapCanvas.generateVisualContent += DrawMap;
            zoneLayer.Insert(0, mapCanvas);

            layerBar = Add(window, "map-layers");
            for (int i = 0; i < LayerNames.Length; i++)
            {
                var layer = (MapLayer)i;
                var b = MakeButton(layerBar, LayerNames[i], () =>
                {
                    mapLayer = layer;
                    sfx.Play(Cue.Click);
                    RefreshMapControls();
                }, "map-layer");
                b.userData = layer;
            }
            mapButton = MakeButton(window, "전체 지도", () => SetMapMode(!mapMode), "map-mode");
            mapLegend = Text(window, "", "map-legend");
            mapLegend.pickingMode = PickingMode.Ignore;

            mapTip = Add(root, "map-tip");
            mapTip.pickingMode = PickingMode.Ignore;
            mapTipTitle = Text(mapTip, "", "map-tip-title");
            mapTipText = Text(mapTip, "", "map-tip-text");
            mapTip.style.display = DisplayStyle.None;

            for (int z = 0; z < ZoneShapes.Length; z++)
            {
                int zone = z;
                var badge = Add(zoneLayer, "zone-badge");
                badge.RegisterCallback<ClickEvent>(e =>
                {
                    var first = session.Anomalies.Where(a => a.Zone == zone).OrderByDescending(a => a.Kind).FirstOrDefault();
                    if (first != null)
                    {
                        sfx.Play(Cue.Select);
                        OpenAnomaly(first);
                    }
                    e.StopPropagation();
                });
                Text(badge, "", "zone-badge-text").pickingMode = PickingMode.Ignore;
                badge.style.display = DisplayStyle.None;
                zoneBadges.Add(badge);
            }
            crisisBadge = Add(zoneLayer, "crisis-badge");
            crisisBadge.pickingMode = PickingMode.Ignore;
            crisisBadgeText = Text(crisisBadge, "", "crisis-badge-text");
            crisisBadge.style.display = DisplayStyle.None;

            cityView.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (dragging || currentPage != Page.Overview)
                    return;
                int z = ZoneAt(e.position);
                if (z != hoverZone)
                {
                    hoverZone = z;
                    mapCanvas.MarkDirtyRepaint();
                }
                ShowMapTip(z, e.position);
            });
            cityView.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                hoverZone = -1;
                mapTip.style.display = DisplayStyle.None;
            });
            RefreshMapControls();
        }

        Label mapLegend;
        VisualElement crisisBadge;
        Label crisisBadgeText;

        void RefreshMapControls()
        {
            foreach (var child in layerBar.Children())
                child.EnableInClassList("map-layer--on", child.userData is MapLayer l && l == mapLayer);
            mapLegend.text = mapLayer == MapLayer.Tension ? "긴장: 파랑 잠잠 · 주황 수군거림 · 빨강 모임 이상"
                : mapLayer == MapLayer.Satisfaction ? "만족: 빨강 낮음 · 파랑 보통 · 초록 높음"
                : mapLayer == MapLayer.Population ? "인구: 진할수록 많다"
                : mapLayer == MapLayer.Energy ? "에너지: 시설이 있는 지구와 발전에서 흐르는 선"
                : "";
            mapLegend.style.display = mapLayer == MapLayer.Map ? DisplayStyle.None : DisplayStyle.Flex;
            mapCanvas?.MarkDirtyRepaint();
        }

        void SetMapMode(bool on)
        {
            mapMode = on;
            sfx.Play(Cue.Click);
            overviewPage.EnableInClassList("overview--map", on);
            mapButton.text = on ? "패널 보기" : "전체 지도";
            if (!on)
            {
                zoomTarget = 1f;
                panTarget = Vector2.zero;
            }
            else
                zoomTarget = 1f;
        }

        void ShowMapTip(int zone, Vector2 world)
        {
            if (zone < 0 || overlayOpen)
            {
                mapTip.style.display = DisplayStyle.None;
                return;
            }
            var def = Society.Zones[zone];
            mapTipTitle.text = def.Name;
            if (!session.ZoneOpen(zone))
                mapTipText.text = "공사 부지 · 유전 상담 연구 후 열림";
            else
            {
                int stage = session.TensionStage(zone);
                int seen = session.Anomalies.Count(a => a.Zone == zone);
                int built = session.BuiltZone.Count(z => z == zone);
                mapTipText.text = $"{Tension.StageNames[stage]} · 긴장 {session.ZoneTension[zone]:0}" +
                                  (seen > 0 ? $" · 이상 {seen}" : "") + $" · 시설 {built}/{session.ZoneSlots}\n눌러서 지구 정보";
            }
            var local = root.WorldToLocal(world);
            mapTip.style.left = local.x + 16f;
            mapTip.style.top = local.y + 16f;
            mapTip.style.display = DisplayStyle.Flex;
        }

        // A click (not a drag) on the painting: open that district's card.
        void OnMapClick(Vector2 world)
        {
            int z = ZoneAt(world);
            if (z < 0)
            {
                CloseZoneCard();
                return;
            }
            sfx.Play(Cue.Select);
            OpenZoneCard(z);
        }

        // ------------------------------------------------------------------ district card

        void CloseZoneCard()
        {
            selectedZone = -1;
            zoneCard?.RemoveFromHierarchy();
            zoneCard = null;
            mapCanvas?.MarkDirtyRepaint();
        }

        void OpenZoneCard(int zone)
        {
            CloseZoneCard();
            selectedZone = zone;
            mapCanvas.MarkDirtyRepaint();
            var def = Society.Zones[zone];
            zoneCard = Add(root, "zone-card glass");
            var head = Add(zoneCard, "zone-card-head");
            Text(head, def.Name, "zone-card-title");
            MakeButton(head, "×", CloseZoneCard, "nav-button");
            Text(zoneCard, def.Kind, "detail-kicker");
            if (!session.ZoneOpen(zone))
            {
                Text(zoneCard, def.Trait, "detail-text");
                return;
            }
            var stageRow = Add(zoneCard, "zone-detail-head");
            var stage = Text(stageRow, "", "state-tag");
            SetStage(stage, zone);
            Text(stageRow, $"긴장 {session.ZoneTension[zone]:0}", "zone-detail-value num");
            DeltaLabel(stageRow, -session.TensionTrend(zone), "delta", unit: "/주");
            var bar = Add(zoneCard, "mini-bar mini-bar--wide gauge-bar");
            var fill = Add(bar, "mini-fill");
            fill.style.width = Length.Percent(session.ZoneTension[zone]);
            fill.style.backgroundColor = StageColor(session.TensionStage(zone));

            Text(zoneCard, "주된 원인", "section-title");
            foreach (var (name, value) in session.TensionCauses(zone).Take(3))
            {
                var row = Add(zoneCard, "cause-row");
                Text(row, name, "cause-name");
                Text(row, $"{(value > 0 ? "+" : "")}{value * def.Spread:0.0}", "cause-value num " + (value > 0 ? "tone--bad" : "tone-good"));
            }
            var here = session.Anomalies.Where(a => a.Zone == zone).ToList();
            if (here.Count > 0)
            {
                Text(zoneCard, $"이상 징후 {here.Count}건", "section-title");
                foreach (var a in here.Take(2))
                {
                    var item = Add(zoneCard, "anomaly-item anomaly-item--" + a.Kind.ToString().ToLowerInvariant());
                    Text(item, $"{Tension.KindName(a.Kind)} · {a.Text}", "anomaly-text");
                    var anomaly = a;
                    item.RegisterCallback<ClickEvent>(_ => OpenAnomaly(anomaly));
                }
            }
            var built = Enumerable.Range(0, session.Built.Count).Where(i => session.BuiltZone[i] == zone)
                .Select(i => Facilities.Get(session.Built[i]).Name).ToList();
            Text(zoneCard, $"시설 {built.Count}/{session.ZoneSlots}" + (built.Count > 0 ? " · " + string.Join(", ", built) : ""), "ops-note");
            var actions = Add(zoneCard, "ops-row");
            MakeButton(actions, "카메라", () =>
            {
                sim.ShowZone(zone);
                subPage[Page.Watch] = 0;
                CloseZoneCard();
                GoTo(Page.Watch);
            }, "ops-small");
            MakeButton(actions, "여기에 건설", () =>
            {
                buildZone = zone;
                CloseZoneCard();
                GoTo(Page.Facility);
            }, "ops-small");
        }

        // ------------------------------------------------------------------ per frame: badges and visibility

        void TickMap()
        {
            if (mapCanvas == null)
                return;
            var window = zoneLayer.worldBound;
            for (int z = 0; z < ZoneShapes.Length && z < markers.Count; z++)
            {
                var m = markers[z];
                // Outside the clear window (behind the side panels) markers stay hidden unless the full map is open.
                var world = zoneLayer.LocalToWorld(m.Anchor);
                bool visible = mapMode || window.Contains(world);
                m.Tag.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                int seen = session.Anomalies.Count(a => a.Zone == z);
                var badge = zoneBadges[z];
                badge.style.display = visible && seen > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                if (seen > 0)
                {
                    bool spark = session.SparkIn(z) != null;
                    badge.EnableInClassList("zone-badge--spark", spark);
                    ((Label)badge[0]).text = spark ? "!" : $"{seen}";
                    badge.style.left = m.Anchor.x + 10f;
                    badge.style.top = m.Anchor.y - 10f;
                    badge.style.scale = new Scale(Vector3.one * (1f + 0.12f * Mathf.Sin(cityTime * 4f)));
                }
            }
            // A crisis three weeks away shows a countdown where it will start.
            var crisis = session.NextCrisis;
            int days = crisis == null ? int.MaxValue : session.CrisisDay(crisis) - session.Day;
            if (crisis != null && days <= 21)
            {
                var at = crisis.Zone >= 0 && crisis.Zone < markers.Count ? markers[crisis.Zone].Anchor : PinAt(CenterPin);
                crisisBadge.style.display = DisplayStyle.Flex;
                crisisBadgeText.text = $"{crisis.Title} D-{Math.Max(0, days)}";
                crisisBadge.style.left = at.x;
                crisisBadge.style.top = at.y + 12f;
            }
            else
                crisisBadge.style.display = DisplayStyle.None;
            mapCanvas.MarkDirtyRepaint();
        }

        // ------------------------------------------------------------------ drawing: outlines, layers, facilities

        Color LayerColor(int zone)
        {
            switch (mapLayer)
            {
                case MapLayer.Tension:
                    return StageColor(session.TensionStage(zone));
                case MapLayer.Satisfaction:
                    float sat = session.ZoneSatisfaction(zone);
                    return sat < 30f ? Bad : sat < 45f ? Caution : sat < 60f ? Palette.Cyan : Good;
                case MapLayer.Population:
                    return new Color(0.35f, 0.6f, 1f);
                default:
                    return session.BuiltZone.Any(z => z == zone) ? Good : new Color(0.5f, 0.55f, 0.65f);
            }
        }

        // Light washes: calm districts stay almost clear so only what matters stands out.
        float LayerAlpha(int zone)
        {
            switch (mapLayer)
            {
                case MapLayer.Population:
                    return Mathf.Clamp01(session.ZonePop[zone] / 5000f) * 0.3f + 0.04f;
                case MapLayer.Energy:
                    return session.BuiltZone.Any(z => z == zone) ? 0.2f : 0.03f;
                case MapLayer.Tension:
                    int stage = session.TensionStage(zone);
                    return stage >= 3 ? 0.24f : stage == 2 ? 0.16f : 0.04f;
                default:
                    float sat = session.ZoneSatisfaction(zone);
                    return sat < 45f || sat >= 60f ? 0.18f : 0.05f;
            }
        }

        void DrawMap(MeshGenerationContext ctx)
        {
            if (session == null || cityView == null)
                return;
            var p = ctx.painter2D;
            void Poly(int zone)
            {
                var shape = ZoneShapes[zone];
                p.BeginPath();
                p.MoveTo(PinAt(shape[0]));
                for (int i = 1; i < shape.Length; i++)
                    p.LineTo(PinAt(shape[i]));
                p.ClosePath();
            }
            for (int z = 0; z < ZoneShapes.Length; z++)
            {
                bool open = session.ZoneOpen(z);
                if (mapLayer != MapLayer.Map && open)
                {
                    var c = LayerColor(z);
                    p.fillColor = new Color(c.r, c.g, c.b, LayerAlpha(z));
                    Poly(z);
                    p.Fill();
                }
                bool hot = z == hoverZone || z == selectedZone;
                if (hot || mapLayer != MapLayer.Map)
                {
                    var c = !open ? new Color(0.6f, 0.62f, 0.68f) : hot ? Palette.Cyan : LayerColor(z);
                    p.strokeColor = new Color(c.r, c.g, c.b, hot ? 0.95f : 0.3f);
                    p.lineWidth = hot ? 2.5f : 1f;
                    Poly(z);
                    p.Stroke();
                }
            }
            // Energy layer: power flows from the plants' districts to every district with facilities.
            if (mapLayer == MapLayer.Energy && markers.Count > 6)
            {
                var sources = Enumerable.Range(0, session.Built.Count).Where(i => session.Built[i] == "plant")
                    .Select(i => session.BuiltZone[i]).Distinct().DefaultIfEmpty(6).ToList();
                foreach (var s in sources)
                    for (int z = 0; z < markers.Count; z++)
                        if (z != s && session.ZoneOpen(z) && session.BuiltZone.Any(b => b == z))
                            Dashes(p, markers[s].Anchor, markers[z].Anchor, new Color(1f, 0.65f, 0.25f, 0.75f), cityTime * 50f);
            }
            // Facilities as small marks once zoomed in (or on the full map).
            if (zoom > 1.3f || mapMode)
            {
                for (int i = 0; i < session.Built.Count; i++)
                {
                    int z = session.BuiltZone[i];
                    if (z >= markers.Count)
                        continue;
                    int k = Enumerable.Range(0, i).Count(j => session.BuiltZone[j] == z);
                    var at = markers[z].Anchor + new Vector2(-12f + k * 12f, 16f);
                    p.fillColor = new Color(0.4f, 0.9f, 0.7f, 0.9f);
                    p.BeginPath();
                    p.MoveTo(at + new Vector2(0f, -5f));
                    p.LineTo(at + new Vector2(5f, 0f));
                    p.LineTo(at + new Vector2(0f, 5f));
                    p.LineTo(at + new Vector2(-5f, 0f));
                    p.ClosePath();
                    p.Fill();
                }
            }
        }
    }
}
