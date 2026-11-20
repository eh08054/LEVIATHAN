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
        // District outlines on the painting (image fractions), the ground edge of each district's platform on
        // Tools/Art/painted/ui/city_main_v7.png, rounded (corners kept in Tools/Art/district_layers.py). Society.Zones order.
        internal static Vector2[][] ZoneOutlines => ZoneShapes;

        static readonly Vector2[][] ZoneShapes =
        {
            new[] { V(0.4344f, 0.489f), V(0.4242f, 0.5019f), V(0.4167f, 0.5175f), V(0.4128f, 0.5365f), V(0.4161f, 0.556f), V(0.4291f, 0.5706f), V(0.4548f, 0.5799f), V(0.5092f, 0.5828f), V(0.5636f, 0.5799f), V(0.5896f, 0.5706f), V(0.6026f, 0.556f), V(0.6056f, 0.5365f), V(0.6013f, 0.5175f), V(0.5942f, 0.5019f), V(0.5844f, 0.489f), V(0.5758f, 0.4731f), V(0.5636f, 0.4585f), V(0.5474f, 0.4487f), V(0.5092f, 0.4444f), V(0.471f, 0.4487f), V(0.4548f, 0.4585f), V(0.4426f, 0.4731f) },   // 0 문화·여가
            new[] { V(0.2089f, 0.4597f), V(0.1944f, 0.4706f), V(0.1889f, 0.4788f), V(0.1817f, 0.4869f), V(0.1813f, 0.4964f), V(0.1871f, 0.5032f), V(0.2306f, 0.5236f), V(0.2669f, 0.544f), V(0.2941f, 0.5603f), V(0.3031f, 0.5617f), V(0.314f, 0.5549f), V(0.3575f, 0.5304f), V(0.3938f, 0.5059f), V(0.4101f, 0.4842f), V(0.4173f, 0.4624f), V(0.4137f, 0.4516f), V(0.4083f, 0.4434f), V(0.3992f, 0.4339f), V(0.2955f, 0.3971f) },   // 1 상업
            new[] { V(0.5669f, 0.4096f), V(0.5664f, 0.4136f), V(0.5648f, 0.4175f), V(0.5622f, 0.4212f), V(0.5586f, 0.4248f), V(0.5541f, 0.4281f), V(0.5488f, 0.4311f), V(0.5428f, 0.4337f), V(0.5361f, 0.4359f), V(0.5288f, 0.4377f), V(0.5212f, 0.439f), V(0.5133f, 0.4397f), V(0.5052f, 0.44f), V(0.4971f, 0.4397f), V(0.4892f, 0.439f), V(0.4816f, 0.4377f), V(0.4743f, 0.4359f), V(0.4676f, 0.4337f), V(0.4616f, 0.4311f), V(0.4563f, 0.4281f), V(0.4518f, 0.4248f), V(0.4482f, 0.4212f), V(0.4456f, 0.4175f), V(0.444f, 0.4136f), V(0.4435f, 0.4096f), V(0.444f, 0.4056f), V(0.4456f, 0.4017f), V(0.4482f, 0.398f), V(0.4518f, 0.3944f), V(0.4563f, 0.3911f), V(0.4616f, 0.3881f), V(0.4676f, 0.3855f), V(0.4743f, 0.3833f), V(0.4816f, 0.3815f), V(0.4892f, 0.3802f), V(0.4971f, 0.3795f), V(0.5052f, 0.3792f), V(0.5133f, 0.3795f), V(0.5212f, 0.3802f), V(0.5288f, 0.3815f), V(0.5361f, 0.3833f), V(0.5428f, 0.3855f), V(0.5488f, 0.3881f), V(0.5541f, 0.3911f), V(0.5586f, 0.3944f), V(0.5622f, 0.398f), V(0.5648f, 0.4017f), V(0.5664f, 0.4056f) },   // 2 관리
            new[] { V(0.2105f, 0.6468f), V(0.1995f, 0.6614f), V(0.1892f, 0.6782f), V(0.1817f, 0.6871f), V(0.1832f, 0.6975f), V(0.2173f, 0.7228f), V(0.2481f, 0.7444f), V(0.2782f, 0.7647f), V(0.2971f, 0.7778f), V(0.3044f, 0.7848f), V(0.3154f, 0.7875f), V(0.3246f, 0.7829f), V(0.3427f, 0.7715f), V(0.3719f, 0.7563f), V(0.4026f, 0.7388f), V(0.4334f, 0.7205f), V(0.4437f, 0.7095f), V(0.4469f, 0.6976f), V(0.4418f, 0.6887f), V(0.4336f, 0.6794f), V(0.4173f, 0.6693f), V(0.3132f, 0.5972f) },   // 3 주거
            new[] { V(0.6101f, 0.6622f), V(0.5931f, 0.6765f), V(0.5772f, 0.6917f), V(0.5698f, 0.6997f), V(0.5708f, 0.7099f), V(0.5783f, 0.715f), V(0.625f, 0.7427f), V(0.6675f, 0.7673f), V(0.7058f, 0.7896f), V(0.7143f, 0.7947f), V(0.7249f, 0.7937f), V(0.7738f, 0.7682f), V(0.8163f, 0.7437f), V(0.845f, 0.7214f), V(0.8493f, 0.7109f), V(0.844f, 0.7013f), V(0.8355f, 0.6893f), V(0.8184f, 0.6765f), V(0.7048f, 0.6159f) },   // 4 교육
            new[] { V(0.1798f, 0.3212f), V(0.1635f, 0.3288f), V(0.1454f, 0.3402f), V(0.1341f, 0.3524f), V(0.1363f, 0.3611f), V(0.1834f, 0.3883f), V(0.2288f, 0.415f), V(0.2378f, 0.4204f), V(0.2487f, 0.4171f), V(0.3013f, 0.3959f), V(0.3466f, 0.3796f), V(0.3838f, 0.366f), V(0.391f, 0.3601f), V(0.3883f, 0.353f), V(0.3792f, 0.3452f), V(0.3647f, 0.3365f), V(0.2873f, 0.2921f) },   // 5 물류·산업
            new[] { V(0.225f, 0.2224f), V(0.216f, 0.2269f), V(0.21f, 0.2325f), V(0.2025f, 0.2392f), V(0.2032f, 0.246f), V(0.225f, 0.2577f), V(0.255f, 0.2703f), V(0.282f, 0.2815f), V(0.291f, 0.2838f), V(0.3f, 0.2808f), V(0.33f, 0.2712f), V(0.36f, 0.2577f), V(0.375f, 0.2494f), V(0.3765f, 0.2433f), V(0.3727f, 0.2381f), V(0.3675f, 0.2302f), V(0.36f, 0.2224f), V(0.288f, 0.1987f) },   // 6 에너지·발전
            new[] { V(0.4654f, 0.2121f), V(0.4544f, 0.2172f), V(0.4461f, 0.2224f), V(0.4406f, 0.2271f), V(0.442f, 0.2327f), V(0.4681f, 0.243f), V(0.497f, 0.2543f), V(0.5272f, 0.2677f), V(0.5382f, 0.2698f), V(0.552f, 0.2657f), V(0.585f, 0.2523f), V(0.618f, 0.2378f), V(0.6276f, 0.2327f), V(0.6262f, 0.2275f), V(0.6207f, 0.2244f), V(0.6125f, 0.2193f), V(0.6029f, 0.2141f), V(0.53f, 0.19f) },   // 7 연구·개발
            new[] { V(0.6243f, 0.3156f), V(0.612f, 0.3219f), V(0.6006f, 0.3306f), V(0.5919f, 0.3397f), V(0.591f, 0.3476f), V(0.598f, 0.3534f), V(0.64f, 0.3744f), V(0.6925f, 0.3962f), V(0.7083f, 0.4041f), V(0.7205f, 0.4054f), V(0.7363f, 0.3975f), V(0.7713f, 0.3791f), V(0.8028f, 0.3613f), V(0.8124f, 0.3516f), V(0.8124f, 0.3437f), V(0.8063f, 0.3371f), V(0.7975f, 0.3287f), V(0.7844f, 0.3201f), V(0.6829f, 0.2938f) },   // 8 의료·생명
            new[] { V(0.604f, 0.4585f), V(0.6f, 0.4765f), V(0.607f, 0.49f), V(0.618f, 0.5035f), V(0.652f, 0.541f), V(0.686f, 0.5815f), V(0.695f, 0.5875f), V(0.76f, 0.5815f), V(0.8201f, 0.5734f), V(0.8441f, 0.568f), V(0.8491f, 0.562f), V(0.8471f, 0.5485f), V(0.8421f, 0.5365f), V(0.7541f, 0.451f) },   // 9 치안·보안
            new[] { V(0.7041f, 0.2027f), V(0.6949f, 0.2076f), V(0.6897f, 0.2125f), V(0.6864f, 0.2204f), V(0.6884f, 0.2332f), V(0.7225f, 0.2509f), V(0.7697f, 0.2731f), V(0.775f, 0.2725f), V(0.8393f, 0.246f), V(0.8406f, 0.2381f), V(0.8393f, 0.2302f), V(0.834f, 0.2223f), V(0.8275f, 0.2145f), V(0.7573f, 0.1854f) },   // 10 유전 설계
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
            foreach (var d in districtLayers)
                if (d.Hull.Length > 2 && Inside(d.Hull, f))
                    return d.Zone;
            return -1;
        }

        // ------------------------------------------------------------------ district layers
        // Each district is its own set of layers over the one city painting (Resources/Map, made by
        // Tools/Art/district_layers.py): its boundary glow (white, tinted here), the district cut out of the painting
        // (raised above the darkening on select) and a lit copy (faded in on hover). Placed in image fractions inside
        // cityView, so they pan and zoom with the painting.

        sealed class DistrictLayer
        {
            public int Zone;
            public VisualElement Edge, Cut, Lit;
            public Vector2[] Hull = new Vector2[0];
            public float Hover;   // 0..1, eased
        }

        readonly List<DistrictLayer> districtLayers = new List<DistrictLayer>();
        VisualElement districtDim;
        float dimLevel;

        void BuildDistrictLayers()
        {
            var index = Resources.Load<TextAsset>("Map/layers");
            if (index == null)
                return;
            var host = new VisualElement { pickingMode = PickingMode.Ignore };
            host.AddToClassList("district-layers");
            cityView.Add(host);
            districtDim = new VisualElement { pickingMode = PickingMode.Ignore };
            districtDim.AddToClassList("district-dim");
            host.Add(districtDim);
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var raw in index.text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0)
                    continue;
                var halves = line.Split(';');
                var head = halves[0].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                int zone = int.Parse(head[0], inv);
                float x0 = float.Parse(head[1], inv), y0 = float.Parse(head[2], inv);
                float x1 = float.Parse(head[3], inv), y1 = float.Parse(head[4], inv);
                var nums = halves.Length > 1 ? halves[1].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries) : new string[0];
                var hull = new Vector2[nums.Length / 2];
                for (int k = 0; k < hull.Length; k++)
                    hull[k] = new Vector2(float.Parse(nums[2 * k], inv), float.Parse(nums[2 * k + 1], inv));
                VisualElement Layer(string name)
                {
                    var tex = Resources.Load<Texture2D>($"Map/z{zone}_{name}");
                    var e = new VisualElement { pickingMode = PickingMode.Ignore };
                    e.AddToClassList("district-layer");
                    if (tex != null)
                        e.style.backgroundImage = new StyleBackground(tex);
                    e.style.left = Length.Percent(x0 * 100f);
                    e.style.top = Length.Percent(y0 * 100f);
                    e.style.width = Length.Percent((x1 - x0) * 100f);
                    e.style.height = Length.Percent((y1 - y0) * 100f);
                    host.Add(e);
                    return e;
                }
                var d = new DistrictLayer { Zone = zone, Hull = hull, Cut = Layer("cut"), Lit = Layer("lit"), Edge = Layer("edge") };
                d.Cut.style.display = DisplayStyle.None;
                d.Lit.style.opacity = 0f;
                districtLayers.Add(d);
            }
        }

        // Per frame: ease hover and the darkening, tint the cut-outs like the painting (time of day).
        void TickDistrictLayers(float dt, Color paintTint)
        {
            if (districtLayers.Count == 0)
                return;
            bool mapView = mapLayer == MapLayer.Map && currentPage == Page.Overview;
            float k = Mathf.Min(1f, dt * 9f);
            dimLevel = Mathf.Lerp(dimLevel, mapView && selectedZone >= 0 ? 1f : 0f, k);
            districtDim.style.opacity = dimLevel;
            districtDim.style.display = dimLevel > 0.01f ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (var d in districtLayers)
            {
                bool selected = d.Zone == selectedZone;
                bool hovered = d.Zone == hoverZone;
                d.Hover = Mathf.Lerp(d.Hover, mapView && (hovered || selected) ? 1f : 0f, k);
                d.Lit.style.opacity = d.Hover * (selected ? 0.55f : 0.85f);
                d.Lit.style.display = d.Hover > 0.01f ? DisplayStyle.Flex : DisplayStyle.None;
                d.Lit.style.unityBackgroundImageTintColor = Color.Lerp(paintTint, Color.white, 0.5f);
                d.Cut.style.display = selected && mapView ? DisplayStyle.Flex : DisplayStyle.None;
                d.Cut.style.unityBackgroundImageTintColor = paintTint;
                bool open = session.ZoneOpen(d.Zone);
                var c = ZoneTints[d.Zone];
                float rest = open ? 0.55f : 0.3f;
                // behind the side panels (pin outside the clear window) the glow would shine through the glass: keep it faint
                if (d.Zone < markers.Count && !markers[d.Zone].InView && !mapMode)
                    rest *= 0.25f;
                // others fade back while one district is chosen
                float alpha = Mathf.Lerp(rest, 1f, d.Hover) * (selected ? 1f : 1f - 0.7f * dimLevel);
                d.Edge.style.display = mapView ? DisplayStyle.Flex : DisplayStyle.None;
                d.Edge.style.unityBackgroundImageTintColor = new Color(
                    Mathf.Lerp(c.r, 1f, d.Hover * 0.35f), Mathf.Lerp(c.g, 1f, d.Hover * 0.35f), Mathf.Lerp(c.b, 1f, d.Hover * 0.35f), alpha);
            }
        }

        void BuildMap(VisualElement window)
        {
            mapCanvas = new VisualElement { pickingMode = PickingMode.Ignore };
            mapCanvas.AddToClassList("map-canvas");
            mapCanvas.generateVisualContent += DrawMap;
            zoneLayer.Insert(0, mapCanvas);
            BuildDistrictLayers();

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
            ResetView();
        }

        // The view a reset returns to: the full map pulls back far enough to show every district.
        float HomeFor => mapMode ? MinZoom : HomeZoom;

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
                var crisis = session.NextCrisis;
                int days = crisis == null ? int.MaxValue : session.CrisisDay(crisis) - session.Day;
                string extra = (seen > 0 ? $"\n이상 징후 {seen}건" + (session.SparkIn(zone) != null ? " · 계기 임박" : "") : "") +
                               (crisis != null && crisis.Zone == zone && days <= 21 ? $"\n{crisis.Title} D-{Math.Max(0, days)}" : "");
                mapTipText.text = $"{Tension.StageNames[stage]} · 긴장 {session.ZoneTension[zone]:0} · 시설 {built}/{session.ZoneSlots}" +
                                  extra + "\n눌러서 지구 정보";
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

        string TroubleText(int zone, int crisisZone, int crisisDays)
        {
            int stage = session.TensionStage(zone);
            int seen = session.Anomalies.Count(a => a.Zone == zone);
            if (ripples.Any(r => r.Zone == zone && r.Color == Bad))
                return "사건 발생";
            if (session.SparkIn(zone) != null)
                return "계기";
            if (stage >= 2)
                return Tension.StageNames[stage];
            if (zone == crisisZone)
                return $"위기 D-{Math.Max(0, crisisDays)}";
            return $"이상 {seen}";
        }

        void BuildEdgePointers()
        {
            for (int z = 0; z < ZoneShapes.Length; z++)
            {
                int zone = z;
                var pointer = Add(zoneLayer, "edge-pointer");
                var arrow = Text(pointer, "▶", "edge-pointer-arrow");
                arrow.pickingMode = PickingMode.Ignore;
                Text(pointer, "", "edge-pointer-text").pickingMode = PickingMode.Ignore;
                pointer.RegisterCallback<ClickEvent>(e =>
                {
                    sfx.Play(Cue.Select);
                    CenterOn(zone);
                    OpenZoneCard(zone);
                    e.StopPropagation();
                });
                pointer.style.display = DisplayStyle.None;
                edgePointers.Add(pointer);
            }
        }

        readonly List<VisualElement> edgePointers = new List<VisualElement>();

        // Where a pointer to an off-window district sits: on the window's inner edge, along the line from its middle.
        Vector2 EdgeSpot(Vector2 target, List<Vector2> taken)
        {
            var r = zoneLayer.contentRect;
            float minX = 80f, maxX = r.width - 80f, minY = 60f, maxY = r.height - 40f;
            var c = r.center;
            var d = target - c;
            float tx = d.x > 0f ? (maxX - c.x) / d.x : d.x < 0f ? (minX - c.x) / d.x : float.MaxValue;
            float ty = d.y > 0f ? (maxY - c.y) / d.y : d.y < 0f ? (minY - c.y) / d.y : float.MaxValue;
            var at = c + d * Mathf.Clamp01(Mathf.Min(tx, ty));
            // Stay clear of the guide card in the window's lower right.
            if (guideCard != null && guideCard.resolvedStyle.display != DisplayStyle.None)
            {
                var g = zoneLayer.WorldToLocal(guideCard.worldBound);
                if (at.x > g.xMin - 80f && at.y > g.yMin - 16f)
                    at.y = g.yMin - 20f;
            }
            // Keep pointers on the same edge from piling up.
            for (int guard = 0; guard < 8 && taken.Any(t => Mathf.Abs(t.x - at.x) < 150f && Mathf.Abs(t.y - at.y) < 30f); guard++)
                at.y = at.y + 32f > maxY ? at.y - 32f : at.y + 32f;
            taken.Add(at);
            return at;
        }

        void TickMap()
        {
            if (mapCanvas == null)
                return;
            if (edgePointers.Count == 0)
                BuildEdgePointers();
            var window = zoneLayer.worldBound;
            var crisisNext = session.NextCrisis;
            int crisisDays = crisisNext == null ? int.MaxValue : session.CrisisDay(crisisNext) - session.Day;
            int crisisZone = crisisNext != null && crisisDays <= 21 ? crisisNext.Zone : -1;
            var taken = new List<Vector2>();
            for (int z = 0; z < ZoneShapes.Length && z < markers.Count; z++)
            {
                var m = markers[z];
                // The districts read from their boundaries; pins, names and badges only on the full map.
                // Everywhere else the details come up on hover (ShowMapTip).
                var world = zoneLayer.LocalToWorld(m.Anchor);
                m.InView = window.Contains(world) &&
                           !(guideCard != null && guideCard.resolvedStyle.display != DisplayStyle.None && guideCard.worldBound.Contains(world));
                m.Shown = mapMode && m.InView;
                m.Tag.style.display = m.Shown ? DisplayStyle.Flex : DisplayStyle.None;
                // Pointers to districts out of sight only for what needs the AI now; 수군거림 waits until it is in view.
                bool severe = session.ZoneOpen(z) && (z == crisisZone || session.TensionStage(z) >= 3 || session.SparkIn(z) != null ||
                                                      ripples.Any(r => r.Zone == z && r.Color == Bad));
                var pointer = edgePointers[z];
                if (!m.InView && severe && currentPage == Page.Overview)
                {
                    var spot = EdgeSpot(m.Anchor, taken);
                    var dir = m.Anchor - spot;
                    pointer.style.display = DisplayStyle.Flex;
                    pointer.style.left = spot.x;
                    pointer.style.top = spot.y;
                    pointer[0].style.rotate = new Rotate(Angle.Degrees(Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg));
                    ((Label)pointer[1]).text = $"{Society.Zones[z].Name} · {TroubleText(z, crisisZone, crisisDays)}";
                    pointer.EnableInClassList("edge-pointer--bad", z != crisisZone || session.TensionStage(z) >= 3);
                }
                else
                    pointer.style.display = DisplayStyle.None;
                int seen = session.Anomalies.Count(a => a.Zone == z);
                var badge = zoneBadges[z];
                badge.style.display = m.Shown && seen > 0 ? DisplayStyle.Flex : DisplayStyle.None;
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
            if (crisis != null && days <= 21 && mapMode)
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

        // Each district's boundary takes the colour its own lights have in the painting, softened toward white so the
        // set stays calm (Society.Zones order).
        static readonly Color[] ZoneTints =
        {
            new Color32(255, 214, 110, 255), // 0 문화·여가: warm yellow
            new Color32(255, 70, 220, 255),  // 1 상업: magenta
            new Color32(40, 225, 215, 255),  // 2 관리: teal
            new Color32(255, 232, 180, 255), // 3 주거: warm white / pale gold
            new Color32(170, 110, 255, 255), // 4 교육: violet
            new Color32(255, 190, 50, 255),  // 5 물류·산업: gold
            new Color32(255, 130, 40, 255),  // 6 에너지·발전: orange
            new Color32(110, 200, 255, 255), // 7 연구·개발: sky blue
            new Color32(110, 255, 200, 255), // 8 의료·생명: mint
            new Color32(60, 120, 255, 255),  // 9 치안·보안: blue
            new Color32(225, 230, 240, 255), // 10 유전 설계: silver white
        };

        static Color Alpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        void DrawMap(MeshGenerationContext ctx)
        {
            if (session == null || cityView == null)
                return;
            var p = ctx.painter2D;
            p.lineJoin = LineJoin.Round;
            void Poly(int zone)
            {
                var shape = ZoneShapes[zone];
                p.MoveTo(PinAt(shape[0]));
                for (int i = 1; i < shape.Length; i++)
                    p.LineTo(PinAt(shape[i]));
                p.ClosePath();
            }
            void Line(int zone, Color c, float width)
            {
                p.strokeColor = c;
                p.lineWidth = width;
                p.BeginPath();
                Poly(zone);
                p.Stroke();
            }
            void Area(int zone, Color c)
            {
                p.fillColor = c;
                p.BeginPath();
                Poly(zone);
                p.Fill();
            }

            if (mapLayer == MapLayer.Map && districtLayers.Count > 0)
            {
                // drawn by the district layers (TickDistrictLayers)
            }
            else if (mapLayer == MapLayer.Map)
            {
                // Select: the rest of the city sinks back (one dark sheet with the chosen district cut out).
                if (selectedZone >= 0)
                {
                    var r = zoneLayer.contentRect;
                    p.fillColor = new Color(0.02f, 0.04f, 0.09f, 0.55f);
                    p.BeginPath();
                    p.MoveTo(new Vector2(r.xMin - 4000f, r.yMin - 4000f));
                    p.LineTo(new Vector2(r.xMax + 4000f, r.yMin - 4000f));
                    p.LineTo(new Vector2(r.xMax + 4000f, r.yMax + 4000f));
                    p.LineTo(new Vector2(r.xMin - 4000f, r.yMax + 4000f));
                    p.ClosePath();
                    Poly(selectedZone);
                    p.Fill(FillRule.OddEven);
                }
                for (int z = 0; z < ZoneShapes.Length; z++)
                {
                    var c = ZoneTints[z];
                    bool open = session.ZoneOpen(z);
                    if (z == selectedZone)
                    {
                        Area(z, Alpha(c, 0.12f));
                        Line(z, Alpha(c, 0.18f), 14f);
                        Line(z, Alpha(c, 0.4f), 6f);
                        Line(z, Alpha(Color.Lerp(c, Color.white, 0.5f), 1f), 2f);
                    }
                    else if (z == hoverZone)
                    {
                        // Hover: the whole district lights up softly.
                        Area(z, Alpha(c, 0.2f));
                        Line(z, Alpha(c, 0.16f), 12f);
                        Line(z, Alpha(c, 0.35f), 5f);
                        Line(z, Alpha(Color.Lerp(c, Color.white, 0.4f), 0.95f), 1.8f);
                    }
                    else
                    {
                        // At rest: a thin glowing edge in the district's own colour.
                        float k = open ? 1f : 0.5f;
                        Line(z, Alpha(c, 0.07f * k), 9f);
                        Line(z, Alpha(c, 0.16f * k), 4f);
                        Line(z, Alpha(c, 0.6f * k), 1.3f);
                    }
                }
            }
            else
            {
                for (int z = 0; z < ZoneShapes.Length; z++)
                {
                    bool open = session.ZoneOpen(z);
                    if (open)
                        Area(z, Alpha(LayerColor(z), LayerAlpha(z)));
                    bool hot = z == hoverZone || z == selectedZone;
                    var c = !open ? new Color(0.6f, 0.62f, 0.68f) : hot ? Palette.Cyan : LayerColor(z);
                    Line(z, Alpha(c, hot ? 0.95f : 0.3f), hot ? 2.5f : 1f);
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
