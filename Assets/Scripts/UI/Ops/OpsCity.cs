using System;
using System.Collections.Generic;
using System.Linq;
using Sector.Ops;
using UnityEngine;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;

namespace Sector.UI
{
    // The living city on the overview: zone markers tied to the ground (dot, leader line, data tag), data lines from
    // the AI operations center to the zones it runs, ripples where events happen, a slow camera drift, a light shift
    // with the time of day, drone lights in the sky and an occasional scan line. Also the count-up of numbers.
    public partial class OpsScreen
    {
        const float LeaderLength = 30f;
        // The AI operations center (the tower with the cyan ring, the 관리 district's pin) in image fractions.
        static readonly Vector2 CenterPin = new Vector2(0.507f, 0.41f);

        sealed class ZoneMarker
        {
            public int Zone;
            public VisualElement Tag;
            public Label Title, Sub;
            public Label State;
            public Vector2 Anchor;
            public bool Open;
            public bool InView;   // the pin is inside the clear window
            public bool Shown;    // its name tag is up (see TickMap)
        }

        sealed class Ripple
        {
            public int Zone;
            public float Age;
            public Color Color;
        }

        readonly List<ZoneMarker> markers = new List<ZoneMarker>();
        readonly List<Ripple> ripples = new List<Ripple>();
        readonly List<(VisualElement dot, Vector2 from, Vector2 to, float speed, float phase)> drones =
            new List<(VisualElement, Vector2, Vector2, float, float)>();
        VisualElement cityCanvas;
        float cityTime;
        float lightLevel = 1f;
        Color baseTint = new Color32(178, 182, 196, 255);

        // Count-up: every animated label moves toward its target a little each frame.
        readonly Dictionary<Label, (float shown, float target, Func<float, string> format)> counters =
            new Dictionary<Label, (float, float, Func<float, string>)>();

        void Count(Label label, float target, Func<float, string> format)
        {
            if (counters.TryGetValue(label, out var c))
                counters[label] = (c.shown, target, format);
            else
            {
                counters[label] = (target, target, format);
                label.text = format(target);
            }
        }

        // ------------------------------------------------------------------ build

        void BuildLivingCity(VisualElement window)
        {
            cityCanvas = new VisualElement { pickingMode = PickingMode.Ignore };
            cityCanvas.AddToClassList("city-canvas");
            cityCanvas.generateVisualContent += DrawCity;
            zoneLayer.Add(cityCanvas);
            for (int z = 0; z < Society.Zones.Count; z++)
            {
                int zone = z;
                var m = new ZoneMarker { Zone = z };
                m.Tag = Add(zoneLayer, "zone-tag");
                m.Tag.RegisterCallback<ClickEvent>(e =>
                {
                    sfx.Play(Cue.Select);
                    OpenZoneCard(zone);
                    e.StopPropagation();
                });
                m.Title = Text(m.Tag, Society.Zones[z].Name, "zone-tag-title");
                var row = Add(m.Tag, "zone-tag-row");
                m.Sub = Text(row, "", "zone-tag-sub");
                m.State = Text(row, "", "state-tag");
                markers.Add(m);
            }

            // Drone lights drift across the upper sky of the painting (they move with the camera).
            for (int i = 0; i < 7; i++)
            {
                var dot = Add(cityLayers[0], "drone");
                dot.pickingMode = PickingMode.Ignore;
                float y = Random.Range(6f, 26f);
                bool right = Random.value > 0.5f;
                drones.Add((dot, new Vector2(right ? -5f : 105f, y), new Vector2(right ? 105f : -5f, y + Random.Range(-6f, 6f)),
                    Random.Range(0.012f, 0.03f), Random.value));
            }

            root.schedule.Execute(CityTick).Every(33);
        }

