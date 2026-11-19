using System;
using System.Collections.Generic;
using System.Linq;
using Sector.Ops;
using UnityEngine;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;

namespace Sector.UI
{
    // The AI's simulation feed: one zone of the district in pixel art, with observed residents walking about.
    // Fixed characters can be clicked; everyone else is just a pixel person going somewhere.
    // Scenes are Sim/<zone>_v2 (768 x 432, from Tools/Art/cast2.py); walkers use Sim/Sprites/<id>_<front|right|back><0-3>.
    public class SimPanel : VisualElement
    {
        public const float Width = 768f, Height = 432f;
        // The panel shows the middle of the scene, zoomed 1.5x, so people are big enough to read.
        const float ViewW = 512f, ViewH = 288f;
        static readonly Vector2 ViewOffset = new Vector2((Width - ViewW) / 2f, (Height - ViewH) / 2f + 20f);

        public static readonly string[] ZoneFiles = Society.Zones.Select(z => z.File).ToArray();
        public static readonly string[] ZoneNames = Society.Zones.Select(z => z.Name).ToArray();

        // Where people may stand in each zone (768 x 432 pixels; y is the foot row).
        static readonly Dictionary<string, Rect> WalkableByFile = new Dictionary<string, Rect>
        {
            ["plaza"] = new Rect(190f, 222f, 420f, 92f),   // paving in front of the fountain
            ["cafe"] = new Rect(170f, 240f, 460f, 130f),   // shopping street
            ["lobby"] = new Rect(260f, 232f, 370f, 140f),  // district office forecourt
            ["homes"] = new Rect(300f, 270f, 320f, 90f),   // apartment courtyard
            ["school"] = new Rect(200f, 250f, 380f, 90f),  // school gate street
            ["works"] = new Rect(220f, 205f, 400f, 120f),  // logistics yard
            ["lab"] = new Rect(225f, 255f, 330f, 90f),     // research campus courtyard
            ["clinic"] = new Rect(150f, 200f, 330f, 110f), // hospital forecourt
            ["police"] = new Rect(190f, 205f, 370f, 110f), // police station forecourt
        };

        static readonly Rect[] Walkable = ZoneFiles.Select(f => WalkableByFile.TryGetValue(f, out var r) ? r : WalkableByFile["plaza"]).ToArray();

        // Generic resident sprites (Tools/Art/cast2.py GENERIC).
        const int GenericSprites = 6;


        sealed class Walker
        {
            public string Id;       // c01.. for fixed residents, g01.. looks for samples
            public bool Fixed;
            public int Zone;
            public Vector2 Pos, Target;
            public float Wait;
            public float Frame;
            public string Facing = "front";
            public bool Flip;
            public VisualElement Element;
            public Label Bubble;
            public float BubbleLeft;
            // Observation mode: a recognition box with an ID tag, red while flagged.
            public Label Tag;
            public float AlertLeft;
        }

        bool tracking;
        int nextTag = 1031;

        readonly VisualElement screen;
        readonly VisualElement layer;
        readonly Label caption;
        readonly VisualElement tabs;
        readonly List<Walker> walkers = new List<Walker>();
        readonly Label hoverName;
        int zone;
        int zones = 1;
        string timeOfDay = "noon";
        float sortTimer;

        public event Action<string> ResidentClicked;

        public int Zone => zone;

        public SimPanel()
        {
            AddToClassList("sim");
            var head = new VisualElement();
            head.AddToClassList("sim-head");
            caption = new Label();
            caption.AddToClassList("sim-caption");
            head.Add(caption);
            tabs = new VisualElement();
            tabs.AddToClassList("sim-tabs");
            head.Add(tabs);
            Add(head);

            screen = new VisualElement();
            screen.AddToClassList("sim-screen");
            Add(screen);
            layer = new VisualElement { pickingMode = PickingMode.Ignore };
            layer.AddToClassList("sim-layer");
            screen.Add(layer);
            var scan = new VisualElement { pickingMode = PickingMode.Ignore };
            scan.AddToClassList("sim-scan");
            screen.Add(scan);
            hoverName = new Label { pickingMode = PickingMode.Ignore };
            hoverName.AddToClassList("sim-hover");
            screen.Add(hoverName);
            hoverName.style.display = DisplayStyle.None;

            schedule.Execute(() => Step(0.05f)).Every(50);
            RegisterCallback<GeometryChangedEvent>(_ => Layout());
            ShowZone(0);
        }

