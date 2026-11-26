using System;
using Silence.Core.Battle;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Silence.View
{
    /// <summary>
    /// One card: etched plate, brass hexagonal cost badge, condensed caps name, art of the source ship,
    /// type line, rules text and a stencil footer naming the ship and module. Junk is rust-tinted;
    /// a card that can't be played right now is dimmed with a red cost.
    /// </summary>
    public sealed class CardView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public const float W = 196f, H = 276f;

        public CardDef Def { get; private set; }
        public int InstanceId = -1;
        public Action<CardView> Clicked, Hovered, Unhovered;
        public RectTransform Rect => (RectTransform)transform;

        Image plate, frame, dim;
        TextMeshProUGUI costText;
        int baseCost;

        public static CardView Create(Transform parent, CardDef def, string sourceName, string artSprite, int cost)
        {
            var rt = Ui.Box(parent, "Card " + def.Name, 0, 0, W, H);
            rt.pivot = new Vector2(0.5f, 0f);
            var view = rt.gameObject.AddComponent<CardView>();
            view.Def = def;
            view.baseCost = def.Cost;

            bool junk = def.IsJunk;
            view.plate = Ui.Img(rt, "Plate", Skin.Plate, 0, 0, W, H, junk ? new Color(1f, 0.72f, 0.6f) : Color.white, true);
            view.plate.type = Image.Type.Tiled;
            view.plate.raycastTarget = true;

            var well = Ui.Well(rt, "ArtWell", 10, 50, W - 20, 84);
            well.gameObject.AddComponent<RectMask2D>();
            var art = Data.ArtLibrary.Instance.Sprite(artSprite);
            if (art != null)
            {
                float scale = art.rect.width <= 32 ? 3f : art.rect.width <= 80 ? 2f : 1f;
                float aw = art.rect.width * scale, ah = art.rect.height * scale;
                var img = Ui.Img(well.transform, "Art", art, (W - 20 - aw) / 2f, (84 - ah) / 2f, aw, ah, junk ? new Color(1f, 0.8f, 0.7f) : Color.white);
                img.raycastTarget = false;
            }

            Ui.Img(rt, "Badge", Skin.Pip, 5, 5, 42, 42, Skin.Ink);
            Ui.Img(rt, "BadgeRim", Skin.PipOutline, 5, 5, 42, 42, junk ? Skin.Rust : Skin.Brass);
            view.costText = Ui.Number(rt, def.Unplayable ? "X" : cost.ToString(), 27, 5, 5, 42, 42, TextAlignmentOptions.Center, Skin.Brass);
            if (!def.Unplayable && cost > def.Cost) view.costText.color = Skin.Bad;

            var name = Ui.Heading(rt, def.Name, 25, 52, 9, W - 60, 34, TextAlignmentOptions.Left, junk ? Skin.Warn : Skin.Text);
            name.enableAutoSizing = true;
            name.fontSizeMin = 16;
            name.fontSizeMax = 25;
            name.textWrappingMode = TextWrappingModes.NoWrap;

            string tags = string.IsNullOrEmpty(def.Tags) ? def.Type.ToString() : def.Tags.Replace("·", "/");
            Ui.Stamp(rt, tags, 11, 12, 137, W - 24, 16, TextAlignmentOptions.Left, junk ? Skin.Rust : Skin.BrassDark);

            Ui.Well(rt, "TextWell", 8, 154, W - 16, 98);
            var body = Ui.Body(rt, Highlight(def.Text), 15.5f, 15, 159, W - 30, 90);
            body.enableAutoSizing = true;
            body.fontSizeMin = 11;
            body.fontSizeMax = 15.5f;
            body.lineSpacing = -8f;

            string footer = string.IsNullOrEmpty(sourceName) ? (junk ? "Damage" : "Fleet") : sourceName;
            if (!string.IsNullOrEmpty(def.Module)) footer += " - " + def.Module;
            var foot = Ui.Stamp(rt, footer, 10, 12, 256, W - 24, 14, TextAlignmentOptions.Left, Skin.Faint);
            foot.characterSpacing = 4f;
            foot.textWrappingMode = TextWrappingModes.NoWrap;

            view.frame = Ui.Img(rt, "Frame", Skin.Frame, 0, 0, W, H, Skin.Brass, true);
            view.frame.enabled = false;
            view.dim = Ui.Img(rt, "Dim", Skin.Plate, 0, 0, W, H, new Color(0, 0, 0, 0.5f), true);
            view.dim.enabled = false;
            return view;
        }

        /// <summary>Numbers and keywords in brass, so the key values stand out in the rules text.</summary>
        public static string Highlight(string text)
        {
            var sb = new System.Text.StringBuilder(text.Length + 64);
            bool inNumber = false;
            const string open = "<color=#e8c870>", close = "</color>";
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                bool digit = char.IsDigit(c) || ((c == '+' || c == '-') && i + 1 < text.Length && char.IsDigit(text[i + 1]));
                if (digit && !inNumber) { sb.Append(open); inNumber = true; }
                if (!digit && inNumber) { sb.Append(close); inNumber = false; }
                sb.Append(c);
            }
            if (inNumber) sb.Append(close);
            return sb.ToString()
                .Replace("Exhaust.", "<color=#f0a040>Exhaust.</color>")
                .Replace("Expend", "<color=#7fdcf2>Expend</color>")
                .Replace("Unplayable.", "<color=#d9573f>Unplayable.</color>");
        }

        public void SetPlayable(bool playable, int cost)
        {
            dim.enabled = !playable;
            if (!Def.Unplayable)
            {
                costText.text = cost.ToString();
                costText.color = !playable && cost > 0 ? Skin.Bad : cost > baseCost ? Skin.Warn : Skin.Brass;
            }
        }

        public void SetSelected(bool selected)
        {
            frame.enabled = selected;
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) Clicked?.Invoke(this);
        }

        public void OnPointerEnter(PointerEventData e) => Hovered?.Invoke(this);
        public void OnPointerExit(PointerEventData e) => Unhovered?.Invoke(this);
    }
}
