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

        public static Canvas Canvas(string name, int order = 0)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefW, RefH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            scaler.referencePixelsPerUnit = 32f;
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
            => Img(parent, name, Skin.Well, x, y, w, h, Color.white, true);

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
            float x, float y, float w, float h, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var rt = Box(parent, "Text", x, y, w, h);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.fontSize = size;
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
            var t = Label(parent, text.ToUpperInvariant(), Skin.Stencil, size, color ?? Skin.Dim, x, y, w, h, align);
            t.characterSpacing = 8f;
            return t;
        }

        public static TextMeshProUGUI Body(Transform parent, string text, float size, float x, float y, float w, float h,
            TextAlignmentOptions align = TextAlignmentOptions.TopLeft, Color? color = null)
            => Label(parent, text, Skin.Body, size, color ?? Skin.Text, x, y, w, h, align);

        public static TextMeshProUGUI Number(Transform parent, string text, float size, float x, float y, float w, float h,
            TextAlignmentOptions align = TextAlignmentOptions.Left, Color? color = null)
            => Label(parent, text, Skin.Mono, size, color ?? Skin.Text, x, y, w, h, align);

        /// <summary>Chamfered steel button with a brass label; brass with dark text when highlighted.</summary>
        public static Button Btn(Transform parent, string label, float x, float y, float w, float h, Action onClick, float fontSize = 26f)
        {
            var img = Img(parent, "Button " + label, Skin.Button, x, y, w, h, Color.white, true);
            img.raycastTarget = true;
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Skin.Steel;
            colors.highlightedColor = Skin.Brass;
            colors.pressedColor = Skin.BrassDark;
            colors.selectedColor = Skin.Steel;
            colors.disabledColor = new Color(0.16f, 0.17f, 0.2f, 1f);
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
            label.color = !button.interactable ? Skin.Faint : hover ? Skin.Ink : Skin.Brass;
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
        public static float Volume = 0.6f;

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
}