        float Scale => screen.contentRect.width > 0f ? screen.contentRect.width / ViewW : 1f;

        void Layout()
        {
            float w = screen.contentRect.width;
            if (w <= 0f)
                return;
            screen.style.height = w * ViewH / ViewW;
            layer.style.left = -ViewOffset.x * Scale;
            layer.style.top = -ViewOffset.y * Scale;
            layer.style.width = Width;
            layer.style.height = Height;
            layer.style.transformOrigin = new TransformOrigin(0f, 0f);
            layer.style.scale = new Scale(new Vector3(Scale, Scale, 1f));
        }

        public void SetZones(int count)
        {
            count = Mathf.Clamp(count, 1, ZoneFiles.Length);
            if (count == zones && tabs.childCount > 0)
                return;
            zones = count;
            tabs.Clear();
            for (int i = 0; i < ZoneFiles.Length; i++)
            {
                int z = i;
                var tab = new Button(() => ShowZone(z)) { text = ZoneNames[i] };
                tab.AddToClassList("sim-tab");
                tab.SetEnabled(i < zones);
                tabs.Add(tab);
            }
            if (zone >= zones)
                ShowZone(0);
            else
                MarkTabs();
        }

        void MarkTabs()
        {
            for (int i = 0; i < tabs.childCount; i++)
                tabs[i].EnableInClassList("sim-tab--on", i == zone);
        }

        public void ShowZone(int index)
        {
            zone = Mathf.Clamp(index, 0, ZoneFiles.Length - 1);
            UpdateBackdrop();
            caption.text = $"SIM · {ZoneNames[zone]}";
            foreach (var w in walkers)
                w.Element.style.display = w.Zone == zone ? DisplayStyle.Flex : DisplayStyle.None;
            MarkTabs();
        }

        // morning / noon / evening, from how far through the day the clock is.
        public void SetTime(float dayFraction)
        {
            var slot = dayFraction < 0.33f ? "morning" : dayFraction < 0.7f ? "noon" : "evening";
            if (slot == timeOfDay)
                return;
            timeOfDay = slot;
            UpdateBackdrop();
        }

        void UpdateBackdrop()
        {
            var texture = PixelArt.Load($"Sim/{ZoneFiles[zone]}_v2") ?? PixelArt.Load($"Sim/{ZoneFiles[zone]}_{timeOfDay}");
            layer.style.backgroundImage = texture != null ? new StyleBackground(texture) : StyleKeyword.Null;
            // The new scenes are painted at evening; the time of day only tints them.
            // Muted toward the console's navy so the pixel feed doesn't outshine the UI.
            layer.style.unityBackgroundImageTintColor = timeOfDay == "morning" ? new Color(0.86f, 0.84f, 0.82f)
                : timeOfDay == "noon" ? new Color(0.82f, 0.85f, 0.9f) : new Color(0.7f, 0.74f, 0.88f);
        }

        // Fixed cast plus `samples` generated residents spread across the open zones.
        public void SetPopulation(int samples, int openZones)
        {
            SetZones(openZones);
            var wanted = new List<(string id, bool isFixed, int zone)>();
            foreach (var r in Residents.Cast)
                wanted.Add((r.Id, true, Society.HomeOf(r.Id) < zones ? Society.HomeOf(r.Id) : 0));
            for (int i = 0; i < samples; i++)
                wanted.Add(($"s{i % GenericSprites + 1}", false, i % zones));

            if (walkers.Count == wanted.Count && walkers.Select(w => w.Id + w.Zone).SequenceEqual(wanted.Select(x => x.id + x.zone)))
                return;
            foreach (var w in walkers)
                w.Element.RemoveFromHierarchy();
            walkers.Clear();
            foreach (var (id, isFixed, z) in wanted)
                walkers.Add(MakeWalker(id, isFixed, z));
            ShowZone(zone);
        }

