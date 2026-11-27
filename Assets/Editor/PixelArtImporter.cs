using System.IO;
using UnityEditor;
using UnityEngine;

namespace Sector.EditorTools
{
    // Keeps the generated pixel art crisp: no filtering, compression or mipmaps.
    // Paintings (Tools/Art/paint.py) share the folder; anything wider than PaintedWidth is filtered smoothly instead,
    // except the simulation panel's pixel scenes in Sim/.
    public class PixelArtImporter : AssetPostprocessor
    {
        const string ArtFolder = "Assets/Resources/Art/";
        const int PaintedWidth = 160;

        // UI frames and big paintings outside Art/: smooth, uncompressed, no mipmaps (they are drawn near 1:1).
        // Map/ holds the per-district layers cut from the city painting (Tools/Art/district_layers.py).
        static readonly string[] SmoothFolders = { "Assets/Resources/UiKit/", "Assets/Resources/Macro/", "Assets/Resources/Map/" };

        void OnPreprocessTexture()
        {
            foreach (var folder in SmoothFolders)
                if (assetPath.StartsWith(folder))
                {
                    var smooth = (TextureImporter)assetImporter;
                    smooth.textureType = TextureImporterType.Default;
                    smooth.filterMode = FilterMode.Bilinear;
                    smooth.textureCompression = TextureImporterCompression.Uncompressed;
                    smooth.mipmapEnabled = false;
                    smooth.npotScale = TextureImporterNPOTScale.None;
                    smooth.alphaIsTransparency = true;
                    smooth.wrapMode = TextureWrapMode.Clamp;
                    // the 8K city pictures (Tools/Art/city_v9.py): full size so zooming stays sharp, high-quality
                    // compression (uncompressed they would take ~130 MB each), mipmaps so the whole view stays clean
                    if (assetPath.StartsWith("Assets/Resources/Macro/"))
                    {
                        smooth.maxTextureSize = 8192;
                        smooth.textureCompression = TextureImporterCompression.CompressedHQ;
                        smooth.mipmapEnabled = true;
                        smooth.alphaIsTransparency = false;
                    }
                    return;
                }
            if (!assetPath.StartsWith(ArtFolder))
                return;
            var importer = (TextureImporter)assetImporter;
            // Sim/ holds the simulation panel's pixel scenes, which are wide but never painted.
            bool painted = !assetPath.StartsWith(ArtFolder + "Sim/") && PngWidth(assetPath) > PaintedWidth;
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = painted ? FilterMode.Bilinear : FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = painted;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
        }

        // Width from the PNG header (bytes 16-19, big-endian), before Unity has read the image.
        static int PngWidth(string path)
        {
            try
            {
                using var stream = File.OpenRead(path);
                var header = new byte[24];
                if (stream.Read(header, 0, 24) < 24)
                    return 0;
                return (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
            }
            catch (IOException)
            {
                return 0;
            }
        }
    }
}
