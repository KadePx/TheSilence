using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Silence.Core.Battle;
using Silence.Core.Hex;
using Silence.Data;
using UnityEngine;
using Terrain = Silence.Core.Battle.Terrain;

namespace Silence.View
{
    /// <summary>What the board currently shows for one unit. Updated by events as they are animated.</summary>
    public sealed class UnitView
    {
        public int Id;
        public string Key;
        public Side Side;
        public UnitKind Kind;
        public Transform Root;
        public Transform Body;
        public SpriteRenderer Sprite;
        /// <summary>Four black copies of the sprite a pixel out: a dark edge, so a hull lying over its neighbour reads as in front of it.</summary>
        public SpriteRenderer[] Edge;
        public SpriteRenderer Ring;
        public SpriteRenderer BarBack, BarFill;
        public SpriteRenderer[] ShieldBars;
        public HexCoord Pos;
        public HexDirection Facing;
        public int Hull, MaxHull;
        public bool Capital => Kind == UnitKind.Capital;
    }

    /// <summary>
    /// Draws the hex battlefield in world space: tiles, terrain, ships by facing, drift ghosts, intent
    /// lines and highlights. It only reads the battle; animation methods move what is already drawn.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        public const int HexPx = 64;
        public const float HexSize = HexPx / 32f;
        const float Sqrt3 = 1.7320508f;

        public Battle Battle { get; private set; }
        public readonly Dictionary<int, UnitView> Views = new Dictionary<int, UnitView>();

        Vector2 origin;
        Transform tilesRoot, unitsRoot, overlayRoot, fxRoot, arrowRoot;
        readonly Dictionary<HexCoord, SpriteRenderer> fills = new Dictionary<HexCoord, SpriteRenderer>();
        readonly Dictionary<HexCoord, SpriteRenderer> outlines = new Dictionary<HexCoord, SpriteRenderer>();

        static readonly Color GridColor = new Color(0.62f, 0.72f, 0.9f, 0.11f);

        // ---------------------------------------------------------------- setup

        public void Init(Battle battle, Vector2 centre)
        {
            Battle = battle;
            float w = Sqrt3 * HexSize * (battle.Board.Width - 0.5f), h = 1.5f * HexSize * (battle.Board.Height - 1);
            origin = new Vector2(centre.x - w / 2f, centre.y + h / 2f);

            tilesRoot = Child("Tiles");
            overlayRoot = Child("Overlay");
            unitsRoot = Child("Units");
            fxRoot = Child("Fx");
            arrowRoot = Child("Arrows");

            var bg = NewSprite(transform, "Background", ArtLibrary.Instance.Sprite(string.IsNullOrEmpty(battle.Backdrop) ? "battle_bg_ionreef" : battle.Backdrop) ?? ArtLibrary.Instance.Sprite("battle_bg_ionreef"), -100);
            bg.transform.localScale = Vector3.one * 3f;
            // A hazard colours the whole field, so it is felt before it is read.
            bg.color = battle.Hazard == "Nebula" ? new Color(0.78f, 0.56f, 0.95f, 1f)
                : battle.Hazard == "Ion storm" ? new Color(0.72f, 1f, 1f, 1f)
                : battle.Hazard == "Dense field" ? new Color(0.86f, 0.78f, 0.7f, 1f)
                : new Color(0.8f, 0.8f, 0.86f, 1f);

            foreach (var hex in battle.Board.All())
            {
                var pos = World(hex);
                var fill = NewSprite(tilesRoot, "Fill", Skin.HexFill(HexPx), -60);
                fill.transform.position = pos;
                fill.color = Color.clear;
                fills[hex] = fill;
                var line = NewSprite(tilesRoot, "Hex", Skin.HexOutline(HexPx, 2), -50);
                line.transform.position = pos;
                line.color = GridColor;
                outlines[hex] = line;

                var terrain = battle.Board.Get(hex);
                if (terrain == Terrain.Open) continue;
                int pick = Mathf.Abs(hex.Q * 7 + hex.R * 13);
                string name = terrain == Terrain.Asteroid ? "asteroid_" + (1 + pick % 4) : "debris_" + (1 + pick % 2);
                var t = NewSprite(tilesRoot, name, ArtLibrary.Instance.Sprite(name), -30);
                t.transform.position = pos;
                t.transform.localScale = Vector3.one * 2f;
                if (t.sprite == null)
                {
                    t.sprite = Skin.Circle;
                    t.color = terrain == Terrain.Asteroid ? Skin.Hex("5a5048") : Skin.Hex("3a3e48");
                }
            }
            SyncUnits();
            RefreshOverlay();
        }

