using System;
using System.Collections.Generic;
using System.Linq;
using Silence.Core.Battle;
using Silence.Core.Hex;
using UnityEngine;
using Terrain = Silence.Core.Battle.Terrain;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Silence.View
{
    /// <summary>
    /// How another screen hands a battle to the Battle scene. With nothing pending, the scene offers the
    /// two paper-prototype scenarios as a skirmish.
    /// </summary>
    public static class BattleLauncher
    {
        /// <summary>Builds the battle to fight (not yet started).</summary>
        public static Func<Battle> Create;
        /// <summary>Called when the player leaves the end screen.</summary>
        public static Action<Battle> Finished;
        /// <summary>Called when the player quits mid-battle. Null: back to the skirmish menu.</summary>
        public static Action Quit;
        public static string Subtitle = "";

        public static void Clear()
        {
            Create = null;
            Finished = null;
            Quit = null;
            Subtitle = "";
        }
    }

    /// <summary>
    /// The battle screen. It never changes battle state: clicks become <see cref="Battle.PlayCard"/>,
    /// <see cref="Battle.CommandCraft"/> or <see cref="Battle.EndTurn"/>, and the events those add to
    /// <see cref="Battle.Log"/> are animated in order.
    /// </summary>
    public sealed partial class BattleScreen : MonoBehaviour
    {
        enum Mode { Intro, Idle, Target, Facing, Dart, Busy, Over }

        public Battle Battle { get; private set; }
        /// <summary>For tests and tools: true while events are being animated.</summary>
        public bool IsBusy => mode == Mode.Busy;

        Camera cam;
        BoardView board;
        Mode mode = Mode.Intro;
        int logCursor;

        CardInstance selectedCard;
        Unit facingUnit;
        List<HexDirection> facingOptions;
        Unit selectedDart;
        int panelUnitId = -1;
        int hoverUnitId = -1;
        HexCoord hoverHex = new HexCoord(-99, -99);
        bool expend;
        Battle previousSkirmish;
        ulong skirmishSeed = 1;

        void Awake()
        {
            cam = Camera.main;
            if (cam != null)
            {
                // Whole-number scaling, the same factor as the HUD (Ui.PixelScale): one art pixel is always
                // 1, 2 or 3 screen pixels, and anything left over is letterboxed. PixelScaler keeps the size
                // in step when the window changes.
                var ppc = cam.GetComponent<PixelPerfectCamera>();
                if (ppc != null) ppc.enabled = false;
                cam.orthographic = true;
                cam.orthographicSize = Ui.WorldHalfHeight;
                cam.transform.position = new Vector3(0f, 0f, -10f);
                cam.backgroundColor = Skin.Ink;
            }
            BuildHud();
        }

        void Start()
        {
            if (BattleLauncher.Create != null) Begin(BattleLauncher.Create());
            else ShowSkirmishMenu();
        }

        /// <summary>Start showing a battle. It is started here if it has not been already.</summary>
        public void Begin(Battle battle)
        {
            if (board != null) Destroy(board.gameObject);
            Battle = battle;
            if (battle.Phase == BattlePhase.Setup) battle.Start();
            logCursor = battle.Log.Count;
            logLines.Clear();
            foreach (var e in battle.Log)
                if (e.Kind == EventKind.IntentDeclared) AddLog(e);

            board = new GameObject("Board").AddComponent<BoardView>();
            board.Init(battle, BoardCentre);
            selectedCard = null;
            selectedDart = null;
            facingUnit = null;
            expend = false;
            var flag = battle.Flagship ?? battle.LiveUnits(Side.Player).FirstOrDefault();
            panelUnitId = flag != null ? flag.Id : -1;
            HideOverlay();
            mode = battle.Outcome == BattleOutcome.Ongoing ? Mode.Idle : Mode.Over;
            Refresh();
            if (mode == Mode.Over) ShowEndScreen();
        }

        // ---------------------------------------------------------------- input

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || Battle == null) return;
            if (kb.escapeKey.wasPressedThisFrame) Cancel();
            if (mode == Mode.Idle || mode == Mode.Target || mode == Mode.Facing || mode == Mode.Dart)
            {
                if (kb.eKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) OnEndTurn();
                if (kb.f9Key.wasPressedThisFrame) BotTurn();
                for (int i = 0; i < 9; i++)
                {
                    if (!kb[Key.Digit1 + i].wasPressedThisFrame) continue;
                    if (i < Battle.Deck.Hand.Count) OnCardClicked(Battle.Deck.Hand[i]);
                }
            }
            AnimateHand();
        }

        Vector2 PointerWorld(PointerEventData e)
        {
            var w = cam.ScreenToWorldPoint(new Vector3(e.position.x, e.position.y, 10f));
            return new Vector2(w.x, w.y);
        }

        void OnBoardMove(PointerEventData e)
        {
            if (Battle == null || board == null) return;
            var hex = board.HexAt(PointerWorld(e));
            if (hex == hoverHex) return;
            hoverHex = hex;
            var u = Battle.Board.InBounds(hex) ? Battle.UnitAt(hex) : null;
            hoverUnitId = u != null ? u.Id : -1;
            if (mode != Mode.Busy)
            {
                board.RefreshOverlay(u != null && u.Side == Side.Enemy ? u.Id : -1);
                RefreshChips();
            }
            RefreshUnitPanel();
            RefreshPrompt();
        }

        void OnBoardClick(PointerEventData e)
        {
            if (Battle == null || board == null) return;
            BoardClick(PointerWorld(e), e.button == PointerEventData.InputButton.Right);
        }

        // ---- the same entry points the mouse uses, for keyboard shortcuts, tools and play-testing scripts

        public void ClickCard(int handIndex)
        {
            if (Battle != null && handIndex >= 0 && handIndex < Battle.Deck.Hand.Count) OnCardClicked(Battle.Deck.Hand[handIndex]);
        }

        public void ClickHex(int col, int row) => BoardClick(board.World(HexCoord.FromOffset(col, row)), false);

        public void ClickWorld(Vector2 world, bool rightButton = false) => BoardClick(world, rightButton);

        public void ClickEndTurn() => OnEndTurn();

        public void ClickExpend() => ToggleExpend();

        public string PromptLine => PromptText();

        /// <summary>Let the stand-in bot play the rest of this turn, then animate what happened (F9).</summary>
        public void BotTurn()
        {
            if (mode == Mode.Busy || mode == Mode.Over || mode == Mode.Intro) return;
            ClearSelection();
            SimpleBot.PlayTurn(Battle);
            mode = Mode.Busy;
            StartCoroutine(PlayEvents());
        }

        public bool IsOver => mode == Mode.Over;
        public bool IsIdle => mode == Mode.Idle;

        /// <summary>The end screen's Continue button.</summary>
        public void Continue()
        {
            if (mode == Mode.Over) LeaveBattle();
        }

        /// <summary>Let the bot play the whole battle through this screen (play-testing).</summary>
        public void AutoPlay() => StartCoroutine(AutoPlayRoutine());

        System.Collections.IEnumerator AutoPlayRoutine()
        {
            while (Battle != null && Battle.Outcome == BattleOutcome.Ongoing)
            {
                if (mode == Mode.Idle) BotTurn();
                yield return null;
            }
        }

        void BoardClick(Vector2 world, bool rightButton)
        {
            if (Battle == null || board == null) return;
            if (rightButton)
            {
                Cancel();
                return;
            }
            if (mode == Mode.Busy || mode == Mode.Over || mode == Mode.Intro) return;

            var hex = board.HexAt(world);
            var unit = Battle.Board.InBounds(hex) ? Battle.UnitAt(hex) : null;

            switch (mode)
            {
                case Mode.Facing:
                {
                    var centre = (Vector2)board.World(facingUnit.Pos);
                    var d = world - centre;
                    if (d.magnitude < 0.6f || d.magnitude > BoardView.HexSize * 3.2f) { Flash("Click one of the arrows to choose a heading."); return; }
                    float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                    HexDirection best = facingOptions[0];
                    float bestDiff = 999f;
                    foreach (var o in facingOptions)
                    {
                        float diff = Mathf.Abs(Mathf.DeltaAngle(angle, BoardView.WorldAngle(o)));
                        if (diff < bestDiff) { bestDiff = diff; best = o; }
                    }
                    if (bestDiff > 31f) { Flash("That heading is too sharp a turn."); return; }
                    int steps = TurnSteps(facingUnit.Facing, best);
                    bool onSource = selectedCard.Def.Target != TargetKind.AllyCapital;
                    Play(selectedCard, PlayTarget.Turning(onSource ? -1 : facingUnit.Id, steps));
                    return;
                }
                case Mode.Target:
                {
                    var def = selectedCard.Def;
                    if (def.Target == TargetKind.None)
                    {
                        Play(selectedCard, PlayTarget.None);
                        return;
                    }
                    if (unit == null) { Flash(def.Target == TargetKind.Enemy ? "Choose an enemy." : "Choose one of your capital ships."); return; }
                    if (NeedsFacing(def) && def.Target == TargetKind.AllyCapital)
                    {
                        if (unit.Side != Side.Player || !unit.IsCapital) { Flash("Choose one of your capital ships."); return; }
                        EnterFacing(unit);
                        return;
                    }
                    Play(selectedCard, new PlayTarget { UnitId = unit.Id, Expend = expend });
                    return;
                }
                case Mode.Dart:
                {
                    if (unit != null && unit.Side == Side.Enemy)
                    {
                        var reach = Battle.Reachable(selectedDart.Pos, selectedDart.Move + selectedDart.CraftBonusMove);
                        var spots = reach.Where(h => h.DistanceTo(unit.Pos) == 1).OrderBy(h => h.DistanceTo(selectedDart.Pos)).ToList();
                        if (spots.Count == 0) { Flash("That enemy is out of the dart's reach."); return; }
                        Do(() => Battle.CommandCraft(selectedDart.Id, spots[0], unit.Id));
                        return;
                    }
                    if (unit != null && unit != selectedDart && unit.Side == Side.Player && unit.Kind == UnitKind.Craft && !unit.CraftActed)
                    {
                        SelectDart(unit);
                        return;
                    }
                    if (unit == selectedDart)
                    {
                        Cancel();
                        return;
                    }
                    {
                        var reach = Battle.Reachable(selectedDart.Pos, selectedDart.Move + selectedDart.CraftBonusMove);
                        if (!reach.Contains(hex)) { Flash("Out of the dart's reach."); return; }
                        // After moving, a dart attacks an adjacent enemy automatically: torpedoes first, then the weakest.
                        var auto = Battle.LiveUnits(Side.Enemy).Where(en => en.Pos.DistanceTo(hex) == 1)
                            .OrderBy(en => en.Kind == UnitKind.Torpedo ? 0 : en.Kind == UnitKind.Craft ? 1 : 2).ThenBy(en => en.Hull).FirstOrDefault();
                        Do(() => Battle.CommandCraft(selectedDart.Id, hex, auto != null ? auto.Id : -1));
                    }
                    return;
                }
                default:
                    if (unit == null) return;
                    if (unit.Side == Side.Player && unit.Kind == UnitKind.Craft)
                    {
                        if (unit.CraftActed) { Flash("That dart has already acted this turn."); return; }
                        SelectDart(unit);
                        return;
                    }
                    panelUnitId = unit.Id;
                    RefreshUnitPanel();
                    return;
            }
        }

        static int TurnSteps(HexDirection from, HexDirection to)
        {
            int diff = ((int)to - (int)from + HexDirections.Count) % HexDirections.Count;
            return diff > 3 ? diff - HexDirections.Count : diff;
        }

        static bool NeedsFacing(CardDef def) => def.Effects.Any(e => e is TurnEffect || e is ScreenEffect);

        void OnCardClicked(CardInstance card)
        {
            if (mode == Mode.Busy || mode == Mode.Over || mode == Mode.Intro) return;
            if (Battle.Deck.FindInHand(card.Id) == null) return;
            var def = card.Def;

            if (selectedCard == card && mode == Mode.Target && def.Target == TargetKind.None && !NeedsFacing(def))
            {
                Play(card, PlayTarget.None);
                return;
            }
            if (selectedCard == card)
            {
                Cancel();
                return;
            }
            if (def.Unplayable) { Flash($"{def.Name} is unplayable. It leaves at a shipyard."); return; }
            if (Battle.CostOf(card) > Battle.Command) { Flash($"{def.Name} needs {Battle.CostOf(card)} command; you have {Battle.Command}."); return; }
            if (card.SourceId >= 0 && (Battle.Get(card.SourceId) == null || !Battle.Get(card.SourceId).Alive)) { Flash("That card's ship is gone."); return; }

            ClearSelection();
            selectedCard = card;
            expend = false;
            mode = Mode.Target;
            Sfx.Play("sfx_card");

            if (NeedsFacing(def) && def.Target != TargetKind.AllyCapital)
            {
                EnterFacing(Battle.Get(card.SourceId));
                return;
            }
            if (def.Target == TargetKind.AllyCapital && !NeedsFacing(def))
            {
                // With a single possible ship there is nothing to choose.
                var options = Battle.LiveUnits(Side.Player).Where(u => u.IsCapital && Battle.CheckPlay(card.Id, PlayTarget.At(u.Id)) == null).ToList();
                var all = Battle.LiveUnits(Side.Player).Where(u => u.IsCapital).ToList();
                if (all.Count == 1 && options.Count == 1)
                {
                    Play(card, PlayTarget.At(options[0].Id));
                    return;
                }
            }
            RefreshSelection();
        }

        void EnterFacing(Unit ship)
        {
            if (ship == null) { Cancel(); return; }
            facingUnit = ship;
            facingOptions = new List<HexDirection> { ship.Facing.Rotate(1), ship.Facing.Rotate(-1) };
            bool twice = selectedCard.Def.Effects.OfType<TurnEffect>().Any(t => ship.Speed <= t.SlowSpeed);
            if (twice)
            {
                facingOptions.Add(ship.Facing.Rotate(2));
                facingOptions.Add(ship.Facing.Rotate(-2));
            }
            mode = Mode.Facing;
            RefreshSelection();
        }

        void SelectDart(Unit dart)
        {
            ClearSelection();
            selectedDart = dart;
            panelUnitId = dart.Id;
            mode = Mode.Dart;
            RefreshSelection();
            RefreshUnitPanel();
        }

        void ClearSelection()
        {
            selectedCard = null;
            selectedDart = null;
            facingUnit = null;
            expend = false;
            if (board != null)
            {
                board.ClearHighlights();
                board.ClearRings();
                board.ClearArrows();
            }
        }

        void Cancel()
        {
            if (mode == Mode.Busy || mode == Mode.Over || mode == Mode.Intro) return;
            ClearSelection();
            mode = Mode.Idle;
            RefreshSelection();
        }

        void ToggleExpend()
        {
            expend = !expend;
            RefreshSelection();
        }

        void Play(CardInstance card, PlayTarget target)
        {
            string name = card.Def.Name;
            string error = Battle.CheckPlay(card.Id, target);
            if (error != null)
            {
                Flash($"{name}: {error}.");
                Sfx.Play("sfx_deny");
                return;
            }
            Do(() => Battle.PlayCard(card.Id, target));
        }

        /// <summary>Run one player action against Core, then animate whatever it logged.</summary>
        void Do(Func<string> action)
        {
            string error = action();
            if (error != null)
            {
                Flash(char.ToUpper(error[0]) + error.Substring(1) + ".");
                Sfx.Play("sfx_deny");
                return;
            }
            ClearSelection();
            mode = Mode.Busy;
            RefreshPrompt();
            StartCoroutine(PlayEvents());
        }

        void OnEndTurn()
        {
            if (mode == Mode.Busy || mode == Mode.Over || mode == Mode.Intro) return;
            Do(() => Battle.EndTurn());
        }

        // ---------------------------------------------------------------- selection display

        void RefreshSelection()
        {
            board.ClearHighlights();
            board.ClearRings();
            board.ClearArrows();

            if (mode == Mode.Facing && facingUnit != null)
            {
                board.ShowArrows(facingUnit.Pos, facingUnit.Facing, facingOptions, Skin.Brass);
                board.SetRing(facingUnit.Id, Skin.Brass);
            }
            else if (mode == Mode.Target && selectedCard != null)
            {
                HighlightCard(selectedCard);
            }
            else if (mode == Mode.Dart && selectedDart != null)
            {
                var reach = Battle.Reachable(selectedDart.Pos, selectedDart.Move + selectedDart.CraftBonusMove);
                foreach (var h in reach) board.Highlight(h, Skin.Alpha(Skin.Cyan, 0.05f), Skin.Alpha(Skin.Cyan, 0.5f));
                board.SetRing(selectedDart.Id, Skin.Cyan);
                foreach (var en in Battle.LiveUnits(Side.Enemy))
                    if (reach.Any(h => h.DistanceTo(en.Pos) == 1)) board.SetRing(en.Id, Skin.Bad);
            }
            else if (mode == Mode.Idle)
            {
                // Darts that can still act this turn carry a faint ring, so they are not forgotten.
                foreach (var dart in Battle.LiveUnits(Side.Player))
                    if (dart.Kind == UnitKind.Craft && !dart.CraftActed) board.SetRing(dart.Id, Skin.Alpha(Skin.Cyan, 0.55f));
            }
            RefreshHandState();
            RefreshPrompt();
        }

        void HighlightCard(CardInstance card)
        {
            var def = card.Def;
            var source = card.SourceId >= 0 ? Battle.Get(card.SourceId) : null;
            if (source != null) board.SetRing(source.Id, Skin.Alpha(Skin.Text, 0.6f));

            foreach (var effect in def.Effects)
            {
                if (source == null) break;
                if (effect is AttackEffect atk) Shade(source, atk.Arcs, atk.Range, true);
                else if (effect is SweepEffect sweep) Shade(source, sweep.Arcs, sweep.Range, true);
                else if (effect is RemoveIntentEffect ri) Shade(source, Arc.Fore | Arc.Aft | Arc.Port | Arc.Starboard, ri.Range, false);
                else if (effect is BoardingStrikeEffect bs) Shade(source, Arc.Fore | Arc.Aft | Arc.Port | Arc.Starboard, bs.Range, false);
                else if (effect is FlakEffect) Shade(source, Arc.Fore | Arc.Aft | Arc.Port | Arc.Starboard, 1, false);
                else if (effect is RamEffect ram)
                {
                    var h = source.Pos;
                    for (int i = 0; i < source.Speed + ram.ExtraRange; i++)
                    {
                        h = h.Neighbor(source.Facing);
                        board.Highlight(h, Skin.Alpha(Skin.Brass, 0.06f), Skin.Alpha(Skin.Brass, 0.6f));
                    }
                }
            }

            if (def.Target == TargetKind.Enemy)
            {
                foreach (var en in Battle.LiveUnits(Side.Enemy))
                    if (Battle.CheckPlay(card.Id, new PlayTarget { UnitId = en.Id, Expend = expend }) == null) board.SetRing(en.Id, Skin.Brass);
            }
            else if (def.Target == TargetKind.AllyCapital)
            {
                foreach (var al in Battle.LiveUnits(Side.Player).Where(u => u.IsCapital))
                {
                    bool ok = NeedsFacing(def) || Battle.CheckPlay(card.Id, PlayTarget.At(al.Id)) == null;
                    if (ok) board.SetRing(al.Id, Skin.Cyan);
                }
            }
        }

        void Shade(Unit source, Arc arcs, int range, bool needsLine)
        {
            foreach (var h in Battle.Board.All())
            {
                if (h == source.Pos || source.Pos.DistanceTo(h) > range) continue;
                if (!Arcs.InAny(source.Pos, source.Facing, h, arcs)) continue;
                bool clear = !needsLine || Battle.Board.HasLineOfFire(source.Pos, h);
                if (Battle.Board.Get(h) == Terrain.Asteroid) clear = false;
                board.Highlight(h, Skin.Alpha(Skin.Brass, clear ? 0.05f : 0.015f), Skin.Alpha(Skin.Brass, clear ? 0.55f : 0.15f));
            }
        }

        /// <summary>The line under the board that says exactly what the current selection would do.</summary>
        string PromptText()
        {
            if (flashText != null && Time.unscaledTime < flashUntil) return $"<color=#f0a040>{flashText}</color>";
            switch (mode)
            {
                case Mode.Busy: return "<color=#8a90a0>...</color>";
                case Mode.Over: return "";
                case Mode.Intro: return "";
                case Mode.Facing:
                    return $"<color=#c9a24a>{selectedCard.Def.Name.ToUpperInvariant()}</color>  Click an arrow to set {facingUnit.Name}'s new heading.  <color=#8a90a0>Right-click cancels.</color>";
                case Mode.Dart:
                {
                    var hov = Battle.Get(hoverUnitId);
                    if (hov != null && hov.Side == Side.Enemy)
                        return $"<color=#7fdcf2>DART</color>  Attack {hov.Name}: {selectedDart.AttackDamage} damage" + (selectedDart.CraftAttacks > 1 ? $" x{selectedDart.CraftAttacks}" : "") + ".";
                    return "<color=#7fdcf2>DART</color>  Click a lit hex to move (it attacks an adjacent enemy), or click an enemy to attack it.  <color=#8a90a0>Right-click cancels.</color>";
                }
                case Mode.Target:
                {
                    var def = selectedCard.Def;
                    string head = $"<color=#c9a24a>{def.Name.ToUpperInvariant()}</color>  ";
                    if (def.Target == TargetKind.None) return head + "Click the card again, or anywhere on the board, to play it.  <color=#8a90a0>Right-click cancels.</color>";
                    var hov = Battle.Get(hoverUnitId);
                    if (hov == null)
                        return head + (def.Target == TargetKind.Enemy ? "Choose an enemy with a brass ring." : "Choose one of your capital ships.") + "  <color=#8a90a0>Right-click cancels.</color>";
                    if (NeedsFacing(def)) return head + $"Turn {hov.Name}.";
                    var choice = new PlayTarget { UnitId = hov.Id, Expend = expend };
                    string error = Battle.CheckPlay(selectedCard.Id, choice);
                    if (error != null) return head + $"<color=#d9573f>{hov.Name}: {error}.</color>";
                    var atk = def.Effects.OfType<AttackEffect>().FirstOrDefault();
                    var source = Battle.Get(selectedCard.SourceId);
                    var ram = def.Effects.OfType<RamEffect>().FirstOrDefault();
                    if (ram != null && source != null)
                    {
                        int dmg = ram.DamageFrom(source);
                        return head + $"{hov.Name}: {dmg} damage straight to hull" + (dmg >= hov.Hull ? "  <color=#8cc47a>DESTROYS IT</color>" : $" (it has {hov.Hull})") + $".  <color=#d9573f>{source.Name} is lost.</color>";
                    }
                    if (atk != null && source != null)
                    {
                        var (dmg, flags) = atk.Resolve(Battle, source, hov, expend && def.ExpendCost > 0);
                        var (arc, sh, ar, hull) = Battle.PreviewDamage(hov, dmg, source.Pos, flags);
                        string arcName = arc >= 0 ? $" ({ArcIndex.ToArc(arc)})" : "";
                        string kill = hull >= hov.Hull ? "  <color=#8cc47a>DESTROYS IT</color>" : hull >= Battle.CritThreshold && hov.IsCapital ? "  <color=#f0a040>CRITICAL</color>" : "";
                        return head + $"{hov.Name}{arcName}: {dmg} damage - {sh} shields - {ar} armor = <color=#ffffff>{hull} hull</color>{kill}";
                    }
                    return head + $"Play on {hov.Name}.";
                }
                default:
                {
                    var hov = Battle.Get(hoverUnitId);
                    if (hov != null && hov.Side == Side.Enemy)
                    {
                        var intent = Battle.Intents.FirstOrDefault(i => i.UnitId == hov.Id && !i.Cancelled);
                        if (intent != null) return $"<color=#d9573f>{IntentText(intent, true)}</color>";
                    }
                    if (hov != null && hov.Side == Side.Player && hov.Kind == UnitKind.Craft)
                        return hov.CraftActed ? "This dart has acted this turn." : "Click the dart to move it.";
                    return "Select a card, click a dart to move it, or end the turn.";
                }
            }
        }

        string flashText;
        float flashUntil;

        void Flash(string text)
        {
            flashText = text;
            flashUntil = Time.unscaledTime + 2.2f;
            RefreshPrompt();
        }

        // ---------------------------------------------------------------- skirmish (no run)

        static readonly (CardDef card, int source)[] SkirmishRewards =
        {
            (PaperCards.OverchargeLances, 0), (PaperCards.PrecisionEdict, 0),
        };

        void StartSkirmish(int scenario, bool carry)
        {
            var b = scenario == 1
                ? Scenarios.InterceptAtTheRing(skirmishSeed)
                : Scenarios.TheLongPromise(skirmishSeed + 1000, SkirmishRewards);
            b.DartsInHangar = 6;
            if (carry && previousSkirmish != null) Scenarios.CarryOver(previousSkirmish, b);
            skirmishScenario = scenario;
            Begin(b);
        }

        int skirmishScenario;

        void LeaveBattle()
        {
            if (BattleLauncher.Finished != null)
            {
                var done = BattleLauncher.Finished;
                done(Battle);
                return;
            }
            previousSkirmish = Battle;
            ShowSkirmishMenu();
        }

        void QuitBattle()
        {
            if (mode == Mode.Busy) return;
            if (BattleLauncher.Quit != null) BattleLauncher.Quit();
            else ShowSkirmishMenu();
        }
    }

    /// <summary>Puts a <see cref="BattleScreen"/> into the Battle scene when it loads, so the scene itself stays empty.</summary>
    static class BattleSceneBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure(SceneManager.GetActiveScene());
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Ensure(scene);

        static void Ensure(Scene scene)
        {
            if (scene.name != "Battle") return;
            if (UnityEngine.Object.FindAnyObjectByType<BattleScreen>() != null) return;
            new GameObject("BattleScreen").AddComponent<BattleScreen>();
        }
    }
}
