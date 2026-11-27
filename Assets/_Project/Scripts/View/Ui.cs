using System;
using Silence.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Silence.View
{
    /// <summary>
    /// Small uGUI factory. Everything is placed in a 1920x1080 reference space with the origin at the
    /// top-left: Box(parent, x, y, w, h) puts a rect x pixels right and y pixels down from its parent's corner.
    /// </summary>
    public static class Ui
    {
        public const float RefW = 1920f, RefH = 1080f;

        /// <summary>
        /// How many screen pixels one art pixel covers: the largest whole number that still fits the
        /// 1920x1080 layout on screen (1 up to 1440p, 2 at 4K). Never a fraction, so pixel art stays crisp.
        /// </summary>
        public static int PixelScale => Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(Screen.width / RefW, Screen.height / RefH)));

        /// <summary>Orthographic size that draws world sprites (32 PPU) at exactly <see cref="PixelScale"/>.</summary>
        public static float WorldHalfHeight => Screen.height / (2f * 32f * PixelScale);

        public static Canvas Canvas(string name, int order = 0)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            canvas.pixelPerfect = true; // pixel fonts and plates stay on whole pixels
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = PixelScale;
            scaler.referencePixelsPerUnit = 32f;
            go.AddComponent<PixelScaler>();
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        /// <summary>A 1920x1080 root inside the canvas, centred, so wider screens letterbox the layout rather than stretch it.</summary>
        public static RectTransform Root(Canvas canvas)
        {
            var rt = new GameObject("Root", typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(canvas.transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(RefW, RefH);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<AudioListener>() == null && Camera.main != null) Camera.main.gameObject.AddComponent<AudioListener>();
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        public static RectTransform Box(Transform parent, string name, float x, float y, float w, float h)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            Place(rt, x, y, w, h);
            return rt;
        }

        public static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        public static Image Img(Transform parent, string name, Sprite sprite, float x, float y, float w, float h, Color? color = null, bool sliced = false)
        {
            var rt = Box(parent, name, x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color ?? Color.white;
            img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            img.raycastTarget = false;
            return img;
        }

        public static Image Plate(Transform parent, string name, float x, float y, float w, float h)
        {
            var img = Img(parent, name, Skin.Plate, x, y, w, h, Color.white, true);
            img.type = Image.Type.Tiled; // tile the centre so the grime stays 1:1 instead of stretching
            img.raycastTarget = true; // plates block clicks on the board beneath
            return img;
        }

        public static Image Well(Transform parent, string name, float x, float y, float w, float h)
        {
            var img = Img(parent, name, Skin.Well, x, y, w, h, Color.white, true);
            img.type = Image.Type.Tiled;
            return img;
        }

        /// <summary>A small floating plate (tooltips, fleet cards, chips with content).</summary>
        public static Image Panel(Transform parent, string name, float x, float y, float w, float h)
        {
            var img = Img(parent, name, Skin.Panel, x, y, w, h, Color.white, true);
            img.raycastTarget = true;
            return img;
        }

        /// <summary>A bar: recessed track with a coloured fill, 0 to 1.</summary>
        public static Image Bar(Transform parent, string name, float x, float y, float w, float h, float frac, Color color)
        {
            var track = Img(parent, name, Skin.BarTrack, x, y, w, h, Color.white, true);
            float inner = Mathf.Round((w - 8f) * Mathf.Clamp01(frac));
            if (inner > 0) Fill(track.transform, "Fill", 4, 4, inner, h - 8, color);
            return track;
        }

        public static Image Fill(Transform parent, string name, float x, float y, float w, float h, Color color)
            => Img(parent, name, Skin.Pixel, x, y, w, h, color);

        /// <summary>Pixel art by library name at an integer scale, keeping its aspect.</summary>
        public static Image Art(Transform parent, string spriteName, float x, float y, float scale = 1f, Color? color = null)
        {
            var s = ArtLibrary.Instance.Sprite(spriteName);
            float w = s != null ? s.rect.width * scale : 24 * scale, h = s != null ? s.rect.height * scale : 24 * scale;
            var img = Img(parent, spriteName ?? "art", s, x, y, w, h, color);
            if (s == null) img.enabled = false;
            return img;
        }

        public static TextMeshProUGUI Label(Transform parent, string text, TMP_FontAsset font, float size, Color color,
            float x, float y, float w, float h, TextAlignmentOptions align = TextAlignmentOptions.TopLeft, float bias = 0.15f)
        {
            var rt = Box(parent, "Text", Mathf.Round(x), Mathf.Round(y), Mathf.Round(w), Mathf.Round(h));
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.fontSize = Skin.Snap(font, size, bias); // pixel fonts only at whole multiples of their native size
            t.color = color;
            t.alignment = align;
            t.text = text;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.richText = true;
            return t;
        }

        /// <summary>Headings: Saira Extra Condensed, uppercase.</summary>
        public static TextMeshProUGUI Heading(Transform parent, string text, float size, float x, float y, float w, float h,
            TextAlignmentOptions align = TextAlignmentOptions.Left, Color? color = null)
            => Label(parent, text.ToUpperInvariant(), Skin.Heading, size, color ?? Skin.Text, x, y, w, h, align);

        /// <summary>Labels and stamps: Saira Stencil One, letter-spaced.</summary>
        public static TextMeshProUGUI Stamp(Transform parent, string text, float size, float x, float y, float w, float h,
            TextAlignmentOptions align = TextAlignmentOptions.Left, Color? color = null)
        {
            var t = Label(parent, Gloss.Mark(text.ToUpperInvariant(), true, true), Skin.Stencil, size, color ?? Skin.Dim, x, y, w, h, align);
            return Glossed(t);
        }

        /// <summary>
        /// Make the rules words in a label hoverable. Stamps and body text get this as they are made; a label
        /// whose text is set later calls it once and marks each new text with <see cref="Gloss.Mark"/>.
        /// </summary>
        public static TextMeshProUGUI Glossed(TextMeshProUGUI t, bool always = false)
        {
            if (!always && t.text.IndexOf("<link=", StringComparison.Ordinal) < 0) return t;
            if (t.GetComponent<GlossText>() == null) t.gameObject.AddComponent<GlossText>().Text = t;
            TipLayer.Ensure();
            return t;
        }

        public static TextMeshProUGUI Body(Transform parent, string text, float size, float x, float y, float w, float h,
            TextAlignmentOptions align = TextAlignmentOptions.TopLeft, Color? color = null)
            => Glossed(Label(parent, Gloss.Mark(text), Skin.Body, size, color ?? Skin.Text, x, y, w, h, align));

        public static TextMeshProUGUI Number(Transform parent, string text, float size, float x, float y, float w, float h,
            TextAlignmentOptions align = TextAlignmentOptions.Left, Color? color = null)
            => Label(parent, text, Skin.Mono, size, color ?? Skin.Text, x, y, w, h, align, 0.45f);

        /// <summary>Chamfered steel button with a brass label; brass with dark text when highlighted.</summary>
        public static Button Btn(Transform parent, string label, float x, float y, float w, float h, Action onClick, float fontSize = 26f)
        {
            var img = Img(parent, "Button " + label, Skin.Button, x, y, w, h, Color.white, true);
            img.raycastTarget = true;
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = new Color(0.86f, 0.86f, 0.88f, 1f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.6f, 0.6f, 0.62f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(0.45f, 0.45f, 0.48f, 0.8f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.05f;
            btn.colors = colors;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            var t = Heading(img.transform, label, fontSize, 0, 0, w, h, TextAlignmentOptions.Center, Skin.Brass);
            Stretch(t.rectTransform);
            img.gameObject.AddComponent<ButtonLabelTint>().Init(btn, t);
            if (onClick != null)
                btn.onClick.AddListener(() =>
                {
                    Sfx.Play("sfx_click");
                    onClick();
                });
            return btn;
        }

        public static void SetButtonLabel(Button b, string text)
        {
            var t = b.GetComponentInChildren<TextMeshProUGUI>();
            if (t != null) t.text = text.ToUpperInvariant();
        }

        public static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>A full-screen background image scaled by a whole number and cropped (pixel art stays crisp).</summary>
        public static Image Backdrop(Transform parent, string spriteName, Color? tint = null)
        {
            var s = ArtLibrary.Instance.Sprite(spriteName);
            var img = Img(parent, "Backdrop", s, 0, 0, RefW, RefH, tint ?? Color.white);
            if (s == null)
            {
                img.sprite = Skin.Pixel;
                img.color = Skin.Ink;
                return img;
            }
            float scale = Mathf.Ceil(Mathf.Max(RefW / s.rect.width, RefH / s.rect.height));
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(s.rect.width * scale, s.rect.height * scale);
            return img;
        }

        /// <summary>Invisible click catcher that calls back with pointer events.</summary>
        public static PointerRelay Catcher(Transform parent, string name, float x, float y, float w, float h)
        {
            var img = Img(parent, name, Skin.Pixel, x, y, w, h, new Color(0, 0, 0, 0));
            img.raycastTarget = true;
            return img.gameObject.AddComponent<PointerRelay>();
        }

        public static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
        }
    }

    /// <summary>
    /// Keeps a canvas (and the main camera) at a whole-number pixel scale as the window changes, and masks
    /// whatever lies outside the 1920x1080 layout with black bars.
    /// </summary>
    public sealed class PixelScaler : MonoBehaviour
    {
        CanvasScaler scaler;
        RectTransform[] bars;
        int w, h;

        void Start()
        {
            scaler = GetComponent<CanvasScaler>();
            bars = new RectTransform[4];
            for (int i = 0; i < 4; i++)
            {
                var img = Ui.Img(transform, "Letterbox", Skin.Pixel, 0, 0, 0, 0, Color.black);
                img.raycastTarget = true;
                bars[i] = img.rectTransform;
                bars[i].anchorMin = bars[i].anchorMax = bars[i].pivot = new Vector2(0.5f, 0.5f);
            }
            Apply();
        }

        void LateUpdate()
        {
            if (Screen.width != w || Screen.height != h) Apply();
            for (int i = 0; i < 4; i++) bars[i].SetAsLastSibling();
        }

        void Apply()
        {
            w = Screen.width;
            h = Screen.height;
            int k = Ui.PixelScale;
            scaler.scaleFactor = k;
            var cam = Camera.main;
            if (cam != null && cam.orthographic) cam.orthographicSize = Ui.WorldHalfHeight;

            // Canvas size in layout pixels; bars cover everything beyond the centred 1920x1080 root.
            float cw = w / (float)k, ch = h / (float)k;
            float sideW = Mathf.Max(0f, (cw - Ui.RefW) / 2f) + 2f, capH = Mathf.Max(0f, (ch - Ui.RefH) / 2f) + 2f;
            Set(bars[0], -(Ui.RefW / 2f + sideW / 2f), 0, sideW, ch + 4f, cw > Ui.RefW);
            Set(bars[1], Ui.RefW / 2f + sideW / 2f, 0, sideW, ch + 4f, cw > Ui.RefW);
            Set(bars[2], 0, Ui.RefH / 2f + capH / 2f, cw + 4f, capH, ch > Ui.RefH);
            Set(bars[3], 0, -(Ui.RefH / 2f + capH / 2f), cw + 4f, capH, ch > Ui.RefH);
        }

        static void Set(RectTransform rt, float x, float y, float bw, float bh, bool on)
        {
            rt.gameObject.SetActive(on);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(bw, bh);
        }
    }

    /// <summary>Fades a graphic in and out, to draw the eye without scaling anything.</summary>
    public sealed class Blink : MonoBehaviour
    {
        public float Speed = 3f, Min = 0.3f;
        Graphic graphic;

        void Awake() => graphic = GetComponent<Graphic>();

        void Update()
        {
            if (graphic == null) return;
            var c = graphic.color;
            c.a = Min + (1f - Min) * Mathf.Abs(Mathf.Sin(Time.unscaledTime * Speed));
            graphic.color = c;
        }
    }

    /// <summary>Brass label on steel; dark label when the button turns brass under the pointer; grey when disabled.</summary>
    public sealed class ButtonLabelTint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        Button button;
        TextMeshProUGUI label;
        bool hover;

        public void Init(Button b, TextMeshProUGUI t)
        {
            button = b;
            label = t;
        }

        public void OnPointerEnter(PointerEventData e) => hover = true;
        public void OnPointerExit(PointerEventData e) => hover = false;
        void OnDisable() => hover = false;

        void LateUpdate()
        {
            if (label == null || button == null) return;
            label.color = !button.interactable ? Skin.Faint : hover ? Skin.BrassLight : Skin.Brass;
        }
    }

    /// <summary>Forwards pointer events from a UI element to plain callbacks.</summary>
    public sealed class PointerRelay : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        public Action<PointerEventData> Click, Move;
        public Action Enter, Exit;

        public void OnPointerClick(PointerEventData e) => Click?.Invoke(e);
        public void OnPointerEnter(PointerEventData e) => Enter?.Invoke();
        public void OnPointerExit(PointerEventData e) => Exit?.Invoke();
        public void OnPointerMove(PointerEventData e) => Move?.Invoke(e);
    }

    /// <summary>Plays short sound effects by library name. Missing clips are silently skipped.</summary>
    public static class Sfx
    {
        static AudioSource source;
        const string Key = "silence_sfx_volume";
        static float volume = -1f;

        /// <summary>0 to 1, remembered between sessions.</summary>
        public static float Volume
        {
            get
            {
                if (volume < 0f) volume = PlayerPrefs.GetFloat(Key, 0.6f);
                return volume;
            }
            set
            {
                volume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(Key, volume);
            }
        }

        public static void Play(string clip, float volume = 1f)
        {
            var c = ArtLibrary.Instance.Clip(clip);
            if (c == null) return;
            if (source == null)
            {
                var go = new GameObject("Sfx");
                UnityEngine.Object.DontDestroyOnLoad(go);
                source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
            }
            source.PlayOneShot(c, Volume * volume);
        }
    }

    /// <summary>
    /// One looping music track at a time, by library name, cross-faded when the screen changes. Asking for the
    /// track that is already playing does nothing, so every screen can simply say what it wants.
    /// </summary>
    public sealed class Music : MonoBehaviour
    {
        const string Key = "silence_music_volume";
        const float FadeSeconds = 1.5f;
        static Music player;
        static float volume = -1f;

        AudioSource front, back;
        string current = "";

        /// <summary>0 to 1, remembered between sessions.</summary>
        public static float Volume
        {
            get
            {
                if (volume < 0f) volume = PlayerPrefs.GetFloat(Key, 0.35f);
                return volume;
            }
            set
            {
                volume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(Key, volume);
            }
        }

        /// <summary>Name of the track now playing, or empty.</summary>
        public static string Current => player != null ? player.current : "";

        public static void Play(string clip)
        {
            if (player == null)
            {
                var go = new GameObject("Music");
                DontDestroyOnLoad(go);
                player = go.AddComponent<Music>();
                player.front = go.AddComponent<AudioSource>();
                player.back = go.AddComponent<AudioSource>();
                foreach (var s in new[] { player.front, player.back })
                {
                    s.loop = true;
                    s.playOnAwake = false;
                    s.volume = 0f;
                }
            }
            if (player.current == clip) return;
            var c = ArtLibrary.Instance.Clip(clip);
            if (c == null) return;
            player.current = clip;
            // The old track fades out on the back source while the new one fades in on the front.
            var swap = player.front;
            player.front = player.back;
            player.back = swap;
            player.front.clip = c;
            player.front.volume = 0f;
            player.front.Play();
        }

        void Update()
        {
            float step = Time.unscaledDeltaTime / FadeSeconds;
            front.volume = Mathf.MoveTowards(front.volume, Volume, step);
            back.volume = Mathf.MoveTowards(back.volume, 0f, step);
            if (back.isPlaying && back.volume <= 0f) back.Stop();
        }
    }
}
