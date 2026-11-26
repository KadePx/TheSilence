using System.Collections.Generic;
using System.IO;
using System.Linq;
using Silence.Data;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Silence.Editor
{
    /// <summary>
    /// Builds Resources/ArtLibrary.asset from the Art, Fonts and Audio folders, and bakes the TextMeshPro
    /// font assets from the TTF files (static atlases, so playing the game never dirties them).
    /// </summary>
    public static class ArtLibraryBuilder
    {
        const string LibraryPath = "Assets/_Project/Resources/ArtLibrary.asset";
        const string FontDir = "Assets/_Project/Fonts";
        const string Charset = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~°·×–—’“”•…";

        [MenuItem("Silence/Rebuild Art Library")]
        public static void Rebuild()
        {
            var lib = AssetDatabase.LoadAssetAtPath<ArtLibrary>(LibraryPath);
            if (lib == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
                lib = ScriptableObject.CreateInstance<ArtLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }

            lib.Sprites = AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/_Project/Art" })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p)
                .SelectMany(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<Sprite>()).ToList();

            lib.Clips = Directory.Exists("Assets/_Project/Audio")
                ? AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/_Project/Audio" })
                    .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p)
                    .Select(AssetDatabase.LoadAssetAtPath<AudioClip>).ToList()
                : new List<AudioClip>();

            lib.Fonts = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { FontDir })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>).ToList();

            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ArtLibrary] {lib.Sprites.Count} sprites, {lib.Fonts.Count} fonts, {lib.Clips.Count} clips");
        }

        /// <summary>Native glyph size of a pixel font, or 0 for a smooth font.</summary>
        public static int PixelSize(string path)
        {
            if (!path.Contains("/Fonts/Pixel/")) return 0;
            string n = Path.GetFileNameWithoutExtension(path);
            return n.Contains("Logo") ? 32 : n.Contains("Label") ? 12 : 16;
        }

        [MenuItem("Silence/Bake Fonts")]
        public static void BakeFonts()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Font", new[] { FontDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                // Pixel fonts (Fonts/Pixel) are baked as 1-bit bitmaps at their native size and drawn only at
                // whole multiples of it; everything else is a smooth SDF font.
                int px = PixelSize(path);
                string outPath = Path.ChangeExtension(path, null) + (px > 0 ? " PX.asset" : " SDF.asset");
                if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outPath) != null) continue;

                var font = AssetDatabase.LoadAssetAtPath<Font>(path);
                var fa = px > 0
                    ? TMP_FontAsset.CreateFontAsset(font, px, 2, GlyphRenderMode.RASTER, 512, 512, AtlasPopulationMode.Dynamic)
                    : TMP_FontAsset.CreateFontAsset(font, 72, 7, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
                fa.name = Path.GetFileNameWithoutExtension(outPath);
                fa.TryAddCharacters(Charset, out string missing);
                fa.atlasPopulationMode = AtlasPopulationMode.Static;
                if (px > 0) fa.atlasTexture.filterMode = FilterMode.Point;
                AssetDatabase.CreateAsset(fa, outPath);
                fa.material.name = fa.name + " Material";
                fa.atlasTexture.name = fa.name + " Atlas";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
                AssetDatabase.AddObjectToAsset(fa.atlasTexture, fa);
                EditorUtility.SetDirty(fa);
                Debug.Log($"[Fonts] baked {outPath}" + (string.IsNullOrEmpty(missing) ? "" : $" (missing: {missing})"));
            }
            AssetDatabase.SaveAssets();
            Rebuild();
        }
    }
}
