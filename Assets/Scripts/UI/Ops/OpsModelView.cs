using System.Collections.Generic;
using System.Linq;
using Sector.ModelCity;
using Sector.Ops;
using UnityEngine;
using UnityEngine.UIElements;
using Index = Sector.Ops.Index;

namespace Sector.UI
{
    // Main screen view modes (trial, 2026-11-27): 도시 이미지 (the painting with the overview panels, option A) or
    // 입체 모형 (the 3D city model of Assets/ModelCity with its HUD, option C, fed from the session). The model lives in
    // SampleScene as an inactive root "ModelCity" (LEVIATHAN/ModelCity/Add To Game Scene); the top bar and the left
    // menu stay in both modes. The model's hour is the dock slider, not the game clock (a game day is 8 seconds).
    public partial class OpsScreen : IModelData
    {
        ModelCity.ModelCity modelCity;
        ModelHud modelHud;
        bool modelMode;
        Camera gameCamera;
        CameraClearFlags gameClear; Color gameBg; int gameMask;
        float monitorT;   // 0..1 while the city shrinks from the full screen into the monitor
        VisualElement vignetteEl;
        readonly List<Button> viewTabs = new List<Button>();

        // model district key → game zone (Society.Zones order)
        static readonly Dictionary<string, int> ModelZone = new Dictionary<string, int>
        {
            ["U"] = 0, ["C"] = 1, ["A"] = 2, ["H"] = 3, ["Dd"] = 4, ["L"] = 5, ["E"] = 6, ["R"] = 7, ["M"] = 8, ["K"] = 9, ["S"] = 10,
        };

        void BuildViewSwitch(VisualElement top)
        {
            var tabs = Add(top, "view-switch");
            foreach (var (label, model) in new[] { ("도시 이미지", false), ("입체 모형", true) })
            {
                var b = MakeButton(tabs, label, () => SetModelMode(model), "view-tab");
                viewTabs.Add(b);
            }
            viewTabs[0].AddToClassList("view-tab--on");
        }

        void SetModelMode(bool on)
        {
            if (on && modelCity == null)
            {
                modelCity = FindAnyObjectByType<ModelCity.ModelCity>(FindObjectsInactive.Include);
                if (modelCity == null)
                {
                    Toast("이 장면에는 입체 모형이 없습니다.");
                    return;
                }
                modelHud = modelCity.GetComponentInChildren<ModelHud>(true);
            }
            if (on == modelMode)
                return;
            modelMode = on;
            sfx?.Play(Cue.Click);
            for (int i = 0; i < viewTabs.Count; i++)
                viewTabs[i].EnableInClassList("view-tab--on", (i == 1) == on);
            if (modelCity != null)
            {
                if (modelHud != null)
                {
                    modelHud.data = this;
                    modelHud.embedded = true;
                }
                // the game's camera now only paints the dark console around the monitor; the model's camera draws the
                // city inside the monitor (the MainCamera tag moves with it, so Camera.main is the city on screen)
                if (on && gameCamera == null) { gameCamera = Camera.main; gameClear = gameCamera.clearFlags; gameBg = gameCamera.backgroundColor; gameMask = gameCamera.cullingMask; }
                if (gameCamera != null)
                {
                    gameCamera.clearFlags = on ? CameraClearFlags.SolidColor : gameClear;
                    gameCamera.backgroundColor = on ? new Color(0.02f, 0.04f, 0.08f) : gameBg;
                    gameCamera.cullingMask = on ? 0 : gameMask;
                    gameCamera.tag = on ? "Untagged" : "MainCamera";
                }
                modelCity.gameObject.SetActive(on);
                if (modelCity.cam != null) { modelCity.cam.tag = on ? "MainCamera" : "Untagged"; if (on) modelCity.cam.rect = new Rect(0, 0, 1, 1); }
                monitorT = 0f;
            }
            // the monitor: the frame settles in (USS transition on .mh--model), the screen powers on with a line
            mainHud?.EnableInClassList("mh--model", on);
            if (on && mainHud != null)
            {
                var power = mainHud.Q("power");
                power.RemoveFromClassList("mh-power--open");
                power.schedule.Execute(() => power.AddToClassList("mh-power--line")).StartingIn(380);
                power.schedule.Execute(() => { power.AddToClassList("mh-power--open"); power.RemoveFromClassList("mh-power--line"); }).StartingIn(640);
                power.schedule.Execute(() => power.RemoveFromClassList("mh-power--open")).StartingIn(1100);
            }
            vignetteEl ??= root.Q(className: "vignette");
            cityView.style.display = on ? DisplayStyle.None : DisplayStyle.Flex;
            if (vignetteEl != null)
                vignetteEl.style.display = on ? DisplayStyle.None : DisplayStyle.Flex;
            // empty screen areas pass the pointer down to the model's HUD and camera
            root.pickingMode = on ? PickingMode.Ignore : PickingMode.Position;
            // the root's own fill (.ops background, var(--bg)) would hide the 3D view and the HUD under it
            root.EnableInClassList("ops--model", on);
            ApplyViewMode();
        }

