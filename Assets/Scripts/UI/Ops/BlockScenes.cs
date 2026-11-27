using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Sector.UI
{
    // One district's observation picture (Resources/Art/Blocks/<key>.png, 1536 x 1024) and where people may walk.
    // Data comes from Resources/Art/Blocks/layout.txt, written by Tools/Art/blocks_data.py (the same data drives the
    // browser prototype Tools/Art/proto/observe.html). Rectangles are x0 y0 x1 y1 in picture pixels.
    public sealed class BlockScene
    {
        public const float Width = 1536f, Height = 1024f;
        const int Cell = 16, GW = 96, GH = 64;

        public string Key;
        public int Zone;
        public readonly List<RectInt> Walk = new List<RectInt>();
        public readonly List<RectInt> Solid = new List<RectInt>();
        public readonly List<RectInt> Occ = new List<RectInt>();      // stands in front of people behind it
        public readonly List<RectInt> Shops = new List<RectInt>();
        public Vector2 Front, Far, Gather, Watch;
        public Vector2? Queue;
        public RectInt? Screen;                                        // x, y, width, height

        bool[] grid;
        readonly List<int> cells = new List<int>();

        static bool Inside(RectInt r, float x, float y, float pad = 0f) =>
            x >= r.xMin - pad && x <= r.xMax + pad && y >= r.yMin - pad && y <= r.yMax + pad;

        void BuildGrid()
        {
            grid = new bool[GW * GH];
            for (int gy = 0; gy < GH; gy++)
                for (int gx = 0; gx < GW; gx++)
                {
                    float x = gx * Cell + Cell / 2f, y = gy * Cell + Cell / 2f;
                    bool ok = Walk.Exists(r => Inside(r, x, y)) && !Solid.Exists(r => Inside(r, x, y, 6f));
                    grid[gy * GW + gx] = ok;
                    if (ok)
                        cells.Add(gy * GW + gx);
                }
        }

        static Vector2 Centre(int c) => new Vector2((c % GW) * Cell + Cell / 2f, (c / GW) * Cell + Cell / 2f);

        int CellOf(Vector2 p) =>
            Mathf.Clamp((int)(p.y / Cell), 0, GH - 1) * GW + Mathf.Clamp((int)(p.x / Cell), 0, GW - 1);

        int Nearest(Vector2 p)
        {
            int best = cells.Count > 0 ? cells[0] : 0;
            float bd = float.MaxValue;
            foreach (var c in cells)
            {
                float d = (Centre(c) - p).sqrMagnitude;
                if (d < bd) { bd = d; best = c; }
            }
            return best;
        }

        public Vector2 RandomSpot()
        {
            if (cells.Count == 0)
                return new Vector2(Width / 2f, Height * 0.75f);
            return Centre(cells[Random.Range(0, cells.Count)]) + new Vector2(Random.Range(-4f, 4f), Random.Range(-4f, 4f));
        }

        // Breadth-first search on the walk grid (8 directions, no cutting corners); cell centres are the waypoints.
        public List<Vector2> Path(Vector2 from, Vector2 to)
        {
            var path = new List<Vector2>();
            if (cells.Count == 0)
                return path;
            int a = CellOf(from), b = CellOf(to);
            if (!grid[a]) a = Nearest(from);
            if (!grid[b]) b = Nearest(to);
            var prev = new int[GW * GH];
            for (int i = 0; i < prev.Length; i++) prev[i] = -1;
            var queue = new Queue<int>();
            queue.Enqueue(a);
            prev[a] = a;
            while (queue.Count > 0 && prev[b] < 0)
            {
                int c = queue.Dequeue(), cx = c % GW, cy = c / GW;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = cx + dx, ny = cy + dy;
                        if (nx < 0 || ny < 0 || nx >= GW || ny >= GH) continue;
                        int n = ny * GW + nx;
                        if (!grid[n] || prev[n] >= 0) continue;
                        if (dx != 0 && dy != 0 && (!grid[cy * GW + nx] || !grid[ny * GW + cx])) continue;
                        prev[n] = c;
                        queue.Enqueue(n);
                    }
            }
            if (prev[b] < 0)
                return path;
            for (int c = b; c != a; c = prev[c])
                path.Add(Centre(c) + new Vector2(Random.Range(-3f, 3f), Random.Range(-3f, 3f)));
            path.Reverse();
            return path;
        }

        // ---- loading
        static Dictionary<int, BlockScene> byZone;

        public static BlockScene ForZone(int zone)
        {
            if (byZone == null)
                Load();
            return byZone.TryGetValue(zone, out var s) ? s : null;
        }

        static void Load()
        {
            byZone = new Dictionary<int, BlockScene>();
            var text = Resources.Load<TextAsset>("Art/Blocks/layout");
            if (text == null)
                return;
            BlockScene cur = null;
            foreach (var raw in text.text.Split('\n'))
            {
                var p = raw.Trim().Split(' ');
                if (p.Length < 2)
                    continue;
                if (p[0] == "block")
                {
                    if (cur != null) cur.BuildGrid();
                    cur = new BlockScene { Key = p[1], Zone = int.Parse(p[2], CultureInfo.InvariantCulture) };
                    byZone[cur.Zone] = cur;
                    continue;
                }
                if (cur == null)
                    continue;
                var n = new int[p.Length - 1];
                for (int i = 1; i < p.Length; i++)
                    n[i - 1] = int.Parse(p[i], CultureInfo.InvariantCulture);
                RectInt R() => new RectInt(n[0], n[1], n[2] - n[0], n[3] - n[1]);
                switch (p[0])
                {
                    case "walk": cur.Walk.Add(R()); break;
                    case "solid": cur.Solid.Add(R()); break;
                    case "occ": cur.Occ.Add(R()); break;
                    case "shops": cur.Shops.Add(R()); break;
                    case "front": cur.Front = new Vector2(n[0], n[1]); break;
                    case "far": cur.Far = new Vector2(n[0], n[1]); break;
                    case "gather": cur.Gather = new Vector2(n[0], n[1]); break;
                    case "watch": cur.Watch = new Vector2(n[0], n[1]); break;
                    case "queue": cur.Queue = new Vector2(n[0], n[1]); break;
                    case "screen": cur.Screen = new RectInt(n[0], n[1], n[2], n[3]); break;
                }
            }
            if (cur != null) cur.BuildGrid();
        }
    }
}