        Transform Child(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(transform, false);
            return t;
        }

        SpriteRenderer NewSprite(Transform parent, string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        // ---------------------------------------------------------------- geometry

        public Vector3 World(HexCoord h)
        {
            var (x, y) = h.ToScreen(HexSize);
            return new Vector3(origin.x + (float)x, origin.y - (float)y, 0f);
        }

        /// <summary>The hex under a world position (it may be off the board).</summary>
        public HexCoord HexAt(Vector2 world)
        {
            float x = world.x - origin.x, y = origin.y - world.y;
            float q = (Sqrt3 / 3f * x - y / 3f) / HexSize, r = (2f / 3f * y) / HexSize;
            float s = -q - r;
            int rq = Mathf.RoundToInt(q), rr = Mathf.RoundToInt(r), rs = Mathf.RoundToInt(s);
            float dq = Mathf.Abs(rq - q), dr = Mathf.Abs(rr - r), ds = Mathf.Abs(rs - s);
            if (dq > dr && dq > ds) rq = -rr - rs;
            else if (dr > ds) rr = -rq - rs;
            return new HexCoord(rq, rr);
        }

        public static float WorldAngle(HexDirection d) => -d.ScreenAngle();

        public static Vector2 Direction(HexDirection d)
        {
            float a = WorldAngle(d) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        // ---------------------------------------------------------------- units

        static string SpriteKey(Unit u)
        {
            return string.IsNullOrEmpty(u.SpecId) ? "asc_cruiser" : u.SpecId;
        }

        /// <summary>Make the drawn units match the battle exactly (after an animation queue, or at start).</summary>
        public void SyncUnits()
        {
            foreach (var u in Battle.Units)
            {
                if (!u.Alive)
                {
                    Remove(u.Id);
                    continue;
                }
                if (!Views.TryGetValue(u.Id, out var v)) v = Add(u);
                v.Pos = u.Pos;
                v.Root.position = World(u.Pos);
                v.Hull = u.Hull;
                v.MaxHull = u.MaxHull;
                SetFacing(v, u.Facing);
                UpdateBars(v, u);
            }
        }

        public UnitView Add(Unit u)
        {
            var v = new UnitView
            {
                Id = u.Id, Key = SpriteKey(u), Side = u.Side, Kind = u.Kind, Pos = u.Pos, Facing = u.Facing, Hull = u.Hull, MaxHull = u.MaxHull,
            };
            v.Root = new GameObject($"{u.Name} #{u.Id}").transform;
            v.Root.SetParent(unitsRoot, false);
            v.Root.position = World(u.Pos);
            v.Body = new GameObject("Body").transform;
            v.Body.SetParent(v.Root, false);
            v.Sprite = NewSprite(v.Body, "Sprite", null, v.Capital ? 0 : 5);
            if (v.Capital)
            {
                v.Edge = new SpriteRenderer[4];
                for (int i = 0; i < 4; i++)
                {
                    v.Edge[i] = NewSprite(v.Body, "Edge", null, 0);
                    v.Edge[i].color = new Color(0.02f, 0.02f, 0.05f, 1f);
                    float px = 1f / 32f;
                    v.Edge[i].transform.localPosition = new Vector3(i == 0 ? px : i == 1 ? -px : 0f, i == 2 ? px : i == 3 ? -px : 0f, 0.0005f);
                }
            }

            v.Ring = NewSprite(v.Root, "Ring", Skin.HexOutline(HexPx, 3), -5);
            v.Ring.enabled = false;
            if (u.IsCommander)
            {
                // The hunting commander's ship is always marked: a red hex under it, whatever else is selected.
                var mark = NewSprite(v.Root, "Commander", Skin.HexOutline(HexPx, 2), -6);
                mark.color = Skin.Alpha(Skin.Bad, 0.85f);
            }

            if (v.Capital)
            {
                v.BarBack = NewSprite(v.Root, "HullBack", Skin.Pixel, 8);
                v.BarBack.color = new Color(0f, 0f, 0f, 0.8f);
                v.BarBack.transform.localPosition = new Vector3(0f, -1.45f, 0f);
                v.BarBack.transform.localScale = new Vector3(13.5f, 1.6f, 1f);
                v.BarFill = NewSprite(v.Root, "HullFill", Skin.Pixel, 10);
                v.BarFill.color = u.Side == Side.Player ? Skin.Good : Skin.Bad;
                v.ShieldBars = new SpriteRenderer[ArcIndex.Count];
                for (int i = 0; i < ArcIndex.Count; i++)
                {
                    v.ShieldBars[i] = NewSprite(v.Root, "Shield" + i, Skin.Pixel, 7);
                    v.ShieldBars[i].color = Skin.Cyan;
                }
            }
            Views[u.Id] = v;
            SetFacing(v, u.Facing);
            UpdateBars(v, u);
            return v;
        }

        public void Remove(int id)
        {
            if (!Views.TryGetValue(id, out var v)) return;
            Destroy(v.Root.gameObject);
            Views.Remove(id);
        }

        /// <summary>
        /// Swap in the sprite for a facing. Capital ships have drawn E, NE and SE sprites; W, SW and NW are those
        /// turned 180 degrees. Small craft have one sprite and are simply rotated.
        /// </summary>
        public void SetFacing(UnitView v, HexDirection facing)
        {
            v.Facing = facing;
            var lib = ArtLibrary.Instance;
            if (v.Capital)
            {
                var (dir, rotate) = facing.SpriteFor();
                var sprite = lib.Sprite($"{v.Key}_{dir}");
                if (sprite != null)
                {
                    v.Sprite.sprite = sprite;
                    v.Body.localRotation = Quaternion.Euler(0f, 0f, rotate ? 180f : 0f);
                }
                else
                {
                    // No baked facings yet: rotate the East sprite, or a stand-in hull.
                    v.Sprite.sprite = lib.Sprite(v.Key + "_E") ?? lib.Sprite("con_outrider_E");
                    v.Body.localRotation = Quaternion.Euler(0f, 0f, WorldAngle(facing));
                    if (lib.Sprite(v.Key + "_E") == null) v.Sprite.color = v.Key == "con_skiff" ? new Color(0.75f, 0.9f, 1f) : new Color(1f, 0.85f, 0.7f);
                }
            }
            else
            {
                v.Sprite.sprite = lib.Sprite(v.Key + "_E") ?? Skin.Circle;
                v.Body.localRotation = Quaternion.Euler(0f, 0f, WorldAngle(facing));
            }
            if (v.Capital && v.ShieldBars != null) LayoutShieldBars(v, null);
            if (v.Edge != null) foreach (var e in v.Edge) e.sprite = v.Sprite.sprite;
            Layer(v);
        }

        /// <summary>
        /// Capital sprites are wider than a hex, so neighbours overlap. Put the smaller hull in front (it is the one
        /// that would vanish), and between equals the one lower on screen, so the stack never flickers or flips.
        /// All capital sprites share a sorting order; the camera sorts them by this depth.
        /// </summary>
        void Layer(UnitView v)
        {
            if (!v.Capital || v.Sprite.sprite == null) return;
            var r = v.Sprite.sprite.rect;
            float depth = r.width * r.height * 0.00001f + v.Root.position.y * 0.002f;
            v.Body.localPosition = new Vector3(0f, 0f, depth);
        }

        public void UpdateBars(UnitView v, Unit u)
        {
            if (!v.Capital) return;
            float frac = v.MaxHull > 0 ? Mathf.Clamp01(v.Hull / (float)v.MaxHull) : 0f;
            const float full = 12.5f; // in 4px sprite units -> 50 px
            v.BarFill.transform.localScale = new Vector3(full * frac, 0.9f, 1f);
            v.BarFill.transform.localPosition = new Vector3(-(full * (1f - frac)) * 0.0625f, -1.45f, 0f);
            LayoutShieldBars(v, u);
        }

        void LayoutShieldBars(UnitView v, Unit u)
        {
            if (u == null) u = Battle.Get(v.Id);
            if (u == null) return;
            // Fore, Starboard, Aft, Port relative to the facing; starboard is the ship's right.
            float[] angles = { 0f, -90f, 180f, 90f };
            float reach = v.Key.Contains("cruiser") || v.Key.Contains("trimaran") ? 1.75f : 1.2f;
            for (int i = 0; i < ArcIndex.Count; i++)
            {
                var bar = v.ShieldBars[i];
                int s = u.Shields[i];
                bar.enabled = s > 0;
                float a = WorldAngle(v.Facing) + angles[i];
                var dir = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                float dist = i == 0 || i == 2 ? reach : reach * 0.62f;
                bar.transform.localPosition = dir * dist;
                bar.transform.localRotation = Quaternion.Euler(0f, 0f, a + 90f);
                bar.transform.localScale = new Vector3(Mathf.Min(10f, 2f + 1.6f * s), 0.75f, 1f);
                bar.color = Skin.Alpha(Skin.Cyan, s > u.MaxShields ? 1f : 0.75f);
            }
        }

        public void SetRing(int unitId, Color? color)
        {
            if (!Views.TryGetValue(unitId, out var v)) return;
            v.Ring.enabled = color.HasValue;
            if (color.HasValue) v.Ring.color = color.Value;
        }

        public void ClearRings()
        {
            foreach (var v in Views.Values) v.Ring.enabled = false;
        }

        // ---------------------------------------------------------------- highlights

        public void ClearHighlights()
        {
            foreach (var f in fills.Values) f.color = Color.clear;
            foreach (var o in outlines.Values) o.color = GridColor;
        }

        public void Highlight(HexCoord hex, Color fill, Color? outline = null)
        {
            if (fills.TryGetValue(hex, out var f)) f.color = fill;
            if (outline.HasValue && outlines.TryGetValue(hex, out var o)) o.color = outline.Value;
        }

        /// <summary>Heading choices around a ship: one arrow per facing it may turn to.</summary>
        public void ShowArrows(HexCoord at, HexDirection current, IEnumerable<HexDirection> options, Color color)
        {
            ClearArrows();
            var centre = World(at);
            foreach (var d in options)
            {
                var a = NewSprite(arrowRoot, "Heading " + d, Skin.Arrow, 40);
                a.transform.position = centre + (Vector3)(Direction(d) * (HexSize * Sqrt3));
                a.transform.rotation = Quaternion.Euler(0f, 0f, WorldAngle(d));
                a.transform.localScale = Vector3.one * 2f;
                a.color = color;
                var disc = NewSprite(arrowRoot, "Disc", Skin.Circle, 39);
                disc.transform.position = a.transform.position;
                disc.transform.localScale = Vector3.one * 1.9f;
                disc.color = new Color(0f, 0f, 0f, 0.6f);
            }
        }

        public void ClearArrows()
        {
            for (int i = arrowRoot.childCount - 1; i >= 0; i--) Destroy(arrowRoot.GetChild(i).gameObject);
        }

        // ---------------------------------------------------------------- ghosts and intent lines

        public void ClearOverlay()
        {
            for (int i = overlayRoot.childCount - 1; i >= 0; i--) Destroy(overlayRoot.GetChild(i).gameObject);
        }

        /// <summary>Redraw drift ghosts and intent lines from the battle as it stands.</summary>
        public void RefreshOverlay(int emphasisedUnit = -1)
        {
            for (int i = overlayRoot.childCount - 1; i >= 0; i--) Destroy(overlayRoot.GetChild(i).gameObject);
            if (Battle.Outcome != BattleOutcome.Ongoing) return;

            foreach (var u in Battle.Units.Where(u => u.Alive && u.IsCapital && u.Speed > 0))
            {
                var dest = Battle.PredictDrift(u);
                if (dest == u.Pos || !Views.TryGetValue(u.Id, out var v)) continue;
                var ghost = NewSprite(overlayRoot, "Ghost", v.Sprite.sprite, -20);
                ghost.transform.position = World(dest);
                ghost.transform.rotation = v.Body.rotation;
                var c = u.Side == Side.Player ? Skin.Alpha(Skin.Cyan, 0.5f) : Skin.Alpha(Skin.Warn, 0.4f);
                // In the side's colour, not the hull's own: a white ghost one hex ahead read as a second ship lying over the first.
                ghost.color = new Color(c.r, c.g, c.b, 0.3f);
                Dashes(World(u.Pos), World(dest), c, 0.09f, 0.16f, 0.22f, -21, false);
            }

            foreach (var i in Battle.Intents)
            {
                if (i.Cancelled) continue;
                var u = Battle.Get(i.UnitId);
                var t = Battle.Get(i.TargetId);
                if (u == null || !u.Alive || t == null || !t.Alive || t == u) continue;
                bool strong = emphasisedUnit < 0 || emphasisedUnit == i.UnitId;
                Color c;
                bool solid = true;
                switch (i.Kind)
                {
                    case IntentKind.Broadside:
                        solid = Battle.ForecastHit(i);
                        c = solid ? Skin.Bad : Skin.Alpha(Skin.Bad, 0.45f);
                        break;
                    case IntentKind.CraftAttack: c = Skin.Bad; break;
                    case IntentKind.TorpedoRun:
                    case IntentKind.LaunchTorpedo: c = Skin.Warn; break;
                    case IntentKind.PatchUp: c = Skin.Good; break;
                    case IntentKind.JointFire: c = Skin.Warn; break;
                    default: continue;
                }
                if (!strong) c = Skin.Alpha(c, c.a * 0.25f);
                var from = World(u.Pos);
                var to = World(t.Pos);
                if (solid) Dashes(from, to, c, 0.1f, 0.34f, 0.14f, -10, true);
                else Dashes(from, to, c, 0.07f, 0.12f, 0.3f, -10, true);
            }
        }

        void Dashes(Vector3 from, Vector3 to, Color color, float width, float dash, float gap, int order, bool arrow)
        {
            var delta = (Vector2)(to - from);
            float len = delta.magnitude;
            if (len < 0.01f) return;
            var dir = delta / len;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float start = 0.7f, end = len - (arrow ? 0.95f : 0.4f);
            for (float d = start; d < end; d += dash + gap)
            {
                float l = Mathf.Min(dash, end - d);
                var s = NewSprite(overlayRoot, "Dash", Skin.Pixel, order);
                s.transform.position = from + (Vector3)(dir * (d + l / 2f));
                s.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                s.transform.localScale = new Vector3(l * 8f, width * 8f, 1f);
                s.color = color;
            }
            if (!arrow) return;
            var head = NewSprite(overlayRoot, "Arrow", Skin.Arrow, order);
            head.transform.position = from + (Vector3)(dir * (len - 0.75f));
            head.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            head.color = color;
        }

        // ---------------------------------------------------------------- animation

        public IEnumerator MoveTo(UnitView v, HexCoord to, float seconds)
        {
            var a = v.Root.position;
            var b = World(to);
            if (!v.Capital && (b - a).sqrMagnitude > 0.01f)
            {
                var d = b - a;
                v.Body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            }
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                if (v.Root == null) yield break;
                v.Root.position = Vector3.Lerp(a, b, Mathf.SmoothStep(0f, 1f, t / seconds));
                yield return null;
            }
            if (v.Root != null) v.Root.position = b;
            v.Pos = to;
        }

        /// <summary>Lunge toward a hex and back (collisions, rams).</summary>
        public IEnumerator Bump(UnitView v, Vector3 toward, float seconds)
        {
            var a = v.Root.position;
            var b = Vector3.Lerp(a, toward, 0.45f);
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                if (v.Root == null) yield break;
                float k = Mathf.Sin(Mathf.PI * t / seconds);
                v.Root.position = Vector3.Lerp(a, b, k);
                yield return null;
            }
            if (v.Root != null) v.Root.position = a;
        }