        Walker MakeWalker(string id, bool isFixed, int z)
        {
            var area = Walkable[z];
            var w = new Walker
            {
                Id = id, Fixed = isFixed, Zone = z,
                Pos = new Vector2(Random.Range(area.xMin, area.xMax), Random.Range(area.yMin, area.yMax)),
                Wait = Random.Range(0f, 3f), Frame = Random.value,
            };
            w.Target = w.Pos;
            var element = new VisualElement();
            element.AddToClassList("sim-person");
            if (isFixed)
            {
                element.AddToClassList("sim-person--cast");
                element.RegisterCallback<ClickEvent>(_ => ResidentClicked?.Invoke(id));
                element.RegisterCallback<PointerEnterEvent>(_ => ShowHover(w));
                element.RegisterCallback<PointerLeaveEvent>(_ => hoverName.style.display = DisplayStyle.None);
                var marker = new VisualElement { pickingMode = PickingMode.Ignore };
                marker.AddToClassList("sim-marker");
                element.Add(marker);
            }
            else
                element.pickingMode = PickingMode.Ignore;
            w.Bubble = new Label { pickingMode = PickingMode.Ignore };
            w.Bubble.AddToClassList("sim-bubble");
            w.Bubble.style.display = DisplayStyle.None;
            element.Add(w.Bubble);
            var def = Residents.Get(id);
            w.Tag = new Label(def != null ? def.Name : $"#{nextTag++:0000}") { pickingMode = PickingMode.Ignore };
            w.Tag.AddToClassList("sim-tag");
            w.Tag.style.display = tracking && isFixed ? DisplayStyle.Flex : DisplayStyle.None;
            element.Add(w.Tag);
            element.EnableInClassList("sim-person--tracked", tracking);
            w.Element = element;
            layer.Add(element);
            SetFrame(w, "front", 0, false);
            Place(w);
            return w;
        }

        void ShowHover(Walker w)
        {
            var def = Residents.Get(w.Id);
            if (def == null)
                return;
            hoverName.text = $"{def.Name} · {def.Job}";
            hoverName.style.display = DisplayStyle.Flex;
            hoverName.style.left = Mathf.Clamp((w.Pos.x - ViewOffset.x) * Scale - 60f, 4f, screen.contentRect.width - 180f);
            hoverName.style.top = Mathf.Max(4f, (w.Pos.y - ViewOffset.y - w.Element.resolvedStyle.height - 16f) * Scale);
        }

        // direction: front (toward the camera), right, back; frame 0-3.
        static void SetFrame(Walker w, string direction, int frame, bool flip)
        {
            var texture = PixelArt.Load($"Sim/Sprites/{w.Id}_{direction}{frame % 4}");
            w.Element.style.backgroundImage = texture != null ? new StyleBackground(texture) : StyleKeyword.Null;
            if (texture != null)
            {
                w.Element.style.width = texture.width;
                w.Element.style.height = texture.height;
            }
            w.Element.style.scale = new Scale(new Vector3(flip ? -1f : 1f, 1f, 1f));
            w.Bubble.style.scale = new Scale(new Vector3(flip ? -1f : 1f, 1f, 1f));
            w.Tag.style.scale = new Scale(new Vector3(flip ? -1f : 1f, 1f, 1f));
        }

