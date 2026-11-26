using System;
using System.Collections.Generic;
using Silence.Data;
using TMPro;
using UnityEngine;

namespace Silence.View
{
    /// <summary>
    /// The shared interface look from the spec: etched gunmetal plates with chamfered corners and rivets,
    /// recessed wells, brass for anything interactive. Sprites are drawn in code once and cached, so the
    /// whole UI can be built without scene references.
    /// </summary>
    public static class Skin
    {
        public static readonly Color Brass = Hex("c9a24a");
        public static readonly Color BrassLight = Hex("e8c870");
        public static readonly Color BrassDark = Hex("6e5622");
        public static readonly Color Good = Hex("8cc47a");
        public static readonly Color Bad = Hex("d9573f");
        public static readonly Color Warn = Hex("f0a040");
        public static readonly Color Cyan = Hex("7fdcf2");
        public static readonly Color Text = Hex("d6dae4");
        public static readonly Color Dim = Hex("8a90a0");
        public static readonly Color Faint = Hex("555b6a");
        public static readonly Color PlateHi = Hex("2c2f36");
        public static readonly Color PlateLo = Hex("16171b");
        public static readonly Color WellColor = Hex("0b0c0f");
        public static readonly Color Steel = Hex("3a3e48");
        public static readonly Color Rust = Hex("8a4a2a");
        public static readonly Color Ink = Hex("0a0b0e");

        public static Color Hex(string rgb, float a = 1f)
        {
            int v = Convert.ToInt32(rgb, 16);
            return new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, a);
        }

        public static Color Alpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        // ---------------------------------------------------------------- fonts

        // Pixel fonts commissioned from PixelLab (see ArtSource/pixellab-log.csv). Each is a 1-bit bitmap font
        // and is only ever drawn at a whole multiple of its native size, so it sits on the same pixel grid as
        // the sprites. Ui.Label does the snapping.
        public static TMP_FontAsset Logo => Font("SilenceLogo PX", "SilenceHeading PX");
        public static TMP_FontAsset Heading => Font("SilenceHeading PX");
        public static TMP_FontAsset Stencil => Font("SilenceLabel PX");
        public static TMP_FontAsset Body => Font("SilenceBody PX");
        public static TMP_FontAsset BodyBold => Font("SilenceBody PX");
        public static TMP_FontAsset Mono => Font("SilenceLabel PX");

        static readonly Dictionary<string, TMP_FontAsset> fonts = new Dictionary<string, TMP_FontAsset>();

        static TMP_FontAsset Font(string name, string fallback = null)
        {
            if (fonts.TryGetValue(name, out var f) && f != null) return f;
            f = ArtLibrary.Instance.Font(name);
            if (f == null && fallback != null) f = ArtLibrary.Instance.Font(fallback);
            if (f == null) f = TMP_Settings.defaultFontAsset;
            fonts[name] = f;
            return f;
        }

        /// <summary>Native pixel height of a pixel font (its bake size), or 0 for a smooth font.</summary>
        public static int NativePx(TMP_FontAsset f)
        {
            if (f == null || !f.name.EndsWith(" PX")) return 0;
            return Mathf.Max(1, Mathf.RoundToInt(f.faceInfo.pointSize));
        }

        /// <summary>
        /// The size a pixel font is actually drawn at for a requested size: a whole multiple of its native
        /// size. <paramref name="bias"/> shifts where it steps up (headings are wide, so they step up late).
        /// </summary>
        public static float Snap(TMP_FontAsset f, float size, float bias = 0.15f)
        {
            int n = NativePx(f);
            if (n == 0) return size;
            return n * Mathf.Max(1, Mathf.FloorToInt(size / n + bias));
        }

        // ---------------------------------------------------------------- sprites

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Sprite Pixel => Get("pixel", () => Make(4, 4, 0, (x, y) => Color.white));

        // The interface kit is pixel art from PixelLab (Art/UI/Kit), 9-sliced here. Each falls back to a plate
        // drawn in code if the art is missing.

