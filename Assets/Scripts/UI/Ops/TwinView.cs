using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;

namespace Sector.UI
{
    // The observation system's city view: a digital twin drawn in code. Districts are outlines, people are moving
    // points (one per few hundred residents), tense districts glow and anomalies pulse. Quiet by default: names and
    // numbers appear only for the district under the pointer. Clicking a district asks to zoom into its camera.
    public class TwinView : VisualElement
    {
        sealed class Dot
        {
            public int Zone;
            public Vector2 Pos, Target;
            public float Speed;
        }

        readonly Vector2[][] shapes;          // 0..100 grid
        readonly Func<int, int> stageOf;      // tension stage per district
        readonly Func<int, int> anomaliesOf;  // anomalies per district
        readonly Func<int, bool> openOf;
        readonly Func<int, int> peopleOf;     // dots per district
        readonly Func<int, string> labelOf;
        readonly List<Dot> dots = new List<Dot>();
        readonly Label hover;
        int hoverZone = -1;
        float time;

        public event Action<int> ZoneClicked;

        static readonly Color Line = new Color(0.21f, 0.79f, 0.91f, 0.55f);
        static readonly Color Warn = new Color32(242, 182, 93, 255);
        static readonly Color Bad = new Color32(241, 109, 121, 255);

        public TwinView(Vector2[][] shapes, Func<int, int> stageOf, Func<int, int> anomaliesOf, Func<int, bool> openOf,
            Func<int, int> peopleOf, Func<int, string> labelOf)
        {
            this.shapes = shapes;
            this.stageOf = stageOf;
            this.anomaliesOf = anomaliesOf;
            this.openOf = openOf;
            this.peopleOf = peopleOf;
            this.labelOf = labelOf;
            AddToClassList("twin");
            generateVisualContent += Draw;
            hover = new Label { pickingMode = PickingMode.Ignore };
            hover.AddToClassList("twin-hover");
            hover.style.display = DisplayStyle.None;
            Add(hover);
            RegisterCallback<PointerMoveEvent>(e =>
            {
                int z = ZoneAt(e.localPosition);
                hoverZone = z;
                if (z < 0)
                {
                    hover.style.display = DisplayStyle.None;
                    return;
                }
                hover.text = labelOf(z);
                hover.style.display = DisplayStyle.Flex;
                hover.style.left = e.localPosition.x + 14f;
                hover.style.top = e.localPosition.y + 10f;
            });
            RegisterCallback<PointerLeaveEvent>(_ =>
            {
                hoverZone = -1;
                hover.style.display = DisplayStyle.None;
            });
            RegisterCallback<ClickEvent>(e =>
            {
                int z = ZoneAt(e.localPosition);
                if (z >= 0 && openOf(z))
                    ZoneClicked?.Invoke(z);
            });
            // Small, dim district names so the map reads at a glance without shouting.
            for (int z = 0; z < shapes.Length; z++)
            {
                var name = new Label(labelOf(z).Split(' ')[0]) { pickingMode = PickingMode.Ignore };
                name.AddToClassList("twin-name");
                name.style.left = Length.Percent(shapes[z].Min(v => v.x) + 1.5f);
                name.style.top = Length.Percent(shapes[z].Min(v => v.y) + 1.5f);
                Add(name);
            }
            hover.BringToFront();
            Populate();
            schedule.Execute(() => Step(0.05f)).Every(50);
        }

        Vector2 At(Vector2 v) => new Vector2(contentRect.width * v.x / 100f, contentRect.height * v.y / 100f);

        int ZoneAt(Vector2 local)
        {
            var r = contentRect;
            if (r.width <= 0f)
                return -1;
            var p = new Vector2(local.x / r.width * 100f, local.y / r.height * 100f);
            for (int z = 0; z < shapes.Length; z++)
                if (Inside(shapes[z], p))
                    return z;
            return -1;
        }

