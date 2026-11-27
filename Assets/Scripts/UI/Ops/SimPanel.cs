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
    // Zones with a block picture (BlockScene: Resources/Art/Blocks, Tools/Art/blocks_data.py, 2026-11-26) show that
    // picture: people find their way on its walk grid, things in front of them (trees, front buildings) are cut-out
    // layers sorted with them by depth, and a street screen shows the AI's line. Zones without one fall back to the
    // older Sim/<zone>_v2 scenes (768 x 432). Walkers use Sim/Sprites/<id>_<front|right|back><0-3>.
    public class SimPanel : VisualElement
    {
        // Older scenes: the panel shows the middle, zoomed 1.5x.
        const float OldW = 768f, OldH = 432f, OldViewW = 512f, OldViewH = 288f;
        static readonly Vector2 OldOffset = new Vector2((OldW - OldViewW) / 2f, (OldH - OldViewH) / 2f + 20f);
        // Block pictures: the whole width and a 16:9 band of the height; people drawn at 0.6 of their sprite size.
        const float BlockViewH = 864f, BlockTop = 90f, BlockSprite = 0.6f;

        public static readonly string[] ZoneFiles = Society.Zones.Select(z => z.File).ToArray();
        public static readonly string[] ZoneNames = Society.Zones.Select(z => z.Name).ToArray();

        // Where people may stand in each older zone (768 x 432 pixels; y is the foot row).
        static readonly Dictionary<string, Rect> WalkableByFile = new Dictionary<string, Rect>
        {
            ["plaza"] = new Rect(190f, 222f, 420f, 92f),
            ["cafe"] = new Rect(170f, 240f, 460f, 130f),
            ["lobby"] = new Rect(260f, 232f, 370f, 140f),
            ["homes"] = new Rect(300f, 270f, 320f, 90f),
            ["school"] = new Rect(200f, 250f, 380f, 90f),
            ["works"] = new Rect(220f, 205f, 400f, 120f),
            ["lab"] = new Rect(225f, 255f, 330f, 90f),
            ["clinic"] = new Rect(150f, 200f, 330f, 110f),
            ["police"] = new Rect(190f, 205f, 370f, 110f),
        };

        static readonly Rect[] Walkable = ZoneFiles.Select(f => WalkableByFile.TryGetValue(f, out var r) ? r : WalkableByFile["plaza"]).ToArray();

        const int GenericSprites = 6;
        const int AmbientPerBlock = 14;

        sealed class Walker
        {
            public string Id;
            public bool Fixed;
            public int Zone;
            public Vector2 Pos, Target;
            public List<Vector2> Path = new List<Vector2>();
            public float Wait;
            public float Frame;
            public string Facing = "front";
            public bool Flip;
            public VisualElement Element;
            public Label Bubble;
            public float BubbleLeft;
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
        readonly List<(VisualElement element, float baseY)> occluders = new List<(VisualElement, float)>();
        readonly Label screenLine;
        readonly Label hoverName;
        int zone;
        int zones = 1;
        string timeOfDay = "noon";
        float sortTimer;
        string line = "오늘도 힘찬 하루";
        float screenBase;   // the screen sits on a building: it is sorted just above that building

        public event Action<string> ResidentClicked;

        public int Zone => zone;

        BlockScene Block => BlockScene.ForZone(zone);
        float SceneW => Block != null ? BlockScene.Width : OldW;
        float SceneH => Block != null ? BlockScene.Height : OldH;
        float ViewW => Block != null ? BlockScene.Width : OldViewW;
        float ViewH => Block != null ? BlockViewH : OldViewH;
        Vector2 ViewOffset => Block != null ? new Vector2(0f, BlockTop) : OldOffset;
        static float SpriteScale(int z) => BlockScene.ForZone(z) != null ? BlockSprite : 1f;
        // labels inside the picture keep their on-screen size whatever the picture's scale
        static float TextScale(int z) => BlockScene.ForZone(z) != null ? BlockScene.Width / OldViewW : 1f;

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
            screenLine = new Label { pickingMode = PickingMode.Ignore };
            screenLine.AddToClassList("sim-screen-line");
            layer.Add(screenLine);
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
            layer.style.width = SceneW;
            layer.style.height = SceneH;
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
            BuildOccluders();
            caption.text = $"SIM · {ZoneNames[zone]}";
            foreach (var w in walkers)
                w.Element.style.display = w.Zone == zone ? DisplayStyle.Flex : DisplayStyle.None;
            MarkTabs();
            Layout();
            sortTimer = 0f;
        }

        // The picture's things that stand in front of people (cut out by blocks_data.py as <key>_occ<i>).
        void BuildOccluders()
        {
            foreach (var (element, _) in occluders)
                element.RemoveFromHierarchy();
            occluders.Clear();
            var block = Block;
            screenLine.style.display = DisplayStyle.None;
            if (block == null)
                return;
            for (int i = 0; i < block.Occ.Count; i++)
            {
                var r = block.Occ[i];
                var texture = PixelArt.Load($"Blocks/{block.Key}_occ{i}");
                if (texture == null)
                    continue;
                var e = new VisualElement { pickingMode = PickingMode.Ignore };
                e.AddToClassList("sim-occ");
                e.style.position = Position.Absolute;
                e.style.left = r.xMin;
                e.style.top = r.yMin;
                e.style.width = r.width;
                e.style.height = r.height;
                e.style.backgroundImage = new StyleBackground(texture);
                layer.Add(e);
                occluders.Add((e, r.yMax));
            }
            if (block.Screen is RectInt s)
            {
                screenLine.style.display = DisplayStyle.Flex;
                screenLine.style.left = s.x;
                screenLine.style.top = s.y;
                screenLine.style.width = s.width;
                screenLine.style.height = s.height;
                screenLine.style.fontSize = Mathf.Clamp(s.height * 0.24f, 12f, 18f);
                screenLine.text = line;
                screenBase = s.y + s.height;
                foreach (var r in block.Occ)
                    if (r.Contains(new Vector2Int(s.x + s.width / 2, s.y + s.height / 2)))
                        screenBase = r.yMax + 0.5f;
            }
            TintOccluders();
        }

        // The AI's line on the block's street screen ("오늘도 힘찬 하루" until something else is said).
        public void SetScreenLine(string text)
        {
            line = string.IsNullOrEmpty(text) ? "오늘도 힘찬 하루" : text;
            screenLine.text = line;
        }

        // morning / noon / evening, from how far through the day the clock is.
        public void SetTime(float dayFraction)
        {
            var slot = dayFraction < 0.33f ? "morning" : dayFraction < 0.7f ? "noon" : "evening";
            if (slot == timeOfDay)
                return;
            timeOfDay = slot;
            UpdateBackdrop();
            TintOccluders();
        }

        Color Tint()
        {
            var block = Block;
            if (block != null && timeOfDay == "evening" && PixelArt.Load($"Blocks/{block.Key}_night") != null)
                return Color.white;   // a painted night needs no tint
            if (block != null)
                return timeOfDay == "morning" ? new Color(0.95f, 0.93f, 0.9f) : timeOfDay == "noon" ? Color.white : new Color(0.55f, 0.6f, 0.82f);
            return timeOfDay == "morning" ? new Color(0.86f, 0.84f, 0.82f)
                : timeOfDay == "noon" ? new Color(0.82f, 0.85f, 0.9f) : new Color(0.7f, 0.74f, 0.88f);
        }

        void UpdateBackdrop()
        {
            Texture2D texture;
            var block = Block;
            if (block != null)
                texture = (timeOfDay == "evening" ? PixelArt.Load($"Blocks/{block.Key}_night") : null) ?? PixelArt.Load($"Blocks/{block.Key}");
            else
                texture = PixelArt.Load($"Sim/{ZoneFiles[zone]}_v2") ?? PixelArt.Load($"Sim/{ZoneFiles[zone]}_{timeOfDay}");
            layer.style.backgroundImage = texture != null ? new StyleBackground(texture) : StyleKeyword.Null;
            layer.style.unityBackgroundImageTintColor = Tint();
        }

        void TintOccluders()
        {
            // occluders are cut from the day picture; at night (painted or not) they are shaded to match
            var block = Block;
            bool painted = block != null && timeOfDay == "evening" && PixelArt.Load($"Blocks/{block.Key}_night") != null;
            for (int i = 0; i < occluders.Count; i++)
            {
                var e = occluders[i].element;
                if (painted)
                {
                    var night = PixelArt.Load($"Blocks/{block.Key}_night_occ{i}");
                    e.style.backgroundImage = night != null ? new StyleBackground(night) : new StyleBackground(PixelArt.Load($"Blocks/{block.Key}_occ{i}"));
                    e.style.unityBackgroundImageTintColor = night != null ? Color.white : new Color(0.45f, 0.5f, 0.72f);
                }
                else
                    e.style.unityBackgroundImageTintColor = Tint();
            }
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
            // block pictures are big: a few passers-by per open zone so the streets don't look empty
            for (int z = 0; z < zones; z++)
                if (BlockScene.ForZone(z) != null)
                    for (int i = 0; i < AmbientPerBlock; i++)
                        wanted.Add(($"s{(i + z) % GenericSprites + 1}", false, z));

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
            var block = BlockScene.ForZone(z);
            Vector2 pos;
            if (block != null)
                pos = block.RandomSpot();
            else
            {
                var area = Walkable[z];
                pos = new Vector2(Random.Range(area.xMin, area.xMax), Random.Range(area.yMin, area.yMax));
            }
            var w = new Walker { Id = id, Fixed = isFixed, Zone = z, Pos = pos, Target = pos, Wait = Random.Range(0f, 3f), Frame = Random.value * 4f };
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
            float ts = TextScale(z);
            if (ts != 1f)
            {
                // the picture is drawn smaller on screen than the old scenes, so labels are sized up to match
                w.Bubble.style.fontSize = 12f * ts;
                w.Bubble.style.width = 220f * ts;
                w.Bubble.style.marginLeft = -110f * ts;
                w.Bubble.style.marginBottom = 12f * ts;
                w.Tag.style.fontSize = 8f * ts;
            }
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
                float s = SpriteScale(w.Zone);
                w.Element.style.width = texture.width * s;
                w.Element.style.height = texture.height * s;
            }
            w.Element.style.scale = new Scale(new Vector3(flip ? -1f : 1f, 1f, 1f));
            w.Bubble.style.scale = new Scale(new Vector3(flip ? -1f : 1f, 1f, 1f));
            w.Tag.style.scale = new Scale(new Vector3(flip ? -1f : 1f, 1f, 1f));
        }

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

        public void SetZoneStages(Func<int, int> stageOf)
        {
            for (int i = 0; i < tabs.childCount; i++)
            {
                int stage = stageOf(i);
                tabs[i].EnableInClassList("sim-tab--warn", stage == 2);
                tabs[i].EnableInClassList("sim-tab--bad", stage >= 3);
            }
        }

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

        void PickTarget(Walker w)
        {
            var block = BlockScene.ForZone(w.Zone);
            if (block != null)
            {
                w.Path = block.Path(w.Pos, block.RandomSpot());
                w.Target = w.Path.Count > 0 ? w.Path[w.Path.Count - 1] : w.Pos;
            }
            else
            {
                var area = Walkable[w.Zone];
                w.Path.Clear();
                w.Target = new Vector2(Random.Range(area.xMin, area.xMax), Random.Range(area.yMin, area.yMax));
                w.Path.Add(w.Target);
            }
        }

        void Step(float dt)
        {
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
                if (w.Wait > 0f)
                {
                    w.Wait -= dt;
                    if (w.Wait <= 0f)
                        PickTarget(w);
                    SetFrame(w, w.Facing, 0, w.Flip);
                    continue;
                }
                if (w.Path.Count == 0)
                {
                    w.Wait = Random.Range(1.5f, 5f);
                    // standing still, people face the camera
                    w.Facing = "front";
                    w.Flip = false;
                    continue;
                }
                // speed in picture pixels; block pictures are drawn at a larger scale than the old scenes
                float speed = BlockScene.ForZone(w.Zone) != null ? 40f : 34f;
                var to = w.Path[0] - w.Pos;
                if (to.magnitude < 2f)
                {
                    w.Path.RemoveAt(0);
                    continue;
                }
                float step = Mathf.Min(speed * dt, to.magnitude);
                w.Pos += to.normalized * step;
                // one walking frame per few pixels walked, so feet never slide
                w.Frame += step / (BlockScene.ForZone(w.Zone) != null ? 7f : 6f);
                bool vertical = Mathf.Abs(to.y) > Mathf.Abs(to.x) * 1.2f;
                w.Facing = vertical ? (to.y < 0f ? "back" : "front") : "right";
                w.Flip = !vertical && to.x < 0f;
                SetFrame(w, w.Facing, (int)w.Frame, w.Flip);
                Place(w);
            }
            sortTimer -= dt;
            if (sortTimer <= 0f)
            {
                sortTimer = 0.1f;
                // people and the picture's cut-out layers, back to front by where they stand
                var order = walkers.Where(x => x.Zone == zone).Select(x => (x.Element, x.Pos.y))
                    .Concat(occluders.Select(o => (o.element, o.baseY)))
                    .Append((screenLine, screenBase))
                    .OrderBy(x => x.Item2);
                foreach (var (element, _) in order)
                    element.BringToFront();
            }
        }
    }
}
