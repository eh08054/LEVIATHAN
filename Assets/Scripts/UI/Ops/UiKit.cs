using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sector.UI
{
    // The UI's key accent (cyan), for code-drawn shapes; USS uses var(--accent).
    public static class Palette
    {
        public static readonly Color Cyan = new Color32(64, 208, 224, 255);
    }

    // The AI's face where an event has no resident speaker: an open ring, a faint inner ring and a glowing core.
    public class AiEmblem : VisualElement
    {
        public AiEmblem()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            float size = Mathf.Min(r.width, r.height);
            if (size <= 0f)
                return;
            var c = r.center;
            float radius = size * 0.36f;
            var p = ctx.painter2D;
            p.lineCap = LineCap.Round;

            p.lineWidth = Mathf.Max(2f, size * 0.035f);
            p.strokeColor = Palette.Cyan;
            p.BeginPath();
            p.Arc(c, radius, 200f, 520f);
            p.Stroke();

            p.lineWidth = Mathf.Max(1f, size * 0.015f);
            p.strokeColor = new Color(Palette.Cyan.r, Palette.Cyan.g, Palette.Cyan.b, 0.35f);
            p.BeginPath();
            p.Arc(c, radius * 0.66f, 0f, 360f);
            p.Stroke();

            p.fillColor = new Color(Palette.Cyan.r, Palette.Cyan.g, Palette.Cyan.b, 0.18f);
            p.BeginPath();
            p.Arc(c, radius * 0.34f, 0f, 360f);
            p.Fill();
            p.fillColor = Palette.Cyan;
            p.BeginPath();
            p.Arc(c, radius * 0.16f, 0f, 360f);
            p.Fill();
        }
    }

    public enum IconKind
    {
        Smile, Shield, Link, Eye, Target, Bolt, Data, Policy, Research, Facility, People, Alert, Info, Star,
        Log, Save, Reset, Pin, Gauge,
    }

    // Thin line icons drawn with Painter2D on a 24 x 24 grid, so they stay sharp at any size.
    public class LineIcon : VisualElement
    {
        IconKind kind;
        Color color;

        public LineIcon(IconKind kind, Color? color = null)
        {
            this.kind = kind;
            this.color = color ?? Palette.Cyan;
            AddToClassList("icon");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void Set(IconKind next, Color? tint = null)
        {
            kind = next;
            if (tint.HasValue)
                color = tint.Value;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            var r = contentRect;
            float s = Mathf.Min(r.width, r.height) / 24f;
            var o = new Vector2((r.width - 24f * s) / 2f, (r.height - 24f * s) / 2f);
            Vector2 P(float x, float y) => o + new Vector2(x, y) * s;
            var p = context.painter2D;
            p.strokeColor = color;
            p.fillColor = color;
            p.lineWidth = Mathf.Max(1.2f, 1.7f * s);
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;

            void Line(params float[] xy)
            {
                p.BeginPath();
                p.MoveTo(P(xy[0], xy[1]));
                for (int i = 2; i < xy.Length; i += 2)
                    p.LineTo(P(xy[i], xy[i + 1]));
                p.Stroke();
            }
            void Closed(params float[] xy)
            {
                p.BeginPath();
                p.MoveTo(P(xy[0], xy[1]));
                for (int i = 2; i < xy.Length; i += 2)
                    p.LineTo(P(xy[i], xy[i + 1]));
                p.ClosePath();
                p.Stroke();
            }
            void Circle(float x, float y, float radius, bool fill = false)
            {
                p.BeginPath();
                p.Arc(P(x, y), radius * s, 0f, 360f);
                if (fill)
                    p.Fill();
                else
                    p.Stroke();
            }
            void Arc(float x, float y, float radius, float from, float to)
            {
                p.BeginPath();
                p.Arc(P(x, y), radius * s, from, to);
                p.Stroke();
            }

            switch (kind)
            {
                case IconKind.Smile:
                    Circle(12, 12, 9);
                    Circle(9, 10, 1.1f, true);
                    Circle(15, 10, 1.1f, true);
                    Arc(12, 12, 5, 25, 155);
                    break;
                case IconKind.Shield:
                    Closed(12, 3, 19.5f, 6, 19, 13, 12, 21, 5, 13, 4.5f, 6);
                    Line(8.5f, 12, 11, 14.5f, 15.5f, 9.5f);
                    break;
                case IconKind.Link:
                    Circle(9, 12, 5);
                    Circle(15, 12, 5);
                    break;
                case IconKind.Eye:
                    p.BeginPath();
                    p.MoveTo(P(2.5f, 12));
                    p.QuadraticCurveTo(P(12, 3), P(21.5f, 12));
                    p.QuadraticCurveTo(P(12, 21), P(2.5f, 12));
                    p.Stroke();
                    Circle(12, 12, 3);
                    break;
                case IconKind.Target:
                    Circle(12, 12, 8.5f);
                    Circle(12, 12, 4);
                    Line(12, 1.5f, 12, 6);
                    Line(12, 18, 12, 22.5f);
                    Line(1.5f, 12, 6, 12);
                    Line(18, 12, 22.5f, 12);
                    break;
                case IconKind.Bolt:
                    Closed(13.5f, 2.5f, 5, 13.5f, 11, 13.5f, 10, 21.5f, 19, 10, 13, 10);
                    break;
                case IconKind.Data:
                    void Rim(float y, bool full)
                    {
                        p.BeginPath();
                        p.MoveTo(P(4.5f, y));
                        p.BezierCurveTo(P(4.5f, y + 3.6f), P(19.5f, y + 3.6f), P(19.5f, y));
                        if (full)
                            p.BezierCurveTo(P(19.5f, y - 3.6f), P(4.5f, y - 3.6f), P(4.5f, y));
                        p.Stroke();
                    }
                    Rim(6, true);
                    Rim(12, false);
                    Rim(18, false);
                    Line(4.5f, 6, 4.5f, 18);
                    Line(19.5f, 6, 19.5f, 18);
                    break;
                case IconKind.Policy:
                    Closed(6, 3, 15, 3, 19, 7, 19, 21, 6, 21);
                    Line(15, 3, 15, 7, 19, 7);
                    Line(9, 11, 16, 11);
                    Line(9, 14.5f, 16, 14.5f);
                    Line(9, 18, 13, 18);
                    break;
                case IconKind.Research:
                    Line(9.5f, 3, 9.5f, 9, 4.5f, 19, 6, 21, 18, 21, 19.5f, 19, 14.5f, 9, 14.5f, 3);
                    Line(8, 3, 16, 3);
                    Line(7, 15, 17, 15);
                    break;
                case IconKind.Facility:
                    Closed(4, 21, 4, 9, 11, 5, 11, 21);
                    Closed(11, 21, 11, 3, 20, 7, 20, 21);
                    Line(14, 10, 17, 10);
                    Line(14, 14, 17, 14);
                    Line(6.5f, 13, 8.5f, 13);
                    break;
                case IconKind.People:
                    Circle(9, 8, 3.2f);
                    Arc(9, 20, 6, 180, 360);
                    Circle(16.5f, 9, 2.6f);
                    Arc(17, 20, 5, 200, 340);
                    break;
                case IconKind.Alert:
                    Closed(12, 3, 21.5f, 20, 2.5f, 20);
                    Line(12, 9.5f, 12, 14);
                    Circle(12, 17, 0.9f, true);
                    break;
                case IconKind.Info:
                    Circle(12, 12, 9);
                    Line(12, 11, 12, 17);
                    Circle(12, 7.5f, 0.9f, true);
                    break;
                case IconKind.Star:
                    var pts = new List<float>();
                    for (int i = 0; i < 10; i++)
                    {
                        float a = Mathf.Deg2Rad * (-90f + i * 36f);
                        float rr = i % 2 == 0 ? 9.5f : 4.2f;
                        pts.Add(12 + Mathf.Cos(a) * rr);
                        pts.Add(12.5f + Mathf.Sin(a) * rr);
                    }
                    Closed(pts.ToArray());
                    break;
                case IconKind.Log:
                    Line(8, 6, 20, 6);
                    Line(8, 12, 20, 12);
                    Line(8, 18, 20, 18);
                    Circle(4.5f, 6, 0.9f, true);
                    Circle(4.5f, 12, 0.9f, true);
                    Circle(4.5f, 18, 0.9f, true);
                    break;
                case IconKind.Save:
                    Closed(4, 4, 17, 4, 20, 7, 20, 20, 4, 20);
                    Line(8, 4, 8, 9, 15, 9, 15, 4);
                    Closed(8, 20, 8, 14, 16, 14, 16, 20);
                    break;
                case IconKind.Reset:
                    Arc(12, 12, 8, -60, 220);
                    Line(16, 3.5f, 16.2f, 7.2f, 19.8f, 6.8f);
                    break;
                case IconKind.Pin:
                    Arc(12, 9.5f, 6, 150, 390);
                    Line(6.8f, 12.5f, 12, 21, 17.2f, 12.5f);
                    Circle(12, 9.5f, 2.2f);
                    break;
                case IconKind.Gauge:
                    Arc(12, 14, 9, 180, 360);
                    Line(12, 14, 16.5f, 8.5f);
                    Circle(12, 14, 1.2f, true);
                    break;
            }
        }
    }

    // A ring that fills clockwise from the top, with an icon in the middle.
    // Delegation progress: one slanted cell per month the index sum must stay over the next stage's threshold.
    // Full cells are months already met; the current cell fills with the days of this month while the threshold
    // is met, or shows an empty amber (red when stability is failing) outline while it is not.
    public class DelegationGauge : VisualElement
    {
        public enum State { Meeting, Below, Danger, Max }

        static readonly Color Accent = new Color32(53, 201, 232, 255);
        static readonly Color Select = new Color32(76, 141, 255, 255);
        static readonly Color Good = new Color32(66, 214, 164, 255);
        static readonly Color Warn = new Color32(242, 182, 93, 255);
        static readonly Color Bad = new Color32(241, 109, 121, 255);

        int filled, total = 4;
        float month;
        State state = State.Below;
        float time;

        public DelegationGauge()
        {
            AddToClassList("dgauge");
            generateVisualContent += Draw;
            // A slow sheen on the cell being filled.
            schedule.Execute(() =>
            {
                time += 0.05f;
                if (state == State.Meeting || state == State.Max)
                    MarkDirtyRepaint();
            }).Every(50);
        }

        public void Set(int filled, int total, float monthProgress, State state)
        {
            this.filled = Mathf.Clamp(filled, 0, total);
            this.total = Mathf.Max(1, total);
            month = Mathf.Clamp01(monthProgress);
            this.state = state;
            MarkDirtyRepaint();
        }

        static void Cell(Painter2D p, float x, float w, float h, float skew, float top = 0f, float bottom = -1f)
        {
            if (bottom < 0f)
                bottom = h;
            // slant follows the full cell height so partial strips line up with the cell edges
            float sTop = skew * (1f - top / h), sBottom = skew * (1f - bottom / h);
            p.BeginPath();
            p.MoveTo(new Vector2(x + sTop, top));
            p.LineTo(new Vector2(x + sTop + w, top));
            p.LineTo(new Vector2(x + sBottom + w, bottom));
            p.LineTo(new Vector2(x + sBottom, bottom));
            p.ClosePath();
        }

        static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

        void Draw(MeshGenerationContext context)
        {
            var r = contentRect;
            float w = r.width, h = r.height;
            if (w <= 0f || h <= 0f || float.IsNaN(w))
                return;
            var p = context.painter2D;
            const float gap = 5f;
            float skew = Mathf.Min(h * 0.6f, 7f);
            float cell = (w - gap * (total - 1) - skew) / total;
            if (cell <= 0f)
                return;
            p.lineJoin = LineJoin.Round;

            for (int i = 0; i < total; i++)
            {
                float x = i * (cell + gap);
                float t = total > 1 ? i / (float)(total - 1) : 0f;
                var color = state == State.Max ? Good : Color.Lerp(Accent, Select, t);
                bool full = state == State.Max || i < filled;
                bool current = !full && i == filled;

                // track
                Cell(p, x, cell, h, skew);
                p.fillColor = new Color(0.08f, 0.16f, 0.28f, 0.85f);
                p.Fill();
                p.lineWidth = 1f;
                p.strokeColor = A(Accent, 0.22f);
                Cell(p, x, cell, h, skew);
                p.Stroke();

                if (full)
                {
                    // glow, body, top gloss, bright rim
                    p.lineWidth = 5f;
                    p.strokeColor = A(color, 0.16f);
                    Cell(p, x, cell, h, skew);
                    p.Stroke();
                    p.fillColor = A(color, 0.9f);
                    Cell(p, x, cell, h, skew);
                    p.Fill();
                    p.fillColor = new Color(1f, 1f, 1f, 0.22f);
                    Cell(p, x, cell, h, skew, 0f, h * 0.42f);
                    p.Fill();
                    p.lineWidth = 1f;
                    p.strokeColor = A(Color.Lerp(color, Color.white, 0.45f), 0.9f);
                    Cell(p, x, cell, h, skew);
                    p.Stroke();
                }
                else if (current && state == State.Meeting)
                {
                    float pw = Mathf.Max(2f, cell * month);
                    float pulse = 0.62f + 0.18f * Mathf.Sin(time * 2.4f);
                    p.fillColor = A(color, pulse);
                    Cell(p, x, pw, h, skew);
                    p.Fill();
                    p.fillColor = new Color(1f, 1f, 1f, 0.16f);
                    Cell(p, x, pw, h, skew, 0f, h * 0.42f);
                    p.Fill();
                    // leading edge
                    p.lineWidth = 2f;
                    p.strokeColor = A(Color.Lerp(color, Color.white, 0.6f), 0.95f);
                    p.BeginPath();
                    p.MoveTo(new Vector2(x + skew + pw, 0f));
                    p.LineTo(new Vector2(x + pw, h));
                    p.Stroke();
                    p.lineWidth = 1f;
                    p.strokeColor = A(color, 0.8f);
                    Cell(p, x, cell, h, skew);
                    p.Stroke();
                }
                else if (current)
                {
                    var c = state == State.Danger ? Bad : Warn;
                    p.fillColor = A(c, 0.08f);
                    Cell(p, x, cell, h, skew);
                    p.Fill();
                    p.lineWidth = 1.2f;
                    p.strokeColor = A(c, 0.85f);
                    Cell(p, x, cell, h, skew);
                    p.Stroke();
                }
            }
        }
    }

    public class RingGauge : VisualElement
    {
        float value;
        Color color;
        readonly IconKind kind;
        readonly LineIcon icon;

        public RingGauge(IconKind kind, Color color, bool icon = true)
        {
            this.kind = kind;
            this.color = color;
            AddToClassList("ring");
            if (icon)
            {
                this.icon = new LineIcon(kind, color);
                this.icon.AddToClassList("ring-icon");
                Add(this.icon);
            }
            generateVisualContent += Draw;
        }

        public void Set(float percent, Color? tint = null)
        {
            value = Mathf.Clamp01(percent / 100f);
            if (tint.HasValue)
            {
                color = tint.Value;
                icon?.Set(kind, color);
            }
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            var r = contentRect;
            float size = Mathf.Min(r.width, r.height);
            var c = r.center;
            float radius = size / 2f - 5f;
            var p = context.painter2D;
            p.lineCap = LineCap.Round;
            p.lineWidth = 5f;
            p.strokeColor = new Color(color.r, color.g, color.b, 0.16f);
            p.BeginPath();
            p.Arc(c, radius, 0f, 360f);
            p.Stroke();
            if (value > 0.001f)
            {
                // soft halo under the value arc
                p.lineWidth = 9f;
                p.strokeColor = new Color(color.r, color.g, color.b, 0.18f);
                p.BeginPath();
                p.Arc(c, radius, -90f, -90f + 360f * value);
                p.Stroke();
                p.lineWidth = 4.5f;
                p.strokeColor = color;
                p.BeginPath();
                p.Arc(c, radius, -90f, -90f + 360f * value);
                p.Stroke();
            }
            // tick marks
            p.lineWidth = 1f;
            p.strokeColor = new Color(color.r, color.g, color.b, 0.35f);
            for (int i = 0; i < 24; i++)
            {
                float a = Mathf.Deg2Rad * (i * 15f);
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                p.BeginPath();
                p.MoveTo(c + dir * (radius - 8f));
                p.LineTo(c + dir * (radius - (i % 6 == 0 ? 12f : 10f)));
                p.Stroke();
            }
        }
    }

    // Several series on one small chart (0..100), newest on the right, with a soft grid.
    public class LineChart : VisualElement
    {
        readonly List<(IReadOnlyList<float> values, Color color, bool dashed)> series =
            new List<(IReadOnlyList<float>, Color, bool)>();

        // Fit the vertical range to the data instead of 0..100 (for small changes such as forecasts).
        public bool AutoRange;

        public LineChart()
        {
            AddToClassList("chart");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void Set(IEnumerable<(IReadOnlyList<float> values, Color color, bool dashed)> lines)
        {
            series.Clear();
            series.AddRange(lines);
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            var r = contentRect;
            var p = context.painter2D;
            p.lineWidth = 1f;
            p.strokeColor = new Color(0.47f, 0.75f, 0.96f, 0.12f);
            for (int i = 0; i <= 4; i++)
            {
                float y = r.height * i / 4f;
                p.BeginPath();
                p.MoveTo(new Vector2(0f, y));
                p.LineTo(new Vector2(r.width, y));
                p.Stroke();
            }
            float low = 0f, high = 100f;
            if (AutoRange && series.Any(x => x.values.Count > 0))
            {
                low = series.SelectMany(x => x.values).Min();
                high = series.SelectMany(x => x.values).Max();
                float pad = Mathf.Max(2f, (high - low) * 0.12f);
                low -= pad;
                high += pad;
            }
            foreach (var (values, color, dashed) in series)
            {
                if (values.Count < 2)
                    continue;
                int n = values.Count;
                Vector2 At(int i) => new Vector2(r.width * i / (n - 1), r.height - r.height * Mathf.InverseLerp(low, high, values[i]));
                // "dashed" series (the autonomy pair) are drawn thinner and fainter.
                p.lineWidth = dashed ? 1.5f : 2f;
                p.strokeColor = dashed ? new Color(color.r, color.g, color.b, 0.8f) : color;
                p.lineJoin = LineJoin.Round;
                p.BeginPath();
                p.MoveTo(At(0));
                for (int i = 1; i < n; i++)
                    p.LineTo(At(i));
                p.Stroke();
                p.fillColor = color;
                p.BeginPath();
                p.Arc(At(n - 1), 3f, 0f, 360f);
                p.Fill();
            }
        }
    }
}
