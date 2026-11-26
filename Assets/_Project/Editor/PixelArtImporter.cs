using UnityEditor;
using UnityEngine;

namespace Silence.Editor
{
    /// <summary>
    /// Applies the pixel-art import settings to every PNG under Assets/_Project/Art the first time it is imported:
    /// Point filter, no compression, 32 pixels per unit, full-rect mesh, centre pivot, no mipmaps.
    /// Files ending in "_n" are normal maps: imported as Default textures (PixelLab already outputs OpenGL normals)
    /// so they can be assigned as a sprite's _NormalMap secondary texture.
    /// Changes made by hand in the Inspector afterwards are kept.
    /// </summary>
    public sealed class PixelArtImporter : AssetPostprocessor
    {
        const string ArtRoot = "Assets/_Project/Art/";
        public const int PixelsPerUnit = 32;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot) || !assetPath.EndsWith(".png")) return;
            if (!assetImporter.importSettingsMissing) return; // only on first import

            var ti = (TextureImporter)assetImporter;
            bool isNormal = System.IO.Path.GetFileNameWithoutExtension(assetPath).EndsWith("_n");

            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.alphaIsTransparency = !isNormal;

            if (isNormal)
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = false;
                return;
            }

            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = PixelsPerUnit;

            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteGenerateFallbackPhysicsShape = false;
            ti.SetTextureSettings(settings);
        }
    }
}