        public IEnumerator Beam(Vector3 from, Vector3 to, Color color, float width, float seconds)
        {
            var delta = (Vector2)(to - from);
            var s = NewSprite(fxRoot, "Beam", Skin.Pixel, 30);
            var core = NewSprite(fxRoot, "BeamCore", Skin.Pixel, 31);
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            foreach (var r in new[] { s, core })
            {
                r.transform.position = (from + to) / 2f;
                r.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                float k = 1f - t / seconds;
                s.transform.localScale = new Vector3(delta.magnitude * 8f, width * 8f * (0.4f + k), 1f);
                core.transform.localScale = new Vector3(delta.magnitude * 8f, width * 3f * k, 1f);
                s.color = Skin.Alpha(color, 0.75f * k);
                core.color = new Color(1f, 1f, 1f, k);
                yield return null;
            }
            Destroy(s.gameObject);
            Destroy(core.gameObject);
        }

        public IEnumerator Shots(Vector3 from, Vector3 to, Color color, int count, float seconds)
        {
            var shots = new List<SpriteRenderer>();
            var delta = to - from;
            var side = new Vector3(-delta.y, delta.x, 0f).normalized;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            for (int i = 0; i < count; i++)
            {
                var s = NewSprite(fxRoot, "Shot", Skin.Pixel, 30);
                s.color = color;
                s.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                s.transform.localScale = new Vector3(3.5f, 1f, 1f);
                shots.Add(s);
            }
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                for (int i = 0; i < count; i++)
                {
                    float k = Mathf.Clamp01(t / seconds * 1.4f - i * 0.12f);
                    shots[i].enabled = k > 0f && k < 1f;
                    shots[i].transform.position = Vector3.Lerp(from, to, k) + side * ((i - (count - 1) / 2f) * 0.18f);
                }
                yield return null;
            }
            foreach (var s in shots) Destroy(s.gameObject);
        }

        public IEnumerator Burst(Vector3 at, Color color, float radius, float seconds)
        {
            var disc = NewSprite(fxRoot, "Burst", Skin.Circle, 32);
            var ring = NewSprite(fxRoot, "BurstRing", Skin.Ring, 33);
            disc.transform.position = ring.transform.position = at;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                disc.transform.localScale = Vector3.one * radius * (0.4f + k * 0.9f);
                ring.transform.localScale = Vector3.one * radius * (0.3f + k * 1.3f);
                disc.color = Color.Lerp(Color.white, Skin.Alpha(color, 0f), k);
                ring.color = Skin.Alpha(color, 1f - k);
                yield return null;
            }
            Destroy(disc.gameObject);
            Destroy(ring.gameObject);
        }

        readonly Dictionary<string, Sprite[]> sheets = new Dictionary<string, Sprite[]>();

        /// <summary>Frames of an effect sheet from the art library: square cells in rows from the top-left.</summary>
        Sprite[] Frames(string sheet, int cell, int count)
        {
            if (sheets.TryGetValue(sheet, out var frames)) return frames;
            var whole = ArtLibrary.Instance.Sprite(sheet);
            if (whole == null)
            {
                sheets[sheet] = null;
                return null;
            }
            var tex = whole.texture;
            int cols = tex.width / cell;
            frames = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                var rect = new Rect((i % cols) * cell, tex.height - (i / cols + 1) * cell, cell, cell);
                frames[i] = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
            }
            sheets[sheet] = frames;
            return frames;
        }

        /// <summary>Play a one-shot effect sheet at a point. Returns false when the sheet is not in the library.</summary>
        public bool PlaySheet(string sheet, int cell, int count, float fps, Vector3 at, float scale, Color tint)
        {
            var frames = Frames(sheet, cell, count);
            if (frames == null) return false;
            StartCoroutine(SheetRoutine(frames, fps, at, scale, tint));
            return true;
        }

        IEnumerator SheetRoutine(Sprite[] frames, float fps, Vector3 at, float scale, Color tint)
        {
            var sr = NewSprite(fxRoot, "Sheet", frames[0], 34);
            sr.transform.position = at;
            sr.transform.localScale = Vector3.one * scale;
            sr.color = tint;
            float life = frames.Length / fps;
            for (float t = 0; t < life; t += Time.deltaTime)
            {
                sr.sprite = frames[Mathf.Min(frames.Length - 1, (int)(t * fps))];
                yield return null;
            }
            Destroy(sr.gameObject);
        }

        public void Explosion(Vector3 at, bool large)
        {
            if (!PlaySheet("fx_explosion", 96, 10, 14f, at, large ? 2f : 1f, Color.white))
                StartCoroutine(Burst(at, Skin.Warn, large ? 3.2f : 1.1f, large ? 0.6f : 0.3f));
        }

        public void HitSpark(Vector3 at)
        {
            if (!PlaySheet("fx_hit", 64, 5, 16f, at, 1f, Color.white)) StartCoroutine(Burst(at, Skin.Warn, 0.7f, 0.22f));
        }

        public void ShieldFlare(Vector3 at, bool capital)
        {
            if (!PlaySheet("fx_shield", 96, 8, 22f, at, capital ? 1.5f : 0.75f, new Color(0.6f, 0.95f, 1f, 0.9f)))
                StartCoroutine(Burst(at, Skin.Cyan, 1.1f, 0.25f));
        }

        /// <summary>The slice of hull bar just lost stays white for a moment, then drains away: the eye sees how much went.</summary>
        public IEnumerator BarLoss(UnitView v, int hullBefore)
        {
            if (!v.Capital || v.MaxHull <= 0 || hullBefore <= v.Hull) yield break;
            const float full = 12.5f;
            float a = Mathf.Clamp01(v.Hull / (float)v.MaxHull), b = Mathf.Clamp01(hullBefore / (float)v.MaxHull);
            var lost = NewSprite(v.Root, "HullLost", Skin.Pixel, 9);
            lost.color = Color.white;
            const float hold = 0.18f, drain = 0.4f;
            for (float t = 0; t < hold + drain; t += Time.deltaTime)
            {
                if (lost == null) yield break;
                float top = t < hold ? b : Mathf.Lerp(b, a, (t - hold) / drain);
                lost.transform.localScale = new Vector3(full * (top - a), 0.9f, 1f);
                lost.transform.localPosition = new Vector3(-full * 0.0625f + (a + top) / 2f * full * 0.125f, -1.45f, 0f);
                yield return null;
            }
            if (lost != null) Destroy(lost.gameObject);
        }

        /// <summary>A hit shoves the hull a few pixels away from whoever fired, and it settles back.</summary>
        public IEnumerator Recoil(UnitView v, Vector2 away, float pixels, float seconds)
        {
            if (v.Body == null || away.sqrMagnitude < 0.0001f) yield break;
            away.Normalize();
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                if (v.Body == null) yield break;
                float k = Mathf.Sin(Mathf.PI * Mathf.Sqrt(t / seconds));
                var p = away * (Mathf.Round(pixels * k) / 32f);
                v.Body.localPosition = new Vector3(p.x, p.y, v.Body.localPosition.z);
                yield return null;
            }
            if (v.Body != null) v.Body.localPosition = new Vector3(0f, 0f, v.Body.localPosition.z);
        }

        public IEnumerator Flash(UnitView v, Color color, float seconds)
        {
            if (v.Sprite == null) yield break;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                if (v.Sprite == null) yield break;
                v.Sprite.color = Color.Lerp(color, Color.white, t / seconds);
                yield return null;
            }
            if (v.Sprite != null) v.Sprite.color = Color.white;
        }
    }
}