        /// <summary>Gunmetal plate with a brass inlay and corner rivets. 9-sliced.</summary>
        public static Sprite Plate => Get("plate", () => Slice("ui_plate", 18) ?? MakePlate(128, 12, PlateHi, PlateLo, true, Hex("4a4e59"), 24));

        /// <summary>Smaller plate for floating panels and tooltips. 9-sliced.</summary>
        public static Sprite Panel => Get("panel", () => Slice("ui_tooltip", 14) ?? Plate);

        /// <summary>Recessed dark well for data. 9-sliced.</summary>
        public static Sprite Well => Get("well", () => Slice("ui_well", 14) ?? MakePlate(64, 5, WellColor, Hex("101116"), false, Hex("05060a"), 10));

        /// <summary>Shallow recessed slot (scanlines) for rows and readouts. 9-sliced.</summary>
        public static Sprite Slot => Get("slot", () => Slice("ui_slot", 12) ?? Well);

        /// <summary>Button plate, tinted by state. 9-sliced.</summary>
        public static Sprite Button => Get("button", () => Slice("ui_button", 12) ?? MakePlate(40, 8, Hex("ffffff"), Hex("b9b9b9"), false, Hex("ffffff"), 14));

        /// <summary>Long thin plate for top bars and headers. 9-sliced.</summary>
        public static Sprite Header => Get("header", () => Slice("ui_header", 14) ?? Plate);

        /// <summary>Small label plate for chips over the board. 9-sliced.</summary>
        public static Sprite Chip => Get("chip", () => Slice("ui_chip", 10) ?? Button);

        /// <summary>Recessed track for hull and heat bars. 9-sliced.</summary>
        public static Sprite BarTrack => Get("bar", () => Slice("ui_bar_thin", 8) ?? Well);

        /// <summary>Empty hexagonal socket for a command pip.</summary>
        public static Sprite PipSocket => Get("pipSocket", () => ArtLibrary.Instance.Sprite("ui_pip") ?? PipOutline);

