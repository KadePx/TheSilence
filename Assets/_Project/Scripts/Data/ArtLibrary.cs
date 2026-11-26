using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Silence.Data
{
    /// <summary>
    /// Every sprite, font and sound the game loads by name. Built by the editor (Silence > Rebuild Art Library)
    /// from Assets/_Project/Art, Fonts and Audio and stored in Resources, so the view can ask for
    /// "asc_cruiser_NE" or "sfx_lance" without scene references.
    /// </summary>
    [CreateAssetMenu(menuName = "Silence/Art Library")]
    public sealed class ArtLibrary : ScriptableObject
    {
        public const string ResourcePath = "ArtLibrary";

        public List<Sprite> Sprites = new List<Sprite>();
        public List<TMP_FontAsset> Fonts = new List<TMP_FontAsset>();
        public List<AudioClip> Clips = new List<AudioClip>();

        Dictionary<string, Sprite> spriteIndex;
        Dictionary<string, AudioClip> clipIndex;
        static ArtLibrary instance;

        public static ArtLibrary Instance
        {
            get
            {
                if (instance == null) instance = Resources.Load<ArtLibrary>(ResourcePath);
                if (instance == null) instance = CreateInstance<ArtLibrary>();
                return instance;
            }
        }

        public Sprite Sprite(string name)
        {
            if (spriteIndex == null)
            {
                spriteIndex = new Dictionary<string, Sprite>(StringComparer.Ordinal);
                foreach (var s in Sprites)
                    if (s != null) spriteIndex[s.name] = s;
            }
            return name != null && spriteIndex.TryGetValue(name, out var found) ? found : null;
        }

        public AudioClip Clip(string name)
        {
            if (clipIndex == null)
            {
                clipIndex = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
                foreach (var c in Clips)
                    if (c != null) clipIndex[c.name] = c;
            }
            return name != null && clipIndex.TryGetValue(name, out var found) ? found : null;
        }

        public TMP_FontAsset Font(string name)
        {
            foreach (var f in Fonts)
                if (f != null && f.name == name) return f;
            return null;
        }

        void OnEnable()
        {
            spriteIndex = null;
            clipIndex = null;
        }
    }
}