        // Every frame in model mode: the 3D view eases from the full screen into the monitor's opening, and the
        // model HUD is laid out inside that opening.
        void TickModelView()
        {
            if (!modelMode || modelCity == null || modelCity.cam == null || mainHud == null)
                return;
            var bezel = mainHud.Q("bezel");
            var r = bezel.layout; float W = root.layout.width, H = root.layout.height;
            if (float.IsNaN(r.width) || r.width <= 0 || W <= 0)
                return;
            // the opening: inside the monitor's 2 px cyan line (MainHud.uss .mh-bezel)
            var open = new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4);
            var target = new Rect(open.x / W, 1f - open.yMax / H, open.width / W, open.height / H);
            monitorT = Mathf.Min(1f, monitorT + Time.unscaledDeltaTime / 0.55f);
            float e = 1f - Mathf.Pow(1f - monitorT, 3f);
            var full = new Rect(0, 0, 1, 1);
            modelCity.cam.rect = new Rect(Mathf.Lerp(full.x, target.x, e), Mathf.Lerp(full.y, target.y, e), Mathf.Lerp(full.width, target.width, e), Mathf.Lerp(full.height, target.height, e));
            if (modelHud != null) modelHud.frame = open;
        }

        // Called from GoTo as well: the model's HUD shows only on the overview.
        void ApplyViewMode()
        {
            bool overview = currentPage == Page.Overview;
            overviewPage.style.display = overview && !modelMode ? DisplayStyle.Flex : DisplayStyle.None;
            modelHud?.SetShown(modelMode && overview);
            // pages sit in the middle of the screen: the side gauges and cards make room
            mainHud?.EnableInClassList("mh--page", !overview);
            RefreshMainHudControls();
        }

        // ------------------------------------------------------------------ data for the model HUD
        (string name, float v, float d)[] IModelData.Indices()
        {
            var before = session.History.Count > 7 ? session.History[session.History.Count - 8] : null;
            return IndexNames.All.Select(i =>
            {
                string n = i == Index.AutonomyFelt ? "자율성 (체감)" : i == Index.AutonomyReal ? "자율성 (실질)" : IndexNames.Name(i).Replace(" 지수", "");
                float v = session.Indices[i];
                return (n, v, before != null ? v - before[i] : 0f);
            }).ToArray();
        }

        List<(string k, Color c, string a, string b)> IModelData.Alerts()
        {
            // the tensest districts, anomalies on camera, then the next forecast crisis
            var list = new List<(string, Color, string, string)>();
            foreach (var (k, z) in ModelZone.OrderByDescending(p => session.ZoneTension[p.Value]))
            {
                if (list.Count >= 3 || !session.ZoneOpen(z))
                    continue;
                int stage = session.TensionStage(z);
                if (stage < 1)
                    continue;
                int anomalies = session.Anomalies.Count(a => a.Zone == z);
                list.Add((k, ModelCity.ModelCity.TensionColor(session.ZoneTension[z]), $"{Society.Zones[z].Name} · {Tension.StageNames[stage]}",
                    $"긴장 {session.ZoneTension[z]:0}" + (anomalies > 0 ? $" · 이상 징후 {anomalies}" : "")));
            }
            return list;
        }

        (float tension, float pop, float lit) IModelData.District(string k)
        {
            int z = ModelZone[k];
            if (!session.ZoneOpen(z))
                return (0f, 0f, 0.1f);
            return (session.ZoneTension[z], session.ZonePop[z], Mathf.Clamp01(0.35f + session.ZonePop[z] / 12000f));
        }

        string IModelData.DateText() => $"{session.Date:yyyy.MM.dd}";

        void IModelData.OpenCamera(string k)
        {
            int z = ModelZone[k];
            sim.ShowZone(z);
            watchCamera = true;
            subPage[Page.Watch] = 0;
            GoTo(Page.Watch);
        }
    }
}
