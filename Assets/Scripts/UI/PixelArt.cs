using System.Collections.Generic;
using UnityEngine;

namespace Sector.UI
{
    // Art in Resources/Art (portraits, sim scenes and sprites from Tools/Art/cast2.py, illustrations from
    // Tools/Art/illust.py), cached by path. Anything missing comes back null.
    public static class PixelArt
    {
        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        public static Texture2D Load(string path)
        {
            if (!Cache.TryGetValue(path, out var texture))
            {
                texture = Resources.Load<Texture2D>("Art/" + path);
                Cache[path] = texture;
            }
            return texture;
        }
    }
}
