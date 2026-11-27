// Trial (2026-11-27): the main screen's 3D city model, ported from Tools/Art/proto/city.html (same plan and the same
// random sequence, so the layout matches the browser model and the look target Tools/Art/painted/model/model_target.png).
// Builds the city at start (combined meshes per material), then runs the camera (grab-drag pan, right-drag turn,
// wheel zoom that tilts to look down), time of day (sun, sky, fog, window lights) and the moving things (pods,
// maglev trains, drones, cranes, the AI centre's ring). Scene: Assets/Scenes/ModelCity.unity (LEVIATHAN/ModelCity/Build Scene).
// JS units are 0.1 m; JS (x, y, z-up) maps to Unity (x, z, y).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sector.ModelCity
{
    public class ModelCity : MonoBehaviour
    {
        public Light sun;
        public Camera cam;
        [Range(0, 24)] public float hour = 16;
        public float speed = 0;   // game hours per real second / 20 (1 = one hour in 20 s)

        const float S = 0.1f;
        static readonly string[] Base = { "HHHMMHHHS", "HHCCMHHRR", "EECAACCRR", "ELCAACUUH", "LLKUUDDHH", "LKKUUDDHH" };
        const int Cell = 100, St = 18, Av = 40;
        public static readonly string[] Keys = { "H", "M", "S", "C", "R", "E", "A", "L", "U", "K", "Dd" };
        static readonly Dictionary<string, Color32> Tint = new Dictionary<string, Color32>
        {
            ["H"] = new Color32(236, 238, 244, 255), ["M"] = new Color32(246, 230, 230, 255), ["S"] = new Color32(226, 212, 186, 255),
            ["C"] = new Color32(214, 232, 252, 255), ["R"] = new Color32(222, 244, 234, 255), ["E"] = new Color32(214, 226, 244, 255),
            ["A"] = new Color32(232, 250, 255, 255), ["L"] = new Color32(224, 224, 232, 255), ["U"] = new Color32(232, 244, 240, 255),
            ["K"] = new Color32(226, 228, 244, 255), ["Dd"] = new Color32(246, 240, 228, 255),
        };

        string[] plan; int rows, cols; int[] xs, ys; float cw, ch;
        readonly Dictionary<string, (float tension, float pop, float lit)> state = new Dictionary<string, (float, float, float)>();
        public static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            ["H"] = "주거", ["M"] = "의료·생명", ["S"] = "공사 부지", ["C"] = "상업", ["R"] = "연구·개발", ["E"] = "에너지·발전",
            ["A"] = "관리 (AI 센터)", ["L"] = "물류·산업", ["U"] = "문화·여가", ["K"] = "치안·보안", ["Dd"] = "교육",
        };
        // made-up district numbers for the trial (the game will feed the real ones)
        public IReadOnlyDictionary<string, (float tension, float pop, float lit)> State => state;
        public ModelHud hud;
        public string layer = "none";   // none | tension | pop
        public float Dist => dist;
        (float tension, float pop, float lit) Live(string k) => hud != null ? hud.D(k) : state[k];
        float PopScale { get { float m = 1; foreach (var k in Keys) m = Mathf.Max(m, Live(k).pop); return m; } }
        public Camera Cam => cam;
        public static Color TensionColor(float t) => t >= 75 ? new Color32(238, 90, 90, 255) : t >= 50 ? new Color32(245, 160, 70, 255) : t >= 30 ? new Color32(230, 210, 90, 255) : new Color32(53, 201, 232, 255);
        readonly Dictionary<string, Vector3> centres = new Dictionary<string, Vector3>();
        public Vector3 CentreOf(string k) => centres[k];
        public string DistrictAt(Vector3 g)
        {
            float x = g.x / S, y = g.z / S;
            foreach (var l in lots) if (x >= l.x0 - St / 2f && x <= l.x1 + St / 2f && y >= l.y0 - St / 2f && y <= l.y1 + St / 2f) return l.k;
            return null;
        }
        public void FlyTo(string k, float d = 150) { var c = centres[k]; goal = new Vector3(c.x, c.z, d); }
        public void GoHome() { goal = new Vector3(cw * S / 2, ch * S * 0.42f, Home); tiltGoal = 0; }
        public Rect PlanRect => new Rect(0, 0, cw * S, ch * S);
        public Vector2 Target => target;
        public IEnumerable<(Rect r, string k)> LotRects() { foreach (var l in lots) yield return (new Rect(l.x0 * S, l.y0 * S, (l.x1 - l.x0) * S, (l.y1 - l.y0) * S), l.k); }
        int seed = 7;
        float Rand() { seed = (int)((seed * 16807L) % 2147483647L); return seed / 2147483647f; }
        int Gap(int i) => i % 3 == 0 ? Av : St;

        // ------------------------------------------------------------------ meshes
        class Buf { public readonly List<Vector3> v = new List<Vector3>(); public readonly List<Vector3> n = new List<Vector3>(); public readonly List<Vector2> uv = new List<Vector2>(); public readonly List<int> t = new List<int>(); }
        readonly Dictionary<string, Buf> bufs = new Dictionary<string, Buf>();
        readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        Buf B(string key) { if (!bufs.TryGetValue(key, out var b)) bufs[key] = b = new Buf(); return b; }
        static Vector3 P(float x, float y, float z) => new Vector3(x * S, z * S, y * S);

        static void Quad(Buf b, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 normal, Vector2 u0, Vector2 u1, Vector2 u2, Vector2 u3)
        {
            int i = b.v.Count;
            b.v.Add(p0); b.v.Add(p1); b.v.Add(p2); b.v.Add(p3);
            for (int k = 0; k < 4; k++) b.n.Add(normal);
            b.uv.Add(u0); b.uv.Add(u1); b.uv.Add(u2); b.uv.Add(u3);
            if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), normal) >= 0) { b.t.Add(i); b.t.Add(i + 1); b.t.Add(i + 2); b.t.Add(i); b.t.Add(i + 2); b.t.Add(i + 3); }
            else { b.t.Add(i); b.t.Add(i + 2); b.t.Add(i + 1); b.t.Add(i); b.t.Add(i + 3); b.t.Add(i + 2); }
        }
        // a wall from a to b (JS plan coords) between heights z0..z1; windows tile one per 7 x 5.5 units
        // facade textures: how many plan units one texture spans (bays x bay size); see Tools/Art/model_textures.py
        Vector2 Span(Buf b)
        {
            foreach (var kv in bufs) if (kv.Value == b) { var k = kv.Key; if (k.StartsWith("face_")) return HomeKind(k.Substring(5)) ? homeSpan : glassSpan; if (k == "glass") return glassSpan; break; }
            return new Vector2(7 * 64, 5.5f * 64);
        }
        static bool HomeKind(string k) => k == "H" || k == "Dd" || k == "M";
        Vector2 glassSpan = new Vector2(7 * 64, 5.5f * 64), homeSpan = new Vector2(7 * 64, 5.5f * 64);
        void Wall(Buf b, float ax, float ay, float bx, float by, float z0, float z1, float uOff, Vector3 outward)
        {
            float len = Mathf.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
            var sp = Span(b);
            float u0 = uOff, u1 = uOff + len / sp.x, v0 = z0 / sp.y, v1 = z1 / sp.y;
            Quad(b, P(ax, ay, z0), P(bx, by, z0), P(bx, by, z1), P(ax, ay, z1), outward, new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1));
        }
        void Top(Buf b, float x0, float y0, float x1, float y1, float z)
        {
            const float r = 140;   // one roof texture per 90 plan units, mirrored when tiling
            Quad(b, P(x0, y0, z), P(x1, y0, z), P(x1, y1, z), P(x0, y1, z), Vector3.up, new Vector2(x0 / r, y0 / r), new Vector2(x1 / r, y0 / r), new Vector2(x1 / r, y1 / r), new Vector2(x0 / r, y1 / r));
        }
        void Box(string side, string top, float x0, float y0, float x1, float y1, float z0, float h, bool bottom = false)
        {
            var s = B(side); float z1 = z0 + h, uo = Mathf.Floor(Rand2() * 8) / 8f;
            Wall(s, x0, y0, x1, y0, z0, z1, uo, Vector3.back);
            Wall(s, x1, y0, x1, y1, z0, z1, uo + 0.3f, Vector3.right);
            Wall(s, x1, y1, x0, y1, z0, z1, uo + 0.6f, Vector3.forward);
            Wall(s, x0, y1, x0, y0, z0, z1, uo + 0.8f, Vector3.left);
            Top(B(top), x0, y0, x1, y1, z1);
        }
        void Prism(string side, string top, float cx, float cy, float rad, float z0, float h, int sides)
        {
            var s = B(side); float z1 = z0 + h, uo = Mathf.Floor(Rand2() * 64) / 64f;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2 / sides, a1 = (i + 1) * Mathf.PI * 2 / sides, am = (a0 + a1) / 2;
                Wall(s, cx + Mathf.Cos(a0) * rad, cy + Mathf.Sin(a0) * rad, cx + Mathf.Cos(a1) * rad, cy + Mathf.Sin(a1) * rad, z0, z1, uo + i * 0.02f, new Vector3(Mathf.Cos(am), 0, Mathf.Sin(am)));
            }
            if (top != null) Disc(top, cx, cy, rad, z1, sides);
        }
        void Disc(string key, float cx, float cy, float rad, float z, int sides, float inner = 0)
        {
            var b = B(key);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2 / sides, a1 = (i + 1) * Mathf.PI * 2 / sides;
                Vector3 o0 = P(cx + Mathf.Cos(a0) * rad, cy + Mathf.Sin(a0) * rad, z), o1 = P(cx + Mathf.Cos(a1) * rad, cy + Mathf.Sin(a1) * rad, z);
                Vector3 i0 = P(cx + Mathf.Cos(a0) * inner, cy + Mathf.Sin(a0) * inner, z), i1 = P(cx + Mathf.Cos(a1) * inner, cy + Mathf.Sin(a1) * inner, z);
                Quad(b, i0, o0, o1, i1, Vector3.up, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            }
        }
        // a second, independent random stream for looks only (window offsets), so the layout stream stays the JS one
        int seed2 = 99;
        float Rand2() { seed2 = (int)((seed2 * 48271L) % 2147483647L); return seed2 / 2147483647f; }

        // ------------------------------------------------------------------ the city
        readonly List<(float x0, float y0, float x1, float y1, string k)> lots = new List<(float, float, float, float, string)>();
        readonly List<(float x, float y, float h, float ph, Transform jib, Renderer lamp)> cranes = new List<(float, float, float, float, Transform, Renderer)>();
        readonly List<float> roadsX = new List<float>(), roadsY = new List<float>();
        Vector2[] loop; float loopLen; float ax, ay;
        Transform ringA, ringB;

        string Face(string k) => "face_" + k;
        string Roof(string k) => "roof_" + k;
        void Item() => Rand();   // the JS item() draws one number for its window seed
        void Tower(string k, float x, float y, float w, float l, float h)
        {
            float ph = 10 + Rand() * 8, inset = Mathf.Min(w, l) * 0.18f;
            Box(Face(k), Roof(k), x, y, x + w, y + l, 0, ph);
            Box(Face(k), Roof(k), x + inset, y + inset, x + w - inset, y + l - inset, ph, h - ph);
            if (Rand() < 0.6f) { float i2 = inset * 1.6f; Box(Face(k), Roof(k), x + i2, y + i2, x + w - i2, y + l - i2, h, 6); Box("glow", "glow", x + i2 - 0.3f, y + i2 - 0.3f, x + w - i2 + 0.3f, y + l - i2 + 0.3f, h + 2.5f, 0.8f); }
            Item();
        }
        void Slab(string k, float x, float y, float w, float l, float h) { Rand(); Box(Face(k), Roof(k), x, y, x + w, y + l, 0, h); Item(); }
        void Rect(string mat, float x0, float y0, float x1, float y1, float z = 1.6f, float h = 0.4f) => Box(mat, mat, x0, y0, x1, y1, z, h);
        void Ring(string mat, float cx, float cy, float rad, float z, float width) => Disc(mat, cx, cy, rad + width / 2, z, 32, rad - width / 2);

        void Build()
        {
            plan = new string[Base.Length * 2];
            for (int i = 0; i < Base.Length; i++) { var r = ""; foreach (var c in Base[i]) r += c.ToString() + c; plan[i * 2] = plan[i * 2 + 1] = r; }
            rows = plan.Length; cols = plan[0].Length;
            xs = new int[cols]; ys = new int[rows];
            { int x = Av; for (int c = 0; c < cols; c++) { xs[c] = x; x += Cell + Gap(c + 1); } }
            { int y = Av; for (int r = 0; r < rows; r++) { ys[r] = y; y += Cell + Gap(r + 1); } }
            cw = xs[cols - 1] + Cell + Av; ch = ys[rows - 1] + Cell + Av;
            foreach (var k in Keys)
            {
                float t = Mathf.Round(Rand() * 85), pop = k == "H" ? 58000 : Mathf.Round(1500 + Rand() * 9000), lit = 0.4f + Rand() * 0.5f;
                state[k] = (t, pop, lit);
            }

            float[][] containers = { new[] { 80f, 120, 170 }, new[] { 200f, 110, 80 }, new[] { 120f, 150, 100 } };
            for (int r = 0; r < rows; r++) for (int c = 0; c < cols; c++)
            {
                string k = plan[r][c] == 'D' ? "Dd" : plan[r][c].ToString();
                float x0 = xs[c], y0 = ys[rows - 1 - r], x1 = x0 + Cell, y1 = y0 + Cell, cx = (x0 + x1) / 2, cy = (y0 + y1) / 2;
                lots.Add((x0, y0, x1, y1, k));
                Box("lotside", "lot_" + k, x0, y0, x1, y1, 0, 1.5f);
                float t = r <= 3 ? 1 : r <= 7 ? 0.65f : 0.35f;
                if (k == "H")
                {
                    float v = Rand();
                    if (v < 0.45f)
                    {
                        Slab(k, x0 + 4, y0 + 4, 92, 16, 40 + 60 * t * Rand() + 20 * t); Slab(k, x0 + 4, y0 + 80, 92, 16, 50 + 80 * t);
                        Slab(k, x0 + 4, y0 + 24, 16, 52, 30 + 40 * t); Rect("court", x0 + 28, y0 + 28, x1 - 10, y1 - 28);
                    }
                    else if (v < 0.85f)
                    {
                        Tower(k, x0 + 6, y0 + 50, 38, 38, 70 + 110 * t * (0.6f + Rand() * 0.6f)); Tower(k, x0 + 56, y0 + 54, 36, 36, 60 + 90 * t * (0.6f + Rand() * 0.6f));
                        Slab(k, x0 + 6, y0 + 6, 88, 22, 12);
                    }
                    else Tower(k, x0 + 22, y0 + 22, 56, 56, 140 + 120 * t);
                }
                else if (k == "C")
                {
                    float v = Rand();
                    if (v < 0.5f) { Tower(k, x0 + 8, y0 + 8, 50, 50, 90 + 160 * t * Rand() + 40); Slab(k, x0 + 64, y0 + 8, 30, 86, 24 + Rand() * 30); }
                    else if (v < 0.8f) { foreach (var (a, b) in new[] { (0, 0), (1, 0), (0, 1), (1, 1) }) Tower(k, x0 + 6 + a * 50, y0 + 6 + b * 50, 40, 40, 40 + Rand() * 120 * (0.5f + t)); }
                    else { Disc("plaza", cx, cy, 34, 1.7f, 24); Prism("glow", "glow", cx, cy, 10, 0, 18, 12); Ring("glow", cx, cy, 22, 30, 1.2f); Item(); }
                }
                else if (k == "M")
                {
                    if ((c + r) % 2 == 0) { Box(Face(k), Roof(k), x0 + 6, y0 + 6, x1 - 6, y1 - 6, 0, 16); Box(Face(k), Roof(k), x0 + 18, y0 + 30, x1 - 18, y1 - 14, 16, 34); Ring("glow", (x0 + x1) / 2, (y0 + 30 + y1 - 14) / 2, 12, 50.2f, 1.5f); Item(); }
                    else { Slab(k, x0 + 8, y0 + 8, 84, 30, 22); Disc("pool", cx, cy + 20, 22, 1.7f, 24); }
                }
                else if (k == "E")
                {
                    if ((c + r) % 2 == 0) { for (float y = y0 + 4; y < y1 - 10; y += 10) Box("solar", "solar", x0 + 4, y, x1 - 4, y + 7, 2, 2.5f); }
                    else
                    {
                        foreach (var (a, b) in new[] { (0, 0), (1, 0), (0, 1) }) { float px = x0 + 28 + a * 46, py = y0 + 28 + b * 46; Prism("tank", "tanktop", px, py, 18, 0, 26, 16); Prism("glow", null, px, py, 18.4f, 12, 2, 16); Item(); }
                        Slab(k, x0 + 56, y0 + 56, 38, 38, 12);
                    }
                }
                else if (k == "R")
                {
                    if ((c * 3 + r) % 3 == 0) { Box(Face(k), Roof(k), x0 + 10, y0 + 10, x1 - 10, y1 - 10, 0, 14); Dome(cx, cy, 24, 14); Item(); }
                    else { Slab(k, x0 + 6, y0 + 6, 56, 30, 18); Tower(k, x0 + 60, y0 + 40, 34, 50, 50 + Rand() * 50); Rect("court", x0 + 8, y0 + 50, x0 + 52, y1 - 8); }
                }
                else if (k == "L")
                {
                    Box(Face(k), Roof(k), x0 + 4, y0 + 10, x1 - 4, y1 - 24, 0, 16); Ring("glow", cx, (y0 + 10 + y1 - 24) / 2, 14, 16.2f, 1.5f); Item();
                    for (int i = 0; i < 5; i++) { var col = containers[(int)(Rand() * 3)]; Box(Mat(col), Mat(col), x0 + 6 + i * 18, y0 + 80, x0 + 20 + i * 18, y0 + 92, 1.5f, 6); }
                }
                else if (k == "K")
                {
                    if ((c + r) % 2 == 0) { Prism(Face(k), Roof(k), cx, cy, 12, 0, 90, 8); Prism(Face(k), Roof(k), cx, cy, 20, 90, 10, 8); Prism("glow", null, cx, cy, 20.4f, 94, 1.2f, 8); Item(); }
                    else Slab(k, x0 + 8, y0 + 8, 84, 60, 20);
                }
                else if (k == "U")
                {
                    int v = (c * 7 + r * 3) % 4;
                    if (v == 0) { Prism("panel", "panel", cx, cy, 44, 0, 16, 24); Disc("grass", cx, cy, 32, 16.1f, 24); Item(); }
                    else if (v == 1) Rect("water", x0 + 10, y0 + 30, x1 - 10, y1 - 30);
                    else { Disc("plaza", cx, cy, 38, 1.7f, 24); Prism("glow", "glow", cx, cy, 8, 0, 6, 12); Ring("glow", cx, cy, 18, 20, 1f); Item(); }
                }
                else if (k == "Dd")
                {
                    if ((c + r) % 2 == 0) { Rect("track", x0 + 6, y0 + 30, x1 - 6, y1 - 6); Rect("grass", x0 + 14, y0 + 38, x1 - 14, y1 - 14, 2.0f); Slab(k, x0 + 6, y0 + 4, 88, 20, 14); }
                    else { Slab(k, x0 + 8, y0 + 8, 50, 84, 18); Tower(k, x0 + 64, y0 + 50, 30, 42, 46); }
                }
                else if (k == "S")
                {
                    foreach (var (a, b, w, l) in new[] { (0, 0, 100, 4), (0, 96, 100, 4), (0, 0, 4, 100), (96, 0, 4, 100) }) { Box("panel", "panel", x0 + a, y0 + b, x0 + a + w, y0 + b + l, 0, 7); Item(); }
                    Rect("dirt", x0 + 4, y0 + 4, x1 - 4, y1 - 4);
                    if ((c + r) % 2 == 0)
                    {
                        Frame(cx - 22, cy - 22, cx + 22, cy + 22, 40); Item();
                        cranes.Add((cx + 30, cy + 30, 110, Rand() * 6, null, null));
                    }
                }
            }
            // the AI centre
            float ax0 = 1e9f, ax1 = -1e9f, ay0 = 1e9f, ay1 = -1e9f;
            foreach (var l in lots) if (l.k == "A") { ax0 = Mathf.Min(ax0, l.x0); ax1 = Mathf.Max(ax1, l.x1); ay0 = Mathf.Min(ay0, l.y0); ay1 = Mathf.Max(ay1, l.y1); }
            ax = (ax0 + ax1) / 2; ay = (ay0 + ay1) / 2;
            // a district's tag and fly-to point: its own block nearest its average (commerce rings the AI centre)
            foreach (var k in Keys)
            {
                if (k == "A") { centres[k] = P(ax, ay, 0); continue; }
                float mx = 0, my = 0; int n = 0;
                foreach (var l in lots) if (l.k == k) { mx += (l.x0 + l.x1) / 2; my += (l.y0 + l.y1) / 2; n++; }
                mx /= n; my /= n; float bd = 1e18f; Vector3 best = default;
                foreach (var l in lots) if (l.k == k) { float cx = (l.x0 + l.x1) / 2, cy = (l.y0 + l.y1) / 2, d = (cx - mx) * (cx - mx) + (cy - my) * (cy - my); if (d < bd) { bd = d; best = P(cx, cy, 0); } }
                centres[k] = best;
            }
            Disc("plaza", ax, ay, 200, 1.8f, 48); Rect("water", ax - 40, ay - 230, ax + 40, ay - 70, 1.9f);
            Prism("glass", "lot", ax, ay, 110, 0, 40, 32); Prism("glass", "lot", ax, ay, 70, 40, 90, 28); Prism("glow", "glow", ax, ay, 26, 130, 120, 16);
            Prism("glow", null, ax, ay, 110.5f, 36, 2, 32); Prism("glow", null, ax, ay, 70.5f, 126, 2, 28);
            Item();

            // maglev loop and roads
            loop = new[] { new Vector2(xs[3] - Av / 2f, ys[3] - Av / 2f), new Vector2(xs[15] - Av / 2f, ys[3] - Av / 2f), new Vector2(xs[15] - Av / 2f, ys[9] - Av / 2f), new Vector2(xs[3] - Av / 2f, ys[9] - Av / 2f) };
            loopLen = 2 * ((loop[1].x - loop[0].x) + (loop[2].y - loop[1].y));
            for (int i = 0; i < 4; i++)
            {
                var a = loop[i]; var b = loop[(i + 1) % 4];
                Box("beam", "beam", Mathf.Min(a.x, b.x) - 3, Mathf.Min(a.y, b.y) - 3, Mathf.Max(a.x, b.x) + 3, Mathf.Max(a.y, b.y) + 3, 23, 3);
                Box("glow", "glow", Mathf.Min(a.x, b.x) - 0.5f, Mathf.Min(a.y, b.y) - 0.5f, Mathf.Max(a.x, b.x) + 0.5f, Mathf.Max(a.y, b.y) + 0.5f, 26, 0.6f);
            }
            for (float s = 0; s < loopLen; s += 90) { var p = OnLoop(s); Box("beam", "beam", p.x - 1.5f, p.y - 1.5f, p.x + 1.5f, p.y + 1.5f, 0, 23); }
            roadsX.Add(Av / 2f); roadsY.Add(Av / 2f);
            for (int c = 0; c < cols; c++) roadsX.Add(xs[c] + Cell + Gap(c + 1) / 2f);
            for (int r = 0; r < rows; r++) roadsY.Add(ys[r] + Cell + Gap(r + 1) / 2f);
            // avenue lamps
            for (int i = 0; i < roadsY.Count; i += 3) for (float x = 0; x < cw; x += 60) Box("lamp", "lamp", x - 0.8f, roadsY[i] - 0.8f, x + 0.8f, roadsY[i] + 0.8f, 0, 3);

            Outskirts();
            // the ground: city roads, the outside ground beyond
            Rect("road", -40, -40, cw + 40, ch + 40, -0.5f, 0.5f);
            Rect("outside", -9000, -9000, 9000 + cw, 9000 + ch, -1.2f, 0.4f);

            foreach (var kv in bufs)
            {
                var b = kv.Value;
                var mesh = new Mesh { name = kv.Key, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(b.v); mesh.SetNormals(b.n); mesh.SetUVs(0, b.uv); mesh.SetTriangles(b.t, 0); mesh.RecalculateBounds();
                var go = new GameObject(kv.Key);
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = Material(kv.Key);
                if (kv.Key == "glow" || kv.Key.StartsWith("outside") || kv.Key == "road") mr.shadowCastingMode = ShadowCastingMode.Off;
            }
            bufs.Clear();

            // cranes: a mast, a jib that turns, a blinking lamp
            for (int i = 0; i < cranes.Count; i++)
            {
                var cr = cranes[i];
                var mast = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(mast.GetComponent<Collider>());
                mast.transform.SetParent(transform); mast.transform.localScale = new Vector3(0.3f, cr.h * S, 0.3f); mast.transform.position = P(cr.x, cr.y, cr.h / 2);
                mast.GetComponent<Renderer>().sharedMaterial = Material("crane");
                var jib = new GameObject("jib").transform; jib.SetParent(transform); jib.position = P(cr.x, cr.y, cr.h);
                var arm = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(arm.GetComponent<Collider>());
                arm.transform.SetParent(jib, false); arm.transform.localScale = new Vector3(9.2f, 0.25f, 0.25f); arm.transform.localPosition = new Vector3(2.4f, 0, 0);
                arm.GetComponent<Renderer>().sharedMaterial = Material("crane");
                var lamp = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(lamp.GetComponent<Collider>());
                lamp.transform.SetParent(jib, false); lamp.transform.localScale = Vector3.one * 0.45f; lamp.transform.localPosition = new Vector3(0, 0.4f, 0);
                lamp.GetComponent<Renderer>().sharedMaterial = Material("red");
                cranes[i] = (cr.x, cr.y, cr.h, cr.ph, jib, lamp.GetComponent<Renderer>());
            }
            // the ring over the AI centre
            ringA = RingObject(150, 290, 2.5f); ringB = RingObject(150, 290, 7f);
            ringB.GetComponent<Renderer>().sharedMaterial = Material("glowsoft");
        }

        void Dome(float cx, float cy, float rad, float z)
        {
            Prism("dome", null, cx, cy, rad, z, 3, 20);
            const int steps = 5;
            for (int i = 0; i < steps; i++)
            {
                float a0 = i * Mathf.PI / 2 / steps, a1 = (i + 1) * Mathf.PI / 2 / steps;
                float r0 = rad * Mathf.Cos(a0), r1 = rad * Mathf.Cos(a1), z0 = z + 3 + rad * 0.6f * Mathf.Sin(a0), z1 = z + 3 + rad * 0.6f * Mathf.Sin(a1);
                var b = B("dome");
                for (int s = 0; s < 20; s++)
                {
                    float b0 = s * Mathf.PI * 2 / 20, b1 = (s + 1) * Mathf.PI * 2 / 20, bm = (b0 + b1) / 2, am = (a0 + a1) / 2;
                    var nrm = new Vector3(Mathf.Cos(bm) * Mathf.Cos(am), Mathf.Sin(am), Mathf.Sin(bm) * Mathf.Cos(am));
                    Quad(b, P(cx + Mathf.Cos(b0) * r0, cy + Mathf.Sin(b0) * r0, z0), P(cx + Mathf.Cos(b1) * r0, cy + Mathf.Sin(b1) * r0, z0),
                        P(cx + Mathf.Cos(b1) * r1, cy + Mathf.Sin(b1) * r1, z1), P(cx + Mathf.Cos(b0) * r1, cy + Mathf.Sin(b0) * r1, z1), nrm,
                        Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
                }
            }
        }
        void Frame(float x0, float y0, float x1, float y1, float h)
        {
            foreach (var (x, y) in new[] { (x0, y0), (x1, y0), (x0, y1), (x1, y1) }) Box("crane", "crane", x - 1, y - 1, x + 1, y + 1, 0, h);
            for (float z = 8; z <= h; z += 8)
            {
                Box("crane", "crane", x0, y0 - 0.6f, x1, y0 + 0.6f, z, 1); Box("crane", "crane", x0, y1 - 0.6f, x1, y1 + 0.6f, z, 1);
                Box("crane", "crane", x0 - 0.6f, y0, x0 + 0.6f, y1, z, 1); Box("crane", "crane", x1 - 0.6f, y0, x1 + 0.6f, y1, z, 1);
            }
        }
        // the low city outside the sector: plain blocks fading into the haze
        void Outskirts()
        {
            int s3 = 4242; float R() { s3 = (int)((s3 * 48271L) % 2147483647L); return s3 / 2147483647f; }
            for (float x = -2600; x < cw + 2600; x += 70) for (float y = -1200; y < ch + 4200; y += 70)
            {
                if (x > -260 && x < cw + 200 && y > -260 && y < ch + 200) continue;
                if (R() < 0.7f) continue;
                float w = 16 + R() * 30, l = 16 + R() * 30, h = 5 + R() * R() * 30;
                Box("outface", "outroof", x, y, x + w, y + l, 0, h);
            }
        }
        string Mat(float[] col) { var key = $"col_{col[0]}_{col[1]}_{col[2]}"; if (!colours.ContainsKey(key)) colours[key] = new Color(col[0] / 255f, col[1] / 255f, col[2] / 255f); return key; }
        readonly Dictionary<string, Color> colours = new Dictionary<string, Color>();

        Transform RingObject(float rad, float z, float width)
        {
            var b = new Buf(); Disc2(b, ax, ay, rad, z, width);
            var mesh = new Mesh(); mesh.SetVertices(b.v); mesh.SetNormals(b.n); mesh.SetUVs(0, b.uv); mesh.SetTriangles(b.t, 0); mesh.RecalculateBounds();
            var go = new GameObject("ring"); go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = Material("glow"); mr.shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }
        void Disc2(Buf b, float cx, float cy, float rad, float z, float width)
        {
            // a band standing up (a short cylinder wall) so the ring reads from the side as well as above
            for (int i = 0; i < 64; i++)
            {
                float a0 = i * Mathf.PI * 2 / 64, a1 = (i + 1) * Mathf.PI * 2 / 64, am = (a0 + a1) / 2;
                var nrm = new Vector3(Mathf.Cos(am), 0, Mathf.Sin(am));
                var p0 = P(cx + Mathf.Cos(a0) * rad, cy + Mathf.Sin(a0) * rad, z - width / 2); var p1 = P(cx + Mathf.Cos(a1) * rad, cy + Mathf.Sin(a1) * rad, z - width / 2);
                var p2 = P(cx + Mathf.Cos(a1) * rad, cy + Mathf.Sin(a1) * rad, z + width / 2); var p3 = P(cx + Mathf.Cos(a0) * rad, cy + Mathf.Sin(a0) * rad, z + width / 2);
                Quad(b, p0, p1, p2, p3, nrm, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
                Quad(b, p0, p3, p2, p1, -nrm, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            }
        }
        Vector2 OnLoop(float s)
        {
            s = ((s % loopLen) + loopLen) % loopLen;
            for (int i = 0; i < 4; i++) { var a = loop[i]; var b = loop[(i + 1) % 4]; float len = Vector2.Distance(a, b); if (s <= len) return Vector2.Lerp(a, b, s / len); s -= len; }
            return loop[0];
        }

        // ------------------------------------------------------------------ materials
        Shader lit, unlit; Texture2D winTex, winEmit, glassTex, glassLit, homeTex, homeLit, roofTex;
        readonly List<Material> facades = new List<Material>();
        Material Material(string key)
        {
            if (mats.TryGetValue(key, out var m)) return m;
            if (key.StartsWith("face_") || key == "outface" || key == "glass")
            {
                string k = key.StartsWith("face_") ? key.Substring(5) : key == "glass" ? "A" : null;
                Color tint = k != null ? (Color)Tint[k] : new Color(0.62f, 0.66f, 0.72f);
                bool painted = k != null && glassTex != null, home = k != null && HomeKind(k) && key != "glass";
                m = new Material(lit);
                m.SetTexture("_BaseMap", painted ? (home ? homeTex : glassTex) : winTex);
                m.SetColor("_BaseColor", painted ? Color.Lerp(Color.white, tint, 0.45f) : tint); m.SetFloat("_Smoothness", key == "glass" || (painted && !home) ? 0.6f : 0.3f);
                m.EnableKeyword("_EMISSION"); m.SetTexture("_EmissionMap", painted ? (home ? homeLit : glassLit) : winEmit); m.SetColor("_EmissionColor", Color.black);
                m.name = key; facades.Add(m);
            }
            else if (key == "glow" || key == "glowsoft" || key == "red" || key == "lamp")
            {
                m = new Material(unlit);
                m.SetColor("_BaseColor", key == "red" ? new Color(4f, 0.4f, 0.4f) : key == "lamp" ? new Color(1.6f, 2.6f, 3f) : key == "glowsoft" ? new Color(0.25f, 0.9f, 1.1f) : new Color(0.5f, 1.9f, 2.4f));
                if (key == "glowsoft") { m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 2); m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = 3000;
                    m.SetFloat("_SrcBlend", (float)BlendMode.One); m.SetFloat("_DstBlend", (float)BlendMode.One); m.SetFloat("_ZWrite", 0); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); }
            }
            else
            {
                Color c; float sm = 0.15f;
                if (key.StartsWith("lot_")) c = new Color32(178, 184, 188, 255);
                else if (key.StartsWith("roof_")) { var t = (Color)Tint[key.Substring(5)]; c = roofTex != null ? Color.Lerp(Color.white, t, 0.4f) : t * 1.02f; }
                else if (key.StartsWith("col_")) c = colours[key];
                else switch (key)
                {
                    case "lot": c = new Color32(178, 184, 188, 255); break;
                    case "lotside": c = new Color32(150, 156, 162, 255); break;
                    case "road": c = new Color32(70, 78, 92, 255); sm = 0.3f; break;
                    case "outside": c = new Color32(128, 134, 144, 255); break;
                    case "outroof": c = new Color32(150, 156, 164, 255); break;
                    case "court": c = new Color32(200, 214, 206, 255); break;
                    case "plaza": c = new Color32(200, 206, 210, 255); break;
                    case "pool": c = new Color32(140, 196, 226, 255); sm = 0.9f; break;
                    case "water": c = new Color32(90, 170, 220, 255); sm = 0.95f; break;
                    case "solar": c = new Color32(40, 66, 130, 255); sm = 0.85f; break;
                    case "tank": c = new Color32(226, 232, 240, 255); sm = 0.5f; break;
                    case "tanktop": c = new Color32(240, 244, 248, 255); break;
                    case "dome": c = new Color32(246, 248, 250, 255); sm = 0.6f; break;
                    case "panel": c = new Color32(240, 240, 236, 255); break;
                    case "grass": c = new Color32(100, 160, 100, 255); break;
                    case "track": c = new Color32(190, 92, 78, 255); break;
                    case "dirt": c = new Color32(190, 168, 130, 255); break;
                    case "beam": c = new Color32(232, 236, 240, 255); sm = 0.4f; break;
                    case "crane": c = new Color32(236, 232, 220, 255); break;
                    case "pod": c = new Color32(250, 252, 255, 255); break;
                    default: c = Color.magenta; break;
                }
                m = new Material(lit); m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", sm);
                if (key.StartsWith("roof_") && roofTex != null) m.SetTexture("_BaseMap", roofTex);
            }
            m.enableInstancing = true;
            return mats[key] = m;
        }
        // the GPT textures (Tools/Art/model_look.py, model_textures.py); without them the procedural windows stay
        void LoadPainted()
        {
            glassTex = Resources.Load<Texture2D>("ModelCity/facade_glass"); glassLit = Resources.Load<Texture2D>("ModelCity/facade_glass_lit");
            homeTex = Resources.Load<Texture2D>("ModelCity/facade_home"); homeLit = Resources.Load<Texture2D>("ModelCity/facade_home_lit");
            roofTex = Resources.Load<Texture2D>("ModelCity/roof");
            if (roofTex != null) roofTex.wrapMode = TextureWrapMode.Mirror;
            var bays = Resources.Load<TextAsset>("ModelCity/bays");
            if (bays == null) return;
            foreach (var line in bays.text.Split('\n'))
            {
                var f = line.Trim().Split(' '); if (f.Length < 3) continue;
                // a bay is one storey (5.5 plan units) tall; its width follows the picture's bay shape (texture is square)
                int nx = int.Parse(f[1]), ny = int.Parse(f[2]); float bayH = 5.5f, span = bayH * ny;
                var v = new Vector2(span, span);   // square texture: the same span both ways keeps the bays' shape
                if (f[0] == "facade_glass") glassSpan = v; else if (f[0] == "facade_home") homeSpan = v;
            }
        }
        void MakeWindowTextures()
        {
            // 64 x 64 windows of 8 x 8 pixels: a pale frame, blue-grey glass; the emission map lights about half of them
            const int n = 64, px = 8;
            winTex = new Texture2D(n * px, n * px, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4, name = "windows" };
            winEmit = new Texture2D(n * px, n * px, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4, name = "windows_lit" };
            var a = new Color32[n * px * n * px]; var e = new Color32[a.Length];
            int s4 = 1234; float R() { s4 = (int)((s4 * 48271L) % 2147483647L); return s4 / 2147483647f; }
            for (int cy = 0; cy < n; cy++) for (int cx = 0; cx < n; cx++)
            {
                float g = 0.96f + R() * 0.08f; bool on = R() < 0.55f; float warm = R();
                var glass = new Color(0.52f * g, 0.67f * g, 0.82f * g);
                var light = Color.Lerp(new Color(1f, 0.78f, 0.45f), new Color(0.85f, 0.92f, 1f), warm < 0.8f ? 0 : 1) * (0.6f + R() * 0.4f);
                for (int y = 0; y < px; y++) for (int x = 0; x < px; x++)
                {
                    bool frame = y < 2 || x < 1;
                    int i = (cy * px + y) * n * px + cx * px + x;
                    a[i] = frame ? new Color(0.97f, 0.97f, 0.98f) : Color.Lerp(glass, Color.white, y / (float)px * 0.25f);
                    e[i] = !frame && on ? light : Color.black;
                }
            }
            winTex.SetPixels32(a); winTex.Apply(true); winEmit.SetPixels32(e); winEmit.Apply(true);
        }

        // ------------------------------------------------------------------ camera
        const float Home = 300, DMin = 60, DMax = 460;
        Vector2 target; float dist = Home, yaw; Vector3? goal;
        // Alt + drag: a tilt added to the zoom's own pitch (degrees), eased back to 0 by GoHome
        float tilt; float? tiltGoal; bool tilting;
        bool grabbing; float moved; Vector3 grab; float shadowWas; UniversalRenderPipelineAsset urp;

        float Pitch() { float t = Mathf.Clamp01((DMax - dist) / (DMax - DMin)); return Mathf.Clamp(32 + 54 * t * t + tilt, 18, 89) * Mathf.Deg2Rad; }
        void Place()
        {
            float p = Pitch();
            var f = new Vector3(Mathf.Sin(yaw) * Mathf.Cos(p), -Mathf.Sin(p), Mathf.Cos(yaw) * Mathf.Cos(p));
            cam.transform.SetPositionAndRotation(new Vector3(target.x, 0, target.y) - f * dist, Quaternion.LookRotation(f, Vector3.up));
        }
        public bool GroundAt(Vector2 screen, out Vector3 hit)
        {
            var ray = cam.ScreenPointToRay(screen); hit = default;
            if (ray.direction.y >= -1e-4f) return false;
            hit = ray.origin + ray.direction * (-ray.origin.y / ray.direction.y); return true;
        }
        void UpdateCamera()
        {
            var m = Mouse.current; if (m == null) return;
            var mp = m.position.ReadValue();
            bool overUi = hud != null && hud.Over(mp);
            var keys = Keyboard.current;
            bool alt = keys != null && (keys.leftAltKey.isPressed || keys.rightAltKey.isPressed);
            if (m.leftButton.wasPressedThisFrame) { tilting = alt && !overUi; grabbing = !tilting && !overUi && GroundAt(mp, out grab); moved = 0; if (grabbing || tilting) { goal = null; tiltGoal = null; } }
            if (!m.leftButton.isPressed) tilting = false;
            if (tilting)
            {
                // pull down: the far side comes toward you and the view turns to look from above; sideways turns the city
                var dlt = m.delta.ReadValue();
                float basePitch = Pitch() * Mathf.Rad2Deg - tilt;
                tilt = Mathf.Clamp(tilt - dlt.y * 0.25f, 18 - basePitch, 89 - basePitch);
                yaw += dlt.x * 0.005f; Place();
            }
            if (grabbing) moved += m.delta.ReadValue().magnitude;
            if (m.leftButton.wasReleasedThisFrame && grabbing && moved < 5 && hud != null && GroundAt(mp, out var hitP)) hud.Pick(DistrictAt(hitP));
            if (!m.leftButton.isPressed) grabbing = false;
            if (grabbing && GroundAt(mp, out var now)) { target += new Vector2(grab.x - now.x, grab.z - now.z); Place(); }
            if (m.rightButton.isPressed && !overUi) { yaw += m.delta.ReadValue().x * 0.005f; goal = null; Place(); }
            float wheel = m.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > 0.01f && !overUi)
            {
                goal = null;
                bool had = GroundAt(mp, out var before);
                dist = Mathf.Clamp(dist * (wheel < 0 ? 1.12f : 0.89f), DMin, DMax); Place();
                if (had && GroundAt(mp, out var after)) target += new Vector2(before.x - after.x, before.z - after.z);
            }
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame) { GoHome(); if (hud != null) hud.Pick(null); }
                if (kb.digit1Key.wasPressedThisFrame) hour = 10; if (kb.digit2Key.wasPressedThisFrame) hour = 16.5f;
                if (kb.digit3Key.wasPressedThisFrame) hour = 19; if (kb.digit4Key.wasPressedThisFrame) hour = 22;
                if (kb.tKey.wasPressedThisFrame) speed = speed > 0 ? 0 : 1;
            }
            if (tiltGoal is float tg) { tilt = Mathf.Lerp(tilt, tg, 0.08f); if (Mathf.Abs(tilt - tg) < 0.1f) { tilt = tg; tiltGoal = null; } }
            if (goal is Vector3 gl)
            {
                target = Vector2.Lerp(target, new Vector2(gl.x, gl.y), 0.08f); dist = Mathf.Lerp(dist, gl.z, 0.08f);
                if (Mathf.Abs(dist - gl.z) < 0.1f) goal = null;
            }
            target.x = Mathf.Clamp(target.x, -30, cw * S + 30); target.y = Mathf.Clamp(target.y, -30, ch * S + 30);
            Place();
        }

        // ------------------------------------------------------------------ time of day
        Material sky;
        float Night() => hour < 5 || hour >= 20 ? 1 : hour < 7 ? (7 - hour) / 2 : hour >= 17 ? (hour - 17) / 3 : 0;
        void UpdateLight()
        {
            float n = Night(), t = Mathf.Clamp01((hour - 6) / 12f), up = Mathf.Sin(Mathf.PI * t);
            bool day = hour > 6 && hour < 18;
            // morning sun on the right, evening sun on the left, noon behind the viewer
            var pos = day ? new Vector3(Mathf.Cos(Mathf.PI * t), Mathf.Max(0.06f, up) * 0.9f, -Mathf.Sin(Mathf.PI * t) * 0.7f) : new Vector3(-0.3f, 0.8f, 0.4f);
            sun.transform.rotation = Quaternion.LookRotation(-pos.normalized);
            sun.intensity = day ? Mathf.Lerp(1.5f, 2.2f, Mathf.Clamp01(up * 1.6f)) * (1 - n * 0.8f) : 0.12f;
            sun.color = day ? Color.Lerp(new Color(1f, 0.78f, 0.56f), new Color(1f, 0.95f, 0.88f), Mathf.Clamp01((up - 0.3f) * 2.2f)) : new Color(0.55f, 0.65f, 1f);
            var haze = Color.Lerp(new Color(0.66f, 0.73f, 0.82f), new Color(0.086f, 0.133f, 0.25f), n);
            if (day && up < 0.5f) haze = Color.Lerp(haze, new Color(0.86f, 0.72f, 0.62f), (0.5f - up) * 0.8f * (1 - n));
            RenderSettings.fogColor = haze;
            RenderSettings.fogStartDistance = dist * 1.05f; RenderSettings.fogEndDistance = dist * 2.1f;   // clear over the city, thick beyond it
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.40f, 0.46f, 0.58f), new Color(0.07f, 0.09f, 0.18f), n);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.36f, 0.37f, 0.40f), new Color(0.06f, 0.07f, 0.13f), n);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.32f, 0.33f, 0.36f), new Color(0.03f, 0.03f, 0.06f), n);
            if (sky != null) { sky.SetFloat("_Exposure", Mathf.Lerp(1.25f, 0.12f, n)); sky.SetFloat("_AtmosphereThickness", day && up < 0.4f ? 1.6f : 1f); }
            cam.backgroundColor = haze;
            // the tension layer tints each district's ground
            foreach (var k in Keys) if (mats.TryGetValue("lot_" + k, out var lm))
                lm.SetColor("_BaseColor", Color.Lerp(new Color32(178, 184, 188, 255), layer == "tension" ? TensionColor(Live(k).tension) : layer == "pop" ? new Color(1f, 0.85f, 0.55f) : new Color32(178, 184, 188, 255), layer == "pop" ? Mathf.Clamp01(Live(k).pop / PopScale) * 0.8f : layer == "tension" ? 0.6f : 0));
            foreach (var f in facades)
            {
                string k = f.name.StartsWith("face_") ? f.name.Substring(5) : f.name == "glass" ? "A" : null;
                float litK = k != null ? state[k].lit : 0.03f;   // the outside city: few lights
                f.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.62f) * (Mathf.Max(n, hour > 15.5f && hour < 20 ? 0.12f : 0) * litK * 2.4f));
            }
        }

        // ------------------------------------------------------------------ moving things (instanced)
        Mesh cube; readonly List<Matrix4x4> podsWarm = new List<Matrix4x4>(), podsCool = new List<Matrix4x4>(), drones = new List<Matrix4x4>(), cars = new List<Matrix4x4>();
        Material podWarm, podCool, carMat;
        void UpdateMovers()
        {
            float time = Time.time * 1000;
            podsWarm.Clear(); podsCool.Clear(); drones.Clear(); cars.Clear();
            for (int i = 0; i < roadsY.Count; i++)
            {
                float y = roadsY[i]; bool wide = i % 3 == 0; int count = wide ? 9 : 4;
                for (int k = 0; k < count; k++)
                {
                    float x = Mod(time * (wide ? 0.07f : 0.05f) * (k % 2 == 1 ? 1 : -1) + k * cw / count + i * 97, cw);
                    (k % 2 == 1 ? podsWarm : podsCool).Add(Matrix4x4.TRS(P(x, y + (k % 2 == 1 ? 4 : -4), 1.5f), Quaternion.identity, new Vector3(0.45f, 0.18f, 0.22f)));
                }
            }
            for (int i = 0; i < roadsX.Count; i++)
            {
                float x = roadsX[i]; bool wide = i % 3 == 0; int count = wide ? 7 : 3;
                for (int k = 0; k < count; k++)
                {
                    float y = Mod(time * (wide ? 0.06f : 0.04f) * (k % 2 == 1 ? 1 : -1) + k * ch / count + i * 61, ch);
                    (k % 2 == 1 ? podsWarm : podsCool).Add(Matrix4x4.TRS(P(x + (k % 2 == 1 ? 4 : -4), y, 1.5f), Quaternion.identity, new Vector3(0.22f, 0.18f, 0.45f)));
                }
            }
            var lanes = new[] { (new Vector2(0, ch * 0.3f), new Vector2(cw, ch * 0.45f)), (new Vector2(cw * 0.2f, 0), new Vector2(cw * 0.5f, ch)), (new Vector2(cw, ch * 0.8f), new Vector2(0, ch * 0.65f)) };
            foreach (var (a, b) in lanes)
                for (int i = 0; i < 10; i++) { float t = Mod(time * 0.00004f + i / 10f, 1); var p = Vector2.Lerp(a, b, t); drones.Add(Matrix4x4.TRS(P(p.x, p.y, 110), Quaternion.identity, Vector3.one * 0.35f)); }
            foreach (var o in new[] { 0f, 0.5f })
            {
                float s = time * 0.12f + o * loopLen;
                for (int i = 0; i < 3; i++)
                {
                    var a = OnLoop(s - i * 30); var b = OnLoop(s - i * 30 - 26); var mid = (a + b) / 2; var d = b - a;
                    if (d.sqrMagnitude < 1) continue;
                    cars.Add(Matrix4x4.TRS(P(mid.x, mid.y, 29), Quaternion.LookRotation(new Vector3(d.x, 0, d.y)), new Vector3(0.7f, 0.55f, 2.5f)));
                }
            }
            var rp = new RenderParams(podWarm) { shadowCastingMode = ShadowCastingMode.Off };
            if (podsWarm.Count > 0) Graphics.RenderMeshInstanced(rp, cube, 0, podsWarm);
            rp.material = podCool; if (podsCool.Count > 0) Graphics.RenderMeshInstanced(rp, cube, 0, podsCool);
            if (drones.Count > 0) Graphics.RenderMeshInstanced(rp, cube, 0, drones);
            var rc = new RenderParams(carMat) { shadowCastingMode = ShadowCastingMode.On };
            if (cars.Count > 0) Graphics.RenderMeshInstanced(rc, cube, 0, cars);

            float tt = Time.time;
            foreach (var cr in cranes)
            {
                if (cr.jib == null) continue;
                cr.jib.rotation = Quaternion.Euler(0, -(cr.ph + tt * 0.15f) * Mathf.Rad2Deg, 0);
                cr.lamp.enabled = Mathf.Sin(tt * 4) > 0;
            }
            if (ringB != null) ringB.localPosition = new Vector3(0, Mathf.Sin(tt) * 0.6f, 0);
        }
        static float Mod(float a, float m) => ((a % m) + m) % m;

        // ------------------------------------------------------------------ life cycle
        void Start()
        {
            lit = Shader.Find("Universal Render Pipeline/Lit"); unlit = Shader.Find("Universal Render Pipeline/Unlit");
            MakeWindowTextures();
            LoadPainted();
            Build();
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); cube = go.GetComponent<MeshFilter>().sharedMesh; Destroy(go);
            podWarm = new Material(unlit) { enableInstancing = true }; podWarm.SetColor("_BaseColor", new Color(3f, 2.4f, 1.6f));
            podCool = new Material(unlit) { enableInstancing = true }; podCool.SetColor("_BaseColor", new Color(1.2f, 3f, 3.6f));
            carMat = Material("pod");
            target = new Vector2(cw * S / 2, ch * S * 0.42f);
            // the lens sits low (as in the browser model): the city settles in the lower part, the sky above holds data
            cam.usePhysicalProperties = true; cam.gateFit = Camera.GateFitMode.Vertical; cam.fieldOfView = 32.8f; cam.lensShift = new Vector2(0, 0.18f);
            cam.farClipPlane = 2500; cam.nearClipPlane = 1;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.ambientMode = AmbientMode.Trilight;
            sky = new Material(Shader.Find("Skybox/Procedural")); sky.SetFloat("_SunDisk", 0); RenderSettings.skybox = sky; RenderSettings.sun = sun;
            urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null) { shadowWas = urp.shadowDistance; urp.shadowDistance = 700; }
            Place();
        }
        // the model wants long shadows; the setting lives in the shared URP asset, so it is put back when the model hides
        void OnEnable() { if (urp != null) { shadowWas = urp.shadowDistance; urp.shadowDistance = 700; } }
        void OnDisable() { if (urp != null) urp.shadowDistance = shadowWas; }
        void Update()
        {
            if (speed > 0) hour = Mod(hour + Time.deltaTime * speed / 20f, 24);
            UpdateCamera();
            UpdateLight();
            UpdateMovers();
        }
    }
}
