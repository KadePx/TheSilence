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

        [MenuItem("Silence/Bake Fonts")]
        public static void BakeFonts()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Font", new[] { FontDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string outPath = Path.ChangeExtension(path, null) + " SDF.asset";
                if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outPath) != null) continue;

                var font = AssetDatabase.LoadAssetAtPath<Font>(path);
                var fa = TMP_FontAsset.CreateFontAsset(font, 72, 7, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
                fa.name = Path.GetFileNameWithoutExtension(outPath);
                fa.TryAddCharacters(Charset, out string missing);
                fa.atlasPopulationMode = AtlasPopulationMode.Static;
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