        // Called from Refresh: marker texts and states (positions move every frame in CityTick).
        void RefreshZones()
        {
            if (markers.Count == 0)
                return;
            // Quiet markers: the name only; the stage shows once it matters (수군거림 and up), the details on hover.
            foreach (var m in markers)
            {
                m.Open = session.ZoneOpen(m.Zone);
                bool alarm = ripples.Any(r => r.Zone == m.Zone && r.Color == Bad);
                m.Tag.EnableInClassList("zone-tag--alarm", alarm);
                m.Tag.EnableInClassList("zone-tag--locked", !m.Open);
                m.Sub.style.display = alarm ? DisplayStyle.Flex : DisplayStyle.None;
                m.Sub.text = "사건 발생";
                if (!m.Open)
                {
                    m.State.text = "공사 중";
                    m.State.style.visibility = Visibility.Visible;
                    continue;
                }
                SetStage(m.State, m.Zone);
                if (session.TensionStage(m.Zone) < 2)
                    m.State.style.visibility = Visibility.Hidden;
            }
        }

        // An event lands somewhere: the zone where its group lives most, among the zones the AI runs.
        void RippleForEvent(EventDef def)
        {
            int zone = session.PendingZone >= 0 ? session.PendingZone : Crises.Get(def.Id)?.Zone ?? -1;
            if (zone < 0 && Crises.Get(def.Id) != null)
            {
                RippleAll(Bad);
                return;
            }
            if (zone >= 0)
            {
                for (int i = 0; i < 3; i++)
                    ripples.Add(new Ripple { Zone = zone, Age = -i * 0.6f, Color = Bad });
                RefreshZones();
                return;
            }
            zone = 0;
            if (Society.EventGroups.TryGetValue(def.Id, out var group))
            {
                float best = -1f;
                for (int z = 0; z < session.Zones && z < Society.Zones.Count; z++)
                {
                    float share = Society.Zones[z].Mix[(int)group];
                    if (share > best)
                    {
                        best = share;
                        zone = z;
                    }
                }
            }
            for (int i = 0; i < 3; i++)
                ripples.Add(new Ripple { Zone = zone, Age = -i * 0.6f, Color = Bad });
            RefreshZones();
        }

        void RippleAll(Color color)
        {
            for (int z = 0; z < session.Zones && z < Society.Zones.Count; z++)
                ripples.Add(new Ripple { Zone = z, Age = 0f, Color = color });
        }

        // ------------------------------------------------------------------ per frame

        void CityTick(TimerState t)
        {
            float dt = Mathf.Min(0.1f, t.deltaTime / 1000f);
            cityTime += dt;
            TickCounters(dt);
            if (session == null || cityView == null)
                return;

            TickPanZoom(dt);

            // Light follows the time of day, softly (eased, so fast speeds blur into a gentle drift).
            float day = Mathf.Clamp01(dayClock / DaySeconds);
            float target = 0.9f + 0.14f * Mathf.Clamp01(Mathf.Sin(day * Mathf.PI));
            lightLevel = Mathf.Lerp(lightLevel, target, dt * 0.35f);
            var tint = new Color(baseTint.r * lightLevel, baseTint.g * lightLevel, Mathf.Min(1f, baseTint.b * (lightLevel * 0.9f + 0.1f)), 1f);
            foreach (var layer in cityLayers)
                layer.style.unityBackgroundImageTintColor = tint;
            TickDistrictLayers(dt, tint);

            foreach (var (dot, from, to, speed, phase) in drones)
            {
                float p = (phase + cityTime * speed) % 1f;
                var at = Vector2.Lerp(from, to, p);
                dot.style.left = Length.Percent(at.x);
                dot.style.top = Length.Percent(at.y);
                dot.style.opacity = 0.35f + 0.65f * (Mathf.Sin(cityTime * 6f + phase * 20f) > 0.6f ? 1f : 0.3f);
            }

            if (currentPage != Page.Overview)
                return;

            foreach (var m in markers)
            {
                m.Anchor = PinAt(ZonePins[m.Zone]);
                m.Tag.style.left = m.Anchor.x;
                m.Tag.style.top = m.Anchor.y - LeaderLength;
            }
            TickMap();
            for (int i = ripples.Count - 1; i >= 0; i--)
            {
                ripples[i].Age += dt;
                if (ripples[i].Age > 3.2f)
                {
                    ripples.RemoveAt(i);
                    RefreshZones();
                }
            }
            cityCanvas.MarkDirtyRepaint();
        }

        void TickCounters(float dt)
        {
            if (counters.Count == 0)
                return;
            foreach (var label in counters.Keys.ToList())
            {
                var (shown, target, format) = counters[label];
                if (Mathf.Abs(shown - target) < 0.01f)
                    continue;
                shown = Mathf.Abs(target - shown) < 0.05f ? target : Mathf.Lerp(shown, target, Mathf.Min(1f, dt * 7f));
                counters[label] = (shown, target, format);
                label.text = format(shown);
            }
        }

