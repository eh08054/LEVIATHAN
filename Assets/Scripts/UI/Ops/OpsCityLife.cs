using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sector.UI
{
    // Life over the city picture (2026-11-27): the painting stays one still image, so what changes is drawn over it from
    // the same ground data as the district outlines (Resources/Map/zones_v8.txt, from the Unity model): pods flowing
    // along every street (warm and cool lights, more on the avenues, brighter at night) and districts with rising tension breathing in their stage colour. Drawn in image fractions through PinAt, so
    // it pans and zooms with the painting.
    public partial class OpsScreen
    {
        VisualElement lifeCanvas;
        static List<(bool wide, Vector2 a, Vector2 b)> roads;
        static Vector3? aiRing;   // x, y, radius (x fraction)

        static void LoadLife()
        {
            if (roads != null) return;
            roads = new List<(bool, Vector2, Vector2)>();
            var file = Resources.Load<TextAsset>("Map/zones_v8");
            if (file == null) return;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var line in file.text.Split('\n'))
            {
                var f = line.Trim().Split(' ');
                if (f[0] == "road" && f.Length >= 6)
                    roads.Add((f[1] == "1", new Vector2(float.Parse(f[2], inv), float.Parse(f[3], inv)), new Vector2(float.Parse(f[4], inv), float.Parse(f[5], inv))));
                else if (f[0] == "ring" && f.Length >= 4)
                    aiRing = new Vector3(float.Parse(f[1], inv), float.Parse(f[2], inv), float.Parse(f[3], inv));
            }
        }

        void BuildCityLife()
        {
            LoadLife();
            if (roads.Count == 0) return;
            lifeCanvas = new VisualElement { pickingMode = PickingMode.Ignore };
            lifeCanvas.style.position = Position.Absolute;
            lifeCanvas.style.left = 0; lifeCanvas.style.top = 0; lifeCanvas.style.right = 0; lifeCanvas.style.bottom = 0;
            lifeCanvas.generateVisualContent += DrawCityLife;
            zoneLayer.Insert(0, lifeCanvas);
        }

        void TickCityLife() => lifeCanvas?.MarkDirtyRepaint();

        void DrawCityLife(MeshGenerationContext ctx)
        {
            if (session == null || roads == null || modelMode || !PaintingDistricts) return;
            var p = ctx.painter2D;
            float t = cityTime;

            // (pod lights along the roads were taken out on request, 2026-11-27)

            // (the AI centre's ring is painted; a pulse over it would need its height, which the ground homography
            // in Tools/Art/city_v9_zones.py does not carry, so it is left to the picture)

            // districts with rising tension breathe in their stage colour
            for (int z = 0; z < ZoneShapes.Length; z++)
            {
                if (!session.ZoneOpen(z)) continue;
                int stage = session.TensionStage(z);
                if (stage < 2) continue;
                var col = StageColor(stage);
                float a = (stage >= 3 ? 0.16f : 0.09f) * (0.6f + 0.4f * Mathf.Sin(t * (stage >= 3 ? 3.2f : 1.8f) + z));
                p.fillColor = new Color(col.r, col.g, col.b, a);
                p.BeginPath();
                var shape = ZoneShapes[z];
                p.MoveTo(PinAt(shape[0]));
                for (int i = 1; i < shape.Length; i++) p.LineTo(PinAt(shape[i]));
                p.ClosePath(); p.Fill();
            }
        }

        static void Ellipse(Painter2D p, Vector2 c, float rx, float ry)
        {
            p.BeginPath();
            for (int i = 0; i <= 48; i++)
            {
                float a = i / 48f * Mathf.PI * 2f;
                var q = c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
                if (i == 0) p.MoveTo(q); else p.LineTo(q);
            }
            p.Stroke();
        }
    }
}