        static bool Inside(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        Vector2 RandomIn(int zone)
        {
            var s = shapes[zone];
            float x0 = s.Min(v => v.x), x1 = s.Max(v => v.x), y0 = s.Min(v => v.y), y1 = s.Max(v => v.y);
            for (int i = 0; i < 12; i++)
            {
                var p = new Vector2(Random.Range(x0 + 2f, x1 - 2f), Random.Range(y0 + 2f, y1 - 2f));
                if (Inside(s, p))
                    return p;
            }
            return new Vector2(s.Average(v => v.x), s.Average(v => v.y));
        }

        void Populate()
        {
            dots.Clear();
            for (int z = 0; z < shapes.Length; z++)
            {
                if (!openOf(z))
                    continue;
                int n = peopleOf(z);
                for (int i = 0; i < n; i++)
                {
                    var p = RandomIn(z);
                    dots.Add(new Dot { Zone = z, Pos = p, Target = RandomIn(z), Speed = Random.Range(1.5f, 4f) });
                }
            }
        }

        void Step(float dt)
        {
            time += dt;
            foreach (var d in dots)
            {
                // In tense districts people drift together toward one spot (a gathering forming).
                var target = stageOf(d.Zone) >= 3 ? Center(d.Zone) + (d.Target - Center(d.Zone)) * 0.25f : d.Target;
                var to = target - d.Pos;
                if (to.magnitude < 0.6f)
                    d.Target = RandomIn(d.Zone);
                else
                    d.Pos += to.normalized * Mathf.Min(d.Speed * dt, to.magnitude);
            }
            MarkDirtyRepaint();
        }

        Vector2 Center(int zone) => new Vector2(shapes[zone].Average(v => v.x), shapes[zone].Average(v => v.y));

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width <= 0f)
                return;
            var p = ctx.painter2D;
            // grid
            p.strokeColor = new Color(0.47f, 0.75f, 0.96f, 0.06f);
            p.lineWidth = 1f;
            for (int i = 1; i < 20; i++)
            {
                p.BeginPath();
                p.MoveTo(new Vector2(r.width * i / 20f, 0f));
                p.LineTo(new Vector2(r.width * i / 20f, r.height));
                p.Stroke();
                p.BeginPath();
                p.MoveTo(new Vector2(0f, r.height * i / 20f));
                p.LineTo(new Vector2(r.width, r.height * i / 20f));
                p.Stroke();
            }
            for (int z = 0; z < shapes.Length; z++)
            {
                bool open = openOf(z);
                int stage = open ? stageOf(z) : 0;
                var glow = stage >= 3 ? Bad : stage == 2 ? Warn : Line;
                p.BeginPath();
                p.MoveTo(At(shapes[z][0]));
                for (int i = 1; i < shapes[z].Length; i++)
                    p.LineTo(At(shapes[z][i]));
                p.ClosePath();
                float pulse = 0.5f + 0.5f * Mathf.Sin(time * 2.5f + z);
                p.fillColor = new Color(glow.r, glow.g, glow.b, !open ? 0.0f : stage >= 2 ? 0.08f + 0.06f * pulse : 0.0f);
                p.Fill();
                p.strokeColor = !open ? new Color(0.6f, 0.62f, 0.68f, 0.3f)
                    : z == hoverZone ? new Color(0.6f, 0.95f, 1f, 1f) : new Color(glow.r, glow.g, glow.b, stage >= 2 ? 0.8f : 0.35f);
                p.lineWidth = z == hoverZone ? 2.2f : 1.2f;
                p.Stroke();
            }
            foreach (var d in dots)
            {
                int stage = stageOf(d.Zone);
                var c = stage >= 3 ? Bad : stage == 2 ? Warn : new Color(0.55f, 0.85f, 1f);
                p.fillColor = new Color(c.r, c.g, c.b, 0.85f);
                p.BeginPath();
                p.Arc(At(d.Pos), 1.6f, 0f, 360f);
                p.Fill();
            }
            for (int z = 0; z < shapes.Length; z++)
            {
                int n = openOf(z) ? anomaliesOf(z) : 0;
                if (n == 0)
                    continue;
                var c = At(Center(z));
                float k = (time * 0.8f + z * 0.3f) % 1f;
                var col = stageOf(z) >= 4 ? Bad : Warn;
                p.strokeColor = new Color(col.r, col.g, col.b, 0.9f * (1f - k));
                p.lineWidth = 2f;
                p.BeginPath();
                p.Arc(c, 6f + k * 22f, 0f, 360f);
                p.Stroke();
                p.fillColor = col;
                p.BeginPath();
                p.Arc(c, 4f, 0f, 360f);
                p.Fill();
            }
        }
    }
}