        // ------------------------------------------------------------------ pan and zoom

        // Zoom 1 shows the whole painting (the districts plus the city around them); HomeZoom frames the districts
        // (the painting's middle 75%).
        const float MinZoom = 1f, HomeZoom = 1.2f, MaxZoom = 3.4f;
        float zoom = HomeZoom, zoomTarget = HomeZoom;

        void ResetView()
        {
            zoomTarget = HomeFor;
            // The full map sits a little low so the top districts clear the layer buttons.
            panTarget = mapMode ? new Vector2(0f, root.contentRect.height * 0.05f) : Vector2.zero;
        }

        // Slide the painting so a district's pin comes to the middle of the clear window.
        void CenterOn(int zone)
        {
            var window = zoneLayer.contentRect;
            panTarget += window.center - PinAt(ZonePins[zone]);
        }
        Vector2 pan, panTarget;
        bool dragging;
        Vector2 dragLast, dragStart;

        void BuildPanZoom()
        {
            cityView.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0 || currentPage != Page.Overview)
                    return;
                if (e.clickCount == 2)
                {
                    ResetView();
                    return;
                }
                dragging = true;
                dragLast = e.position;
                dragStart = e.position;
                cityView.CapturePointer(e.pointerId);
            });
            cityView.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!dragging)
                    return;
                var p = (Vector2)e.position;
                panTarget += p - dragLast;
                pan += p - dragLast;  // follow the hand directly while dragging
                dragLast = p;
            });
            cityView.RegisterCallback<PointerUpEvent>(e =>
            {
                bool wasDragging = dragging;
                dragging = false;
                cityView.ReleasePointer(e.pointerId);
                if (wasDragging && e.button == 0 && ((Vector2)e.position - dragStart).magnitude < 6f && currentPage == Page.Overview)
                    OnMapClick(e.position);
            });
            cityView.RegisterCallback<WheelEvent>(e =>
            {
                if (currentPage != Page.Overview)
                    return;
                float next = Mathf.Clamp(zoomTarget * (e.delta.y > 0 ? 0.88f : 1.14f), MinZoom, MaxZoom);
                // keep the point under the cursor in place
                var r = root.contentRect;
                var cursor = (Vector2)e.mousePosition - new Vector2(r.width / 2f, r.height / 2f);
                panTarget = cursor - (cursor - panTarget) * (next / zoomTarget);
                zoomTarget = next;
                e.StopPropagation();
            });
        }

        // Size the painting to its aspect so it covers the screen, then apply zoom and pan within the edges.
        void TickPanZoom(float dt)
        {
            var screen = root.contentRect;
            if (screen.width <= 0f)
                return;
            // At zoom 1 the whole painting just covers the screen.
            float w = Mathf.Max(screen.width, screen.height * CityAspect), h = w / CityAspect;
            cityView.style.width = w;
            cityView.style.height = h;
            cityView.style.left = (screen.width - w) / 2f;
            cityView.style.top = (screen.height - h) / 2f;

            zoom = Mathf.Lerp(zoom, zoomTarget, Mathf.Min(1f, dt * 10f));
            // The painting's edge may slide in as far as the clear window's edge, so a district on the rim can reach the
            // window; what shows past the edge stays behind the side panels and the top bar.
            var window = root.WorldToLocal(zoneLayer.worldBound);
            float slackL = Mathf.Max(0f, window.xMin - screen.xMin), slackR = Mathf.Max(0f, screen.xMax - window.xMax);
            float slackT = Mathf.Max(0f, window.yMin - screen.yMin), slackB = Mathf.Max(0f, screen.yMax - window.yMax);
            if (float.IsNaN(window.width) || window.width <= 0f || currentPage != Page.Overview)
                slackL = slackR = slackT = slackB = 0f;
            Vector2 Clamp(Vector2 v, float z) => new Vector2(
                Mathf.Clamp(v.x, -(w * z - screen.width) / 2f - slackR, (w * z - screen.width) / 2f + slackL),
                Mathf.Clamp(v.y, -(h * z - screen.height) / 2f - slackB, (h * z - screen.height) / 2f + slackT));
            panTarget = Clamp(panTarget, zoomTarget);
            pan = dragging ? Clamp(pan, zoom) : Vector2.Lerp(pan, panTarget, Mathf.Min(1f, dt * 10f));
            pan = Clamp(pan, zoom);
            cityView.style.scale = new Scale(new Vector3(zoom, zoom, 1f));
            cityView.style.translate = new Translate(pan.x, pan.y);
        }

        // ------------------------------------------------------------------ drawing

        void DrawCity(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            if (markers.Count == 0)
                return;
            var center = PinAt(CenterPin);

            // Data lines from the operations center, only where it matters (rising tension, an event, the district in focus).
            foreach (var m in markers)
            {
                if (m.Zone == 2 || !m.Open || !m.Shown)
                    continue;
                bool alarm = ripples.Any(r => r.Zone == m.Zone && r.Color == Bad);
                if (!alarm && m.Zone != hoverZone && m.Zone != selectedZone && session.TensionStage(m.Zone) < 2)
                    continue;
                var color = !m.Open ? new Color(0.6f, 0.68f, 0.8f, 0.18f) : alarm ? new Color(Bad.r, Bad.g, Bad.b, 0.8f) : new Color(0.21f, 0.79f, 0.91f, 0.7f);
                Dashes(p, center, m.Anchor, color, m.Open ? cityTime * 40f : 0f);
            }

            // Pins (ground dot, leader line, pulse) only on the full map; elsewhere the boundaries carry the districts.
            foreach (var m in markers)
            {
                if (!m.Shown)
                    continue;
                var a = m.Anchor;
                int stage = m.Open ? session.TensionStage(m.Zone) : 0;
                var c = !m.Open ? new Color(0.6f, 0.65f, 0.72f) : stage >= 2 ? StageColor(stage) : Palette.Cyan;
                if (m.Shown)
                {
                    p.strokeColor = new Color(c.r, c.g, c.b, m.Open ? 0.8f : 0.4f);
                    p.lineWidth = 1.2f;
                    p.BeginPath();
                    p.MoveTo(a);
                    p.LineTo(a + new Vector2(0f, -LeaderLength + 6f));
                    p.Stroke();
                }
                // pulse ring
                if (m.Open && m.Shown)
                {
                    float pulse = (cityTime * 0.6f + m.Zone * 0.33f) % 1f;
                    p.strokeColor = new Color(c.r, c.g, c.b, 0.6f * (1f - pulse));
                    p.lineWidth = 1.5f;
                    p.BeginPath();
                    p.Arc(a, 6f + pulse * 16f, 0f, 360f);
                    p.Stroke();
                }
                // ground dot
                p.fillColor = new Color(c.r, c.g, c.b, 0.25f);
                p.BeginPath();
                p.Arc(a, 7f, 0f, 360f);
                p.Fill();
                p.fillColor = c;
                p.BeginPath();
                p.Arc(a, 3.5f, 0f, 360f);
                p.Fill();
            }

            foreach (var r in ripples)
            {
                if (r.Age < 0f || r.Zone >= markers.Count)
                    continue;
                float k = Mathf.Clamp01(r.Age / 1.6f);
                p.strokeColor = new Color(r.Color.r, r.Color.g, r.Color.b, 0.9f * (1f - k));
                p.lineWidth = 2.5f;
                p.BeginPath();
                p.Arc(markers[r.Zone].Anchor, 8f + k * 70f, 0f, 360f);
                p.Stroke();
            }
        }

        static void Dashes(Painter2D p, Vector2 from, Vector2 to, Color color, float offset)
        {
            var dir = to - from;
            float length = dir.magnitude;
            if (length < 1f)
                return;
            dir /= length;
            const float dash = 8f, gap = 6f;
            p.strokeColor = color;
            p.lineWidth = 1.5f;
            for (float d = -(offset % (dash + gap)); d < length; d += dash + gap)
            {
                float a = Mathf.Max(0f, d), b = Mathf.Min(length, d + dash);
                if (b <= a)
                    continue;
                p.BeginPath();
                p.MoveTo(from + dir * a);
                p.LineTo(from + dir * b);
                p.Stroke();
            }
        }
    }
}