        /// <summary>A 9-sliced copy of a library sprite (the import keeps no border).</summary>
        static Sprite Slice(string name, int border)
        {
            var s = ArtLibrary.Instance.Sprite(name);
            if (s == null) return null;
            return Sprite.Create(s.texture, s.rect, new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
        }

        /// <summary>Thin chamfered outline, for selection frames. 9-sliced.</summary>
        public static Sprite Frame => Get("frame", () => MakeFrame(40, 8, 2));

        public static Sprite Circle => Get("circle", () => Make(32, 32, 0, (x, y) =>
        {
            float d = Mathf.Sqrt((x - 15.5f) * (x - 15.5f) + (y - 15.5f) * (y - 15.5f));
            return d <= 15.5f ? Color.white : Color.clear;
        }));

        public static Sprite Ring => Get("ring", () => Make(64, 64, 0, (x, y) =>
        {
            float d = Mathf.Sqrt((x - 31.5f) * (x - 31.5f) + (y - 31.5f) * (y - 31.5f));
            return d <= 31.5f && d >= 27.5f ? Color.white : Color.clear;
        }));

        /// <summary>Small flat-top hexagon used for command pips and the cost badge fallback.</summary>
        public static Sprite Pip => Get("pip", () => Make(28, 28, 0, (x, y) => InHex(x - 13.5f, y - 13.5f, 13.5f, false) ? Color.white : Color.clear));

        public static Sprite PipOutline => Get("pipOutline", () => Make(28, 28, 0, (x, y) =>
            InHex(x - 13.5f, y - 13.5f, 13.5f, false) && !InHex(x - 13.5f, y - 13.5f, 10.5f, false) ? Color.white : Color.clear));

        /// <summary>Arrowhead pointing right.</summary>
        public static Sprite Arrow => Get("arrow", () => Make(24, 24, 0, (x, y) =>
        {
            float dy = Mathf.Abs(y - 11.5f);
            // A shafted arrow: an equilateral head alone looks the same turned 60 degrees.
            bool head = x >= 11 && x <= 23 && dy <= (23 - x) * 0.8f;
            bool shaft = x >= 1 && x < 11 && dy <= 2.6f;
            return head || shaft ? Color.white : Color.clear;
        }));

        /// <summary>Filled pointy-top hex of the board's size, in pixels.</summary>
        public static Sprite HexFill(int size) => Get("hexFill" + size, () => MakeHex(size, 0));

        public static Sprite HexOutline(int size, int thickness = 2) => Get("hexLine" + size + "_" + thickness, () => MakeHex(size, thickness));

        static Sprite Get(string key, Func<Sprite> make)
        {
            if (cache.TryGetValue(key, out var s) && s != null) return s;
            s = make();
            s.name = key;
            cache[key] = s;
            return s;
        }

        static Sprite Make(int w, int h, int border, Func<int, int, Color> pixel)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = pixel(x, y);
            tex.SetPixels(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
        }

        static float Noise(int x, int y)
        {
            unchecked
            {
                uint n = (uint)(x * 374761393 + y * 668265263);
                n = (n ^ (n >> 13)) * 1274126177u;
                return ((n ^ (n >> 16)) & 1023) / 1023f;
            }
        }

        static Sprite MakePlate(int size, int chamfer, Color top, Color bottom, bool rivets, Color edge, int border)
        {
            return Make(size, size, border, (x, y) =>
            {
                int fx = Mathf.Min(x, size - 1 - x), fy = Mathf.Min(y, size - 1 - y);
                int cut = fx + fy;
                if (cut < chamfer) return Color.clear;
                bool onEdge = fx == 0 || fy == 0 || cut == chamfer;
                bool inner = fx == 1 || fy == 1 || cut == chamfer + 1;
                var c = Color.Lerp(bottom, top, y / (float)(size - 1));
                float n = (Noise(x, y) - 0.5f) * 0.035f;
                c = new Color(c.r + n, c.g + n, c.b + n, 1f);
                if (onEdge) return Skin.Ink;
                if (inner) return Color.Lerp(c, edge, y > size / 2 ? 0.75f : 0.3f);
                if (rivets)
                {
                    int rx = fx - (chamfer + 2), ry = fy - (chamfer + 2);
                    if (rx >= 0 && rx <= 2 && ry >= 0 && ry <= 2)
                        return (rx == 1 && ry == 1) ? Hex("6b7080") : Hex("0d0e11");
                }
                return c;
            });
        }

        static Sprite MakeFrame(int size, int chamfer, int thickness)
        {
            return Make(size, size, 14, (x, y) =>
            {
                int fx = Mathf.Min(x, size - 1 - x), fy = Mathf.Min(y, size - 1 - y);
                int cut = fx + fy;
                if (cut < chamfer) return Color.clear;
                return fx < thickness || fy < thickness || cut < chamfer + thickness + 1 ? Color.white : Color.clear;
            });
        }

        /// <summary>Is (dx, dy) inside a hexagon of the given radius centred on the origin?</summary>
        static bool InHex(float dx, float dy, float radius, bool pointy)
        {
            float ax = Mathf.Abs(pointy ? dx : dy), ay = Mathf.Abs(pointy ? dy : dx);
            float halfW = radius * 0.8660254f;
            if (ax > halfW || ay > radius) return false;
            return ay <= radius - ax * 0.5773503f;
        }

        static Sprite MakeHex(int size, int thickness)
        {
            int w = Mathf.CeilToInt(size * 1.7320508f), h = size * 2;
            float cx = (w - 1) / 2f, cy = (h - 1) / 2f;
            return Make(w, h, 0, (x, y) =>
            {
                bool inside = InHex(x - cx, y - cy, size - 0.5f, true);
                if (!inside) return Color.clear;
                if (thickness <= 0) return Color.white;
                return InHex(x - cx, y - cy, size - 0.5f - thickness, true) ? Color.clear : Color.white;
            });
        }
    }
}
