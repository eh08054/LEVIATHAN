using System;
using System.Collections.Generic;
using System.Linq;
using Sector.Ops;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sector.UI
{
    // The district screen (trial, residential only): the district as a 2.5D isometric grid where the player builds.
    // Opened from the district card ('지구 열기'), it replaces the overview inside the page host; time keeps running,
    // so construction can be watched. Placeholder graphics drawn in code (IsoGrid).
    public partial class OpsScreen
    {
        VisualElement districtPage, districtStats, districtInfo, districtFloat;
        IsoGrid isoGrid;
        int districtZone = -1;
        // Picked building id, "demolish", or null.
        string districtTool;
        readonly Dictionary<string, Button> toolButtons = new Dictionary<string, Button>();
        Label buildChip;
        // Where the picked building waits as a hologram for '건설 확정' (-1: follows the cursor).
        Vector2Int pendingSpot = new Vector2Int(-1, -1);
        Button confirmButton;
        Label ghostTag;

        static readonly Dictionary<string, Color> BuildingColors = new Dictionary<string, Color>
        {
            ["old_homes"] = new Color32(120, 112, 104, 255),
            ["apartment"] = new Color32(214, 190, 150, 255),
            ["park"] = new Color32(70, 150, 90, 255),
            ["shop"] = new Color32(214, 96, 170, 255),
            ["substation"] = new Color32(230, 170, 60, 255),
            ["tower"] = new Color32(90, 140, 230, 255),
            ["center"] = new Color32(80, 200, 190, 255),
        };

        void BuildDistrictPage()
        {
            districtPage = Add(pageHost, "district-page glass");
            districtPage.style.display = DisplayStyle.None;

            buildChip = Text(zoneLayer, "", "build-chip");
            buildChip.pickingMode = PickingMode.Ignore;
            buildChip.style.display = DisplayStyle.None;
        }

        bool DistrictOpen => districtZone >= 0 && districtPage != null && districtPage.resolvedStyle.display == DisplayStyle.Flex;

        void OpenDistrict(int zone)
        {
            if (session.PlanOf(zone) == null)
                return;
            CloseZoneCard();
            districtZone = zone;
            districtTool = null;
            overviewPage.style.display = DisplayStyle.None;
            pageView.style.display = DisplayStyle.None;
            dim.style.display = DisplayStyle.Flex;
            districtPage.style.display = DisplayStyle.Flex;
            sfx.Play(Cue.Select);
            RenderDistrict();
        }

        void CloseDistrict()
        {
            if (districtPage == null)
                return;
            districtPage.style.display = DisplayStyle.None;
            districtZone = -1;
            districtTool = null;
        }

        void RenderDistrict()
        {
            districtPage.Clear();
            toolButtons.Clear();
            var def = Society.Zones[districtZone];
            var head = Add(districtPage, "district-head");
            MakeButton(head, "‹ 도시 전체", () =>
            {
                CloseDistrict();
                GoTo(Page.Overview);
            }, "ops-small");
            var titles = Add(head, "district-titles");
            Text(titles, def.Name, "page-title");
            Text(titles, def.Kind + " · 건설 화면 (시험)", "page-sub");
            districtStats = Add(head, "district-stats");

            // the grid fills the screen; the building list and the info card float over it
            var body = Add(districtPage, "district-body");
            var stage = Add(body, "district-stage");
            isoGrid = new IsoGrid(this);
            stage.Add(isoGrid);
            // painted buildings (sprites) above the ground, then holograms and highlights above them
            var spriteLayer = Add(stage, "iso-layer");
            spriteLayer.pickingMode = PickingMode.Ignore;
            var overlayLayer = Add(stage, "iso-layer");
            overlayLayer.pickingMode = PickingMode.Ignore;
            isoGrid.Attach(spriteLayer, overlayLayer);
            districtFloat = Add(stage, "district-float");
            districtFloat.pickingMode = PickingMode.Ignore;
            ghostTag = Text(districtFloat, "", "district-ghost-tag");
            ghostTag.style.display = DisplayStyle.None;
            var bar = Add(stage, "district-confirm-bar");
            Text(bar, "드래그: 이동 · 휠: 확대/축소 · 클릭: 위치 지정 · 오른쪽 클릭/Esc: 취소 · Enter: 확정", "district-hint");
            MakeButton(bar, "취소", CancelPlacement, "ops-small");
            confirmButton = MakeButton(bar, "건설 확정", ConfirmPlacement, "district-confirm");

            var palette = Add(body, "district-palette");
            Text(palette, "건설", "section-title");
            foreach (var b in Buildings.Buildable)
            {
                var id = b.Id;
                var button = MakeButton(palette, "", () => PickTool(id), "district-tool");
                var swatch = Add(button, "district-swatch");
                swatch.style.backgroundColor = BuildingColors[id];
                var words = Add(button, "district-tool-words");
                Text(words, b.Name, "district-tool-name");
                Text(words, $"예산 {b.Cost:0} · {b.Weeks}주 · {b.W}×{b.H}", "district-tool-cost");
                toolButtons[id] = button;
            }
            Text(palette, "정리", "section-title");
            var demolish = MakeButton(palette, "", () => PickTool("demolish"), "district-tool");
            Add(demolish, "district-swatch district-swatch--demolish");
            var dwords = Add(demolish, "district-tool-words");
            Text(dwords, "철거", "district-tool-name");
            Text(dwords, "완공 건물은 비용 30% 환급", "district-tool-cost");
            toolButtons["demolish"] = demolish;

            districtInfo = Add(body, "district-info");
            RefreshDistrict();
        }

        void PickTool(string id)
        {
            districtTool = districtTool == id ? null : id;
            pendingSpot = new Vector2Int(-1, -1);
            sfx.Play(Cue.Click);
            RefreshDistrict();
        }

        // Header numbers, tool states and the side panel; called on every day passed while open.
        void RefreshDistrict()
        {
            if (districtZone < 0 || districtStats == null)
                return;
            int z = districtZone;
            districtStats.Clear();
            void Stat(string label, string value, string extra = null, string tone = null)
            {
                var cell = Add(districtStats, "district-stat");
                Text(cell, label, "district-stat-label");
                var v = Text(cell, value, "district-stat-value num");
                if (tone != null)
                    v.AddToClassList(tone);
                if (extra != null)
                    Text(cell, extra, "district-stat-extra");
            }
            Stat("예산", $"{session.Credit:0}", $"+{session.CreditPerWeek:0.0}/주");
            int busy = session.UnderConstruction(z), crews = session.Crews(z);
            Stat("공사 인력", $"{Math.Max(0, crews - busy)}/{crews}", busy > 0 ? $"공사 중 {busy}곳" : "팀 대기");
            float cap = session.Capacity(z), pop = session.ZonePop[z];
            Stat("인구", $"{pop:0}", $"수용 {cap:0}", pop > cap * 1.05f ? "tone--bad" : null);
            Stat("만족", $"{session.ZoneSatisfaction(z):0}", $"건물 {session.DistrictComfort(z):+0.0;-0.0;0}");
            Stat("긴장", $"{session.ZoneTension[z]:0}", $"{-session.TensionTrend(z):+0.0;-0.0;0}/주",
                session.TensionTrend(z) > 0.3f ? "tone--bad" : null);

            foreach (var pair in toolButtons)
            {
                pair.Value.EnableInClassList("district-tool--on", pair.Key == districtTool);
                var b = Buildings.Get(pair.Key);
                pair.Value.EnableInClassList("district-tool--off", b != null && session.Credit < b.Cost);
            }
            RenderDistrictInfo(null);
            RefreshGhost();
            isoGrid?.Relayout();
        }

        // The side panel: what is under the cursor, or the picked tool, then the district's own causes.
        void RenderDistrictInfo(Placed hovered)
        {
            if (districtInfo == null)
                return;
            districtInfo.Clear();
            int z = districtZone;
            var plan = session.PlanOf(z);
            var tool = Buildings.Get(districtTool);
            if (hovered != null)
            {
                var b = hovered.Def;
                Text(districtInfo, b.Name, "detail-title");
                if (!session.IsDone(hovered))
                {
                    int left = hovered.DoneDay - session.Day;
                    Text(districtInfo, $"공사 중 · {left}일 남음", "state-tag state-tag--warn");
                }
                else if (!session.Powered(plan, hovered))
                    Text(districtInfo, "전력 없음 · 효과 절반", "state-tag state-tag--bad");
                Text(districtInfo, b.Desc, "detail-text");
            }
            else if (tool != null)
            {
                RenderPlanCard(tool);
                return;
            }
            else if (districtTool == "demolish")
            {
                Text(districtInfo, "철거", "detail-title");
                Text(districtInfo, "건물을 누르면 헌다. 집을 헐면 사는 사람이 이주해야 하고, 한동안 긴장이 오른다.", "detail-text");
            }
            else
            {
                Text(districtInfo, "지구 운영", "detail-title");
                Text(districtInfo, "왼쪽에서 건물을 골라 빈 땅에 놓는다. 공사에는 예산과 공사 인력이 들고, 끝나면 지구가 바뀐다.", "detail-text");
            }
            Text(districtInfo, "이 지구의 긴장 원인", "section-title");
            foreach (var (name, value) in session.TensionCauses(z).Take(6))
            {
                var row = Add(districtInfo, "cause-row");
                Text(row, name, "cause-name");
                Text(row, $"{(value > 0 ? "+" : "")}{value * Society.Zones[z].Spread:0.0}", "cause-value num " + (value > 0 ? "tone--bad" : "tone-good"));
            }
        }

        void DistrictClick(Vector2Int tile, Vector2 local)
        {
            int z = districtZone;
            var at = isoGrid.LocalToWorld(local);
            if (districtTool == "demolish")
            {
                var target = session.PlanOf(z).At(tile.x, tile.y);
                if (target == null)
                    return;
                var name = target.Def.Name;
                session.DemolishAt(z, tile.x, tile.y);
                sfx.Play(Cue.Deny);
                FloatText(local, $"철거 · {name}", false);
                AfterBuildChange();
                return;
            }
            var tool = Buildings.Get(districtTool);
            if (tool == null)
                return;
            pendingSpot = tile;
            sfx.Play(session.CanPlace(z, tool.Id, tile.x, tile.y, out _) ? Cue.Select : Cue.Deny);
            RefreshDistrict();
        }

        // Where the hologram stands: the chosen spot, else the tile under the cursor.
        Vector2Int DistrictSpot() => pendingSpot.x >= 0 ? pendingSpot : isoGrid != null ? isoGrid.Hover : new Vector2Int(-1, -1);

        void CancelPlacement()
        {
            if (pendingSpot.x >= 0)
                pendingSpot = new Vector2Int(-1, -1);
            else
                districtTool = null;
            sfx.Play(Cue.Click);
            RefreshDistrict();
        }

        void ConfirmPlacement()
        {
            var tool = Buildings.Get(districtTool);
            int z = districtZone;
            if (tool == null || pendingSpot.x < 0)
                return;
            var spot = pendingSpot;
            if (!session.CanPlace(z, tool.Id, spot.x, spot.y, out var reason))
            {
                sfx.Play(Cue.Deny);
                Toast(reason);
                return;
            }
            session.Place(z, tool.Id, spot.x, spot.y);
            sfx.Play(Cue.Assign);
            var local = isoGrid.TileCenter(spot.x + tool.W * 0.5f, spot.y + tool.H * 0.5f, 0f);
            ClickRipple(isoGrid.LocalToWorld(local));
            FloatText(local, $"착공 · {tool.Name} · {tool.Weeks}주  예산 -{tool.Cost:0}", true);
            pendingSpot = new Vector2Int(-1, -1);
            if (session.Credit < tool.Cost)
                districtTool = null;
            AfterBuildChange();
        }

        // The picked building: what it costs, and what it would change once finished where the hologram stands.
        void RenderPlanCard(BuildingDef tool)
        {
            int z = districtZone;
            var head = Add(districtInfo, "plan-card-head");
            var swatch = Add(head, "district-swatch district-swatch--big");
            swatch.style.backgroundColor = BuildingColors[tool.Id];
            var words = Add(head, "district-tool-words");
            Text(words, tool.Name, "detail-title");
            Text(words, $"{tool.W}×{tool.H}칸 · 도로에 닿아야 함", "ops-note");
            Text(districtInfo, tool.Desc, "detail-text");

            void Row(VisualElement parent, string label, string value, string tone = null)
            {
                var row = Add(parent, "plan-row");
                Text(row, label, "plan-row-label");
                var v = Text(row, value, "plan-row-value num");
                if (tone != null)
                    v.AddToClassList(tone);
            }
            Text(districtInfo, "건설 비용", "section-title");
            var cost = Add(districtInfo, "plan-block");
            Row(cost, "예산", $"{tool.Cost:0}  (보유 {session.Credit:0})", session.Credit < tool.Cost ? "tone--bad" : null);
            int free = session.Crews(z) - session.UnderConstruction(z);
            Row(cost, "공사 인력", $"1팀  (대기 {Math.Max(0, free)}팀)", free < 1 ? "tone--bad" : null);
            Row(cost, "공사 기간", $"{tool.Weeks}주");

            var spot = DistrictSpot();
            Text(districtInfo, "건설 후 효과" + (pendingSpot.x >= 0 ? " · 지정한 자리" : " · 커서 위치"), "section-title");
            var fx = Add(districtInfo, "plan-block");
            if (spot.x < 0 || spot.y < 0 || spot.x >= DistrictPlan.Size || spot.y >= DistrictPlan.Size)
            {
                Text(fx, "격자 위에 올리면 그 자리에서의 효과를 미리 계산한다.", "ops-note");
                return;
            }
            if (!session.CanPlace(z, tool.Id, spot.x, spot.y, out var why))
                Text(fx, "이 자리에는 지을 수 없다: " + why, "plan-warning");
            var pv = session.Preview(z, tool.Id, spot.x, spot.y);
            string Signed(float v, string fmt) => (v > 0 ? "+" : "") + v.ToString(fmt);
            string Tone(float v, bool higherIsBetter) =>
                Mathf.Abs(v) < 0.05f ? null : (v > 0) == higherIsBetter ? "tone-good" : "tone--bad";
            if (Mathf.Abs(pv.Capacity) > 0.5f)
                Row(fx, "수용 인원", Signed(pv.Capacity, "0") + "명", Tone(pv.Capacity, true));
            Row(fx, "지구 만족", Signed(pv.Satisfaction, "0.0"), Tone(pv.Satisfaction, true));
            Row(fx, "긴장 변화", Signed(pv.TensionTrend, "0.0") + "/주", Tone(pv.TensionTrend, false));
            if (Mathf.Abs(pv.Credit) > 0.05f)
                Row(fx, "예산 수입", Signed(pv.Credit, "0.0") + "/주", Tone(pv.Credit, true));
            if (pv.FeltPerWeek < -0.01f)
                Row(fx, "자율성 인지", Signed(pv.FeltPerWeek, "0.00") + "/주", "tone--bad");
            if (pv.InfoPerWeek > 0.01f)
                Row(fx, "정보", Signed(pv.InfoPerWeek, "0.0") + "/주", "tone-good");
            if (pv.HomesHelped > 0)
                Row(fx, "혜택 받는 집", $"{pv.HomesHelped}동", "tone-good");
            if (pv.HomesDisturbed > 0)
                Row(fx, "소음 받는 집", $"{pv.HomesDisturbed}동", "tone--bad");
            if (!pv.Powered)
                Row(fx, "전력", "범위 밖 · 효과 절반", "tone--bad");
            Text(districtInfo, $"공사 중 {pv.Weeks}주 동안 소음으로 긴장 +{pv.BuildNoise:0.0}/주", "ops-note");
        }

        // The hologram's tag on the grid and the confirm button follow the spot.
        void RefreshGhost()
        {
            if (ghostTag == null || isoGrid == null)
                return;
            var tool = Buildings.Get(districtTool);
            var spot = DistrictSpot();
            bool inside = spot.x >= 0 && spot.y >= 0 && spot.x < DistrictPlan.Size && spot.y < DistrictPlan.Size;
            if (tool == null || !inside)
            {
                ghostTag.style.display = DisplayStyle.None;
                confirmButton?.SetEnabled(false);
                return;
            }
            bool ok = session.CanPlace(districtZone, tool.Id, spot.x, spot.y, out var reason);
            int range = Math.Max(tool.Reach, tool.PowerReach);
            string reach = range > 0 ? $" · {(tool.PowerReach > 0 ? "전력" : "영향")} 범위 {range}칸" : "";
            ghostTag.text = (ok ? (pendingSpot.x >= 0 ? "건설 가능 · 확정을 누르세요" : "건설 가능") : reason) + reach;
            ghostTag.EnableInClassList("district-ghost-tag--bad", !ok);
            var at = isoGrid.TileCenter(spot.x + tool.W, spot.y + tool.H * 0.5f, tool.Height);
            ghostTag.style.left = at.x + 12f;
            ghostTag.style.top = at.y;
            ghostTag.style.display = DisplayStyle.Flex;
            confirmButton?.SetEnabled(ok && pendingSpot.x >= 0);
        }

        // Esc cancels, Enter confirms while the district screen is open.
        void TickDistrictKeys()
        {
            if (!DistrictOpen || overlayOpen)
                return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null)
                return;
            if (kb.escapeKey.wasPressedThisFrame && districtTool != null)
                CancelPlacement();
            else if ((kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && pendingSpot.x >= 0)
                ConfirmPlacement();
        }

        void AfterBuildChange()
        {
            RefreshDistrict();
            Refresh();
            OpsSaveStore.Save(session);
        }

        // A short line rising from where something happened on the grid.
        void FloatText(Vector2 local, string text, bool good)
        {
            if (districtFloat == null)
                return;
            var label = Text(districtFloat, text, "district-floater" + (good ? " district-floater--good" : " district-floater--bad"));
            label.style.left = local.x;
            label.style.top = local.y - 24f;
            label.schedule.Execute(() => label.AddToClassList("district-floater--go")).StartingIn(30);
            label.schedule.Execute(() => label.RemoveFromHierarchy()).StartingIn(2200);
        }

        // A building finished: its resident line on the grid (or a banner when the screen is closed).
        void OnBuildingDone(int zone, Placed p)
        {
            var speaker = Residents.Cast.FirstOrDefault(r => Society.HomeOf(r.Id) == zone);
            string who = speaker != null ? speaker.Name : "주민";
            if (zone == districtZone && isoGrid != null && DistrictOpen)
            {
                var spot = isoGrid.TileCenter(p.X + p.Def.W * 0.5f, p.Y + p.Def.H * 0.5f, p.Def.Height);
                FloatText(spot, $"완공 · {p.Def.Name}", true);
                if (p.Def.Say != null)
                {
                    var bubble = Text(districtFloat, $"{who}: \"{p.Def.Say}\"", "district-say");
                    bubble.style.left = spot.x;
                    bubble.style.top = spot.y - 64f;
                    bubble.schedule.Execute(() => bubble.RemoveFromHierarchy()).StartingIn(5200);
                }
                sfx.Play(Cue.Success);
                RefreshDistrict();
            }
            else
                Banner(BannerKind.Good, $"완공 · {Society.Zones[zone].Name}", p.Def.Name, p.Def.Say != null ? $"{who}: \"{p.Def.Say}\"" : null);
        }

        // Construction under way shows on the overview as a small chip at the district (every map mode).
        void TickBuildChip()
        {
            if (buildChip == null)
                return;
            foreach (var zone in session.Plans.Keys)
            {
                int n = session.UnderConstruction(zone);
                if (n > 0 && zone < markers.Count && currentPage == Page.Overview && !DistrictOpen)
                {
                    var at = markers[zone].Anchor;
                    buildChip.text = $"공사 중 {n}";
                    buildChip.style.left = at.x;
                    buildChip.style.top = at.y - 34f;
                    buildChip.style.display = DisplayStyle.Flex;
                    buildChip.style.opacity = 0.75f + 0.25f * Mathf.Sin(cityTime * 3f);
                    return;
                }
            }
            buildChip.style.display = DisplayStyle.None;
        }

        // ------------------------------------------------------------------ the grid

        // The district drawn as an isometric grid: ground and roads, buildings as boxes (back to front),
        // construction as scaffolding with a progress bar, the picked building as a hologram; the homes it would help
        // glow green, the ones it would disturb red. Drag to pan, scroll to zoom around the cursor.
        sealed class IsoGrid : VisualElement
        {
            readonly OpsScreen screen;
            Vector2Int hover = new Vector2Int(-1, -1);
            public Vector2Int Hover => hover;
            float baseTile, tile, ox, oy, zoom = StartZoom;
            Vector2 pan, centre;
            bool framed, pressing, dragging;
            Vector2 pressAt, lastAt;
            const int N = DistrictPlan.Size;
            const float StartZoom = 1.9f, MinZoom = 1f, MaxZoom = 5f;
            // the tile the view starts on: where the old estate meets the empty land
            static readonly Vector2 StartTile = new Vector2(12f, 12f);

            VisualElement spriteLayer, overlayLayer;
            readonly List<(Placed item, VisualElement element)> sprites = new List<(Placed, VisualElement)>();

            public void Attach(VisualElement spritesUnder, VisualElement overlay)
            {
                spriteLayer = spritesUnder;
                overlayLayer = overlay;
                overlayLayer.generateVisualContent += DrawOverlay;
                RegisterCallback<GeometryChangedEvent>(_ => Relayout());
            }

            public void Relayout()
            {
                MarkDirtyRepaint();
                overlayLayer?.MarkDirtyRepaint();
                LayoutSprites();
            }

            // Old housing reuses the new apartment's picture, a little dimmer.
            static Texture2D SpriteFor(string id) => PixelArt.Load("District/" + (id == "old_homes" ? "apartment" : id));

            bool HasSprite(OpsSession s, Placed q) => s.IsDone(q) && SpriteFor(q.Id) != null;

            // Finished buildings with a picture are elements over the ground, kept in back-to-front order and placed
            // so the picture's width spans the plot's diamond and its bottom sits on the plot's front corner.
            void LayoutSprites()
            {
                if (spriteLayer == null)
                    return;
                var s = screen.session;
                var plan = s.PlanOf(screen.districtZone);
                if (plan == null)
                    return;
                Fit();
                var want = plan.Items.Where(q => HasSprite(s, q)).OrderBy(q => q.X + q.Y + (q.Def.W + q.Def.H) * 0.5f).ToList();
                if (want.Count != sprites.Count || want.Where((q, i) => sprites[i].item != q).Any())
                {
                    spriteLayer.Clear();
                    sprites.Clear();
                    foreach (var q in want)
                    {
                        var e = new VisualElement { pickingMode = PickingMode.Ignore };
                        e.AddToClassList("iso-sprite");
                        e.style.backgroundImage = new StyleBackground(SpriteFor(q.Id));
                        spriteLayer.Add(e);
                        sprites.Add((q, e));
                    }
                }
                foreach (var (q, e) in sprites)
                {
                    var tex = SpriteFor(q.Id);
                    float left = P(q.X, q.Y + q.Def.H).x, right = P(q.X + q.Def.W, q.Y).x;
                    float width = right - left, height = width * tex.height / tex.width;
                    float bottom = P(q.X + q.Def.W, q.Y + q.Def.H).y;
                    e.style.left = left;
                    e.style.top = bottom - height;
                    e.style.width = width;
                    e.style.height = height;
                    bool hot = q.Covers(hover.x, hover.y);
                    var tint = q.Def.Existing ? new Color(0.84f, 0.85f, 0.9f) : Color.white;
                    if (!s.Powered(plan, q))
                        tint = new Color(tint.r * 0.55f, tint.g * 0.55f, tint.b * 0.6f);
                    if (hot)
                        tint = Color.Lerp(tint, Color.white, 0.5f) * 1.15f;
                    e.style.unityBackgroundImageTintColor = tint;
                }
            }

            Dictionary<Placed, Color> Marks(OpsSession s, DistrictPlan plan, BuildingDef tool, Vector2Int spot)
            {
                var marks = new Dictionary<Placed, Color>();
                if (tool == null || !Inside(spot))
                    return marks;
                var temp = new Placed { Id = tool.Id, X = spot.x, Y = spot.y };
                foreach (var q in plan.Items.Where(q => s.IsDone(q)))
                {
                    int gap = temp.Gap(q);
                    if (tool.Comfort > 0f && q.Def.IsHome && gap < tool.Reach)
                        marks[q] = Helped;
                    if (tool.PowerReach > 0 && !s.Powered(plan, q) && gap < tool.PowerReach)
                        marks[q] = Helped;
                    if (tool.Noise > 0f && q.Def.IsHome && gap == 0)
                        marks[q] = Disturbed;
                }
                return marks;
            }

            // Over the sprites: highlights on painted buildings, the hologram, the hovered tile.
            void DrawOverlay(MeshGenerationContext ctx)
            {
                var s = screen.session;
                var plan = s.PlanOf(screen.districtZone);
                if (plan == null || contentRect.width < 10f)
                    return;
                Fit();
                var p = ctx.painter2D;
                var tool = Buildings.Get(screen.districtTool);
                var spot = screen.DistrictSpot();
                float glow = 0.6f + 0.4f * Mathf.Sin(Time.realtimeSinceStartup * 5f);
                foreach (var pair in Marks(s, plan, tool, spot))
                {
                    var q = pair.Key;
                    if (!HasSprite(s, q))
                        continue;
                    var m = pair.Value;
                    int x = q.X, y = q.Y, w = q.Def.W, h = q.Def.H;
                    Quad(p, P(x, y), P(x + w, y), P(x + w, y + h), P(x, y + h), new Color(m.r, m.g, m.b, 0.22f * glow));
                    Outline(p, P(x, y), P(x + w, y), P(x + w, y + h), P(x, y + h), new Color(m.r, m.g, m.b, 0.95f), 2.5f);
                    // just above the picture's top
                    var tex = SpriteFor(q.Id);
                    float picWidth = P(x + w, y).x - P(x, y + h).x;
                    var dot = new Vector2(P(x + w * 0.5f, y + h * 0.5f).x, P(x + w, y + h).y - picWidth * tex.height / tex.width - tile * 0.25f);
                    p.fillColor = new Color(m.r, m.g, m.b, 0.95f);
                    p.BeginPath();
                    p.Arc(dot, Mathf.Max(5f, tile * 0.1f), 0f, 360f);
                    p.Fill();
                }
                if (tool != null && Inside(spot))
                    DrawHologram(p, s, tool, spot.x, spot.y);
                if (Inside(hover) && tool == null)
                {
                    int x = hover.x, y = hover.y;
                    var c = screen.districtTool == "demolish" ? new Color(1f, 0.4f, 0.4f, 0.9f) : new Color(0.4f, 0.85f, 1f, 0.8f);
                    Outline(p, P(x, y), P(x + 1, y), P(x + 1, y + 1), P(x, y + 1), c, 2f);
                }
            }

            public IsoGrid(OpsScreen screen)
            {
                this.screen = screen;
                AddToClassList("iso-grid");
                generateVisualContent += Draw;
                // the hologram shimmers
                schedule.Execute(() =>
                {
                    if (screen.districtTool != null)
                    {
                        Relayout();
                        screen.RefreshGhost();
                    }
                }).Every(50);
                RegisterCallback<PointerMoveEvent>(e =>
                {
                    if (pressing)
                    {
                        if (!dragging && Vector2.Distance((Vector2)e.localPosition, pressAt) > 6f)
                            dragging = true;
                        if (dragging)
                        {
                            pan += (Vector2)e.localPosition - lastAt;
                            lastAt = e.localPosition;
                            screen.RefreshGhost();
                            Relayout();
                            return;
                        }
                    }
                    var t = TileAt(e.localPosition);
                    if (t == hover)
                        return;
                    hover = t;
                    var plan = screen.session.PlanOf(screen.districtZone);
                    // with a building picked the panel shows the plan for this spot, not what stands there
                    bool planning = Buildings.Get(screen.districtTool) != null;
                    screen.RenderDistrictInfo(Inside(t) && !planning ? plan.At(t.x, t.y) : null);
                    screen.RefreshGhost();
                    Relayout();
                });
                RegisterCallback<PointerLeaveEvent>(_ =>
                {
                    hover = new Vector2Int(-1, -1);
                    Relayout();
                });
                RegisterCallback<PointerDownEvent>(e =>
                {
                    if (e.button == 1)
                    {
                        screen.CancelPlacement();
                        return;
                    }
                    if (e.button != 0)
                        return;
                    pressing = true;
                    dragging = false;
                    pressAt = lastAt = e.localPosition;
                    this.CapturePointer(e.pointerId);
                });
                RegisterCallback<PointerUpEvent>(e =>
                {
                    if (e.button != 0 || !pressing)
                        return;
                    pressing = false;
                    this.ReleasePointer(e.pointerId);
                    if (dragging)
                    {
                        dragging = false;
                        return;
                    }
                    var t = TileAt(e.localPosition);
                    if (Inside(t))
                        screen.DistrictClick(t, e.localPosition);
                });
                RegisterCallback<WheelEvent>(e =>
                {
                    Fit();
                    float next = Mathf.Clamp(zoom * (e.delta.y > 0 ? 0.88f : 1.14f), MinZoom, MaxZoom);
                    var at = (Vector2)e.localMousePosition;
                    var raw = (at - pan - centre) / zoom + centre;
                    zoom = next;
                    pan = at - centre - (raw - centre) * zoom;
                    screen.RefreshGhost();
                    Relayout();
                    e.StopPropagation();
                });
            }

            static bool Inside(Vector2Int t) => t.x >= 0 && t.y >= 0 && t.x < N && t.y < N;

            // At zoom 1 the whole district fits; zoom and pan move around that picture.
            void Fit()
            {
                var r = contentRect;
                baseTile = Mathf.Max(4f, Mathf.Min(r.width / (N + 1f), (r.height - 40f) / (N * 0.5f + 2.2f)));
                tile = baseTile * zoom;
                centre = new Vector2(r.width * 0.5f, r.height * 0.5f);
                ox = r.width * 0.5f;
                oy = (r.height - N * baseTile * 0.5f) * 0.5f + baseTile * 0.9f;
                if (!framed && r.width > 10f)
                {
                    framed = true;
                    pan = centre - (centre + (Raw(StartTile.x, StartTile.y) - centre) * zoom);
                }
            }

            Vector2 Raw(float x, float y) => new Vector2(ox + (x - y) * baseTile * 0.5f, oy + (x + y) * baseTile * 0.25f);

            Vector2 P(float x, float y) => centre + (Raw(x, y) - centre) * zoom + pan;

            public Vector2 TileCenter(float x, float y, float height)
            {
                Fit();
                return P(x, y) - new Vector2(0f, height * tile * 0.45f);
            }

            Vector2Int TileAt(Vector2 local)
            {
                Fit();
                var raw = (local - pan - centre) / zoom + centre;
                float a = (raw.x - ox) / (baseTile * 0.5f), b = (raw.y - oy) / (baseTile * 0.25f);
                return new Vector2Int(Mathf.FloorToInt((a + b) * 0.5f), Mathf.FloorToInt((b - a) * 0.5f));
            }

            bool Visible(Vector2 at, float margin)
            {
                var r = contentRect;
                return at.x > -margin && at.y > -margin && at.x < r.width + margin && at.y < r.height + margin;
            }

            static Color Shade(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

            void Quad(Painter2D p, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color fill)
            {
                p.fillColor = fill;
                p.BeginPath();
                p.MoveTo(a);
                p.LineTo(b);
                p.LineTo(c);
                p.LineTo(d);
                p.ClosePath();
                p.Fill();
            }

            void Outline(Painter2D p, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color stroke, float width)
            {
                p.strokeColor = stroke;
                p.lineWidth = width;
                p.BeginPath();
                p.MoveTo(a);
                p.LineTo(b);
                p.LineTo(c);
                p.LineTo(d);
                p.ClosePath();
                p.Stroke();
            }

            static readonly Color Helped = new Color(0.35f, 1f, 0.55f), Disturbed = new Color(1f, 0.4f, 0.4f);

            void Draw(MeshGenerationContext ctx)
            {
                var s = screen.session;
                var plan = s.PlanOf(screen.districtZone);
                if (plan == null || contentRect.width < 10f)
                    return;
                Fit();
                var p = ctx.painter2D;
                var tool = Buildings.Get(screen.districtTool);
                bool showPower = tool != null;
                var substations = plan.Items.Where(q => q.Def.PowerReach > 0 && s.IsDone(q)).ToList();

                // the boulevards around the district
                Quad(p, P(-0.6f, -0.6f), P(N + 0.6f, -0.6f), P(N + 0.6f, N + 0.6f), P(-0.6f, N + 0.6f), new Color(0.13f, 0.15f, 0.2f));
                var probe = new Placed { Id = "park" };
                for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    if (!Visible(P(x + 0.5f, y + 0.5f), tile))
                        continue;
                    bool road = plan.IsRoad(x, y);
                    var fill = road ? new Color(0.2f, 0.22f, 0.27f) : new Color(0.16f, 0.24f, 0.24f);
                    probe.X = x;
                    probe.Y = y;
                    if (!road && showPower && substations.Any(q => q.Gap(probe) < q.Def.PowerReach))
                        fill = Color.Lerp(fill, new Color(0.55f, 0.48f, 0.2f), 0.22f);
                    Quad(p, P(x, y), P(x + 1, y), P(x + 1, y + 1), P(x, y + 1), fill);
                    if (tile > 20f)
                        Outline(p, P(x, y), P(x + 1, y), P(x + 1, y + 1), P(x, y + 1), new Color(1f, 1f, 1f, road ? 0.03f : 0.06f), 1f);
                    if (road && tile > 20f)
                    {
                        // a dashed centre line along the street
                        bool along = plan.IsRoad(x - 1, y) || plan.IsRoad(x + 1, y);
                        var a = along ? P(x + 0.2f, y + 0.5f) : P(x + 0.5f, y + 0.2f);
                        var b = along ? P(x + 0.6f, y + 0.5f) : P(x + 0.5f, y + 0.6f);
                        p.strokeColor = new Color(0.9f, 0.8f, 0.45f, 0.35f);
                        p.lineWidth = 1.5f;
                        p.BeginPath();
                        p.MoveTo(a);
                        p.LineTo(b);
                        p.Stroke();
                    }
                }

                // buildings without a picture (and everything under construction) as boxes, back to front;
                // what the picked building would touch glows on their roofs
                var marks = Marks(s, plan, tool, screen.DistrictSpot());
                foreach (var item in plan.Items.OrderBy(q => q.X + q.Y + (q.Def.W + q.Def.H) * 0.5f))
                {
                    if (HasSprite(s, item) || !Visible(P(item.X + item.Def.W * 0.5f, item.Y + item.Def.H * 0.5f), tile * 4f))
                        continue;
                    DrawBuilding(p, s, plan, item, item.Covers(hover.x, hover.y), marks.TryGetValue(item, out var mark) ? mark : (Color?)null);
                }
            }

            void DrawHologram(Painter2D p, OpsSession s, BuildingDef tool, int x, int y)
            {
                bool fits = s.CanPlace(screen.districtZone, tool.Id, x, y, out _);
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.realtimeSinceStartup * 5f);
                var c = fits ? new Color(0.35f, 0.85f, 1f) : new Color(1f, 0.38f, 0.38f);
                Color A(float a) => new Color(c.r, c.g, c.b, a * pulse);
                // reach of its effect (or power), on the ground
                int r = Math.Max(tool.Reach, tool.PowerReach);
                if (r > 0)
                    Outline(p, P(x - r, y - r), P(x + tool.W + r, y - r), P(x + tool.W + r, y + tool.H + r), P(x - r, y + tool.H + r), A(0.18f), 1f);
                // the footprint: a glowing pad
                Quad(p, P(x, y), P(x + tool.W, y), P(x + tool.W, y + tool.H), P(x, y + tool.H), A(0.3f));
                Outline(p, P(x, y), P(x + tool.W, y), P(x + tool.W, y + tool.H), P(x, y + tool.H), A(1f), 2.5f);
                // the building as glass: faint faces, bright edges, scan lines climbing it
                float h = tool.Height * tile * 0.45f;
                var up = new Vector2(0f, -h);
                const float inset = 0.08f;
                float x0 = x + inset, y0 = y + inset, x1 = x + tool.W - inset, y1 = y + tool.H - inset;
                Quad(p, P(x0, y1), P(x1, y1), P(x1, y1) + up, P(x0, y1) + up, A(0.16f));
                Quad(p, P(x1, y0), P(x1, y1), P(x1, y1) + up, P(x1, y0) + up, A(0.1f));
                Quad(p, P(x0, y0) + up, P(x1, y0) + up, P(x1, y1) + up, P(x0, y1) + up, A(0.22f));
                p.strokeColor = A(0.9f);
                p.lineWidth = 1.5f;
                p.BeginPath();
                var edges = new[]
                {
                    (P(x0, y1), P(x0, y1) + up), (P(x1, y1), P(x1, y1) + up), (P(x1, y0), P(x1, y0) + up),
                    (P(x0, y0) + up, P(x1, y0) + up), (P(x1, y0) + up, P(x1, y1) + up),
                    (P(x1, y1) + up, P(x0, y1) + up), (P(x0, y1) + up, P(x0, y0) + up),
                };
                foreach (var (a, b) in edges)
                {
                    p.MoveTo(a);
                    p.LineTo(b);
                }
                p.Stroke();
                float scan = Mathf.Repeat(Time.realtimeSinceStartup * 0.6f, 1f);
                for (int i = 0; i < 4; i++)
                {
                    float k = Mathf.Repeat(scan + i * 0.25f, 1f);
                    var lift = up * k;
                    p.strokeColor = A(0.4f * (1f - k));
                    p.lineWidth = 1f;
                    p.BeginPath();
                    p.MoveTo(P(x0, y1) + lift);
                    p.LineTo(P(x1, y1) + lift);
                    p.LineTo(P(x1, y0) + lift);
                    p.Stroke();
                }
            }

            void DrawBuilding(Painter2D p, OpsSession s, DistrictPlan plan, Placed item, bool hot, Color? mark)
            {
                var def = item.Def;
                var color = BuildingColors.TryGetValue(def.Id, out var c) ? c : Color.gray;
                bool done = s.IsDone(item);
                float h = def.Height * tile * 0.45f;
                const float inset = 0.08f;
                float x0 = item.X + inset, y0 = item.Y + inset, x1 = item.X + def.W - inset, y1 = item.Y + def.H - inset;
                var up = new Vector2(0f, -h);
                if (!done)
                {
                    // scaffolding: the outline of the box, a crane, a progress bar
                    float k = 1f - (item.DoneDay - s.Day) / (float)Math.Max(1, def.Weeks * 7);
                    var hk = new Vector2(0f, -h * Mathf.Max(0.15f, k));
                    Quad(p, P(x0, y0), P(x1, y0), P(x1, y1), P(x0, y1), new Color(0.35f, 0.32f, 0.22f));
                    Quad(p, P(x0, y1), P(x1, y1), P(x1, y1) + hk, P(x0, y1) + hk, new Color(color.r, color.g, color.b, 0.35f));
                    Quad(p, P(x1, y0), P(x1, y1), P(x1, y1) + hk, P(x1, y0) + hk, new Color(color.r * 0.7f, color.g * 0.7f, color.b * 0.7f, 0.35f));
                    // the frame rising to its final height, faint
                    Outline(p, P(x0, y1), P(x1, y1), P(x1, y1) + up, P(x0, y1) + up, new Color(1f, 0.85f, 0.4f, 0.25f), 1f);
                    p.strokeColor = new Color(1f, 0.8f, 0.3f, 0.9f);
                    p.lineWidth = 2f;
                    p.BeginPath();
                    var mast = P(x1, y1) + new Vector2(0f, -2f);
                    var jib = mast + up * 0.9f + new Vector2(0f, -tile * 0.2f);
                    p.MoveTo(mast);
                    p.LineTo(jib);
                    p.LineTo(jib + new Vector2(-tile * 0.55f, tile * 0.05f));
                    p.Stroke();
                    var bar0 = P(x1, y1) + up * 0.9f + new Vector2(-tile * 0.4f, -tile * 0.45f);
                    Quad(p, bar0, bar0 + new Vector2(tile * 0.8f, 0f), bar0 + new Vector2(tile * 0.8f, 4f), bar0 + new Vector2(0f, 4f), new Color(0f, 0f, 0f, 0.6f));
                    Quad(p, bar0, bar0 + new Vector2(tile * 0.8f * k, 0f), bar0 + new Vector2(tile * 0.8f * k, 4f), bar0 + new Vector2(0f, 4f), new Color(1f, 0.8f, 0.3f));
                    return;
                }
                if (hot)
                    color = Color.Lerp(color, Color.white, 0.25f);
                bool powered = s.Powered(plan, item);
                if (!powered)
                    color = Shade(color, 0.6f);
                // front-left face, front-right face, top
                Quad(p, P(x0, y1), P(x1, y1), P(x1, y1) + up, P(x0, y1) + up, Shade(color, 0.78f));
                Quad(p, P(x1, y0), P(x1, y1), P(x1, y1) + up, P(x1, y0) + up, Shade(color, 0.6f));
                Quad(p, P(x0, y0) + up, P(x1, y0) + up, P(x1, y1) + up, P(x0, y1) + up, color);
                Outline(p, P(x0, y0) + up, P(x1, y0) + up, P(x1, y1) + up, P(x0, y1) + up, new Color(1f, 1f, 1f, hot ? 0.6f : 0.15f), 1f);
                if (mark.HasValue)
                {
                    // helped or disturbed by the planned building: a glowing roof and a dot above it
                    float glow = 0.6f + 0.4f * Mathf.Sin(Time.realtimeSinceStartup * 5f);
                    var m = mark.Value;
                    Quad(p, P(x0, y0) + up, P(x1, y0) + up, P(x1, y1) + up, P(x0, y1) + up, new Color(m.r, m.g, m.b, 0.35f * glow));
                    Outline(p, P(x0, y0) + up, P(x1, y0) + up, P(x1, y1) + up, P(x0, y1) + up, new Color(m.r, m.g, m.b, 0.95f), 2.5f);
                    var dot = P((x0 + x1) * 0.5f, (y0 + y1) * 0.5f) + up - new Vector2(0f, tile * 0.35f);
                    p.fillColor = new Color(m.r, m.g, m.b, 0.95f);
                    p.BeginPath();
                    p.Arc(dot, Mathf.Max(4f, tile * 0.08f), 0f, 360f);
                    p.Fill();
                }
                // lit windows on homes and shops
                if (def.IsHome || def.Income > 0f)
                {
                    int floors = Mathf.Max(1, Mathf.RoundToInt(def.Height * 2f));
                    p.fillColor = new Color(1f, 0.85f, 0.5f, powered ? 0.75f : 0.2f);
                    for (int f = 0; f < floors; f++)
                    for (float t = 0.2f; t < def.W - 0.1f; t += 0.45f)
                    {
                        var w = P(x0 + t, y1) + up * ((f + 0.4f) / floors);
                        p.BeginPath();
                        p.MoveTo(w);
                        p.LineTo(w + new Vector2(tile * 0.08f, tile * 0.04f));
                        p.LineTo(w + new Vector2(tile * 0.08f, tile * 0.04f - 3f));
                        p.LineTo(w + new Vector2(0f, -3f));
                        p.ClosePath();
                        p.Fill();
                    }
                }
                if (def.Id == "park")
                {
                    p.fillColor = new Color(0.4f, 0.8f, 0.45f);
                    foreach (var (tx, ty) in new[] { (0.3f, 0.3f), (0.7f, 0.4f), (0.45f, 0.75f) })
                    {
                        var tree = P(item.X + tx, item.Y + ty);
                        p.BeginPath();
                        p.Arc(tree - new Vector2(0f, tile * 0.08f), tile * 0.09f, 0f, 360f);
                        p.Fill();
                    }
                }
                if (def.Watch > 0f)
                {
                    var top = P((x0 + x1) * 0.5f, (y0 + y1) * 0.5f) + up;
                    p.fillColor = new Color(0.6f, 0.85f, 1f, 0.9f);
                    p.BeginPath();
                    p.Arc(top, tile * 0.07f, 0f, 360f);
                    p.Fill();
                }
                if (!powered)
                {
                    var dark = P((x0 + x1) * 0.5f, (y0 + y1) * 0.5f) + up * 1.25f;
                    p.fillColor = new Color(1f, 0.35f, 0.3f);
                    p.BeginPath();
                    p.Arc(dark, 4f, 0f, 360f);
                    p.Fill();
                }
            }
        }
    }
}