        // Observation mode: recognition boxes and ID tags on everyone.
        public void SetTracking(bool on)
        {
            tracking = on;
            EnableInClassList("sim--tracking", on);
            foreach (var w in walkers)
            {
                w.Element.EnableInClassList("sim-person--tracked", on);
                w.Tag.style.display = on && w.Fixed ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        // Marks a visible resident for a few seconds (the screen decides who, from real data). False if not on camera.
        public bool Flag(string residentId)
        {
            var w = walkers.FirstOrDefault(x => x.Id == residentId && x.Zone == zone);
            if (w == null)
                return false;
            w.AlertLeft = 4f;
            w.Element.AddToClassList("sim-person--alert");
            return true;
        }

        static void Place(Walker w)
        {
            float width = w.Element.style.width.value.value, height = w.Element.style.height.value.value;
            w.Element.style.left = Mathf.Round(w.Pos.x - width / 2f);
            w.Element.style.top = Mathf.Round(w.Pos.y - height);
        }

        // Tension stage per zone on the tabs (2 수군거림, 3 모임, 4 계기).
        public void SetZoneStages(Func<int, int> stageOf)
        {
            for (int i = 0; i < tabs.childCount; i++)
            {
                int stage = stageOf(i);
                tabs[i].EnableInClassList("sim-tab--warn", stage == 2);
                tabs[i].EnableInClassList("sim-tab--bad", stage >= 3);
            }
        }

        // Marks an unnamed resident on screen (an anomaly with no fixed person). Returns their id, or null.
        public string FlagSample()
        {
            var pool = walkers.Where(w => !w.Fixed && w.Zone == zone).ToList();
            if (pool.Count == 0)
                return null;
            var w = pool[Random.Range(0, pool.Count)];
            w.AlertLeft = 6f;
            w.Element.AddToClassList("sim-person--alert");
            w.Tag.style.display = DisplayStyle.Flex;
            return w.Id;
        }

        // A fixed resident on screen says something for a few seconds. Returns false if nobody is visible.
        public bool Say(string residentId, string text)
        {
            var w = walkers.FirstOrDefault(x => x.Id == residentId);
            if (w == null || w.Zone != zone || string.IsNullOrEmpty(text))
                return false;
            w.Bubble.text = text;
            w.Bubble.style.display = DisplayStyle.Flex;
            w.BubbleLeft = 5f;
            return true;
        }

        public IEnumerable<string> VisibleCast => walkers.Where(w => w.Fixed && w.Zone == zone).Select(w => w.Id);

        void Step(float dt)
        {
            const float speed = 34f;
            foreach (var w in walkers)
            {
                if (w.BubbleLeft > 0f)
                {
                    w.BubbleLeft -= dt;
                    if (w.BubbleLeft <= 0f)
                        w.Bubble.style.display = DisplayStyle.None;
                }
                if (w.AlertLeft > 0f)
                {
                    w.AlertLeft -= dt;
                    if (w.AlertLeft <= 0f)
                    {
                        w.Element.RemoveFromClassList("sim-person--alert");
                        if (!w.Fixed)
                            w.Tag.style.display = DisplayStyle.None;
                    }
                }
                if (w.Zone != zone)
                    continue;
                var to = w.Target - w.Pos;
                if (w.Wait > 0f)
                {
                    w.Wait -= dt;
                    w.Frame += dt * 1.5f;
                    if (w.Wait <= 0f)
                    {
                        var area = Walkable[w.Zone];
                        w.Target = new Vector2(Random.Range(area.xMin, area.xMax), Random.Range(area.yMin, area.yMax));
                    }
                    SetFrame(w, w.Facing, 0, w.Flip);
                    continue;
                }
                if (to.magnitude < 2f)
                {
                    w.Wait = Random.Range(1.5f, 5f);
                    continue;
                }
                w.Pos += to.normalized * Mathf.Min(speed * dt, to.magnitude);
                w.Frame += dt * 5f;
                bool vertical = Mathf.Abs(to.y) > Mathf.Abs(to.x) * 0.9f;
                w.Facing = vertical ? (to.y < 0f ? "back" : "front") : "right";
                w.Flip = !vertical && to.x < 0f;
                SetFrame(w, w.Facing, (int)w.Frame, w.Flip);
                Place(w);
            }
            sortTimer -= dt;
            if (sortTimer <= 0f)
            {
                sortTimer = 0.25f;
                foreach (var w in walkers.Where(x => x.Zone == zone).OrderBy(x => x.Pos.y))
                    w.Element.BringToFront();
            }
        }
    }
}
