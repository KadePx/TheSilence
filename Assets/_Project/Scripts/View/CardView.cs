using System;
using Silence.Core.Battle;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Silence.View
{
    /// <summary>
    /// One card on the PixelLab card frame (Art/UI/Cards/card_frame, 220x298, drawn 1:1): brass hexagonal
    /// cost badge, name plate, art window, rules text and a footer naming the ship and the card's type.
    /// Junk is rust-tinted; a card that can't be played right now is dimmed with a red cost.
    /// Cards are never scaled, so the frame and the pixel fonts stay crisp.
    /// </summary>
    public sealed class CardView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public const float W = 220f, H = 298f;

        public CardDef Def { get; private set; }
        public int InstanceId = -1;
        public Action<CardView> Clicked, Hovered, Unhovered;
        public RectTransform Rect => (RectTransform)transform;

        Image plate, frame, dim;
        TextMeshProUGUI costText;
        int baseCost;

        public static CardView Create(Transform parent, CardDef def, string sourceName, string artSprite, int cost, string rulesText = null)
        {
            var rt = Ui.Box(parent, "Card " + def.Name, 0, 0, W, H);
            rt.pivot = new Vector2(0.5f, 0f);
            var view = rt.gameObject.AddComponent<CardView>();
            view.Def = def;
            view.baseCost = def.Cost;

            bool junk = def.IsJunk;
            var tint = junk ? new Color(1f, 0.74f, 0.62f) : Color.white;
            var frameSprite = Data.ArtLibrary.Instance.Sprite("card_frame");
            view.plate = Ui.Img(rt, "Plate", frameSprite != null ? frameSprite : Skin.Plate, 0, 0, W, H, tint, frameSprite == null);
            view.plate.raycastTarget = true;

            // Art window of the frame: 182x88 at (19,61).
            var window = Ui.Box(rt, "Art", 19, 61, 182, 88);
            window.gameObject.AddComponent<RectMask2D>();
            var art = Data.ArtLibrary.Instance.Sprite(artSprite);
            if (art != null)
            {
                // A close crop of the part of the ship the card comes from (the spec's "crop of the source
                // module"), at a whole-number zoom, so five cards from one ship do not all look the same.
                var focus = junk || art.rect.width <= 32 ? new Vector2(0.5f, 0.5f) : ModuleFocus(def.Module);
                float scale = art.rect.width <= 32 ? 3f : junk ? 2f : art.rect.width <= 80 ? 4f : 3f;
                float aw = art.rect.width * scale, ah = art.rect.height * scale;
                float ax = Mathf.Clamp(Mathf.Round(91f - focus.x * aw), 182f - aw, 0f), ay = Mathf.Clamp(Mathf.Round(44f - focus.y * ah), 88f - ah, 0f);
                if (aw < 182f) ax = Mathf.Round((182f - aw) / 2f);
                if (ah < 88f) ay = Mathf.Round((88f - ah) / 2f);
                Ui.Img(window, "Sprite", art, ax, ay, aw, ah, junk ? new Color(1f, 0.8f, 0.7f) : Color.white);
            }

            // Cost in the hexagonal badge, centred on (28,28).
            view.costText = Ui.Number(rt, def.Unplayable ? "X" : cost.ToString(), 24, 9, 17, 40, 24, TextAlignmentOptions.Center, Skin.BrassLight);
            if (!def.Unplayable && cost > def.Cost) view.costText.color = Skin.Bad;

            // Name plate: 147x25 at (60,16). One line.
            var name = Ui.Label(rt, def.Name.ToUpperInvariant(), Skin.Body, 16, junk ? Skin.Warn : Skin.Text, 65, 21, 140, 16, TextAlignmentOptions.Left);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.overflowMode = TextOverflowModes.Truncate;

            // Rules text: well 191x101 at (14,165). Six lines of about 25 characters.
            var body = Ui.Label(rt, Highlight(rulesText ?? def.Text), Skin.Body, 16, Skin.Text, 20, 169, 182, 96, TextAlignmentOptions.TopLeft);
            body.overflowMode = TextOverflowModes.Truncate;

            // Footer: 191x12 at (14,276). Ship on the left, card type on the right.
            string source = string.IsNullOrEmpty(sourceName) ? (junk ? "Damage" : "Fleet") : sourceName;
            var foot = Ui.Stamp(rt, source, 12, 20, 276, 120, 12, TextAlignmentOptions.Left, Skin.Dim);
            foot.textWrappingMode = TextWrappingModes.NoWrap;
            foot.overflowMode = TextOverflowModes.Truncate;
            var type = Ui.Stamp(rt, def.Type.ToString(), 12, 120, 276, 82, 12, TextAlignmentOptions.Right, junk ? Skin.Rust : TypeColor(def.Type));
            type.textWrappingMode = TextWrappingModes.NoWrap;

            view.frame = Ui.Img(rt, "Frame", Skin.Frame, -2, -2, W + 4, H + 4, Skin.BrassLight, true);
            view.frame.enabled = false;
            view.dim = Ui.Img(rt, "Dim", frameSprite != null ? frameSprite : Skin.Plate, 0, 0, W, H, new Color(0, 0, 0, 0.55f), frameSprite == null);
            view.dim.enabled = false;
            return view;
        }

        /// <summary>Card types by colour, so a hand reads at a glance: cyan moves the ship, red shoots, green protects.</summary>
        public static Color TypeColor(CardType type)
        {
            switch (type)
            {
                case CardType.Maneuver: return Skin.Cyan;
                case CardType.Weapon: return Skin.Bad;
                case CardType.Defense: return Skin.Good;
                case CardType.Reactor: return Skin.Warn;
                default: return Skin.Brass;
            }
        }

        /// <summary>Where on an east-facing ship sprite a module sits, as a fraction of its width and height from the top-left.</summary>
        static Vector2 ModuleFocus(string module)
        {
            string m = (module ?? "").ToLowerInvariant();
            // The Concord trimaran: bow gun on the centre hull, the drum amidships, outriggers above and below, drones aft.
            if (m.Contains("bow") || m.Contains("torpedo")) return new Vector2(0.84f, 0.5f);
            if (m.Contains("outrigger") || m.Contains("missile")) return new Vector2(0.50f, 0.16f);
            if (m.Contains("flight")) return new Vector2(0.40f, 0.84f);
            if (m.Contains("projector") || m.Contains("ring")) return new Vector2(0.50f, 0.5f);
            if (m.Contains("drone") || m.Contains("cutting")) return new Vector2(0.14f, 0.5f);
            if (m.Contains("plating")) return new Vector2(0.30f, 0.80f);
            if (m.Contains("lance") || m.Contains("fore") || m.Contains("prow")) return new Vector2(0.80f, 0.5f);
            if (m.Contains("broadside") || m.Contains("battery")) return new Vector2(0.52f, 0.30f);
            if (m.Contains("point") || m.Contains("flak")) return new Vector2(0.45f, 0.68f);
            if (m.Contains("hangar")) return new Vector2(0.30f, 0.5f);
            if (m.Contains("reactor") || m.Contains("engine")) return new Vector2(0.16f, 0.5f);
            if (m.Contains("sensor")) return new Vector2(0.62f, 0.5f);
            return new Vector2(0.46f, 0.5f); // bridge, helm and anything unnamed: the middle of the ship
        }

        /// <summary>Numbers and keywords in brass, so the key values stand out in the rules text.</summary>
        public static string Highlight(string text)
        {
            text = text.Replace("°", " deg");
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
                costText.color = !playable && cost > 0 ? Skin.Bad : cost > baseCost ? Skin.Warn : Skin.BrassLight;
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
