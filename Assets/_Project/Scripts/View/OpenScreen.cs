using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Silence.Core.Battle;
using Silence.Core.Open;
using Silence.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Silence.View
{
    /// <summary>
    /// Prototype screen for battle without a grid, seen at a tilt. Like the battle screen it never changes
    /// state: a click becomes <see cref="OpenBattle.Play"/> or <see cref="OpenBattle.EndTurn"/> and the events
    /// those add to the log are played back in order.
    /// </summary>
    public sealed class OpenScreen : MonoBehaviour
    {
        // The plane on screen: thousandths of a span to layout pixels, flattened top to bottom.
        const float U = 0.072f, Tilt = 0.6f, Cx = 960f, Cy = 404f;
        static readonly string[] Facings = { "east", "south_east", "south", "south_west", "west", "north_west", "north", "north_east" };
        static readonly Color PlayerCol = Skin.Hex("8cc47a"), EnemyCol = Skin.Hex("d9573f"), Lit = Skin.Hex("f0a040"), Hazard = Skin.Hex("b48cf0");

        sealed class Shown
        {
            public Vector2 Pos;
            public int Heading, Hull, Shields;
            public bool Alive;
            public Image Img;
        }

        enum Mode { Idle, Target, Heading, Busy, Over }

        OpenBattle battle;
        ulong seed = 1;
        Mode mode;
        int logCursor;
        OpenCard selected;
        int hoverShip = -1;
        int previewHeading;
        Vector2 pointer;

        RectTransform root, spriteRoot, labelRoot, handRoot, panelRoot, overlayRoot;
        ShapeGraphic floor, under, over;
        TextMeshProUGUI hint, turnLabel, deckLabel;
        Button endTurn;
        readonly List<Shown> shown = new List<Shown>();
        readonly List<Image> rockImages = new List<Image>();
        readonly List<CardView> handViews = new List<CardView>();
        readonly List<Image> pips = new List<Image>(), sockets = new List<Image>();
        string flash = "";
        float flashUntil;

        // ---------------------------------------------------------------- build

        void Awake()
        {
            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = Skin.Ink;
            var canvas = Ui.Canvas("OpenBattle");
            canvas.transform.SetParent(transform, false);
            root = Ui.Root(canvas);
            Ui.Backdrop(root, "battle_bg_kessane", new Color(0.55f, 0.55f, 0.6f));
            floor = ShapeGraphic.Create(root, "Floor");
            under = ShapeGraphic.Create(root, "Under");
            spriteRoot = Ui.Box(root, "Sprites", 0, 0, Ui.RefW, Ui.RefH);
            over = ShapeGraphic.Create(root, "Over");
            labelRoot = Ui.Box(root, "Labels", 0, 0, Ui.RefW, Ui.RefH);

            var catcher = Ui.Catcher(root, "Board", 0, 52, Ui.RefW, 718);
            catcher.Move = e => OnPointer(e, false);
            catcher.Click = e => OnPointer(e, true);
            catcher.Exit = () => { if (hoverShip != -1) { hoverShip = -1; Redraw(); } };

            var top = Ui.Plate(root, "Top", 0, 0, Ui.RefW, 52);
            turnLabel = Ui.Heading(top.transform, "Turn 1", 16, 28, 16, 300, 20, TextAlignmentOptions.Left, Skin.Brass);
            Ui.Heading(top.transform, "Gridless prototype - Breaker's Yard", 16, 0, 16, Ui.RefW, 20, TextAlignmentOptions.Center, Skin.Dim);
            Ui.Btn(top.transform, "Help", 1500, 6, 120, 40, ShowHelp, 16);
            Ui.Btn(top.transform, "Restart", 1630, 6, 140, 40, () => Begin(seed + 1), 16);
            Ui.Btn(top.transform, "Quit", 1780, 6, 120, 40, () => SceneManager.LoadScene("Menu"), 16);

            panelRoot = Ui.Box(root, "Panels", 0, 0, Ui.RefW, Ui.RefH);
            var hintWell = Ui.Well(root, "Hint", 400, 736, 1120, 32);
            hint = Ui.Label(hintWell.transform, "", Skin.Body, 16, Skin.Text, 14, 8, 1090, 18);
            hint.textWrappingMode = TextWrappingModes.NoWrap;

            var bottom = Ui.Plate(root, "Bottom", 0, 770, Ui.RefW, 310);
            Ui.Stamp(bottom.transform, "Command", 12, 30, 26, 200, 14, TextAlignmentOptions.Left, Skin.BrassDark);
            for (int i = 0; i < 6; i++)
            {
                sockets.Add(Ui.Img(bottom.transform, "Socket", Skin.PipOutline, 30 + i * 44, 50, 36, 36, Skin.BrassDark));
                pips.Add(Ui.Img(bottom.transform, "Pip", Skin.Pip, 34 + i * 44, 54, 28, 28, Skin.Brass));
            }
            deckLabel = Ui.Label(bottom.transform, "", Skin.Stencil, 12, Skin.Faint, 30, 270, 300, 14);
            handRoot = Ui.Box(root, "Hand", 0, 0, Ui.RefW, Ui.RefH);
            endTurn = Ui.Btn(bottom.transform, "End turn", 1664, 116, 224, 76, OnEndTurn, 32);
            Ui.Label(bottom.transform, "E or Enter. F9 lets the bot play a turn.", Skin.Stencil, 12, Skin.Faint, 1600, 204, 300, 14, TextAlignmentOptions.Center);
            overlayRoot = Ui.Box(root, "Overlay", 0, 0, Ui.RefW, Ui.RefH);

            Music.Play("music_battle");
            Begin(seed);
        }

        void Begin(ulong newSeed)
        {
            StopAllCoroutines();
            seed = newSeed;
            battle = OpenContent.BreakersYard(seed);
            battle.Start();
            logCursor = battle.Log.Count;
            selected = null;
            hoverShip = -1;
            Ui.Clear(spriteRoot);
            Ui.Clear(overlayRoot);
            shown.Clear();
            rockImages.Clear();
            foreach (var r in battle.Rocks)
            {
                var s = ArtLibrary.Instance.Sprite(r.Art);
                var img = Ui.Img(spriteRoot, r.Name, s, 0, 0, 10, 10);
                if (s != null) PlaceSprite(img, s, new Vector2(r.Pos.X, r.Pos.Y), 2, 0.1f);
                else img.enabled = false;
                rockImages.Add(img);
            }
            foreach (var ship in battle.Ships)
            {
                var sh = new Shown { Img = Ui.Img(spriteRoot, ship.Name, null, 0, 0, 10, 10) };
                shown.Add(sh);
            }
            SyncShown();
            DrawFloor();
            mode = Mode.Idle;
            Flash("Breaker's Yard. Wrecks and rocks block fire. Hover a ship to see where its guns bear.", 9f);
            Refresh();
        }

        void SyncShown()
        {
            for (int i = 0; i < shown.Count; i++)
            {
                var ship = battle.Ships[i];
                var sh = shown[i];
                sh.Pos = new Vector2(ship.Pos.X, ship.Pos.Y);
                sh.Heading = ship.Heading;
                sh.Hull = ship.Hull;
                sh.Shields = ship.Shields;
                sh.Alive = ship.Alive;
            }
            PlaceShips();
        }

        // ---------------------------------------------------------------- plane and sprites

        static Vector2 P(Vector2 plane) => new Vector2(Cx + plane.x * U, Cy + plane.y * U * Tilt);
        static Vector2 P(Vec v) => P(new Vector2(v.X, v.Y));
        static Vector2 Dir(float degrees) => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
        static Vector2 Polar(Vector2 centre, float degrees, float radius) => P(centre + Dir(degrees) * radius);

        /// <summary>The drawn facing nearest a heading, once the plane is flattened.</summary>
        static string Facing(int heading)
        {
            var d = Dir(heading);
            float screen = Mathf.Atan2(d.y * Tilt, d.x) * Mathf.Rad2Deg;
            return Facings[((Mathf.RoundToInt(screen / 45f) % 8) + 8) % 8];
        }

        static void PlaceSprite(Image img, Sprite s, Vector2 plane, int scale, float lift)
        {
            var at = P(plane);
            float w = s.rect.width * scale, h = s.rect.height * scale;
            img.sprite = s;
            Ui.Place(img.rectTransform, Mathf.Round(at.x - w / 2f), Mathf.Round(at.y - h / 2f - h * lift), w, h);
        }

        void PlaceShips()
        {
            for (int i = 0; i < shown.Count; i++)
            {
                var sh = shown[i];
                var ship = battle.Ships[i];
                var s = ArtLibrary.Instance.Sprite($"iso_{ship.Art}_{Facing(sh.Heading)}");
                sh.Img.enabled = sh.Alive && s != null;
                if (s != null) PlaceSprite(sh.Img, s, sh.Pos, 2, 0.06f);
            }
            // Nearer the camera draws over what is behind it.
            var order = new List<(float y, Transform t)>();
            for (int i = 0; i < shown.Count; i++) order.Add((shown[i].Pos.y, shown[i].Img.transform));
            for (int i = 0; i < rockImages.Count; i++) order.Add((battle.Rocks[i].Pos.Y, rockImages[i].transform));
            int index = 0;
            foreach (var o in order.OrderBy(o => o.y)) o.t.SetSiblingIndex(index++);
        }

        void Ring(ShapeGraphic g, Vector2 centre, float radius, Color col, float width = 2f, int dash = 0)
        {
            const int n = 72;
            for (int i = 0; i < n; i++)
            {
                if (dash > 0 && i / dash % 2 == 1) continue;
                g.Line(Polar(centre, i * 360f / n, radius), Polar(centre, (i + 1) * 360f / n, radius), width, col);
            }
        }

        void DrawFloor()
        {
            floor.Clear();
            var c = new Color(0.5f, 0.62f, 0.8f, 0.07f);
            float w = OpenBattle.HalfWidth, h = OpenBattle.HalfHeight;
            for (float x = -w; x <= w + 1; x += 1500f) floor.Line(P(new Vector2(x, -h)), P(new Vector2(x, h)), 1f, c);
            for (float y = -h; y <= h + 1; y += 1500f) floor.Line(P(new Vector2(-w, y)), P(new Vector2(w, y)), 1f, c);
            var edge = new Color(0.5f, 0.62f, 0.8f, 0.3f);
            var k = new[] { new Vector2(-w, -h), new Vector2(w, -h), new Vector2(w, h), new Vector2(-w, h) };
            for (int i = 0; i < 4; i++) floor.Dashed(P(k[i]), P(k[(i + 1) % 4]), 2f, edge, 14f, 10f);
        }

        // ---------------------------------------------------------------- overlay: bases, drift, arcs, sight

        /// <summary>How far a ray from a point runs before a wreck or rock stops it.</summary>
        float ClearRun(Vector2 from, float degrees, float max)
        {
            var d = Dir(degrees);
            float best = max;
            foreach (var r in battle.Rocks)
            {
                var rel = new Vector2(r.Pos.X, r.Pos.Y) - from;
                float t = Vector2.Dot(rel, d);
                if (t <= 0) continue;
                float perp2 = rel.sqrMagnitude - t * t, r2 = (float)r.Radius * r.Radius;
                if (perp2 >= r2) continue;
                best = Mathf.Min(best, t - Mathf.Sqrt(r2 - perp2));
            }
            return Mathf.Max(0f, best);
        }

        /// <summary>One arc of a gun, lit as far as it can see and dimmed where a wreck stands in the way.</summary>
        void Wedge(Vector2 centre, float from, float to, float inner, float range, Color col)
        {
            const float step = 2.5f;
            var dim = new Color(col.r, col.g, col.b, col.a * 0.15f);
            for (float a = from; a < to - 0.01f; a += step)
            {
                float b = Mathf.Min(to, a + step);
                float run = Mathf.Max(inner, Mathf.Min(ClearRun(centre, (a + b) / 2f, range), range));
                under.Quad(Polar(centre, a, inner), Polar(centre, b, inner), Polar(centre, b, run), Polar(centre, a, run), col);
                if (run < range) under.Quad(Polar(centre, a, run), Polar(centre, b, run), Polar(centre, b, range), Polar(centre, a, range), dim);
            }
            var line = new Color(col.r, col.g, col.b, Mathf.Min(1f, col.a * 6f));
            under.Line(Polar(centre, from, inner), Polar(centre, from, range), 1f, line);
            under.Line(Polar(centre, to, inner), Polar(centre, to, range), 1f, line);
            for (float a = from; a < to - 0.01f; a += 5f)
                under.Line(Polar(centre, a, range), Polar(centre, Mathf.Min(to, a + 5f), range), 2f, line);
        }

        void WeaponArcs(Vector2 centre, int heading, int radius, OpenWeapon w, Color col)
        {
            if (w.Fore) Wedge(centre, heading - 30, heading + 30, radius, w.Range, col);
            if (w.Sides)
            {
                Wedge(centre, heading + 30, heading + 150, radius, w.Range, col);
                Wedge(centre, heading - 150, heading - 30, radius, w.Range, col);
            }
            if (w.Aft) Wedge(centre, heading + 150, heading + 210, radius, w.Range, col);
        }

        void Redraw()
        {
            under.Clear();
            over.Clear();
            Ui.Clear(labelRoot);
            bool busy = mode == Mode.Busy;

            // What the selected card or the hovered ship can reach.
            if (!busy && selected != null && selected.Kind == OpenKind.Fire)
            {
                var src = battle.Get(selected.SourceId);
                WeaponArcs(shown[src.Id].Pos, src.Heading, src.Radius, selected.Weapon, new Color(Lit.r, Lit.g, Lit.b, 0.1f));
            }
            else if (!busy && selected == null && hoverShip >= 0 && battle.Ships[hoverShip].Side == OpenSide.Enemy)
            {
                var e = battle.Ships[hoverShip];
                foreach (var w in e.Weapons) WeaponArcs(shown[e.Id].Pos, e.Heading, e.Radius, w, new Color(EnemyCol.r, EnemyCol.g, EnemyCol.b, 0.08f));
            }

            foreach (var r in battle.Rocks)
                Ring(under, new Vector2(r.Pos.X, r.Pos.Y), r.Radius, new Color(Hazard.r, Hazard.g, Hazard.b, 0.8f), 2f, 2);

            for (int i = 0; i < shown.Count; i++)
            {
                var sh = shown[i];
                if (!sh.Alive) continue;
                var ship = battle.Ships[i];
                var col = ship.Side == OpenSide.Player ? PlayerCol : EnemyCol;
                bool turning = !busy && mode == Mode.Heading && selected != null && selected.SourceId == i;
                int heading = turning ? previewHeading : sh.Heading;
                Ring(under, sh.Pos, ship.Radius, i == hoverShip ? Color.white : col, 2f);
                under.Line(Polar(sh.Pos, heading, ship.Radius), Polar(sh.Pos, heading, ship.Radius + 420f), 3f, col);

                if (!busy && ship.Speed > 0)
                {
                    // Momentum: where it will be when its side's turn ends.
                    var end = battle.Trace(ship, heading, ship.Speed * OpenMath.Span, out var stop);
                    var endP = new Vector2(end.X, end.Y);
                    // An enemy will turn and change speed before it drifts, so its line is only its present course.
                    bool mine = ship.Side == OpenSide.Player;
                    var faint = new Color(col.r, col.g, col.b, mine ? 0.7f : 0.3f);
                    if (!mine) stop = Stop.None;
                    under.Dashed(Polar(sh.Pos, heading, ship.Radius + 420f), P(endP), mine ? 2f : 1f, faint, 8f, 6f);
                    Ring(under, endP, ship.Radius, stop == Stop.None ? faint : Skin.Bad, stop == Stop.None ? 1f : 2f, 2);
                    if (stop != Stop.None) Callout(P(endP) + new Vector2(0, -22), stop == Stop.Wreck ? "COLLISION" : "STOPPED", Skin.Bad);
                }

                // Hull over shields, under the base.
                var b = P(sh.Pos + new Vector2(0, ship.Radius));
                float bw = Mathf.Round(ship.Radius * U * 0.8f);
                over.Rect(b.x - bw - 1, b.y + 7, bw * 2 + 2, 6, Skin.Ink);
                over.Rect(b.x - bw, b.y + 8, Mathf.Round(bw * 2 * Mathf.Clamp01(sh.Hull / (float)ship.MaxHull)), 4, col);
                for (int s = 0; s < sh.Shields; s++) over.Rect(b.x - bw + s * 7, b.y + 14, 5, 3, Skin.Cyan);
            }

            if (!busy && selected != null && selected.Kind == OpenKind.Fire)
            {
                var src = battle.Get(selected.SourceId);
                foreach (var t in battle.Live(OpenSide.Enemy))
                {
                    var shot = battle.Check(src, selected.Weapon, t);
                    var at = P(shown[t.Id].Pos);
                    if (shot == Shot.Clear)
                    {
                        over.Line(P(shown[src.Id].Pos), at, t.Id == hoverShip ? 3f : 2f, Lit);
                        Callout(at + new Vector2(0, -t.Radius * U - 34), $"{Predict(src, selected.Weapon, t)}  CLICK TO FIRE", Lit);
                    }
                    else
                    {
                        if (shot == Shot.Blocked) over.Dashed(P(shown[src.Id].Pos), at, 1f, Skin.Dim, 6f, 8f);
                        Callout(at + new Vector2(0, -t.Radius * U - 34), shot == Shot.Blocked ? "NO LINE OF SIGHT" : shot == Shot.OutOfRange ? "OUT OF RANGE" : "OUT OF ARC", Skin.Dim);
                    }
                }
            }
            if (!busy && mode == Mode.Heading && selected != null)
            {
                var src = battle.Get(selected.SourceId);
                int limit = battle.TurnLimit(selected, src);
                var centre = shown[src.Id].Pos;
                var fan = new Color(PlayerCol.r, PlayerCol.g, PlayerCol.b, 0.06f);
                for (float a = src.Heading - limit; a < src.Heading + limit - 0.01f; a += 5f)
                    under.Quad(Polar(centre, a, src.Radius), Polar(centre, a + 5f, src.Radius), Polar(centre, a + 5f, src.Radius + 1600f), Polar(centre, a, src.Radius + 1600f), fan);
                for (int a = src.Heading - limit; a <= src.Heading + limit; a += OpenMath.HeadingStep)
                    under.Line(Polar(centre, a, src.Radius + 1400f), Polar(centre, a, src.Radius + 1600f), 2f, PlayerCol);
                under.Line(Polar(centre, previewHeading, src.Radius), Polar(centre, previewHeading, src.Radius + 1600f), 3f, Color.white);
            }
        }

        string Predict(OpenShip from, OpenWeapon w, OpenShip target)
        {
            var arc = OpenMath.ArcOf(target.Pos, target.Heading, from.Pos);
            int rest = Mathf.Max(0, w.Damage - target.Shields);
            int hull = Mathf.Max(0, rest - target.Armor[(int)arc]);
            return hull > 0 ? $"{hull} HULL TO {ArcName(arc)}" : "SHIELDS ONLY";
        }

        static string ArcName(OpenArc arc) => arc == OpenArc.Fore ? "FORE" : arc == OpenArc.Aft ? "AFT" : arc == OpenArc.Port ? "PORT" : "STARBOARD";

        void Callout(Vector2 at, string text, Color col)
        {
            float w = text.Length * 8f + 16f;
            var bg = Ui.Fill(labelRoot, "Callout", Mathf.Round(at.x - w / 2f), Mathf.Round(at.y - 9f), w, 18, new Color(0.04f, 0.05f, 0.07f, 0.9f));
            var t = Ui.Label(bg.transform, text, Skin.Stencil, 12, col, 0, 3, w, 14, TextAlignmentOptions.Center);
            t.textWrappingMode = TextWrappingModes.NoWrap;
        }

        // ---------------------------------------------------------------- HUD

        void Refresh()
        {
            turnLabel.text = "TURN " + battle.Turn;
            for (int i = 0; i < pips.Count; i++)
            {
                sockets[i].gameObject.SetActive(i < battle.MaxCommand);
                pips[i].gameObject.SetActive(i < battle.Command);
            }
            deckLabel.text = $"DECK {battle.DrawPile.Count}    DISCARD {battle.Discard.Count}";
            endTurn.interactable = mode != Mode.Busy && mode != Mode.Over;
            RebuildPanels();
            RebuildHand();
            Redraw();
            ShowHint();
        }

        void RebuildPanels()
        {
            Ui.Clear(panelRoot);
            float y = 64;
            foreach (var s in battle.Ships.Where(x => x.Side == OpenSide.Player))
            {
                var p = Ui.Plate(panelRoot, s.Name, 12, y, 300, 138).transform;
                var sh = shown[s.Id];
                Ui.Heading(p, sh.Alive ? s.Name : s.Name + " - lost", 16, 16, 14, 200, 20, TextAlignmentOptions.Left, sh.Alive ? Skin.Text : Skin.Faint);
                Ui.Label(p, s.Flagship ? "FLAGSHIP" : "ESCORT", Skin.Stencil, 12, Skin.BrassDark, 190, 18, 94, 14, TextAlignmentOptions.Right);
                Ui.Label(p, "HULL", Skin.Stencil, 12, Skin.Good, 16, 44, 60, 14);
                Ui.Bar(p, "Hull", 70, 40, 150, 18, sh.Hull / (float)s.MaxHull, Skin.Good);
                Ui.Label(p, $"{sh.Hull}/{s.MaxHull}", Skin.Mono, 12, Skin.Text, 228, 44, 60, 14);
                Ui.Label(p, "HEAT", Skin.Stencil, 12, Skin.Warn, 16, 66, 60, 14);
                Ui.Bar(p, "Heat", 70, 62, 150, 18, s.Heat / (float)s.HeatCap, Skin.Warn);
                Ui.Label(p, $"{s.Heat}/{s.HeatCap}", Skin.Mono, 12, Skin.Text, 228, 66, 60, 14);
                Ui.Label(p, $"SHIELDS {sh.Shields}", Skin.Stencil, 12, Skin.Cyan, 16, 90, 130, 14);
                Ui.Label(p, $"ARMOR F{s.Armor[0]} S{s.Armor[1]} A{s.Armor[2]} P{s.Armor[3]}", Skin.Stencil, 12, Skin.Dim, 130, 90, 160, 14, TextAlignmentOptions.Right);
                Ui.Label(p, $"SPEED {s.Speed} OF {s.MaxSpeed}", Skin.Stencil, 12, Skin.Cyan, 16, 110, 140, 14);
                Ui.Label(p, $"HEADING {sh.Heading:000}", Skin.Stencil, 12, Skin.Dim, 150, 110, 140, 14, TextAlignmentOptions.Right);
                y += 146;
            }
            var enemies = battle.Ships.Where(x => x.Side == OpenSide.Enemy).ToList();
            var e = Ui.Plate(panelRoot, "Enemies", 1608, 64, 300, 40 + enemies.Count * 40).transform;
            Ui.Label(e, "ENEMY SHIPS", Skin.Stencil, 12, Skin.BrassDark, 16, 14, 200, 14);
            float ey = 36;
            foreach (var s in enemies)
            {
                var sh = shown[s.Id];
                Ui.Label(e, s.Name.ToUpperInvariant(), Skin.Stencil, 12, sh.Alive ? Skin.Text : Skin.Faint, 16, ey, 180, 14);
                Ui.Label(e, sh.Alive ? $"HULL {sh.Hull}  SH {sh.Shields}" : "DESTROYED", Skin.Stencil, 12, sh.Alive ? Skin.Bad : Skin.Faint, 150, ey, 136, 14, TextAlignmentOptions.Right);
                if (sh.Alive) Ui.Label(e, $"SPEED {s.Speed}   ARMOR F{s.Armor[0]} S{s.Armor[1]} A{s.Armor[2]} P{s.Armor[3]}", Skin.Stencil, 12, Skin.Faint, 16, ey + 16, 270, 14);
                ey += 40;
            }
        }

        void RebuildHand()
        {
            Ui.Clear(handRoot);
            handViews.Clear();
            int n = battle.Hand.Count;
            for (int i = 0; i < n; i++)
            {
                var c = battle.Hand[i];
                var src = battle.Get(c.SourceId);
                var def = new CardDef { Id = c.Art, Name = c.Name, Cost = c.Cost, Type = c.Type, Text = c.Text };
                var view = CardView.Create(handRoot, def, src.Name, src.Flagship ? "con_trimaran_E128" : "con_outrider_E", c.Cost, c.Text, false);
                view.Rect.anchoredPosition = new Vector2(960f - (n - 1) * 115f + i * 230f, -(c == selected ? 1060f : 1076f));
                view.SetPlayable(mode != Mode.Busy && mode != Mode.Over && battle.WhyNot(c) == null, c.Cost);
                view.SetSelected(c == selected);
                var card = c;
                view.Clicked = _ => OnCard(card);
                handViews.Add(view);
            }
        }

        void Flash(string text, float seconds)
        {
            flash = text;
            flashUntil = Time.unscaledTime + seconds;
        }

        void ShowHint()
        {
            if (hint == null) return;
            if (mode == Mode.Busy) hint.text = "";
            else if (mode == Mode.Target) hint.text = $"{selected.Name}: click an enemy in the lit arc. Wrecks and rocks block fire. Esc to put the card back.";
            else if (mode == Mode.Heading) hint.text = $"{selected.Name}: point where the bow should go, then click. The ghost shows the drift on that heading. Esc to cancel.";
            else if (hoverShip >= 0 && battle.Ships[hoverShip].Side == OpenSide.Enemy)
            {
                var e = battle.Ships[hoverShip];
                hint.text = $"<color=#d9573f>{e.Name.ToUpperInvariant()}</color>  " + string.Join("  ", e.Weapons.Select(w => $"{w.Name}: {w.Damage} dmg, {w.ArcText}, range {w.Range / 1000}.")) + "  It fires first, then turns and drifts.";
            }
            else if (Time.unscaledTime < flashUntil) hint.text = flash;
            else hint.text = "Pick a card. Ships drift along their heading when you end the turn.";
        }

        // ---------------------------------------------------------------- input

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || battle == null) return;
            if (kb.escapeKey.wasPressedThisFrame) Cancel();
            if (mode == Mode.Busy || mode == Mode.Over) return;
            if (kb.eKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) OnEndTurn();
            if (kb.f9Key.wasPressedThisFrame) BotTurn();
            for (int i = 0; i < 9; i++)
                if (kb[Key.Digit1 + i].wasPressedThisFrame && i < battle.Hand.Count) OnCard(battle.Hand[i]);
            if (flashUntil > 0 && Time.unscaledTime >= flashUntil)
            {
                flashUntil = 0;
                ShowHint();
            }
        }

        void Cancel()
        {
            if (mode != Mode.Target && mode != Mode.Heading) return;
            selected = null;
            mode = Mode.Idle;
            Refresh();
        }

        void OnPointer(PointerEventData e, bool click)
        {
            if (battle == null || mode == Mode.Busy || mode == Mode.Over) return;
            if (click && e.button == PointerEventData.InputButton.Right)
            {
                Cancel();
                return;
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(spriteRoot, e.position, null, out var local);
            pointer = new Vector2((local.x - Cx) / U, (-local.y - Cy) / (U * Tilt));

            int hit = -1;
            float best = float.MaxValue;
            for (int i = 0; i < shown.Count; i++)
            {
                if (!shown[i].Alive) continue;
                float d = (shown[i].Pos - pointer).magnitude;
                if (d > battle.Ships[i].Radius * 1.5f + 150f || d >= best) continue;
                best = d;
                hit = i;
            }
            bool changed = hit != hoverShip;
            hoverShip = hit;

            if (mode == Mode.Heading)
            {
                var src = battle.Get(selected.SourceId);
                int limit = battle.TurnLimit(selected, src);
                var rel = pointer - shown[src.Id].Pos;
                float want = Mathf.Atan2(rel.y, rel.x) * Mathf.Rad2Deg;
                int turn = Mathf.RoundToInt(Mathf.DeltaAngle(src.Heading, want) / OpenMath.HeadingStep) * OpenMath.HeadingStep;
                int heading = OpenMath.Norm(src.Heading + Mathf.Clamp(turn, -limit, limit));
                if (heading != previewHeading) changed = true;
                previewHeading = heading;
                if (click)
                {
                    Act(battle.Play(selected.Id, OpenChoice.Facing(heading)));
                    return;
                }
            }
            else if (mode == Mode.Target && click && hit >= 0 && battle.Ships[hit].Side == OpenSide.Enemy)
            {
                Act(battle.Play(selected.Id, OpenChoice.At(hit)));
                return;
            }
            if (changed)
            {
                Redraw();
                ShowHint();
            }
        }

        void OnCard(OpenCard card)
        {
            if (mode == Mode.Busy || mode == Mode.Over) return;
            if (card == selected)
            {
                Cancel();
                return;
            }
            string why = battle.WhyNot(card);
            if (why != null)
            {
                Flash($"<color=#f0a040>{why}</color>", 3f);
                ShowHint();
                return;
            }
            if (card.Kind == OpenKind.Fire || card.Kind == OpenKind.Turn)
            {
                selected = card;
                mode = card.Kind == OpenKind.Fire ? Mode.Target : Mode.Heading;
                previewHeading = battle.Get(card.SourceId).Heading;
                Refresh();
                return;
            }
            selected = card;
            Act(battle.Play(card.Id, OpenChoice.None));
        }

        /// <summary>After a rules call: say why it was refused, or play back what happened.</summary>
        void Act(string refused)
        {
            if (refused != null)
            {
                Flash($"<color=#f0a040>{refused}</color>", 3f);
                ShowHint();
                return;
            }
            selected = null;
            StartCoroutine(Playback());
        }

        void OnEndTurn()
        {
            if (mode == Mode.Busy || mode == Mode.Over) return;
            selected = null;
            battle.EndTurn();
            StartCoroutine(Playback());
        }

        /// <summary>F9, and play-test scripts: the bot plays the player's turn.</summary>
        public void BotTurn()
        {
            if (mode == Mode.Busy || mode == Mode.Over) return;
            selected = null;
            OpenBot.PlayTurn(battle);
            StartCoroutine(Playback());
        }

        public OpenBattle State => battle;
        public bool IsBusy => mode == Mode.Busy;

        // ---------------------------------------------------------------- playback

        IEnumerator Playback()
        {
            mode = Mode.Busy;
            Refresh();
            while (logCursor < battle.Log.Count)
            {
                var e = battle.Log[logCursor++];
                var sh = e.ShipId >= 0 ? shown[e.ShipId] : null;
                switch (e.Kind)
                {
                    case OpenEventKind.Moved:
                    {
                        Vector2 a = new Vector2(e.From.X, e.From.Y), b = new Vector2(e.To.X, e.To.Y);
                        float seconds = Mathf.Clamp((b - a).magnitude / 4000f, 0.2f, 0.7f);
                        for (float t = 0; t < seconds; t += Time.deltaTime)
                        {
                            sh.Pos = Vector2.Lerp(a, b, Mathf.SmoothStep(0f, 1f, t / seconds));
                            PlaceShips();
                            Redraw();
                            yield return null;
                        }
                        sh.Pos = b;
                        break;
                    }
                    case OpenEventKind.Turned:
                        sh.Heading = e.Absorbed;
                        PlaceShips();
                        Redraw();
                        yield return new WaitForSeconds(0.18f);
                        break;
                    case OpenEventKind.Speed:
                        Floater(P(sh.Pos) + new Vector2(0, -50), "SPEED " + e.Amount, Skin.Cyan, 12);
                        yield return new WaitForSeconds(0.12f);
                        break;
                    case OpenEventKind.Fired:
                    {
                        var target = shown[e.TargetId];
                        var col = battle.Ships[e.ShipId].Side == OpenSide.Player ? Skin.Hex("ffc46a") : Skin.Cyan;
                        Sfx.Play("sfx_lance");
                        for (float t = 0; t < 0.22f; t += Time.deltaTime)
                        {
                            Redraw();
                            over.Line(P(sh.Pos), P(target.Pos), 4f, col);
                            over.Line(P(sh.Pos), P(target.Pos), 2f, Color.white);
                            yield return null;
                        }
                        target.Hull -= e.Amount;
                        target.Shields = Mathf.Max(0, target.Shields - e.Absorbed);
                        var at = P(target.Pos);
                        if (e.Amount > 0) Floater(at + new Vector2(0, -70), "-" + e.Amount, Skin.Bad, 24);
                        if (e.Absorbed > 0) Floater(at + new Vector2(0, 46), e.Amount > 0 ? $"SHIELDS TOOK {e.Absorbed}" : "SHIELDS HOLD", Skin.Cyan, 12);
                        else if (e.Amount == 0) Floater(at + new Vector2(0, 46), "ARMOR HOLDS", Skin.Dim, 12);
                        Flash($"{battle.Ships[e.ShipId].Name} hit {battle.Ships[e.TargetId].Name} to {ArcName(e.Arc).ToLowerInvariant()} with {e.Text}: -{e.Amount} hull" + (e.Absorbed > 0 ? $", shields took {e.Absorbed}" : "") + (e.Armored > 0 ? $", armor took {e.Armored}" : ""), 5f);
                        hint.text = flash;
                        Redraw();
                        RebuildPanels();
                        yield return new WaitForSeconds(0.45f);
                        break;
                    }
                    case OpenEventKind.Shields:
                        sh.Shields += e.Amount;
                        Floater(P(sh.Pos) + new Vector2(0, -60), $"+{e.Amount} SHIELDS", Skin.Cyan, 12);
                        yield return new WaitForSeconds(0.2f);
                        break;
                    case OpenEventKind.Vented:
                        Floater(P(sh.Pos) + new Vector2(0, -60), $"-{e.Amount} HEAT", Skin.Warn, 12);
                        yield return new WaitForSeconds(0.2f);
                        break;
                    case OpenEventKind.Collision:
                        sh.Hull -= e.Amount;
                        Floater(P(sh.Pos) + new Vector2(0, -70), e.Amount > 0 ? $"HIT {e.Text.ToUpperInvariant()}  -{e.Amount}" : "STOPPED AT THE EDGE", Skin.Bad, 12);
                        Sfx.Play("sfx_hit");
                        yield return new WaitForSeconds(0.4f);
                        break;
                    case OpenEventKind.Destroyed:
                        Floater(P(sh.Pos) + new Vector2(0, -40), "DESTROYED", Skin.Bad, 24);
                        Sfx.Play("sfx_explosion");
                        for (float t = 0; t < 0.5f; t += Time.deltaTime)
                        {
                            sh.Img.color = new Color(1f, 1f - t, 1f - t, 1f - t * 2f);
                            yield return null;
                        }
                        sh.Alive = false;
                        sh.Img.color = Color.white;
                        PlaceShips();
                        break;
                }
            }
            SyncShown();
            selected = null;
            mode = battle.Outcome == OpenOutcome.Ongoing ? Mode.Idle : Mode.Over;
            Refresh();
            if (mode == Mode.Over) ShowEnd();
        }

        void Floater(Vector2 at, string text, Color col, float size) => StartCoroutine(FloatUp(at, text, col, size));

        IEnumerator FloatUp(Vector2 at, string text, Color col, float size)
        {
            var t = Ui.Label(overlayRoot, text, size >= 24 ? Skin.Mono : Skin.Stencil, size, col, at.x - 200, at.y - size / 2f, 400, size + 4, TextAlignmentOptions.Center);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.fontMaterial.EnableKeyword("UNDERLAY_ON");
            t.fontMaterial.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 1f));
            for (float s = 0; s < 1.1f; s += Time.deltaTime)
            {
                if (t == null) yield break;
                t.rectTransform.anchoredPosition = new Vector2(Mathf.Round(at.x - 200), -Mathf.Round(at.y - size / 2f - s * 26f));
                t.alpha = s < 0.8f ? 1f : 1f - (s - 0.8f) / 0.3f;
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
        }

        // ---------------------------------------------------------------- dialogs

        void ShowEnd()
        {
            var shade = Ui.Fill(overlayRoot, "Shade", 0, 0, Ui.RefW, Ui.RefH, new Color(0f, 0f, 0f, 0.6f));
            shade.raycastTarget = true;
            var d = Ui.Plate(shade.transform, "End", 660, 340, 600, 300).transform;
            bool won = battle.Outcome == OpenOutcome.Victory;
            Ui.Heading(d, won ? "The yard is yours" : "Fleet lost", 32, 0, 40, 600, 40, TextAlignmentOptions.Center, won ? Skin.BrassLight : Skin.Bad);
            Ui.Body(d, $"Turn {battle.Turn}. This is a prototype of battle without a grid: one fight, no rewards.", 16, 50, 110, 500, 60, TextAlignmentOptions.Top, Skin.Dim);
            Ui.Btn(d, "Again", 60, 200, 220, 60, () => Begin(seed + 1), 24);
            Ui.Btn(d, "Menu", 320, 200, 220, 60, () => SceneManager.LoadScene("Menu"), 24);
        }

        void ShowHelp()
        {
            var shade = Ui.Fill(overlayRoot, "HelpShade", 0, 0, Ui.RefW, Ui.RefH, new Color(0f, 0f, 0f, 0.7f));
            shade.raycastTarget = true;
            var d = Ui.Plate(shade.transform, "Help", 460, 180, 1000, 620).transform;
            Ui.Heading(d, "Battle without a grid", 32, 40, 34, 700, 40);
            Ui.Btn(d, "Close", 810, 30, 150, 48, () => Destroy(shade.gameObject), 16);
            string text =
                "Ships stand on round bases anywhere on the field and face one of 24 headings.\n\n" +
                "Momentum. Every ship keeps its Speed. When you end the turn, each of your ships drifts that many spans straight ahead: the dashed line and ghost base show where. Burn adds Speed, Retro Burn sheds it, Hard About turns the bow.\n\n" +
                "Guns need the target in arc, in range and in sight. Pick a weapon card and its arcs light up; where a wreck or rock stands in the way the arc goes dark behind it.\n\n" +
                "Flanking. A hit lands on the side of the target that faces the shooter. Shields soak first, then the armor of that arc. Most ships have none aft.\n\n" +
                "The enemy. After your drift each enemy fires once from where it sits, then turns, changes speed and drifts. Hover an enemy to see where its guns bear, and end your turn outside those arcs or behind a wreck.\n\n" +
                "Running into a wreck costs 3 hull and stops the ship.";
            Ui.Label(d, text, Skin.Body, 16, Skin.Text, 40, 100, 920, 480);
        }
    }
}
