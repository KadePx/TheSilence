using System.Collections;
using Silence.Core.Battle;
using Silence.Core.Hex;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Silence.View
{
    /// <summary>Animates the events Core logged since the last player action, in order, then resyncs the board.</summary>
    public sealed partial class BattleScreen
    {
        /// <summary>Hold Space to fast-forward.</summary>
        float Pace => Keyboard.current != null && Keyboard.current.spaceKey.isPressed ? 0.25f : 1f;

        IEnumerator Wait(float seconds)
        {
            for (float t = 0; t < seconds * Pace; t += Time.deltaTime) yield return null;
        }

        IEnumerator PlayEvents()
        {
            // Mid-turn actions keep the hand on screen; once the turn has ended the old hand is gone and the
            // new one, with the new intents, appears when the enemy has finished acting.
            bool turnEnded = false;
            for (int i = logCursor; i < Battle.Log.Count; i++)
                if (Battle.Log[i].Kind == EventKind.PhaseChanged && Battle.Log[i].Text == "Drift") turnEnded = true;
            if (turnEnded)
            {
                foreach (var hv in handViews)
                    if (hv != null) Destroy(hv.gameObject);
                handViews.Clear();
                board.ClearOverlay();
            }
            else
            {
                RebuildHand();
                board.RefreshOverlay();
            }
            Ui.Clear(chipRoot);
            endTurnButton.interactable = false;

            while (logCursor < Battle.Log.Count)
            {
                var e = Battle.Log[logCursor++];
                AddLog(e);
                yield return Animate(e);
            }

            board.SyncUnits();
            mode = Battle.Outcome == BattleOutcome.Ongoing ? Mode.Idle : Mode.Over;
            Refresh();
            if (mode == Mode.Over)
            {
                yield return Wait(0.5f);
                Sfx.Play(Battle.Outcome == BattleOutcome.Victory ? "sfx_victory" : "sfx_defeat");
                ShowEndScreen();
            }
        }

        IEnumerator Animate(BattleEvent e)
        {
            board.Views.TryGetValue(e.UnitId, out var v);
            board.Views.TryGetValue(e.OtherId, out var other);

            switch (e.Kind)
            {
                case EventKind.PhaseChanged:
                    phaseText.text = e.Text.ToUpperInvariant() + (e.Text == "EnemyAction" ? "" : " STEP");
                    if (e.Text == "EnemyAction") phaseText.text = "ENEMY ACTION";
                    if (e.Text == "Drift" || e.Text == "EnemyAction") yield return Wait(0.18f);
                    break;

                case EventKind.TurnStarted:
                    turnText.text = $"TURN {e.Amount}";
                    RefreshLog();
                    break;

                case EventKind.CardPlayed:
                    Sfx.Play("sfx_card");
                    RefreshLog();
                    yield return Wait(0.05f);
                    break;

                case EventKind.Moved:
                    if (v == null || e.From == e.To)
                    {
                        if (v != null && e.From == e.To && e.Text.Contains("edge")) Floater(v.Root.position, "HOLDS AT EDGE", Skin.Dim, 18);
                        break;
                    }
                    yield return board.MoveTo(v, e.To, (v.Capital ? 0.17f : 0.2f) * Pace);
                    break;

                case EventKind.Turned:
                    if (v == null) break;
                    board.SetFacing(v, v.Facing.Rotate(e.Amount));
                    Sfx.Play("sfx_turn", 0.6f);
                    yield return Wait(0.14f);
                    break;

                case EventKind.SpeedChanged:
                    if (v != null) Floater(v.Root.position, $"SPEED {e.Amount}", Skin.Cyan, 20);
                    yield return Wait(0.08f);
                    break;

                case EventKind.Spawned:
                {
                    var u = Battle.Get(e.UnitId);
                    if (u == null || board.Views.ContainsKey(u.Id)) break;
                    var nv = board.Add(u);
                    nv.Pos = e.To;
                    nv.Root.position = board.World(e.To);
                    nv.Hull = nv.MaxHull;
                    Sfx.Play("sfx_launch", 0.7f);
                    StartCoroutine(board.Burst(nv.Root.position, u.Side == Side.Player ? Skin.Cyan : Skin.Warn, 0.6f, 0.25f));
                    yield return Wait(0.12f);
                    break;
                }

                case EventKind.Collision:
                    if (v == null) break;
                    Sfx.Play("sfx_hit");
                    yield return board.Bump(v, board.World(e.To), 0.22f * Pace);
                    StartCoroutine(board.Burst(Vector3.Lerp(v.Root.position, board.World(e.To), 0.5f), Skin.Warn, 0.9f, 0.3f));
                    break;

                case EventKind.Attack:
                {
                    if (v == null || other == null) break;
                    var from = v.Root.position;
                    var to = other.Root.position;
                    bool player = v.Side == Side.Player;
                    if (v.Kind == UnitKind.Torpedo)
                    {
                        Sfx.Play("sfx_torpedo");
                        yield return board.MoveTo(v, other.Pos, 0.12f * Pace);
                        board.Explosion(to, false);
                    }
                    else if (e.Text.Contains("Lance") || e.Text.Contains("Mass Driver") || e.Text.Contains("rakes"))
                    {
                        Sfx.Play("sfx_lance");
                        yield return board.Beam(from, to, player ? Skin.Cyan : Skin.Hex("ffc46a"), e.Amount >= 9 ? 0.34f : 0.2f, 0.3f * Pace);
                    }
                    else if (e.Text.Contains("storm"))
                    {
                        Sfx.Play("sfx_launch");
                        yield return board.Shots(from, to, Skin.Text, 4, 0.3f * Pace);
                    }
                    else
                    {
                        Sfx.Play(v.Capital ? "sfx_battery" : "sfx_shot", v.Capital ? 1f : 0.6f);
                        yield return board.Shots(from, to, player ? Skin.Hex("c8f6ff") : Skin.Hex("ffc46a"), v.Capital ? 4 : 2, (v.Capital ? 0.26f : 0.18f) * Pace);
                    }
                    break;
                }

                case EventKind.Damage:
                    if (v == null) break;
                    v.Hull = Mathf.Max(0, v.Hull - e.Amount);
                    board.UpdateBars(v, Battle.Get(v.Id));
                    if (e.Amount > 0)
                    {
                        Floater(v.Root.position, "-" + e.Amount, Skin.Bad, v.Capital ? 34 : 24);
                        Sfx.Play("sfx_hit", 0.8f);
                        StartCoroutine(board.Flash(v, Skin.Bad, 0.25f));
                        board.HitSpark(v.Root.position);
                    }
                    else
                    {
                        Floater(v.Root.position, "ABSORBED", Skin.Cyan, 20);
                        Sfx.Play("sfx_shield", 0.8f);
                        board.ShieldFlare(v.Root.position, v.Capital);
                    }
                    yield return Wait(0.2f);
                    break;

                case EventKind.Destroyed:
                    if (v == null) break;
                    Sfx.Play(v.Capital ? "sfx_explosion" : "sfx_pop", v.Capital ? 1f : 0.7f);
                    board.Explosion(v.Root.position, v.Capital);
                    board.Remove(v.Id);
                    yield return Wait(v.Capital ? 0.45f : 0.12f);
                    break;

                case EventKind.Critical:
                    if (v != null) Floater(v.Root.position + Vector3.up * 0.8f, "CRITICAL", Skin.Warn, 28);
                    yield return Wait(0.15f);
                    break;

                case EventKind.Miss:
                    if (v != null) Floater(v.Root.position, e.Text.Contains("misses") ? "MISS" : "NO TARGET", Skin.Dim, e.Text.Contains("misses") ? 24 : 16);
                    yield return Wait(e.Text.Contains("misses") ? 0.25f : 0.05f);
                    break;

                case EventKind.ShieldsRaised:
                    if (v == null) break;
                    Floater(v.Root.position, $"+{e.Amount} SHIELDS", Skin.Cyan, 22);
                    Sfx.Play("sfx_shield");
                    board.UpdateBars(v, Battle.Get(v.Id));
                    board.ShieldFlare(v.Root.position, true);
                    yield return Wait(0.12f);
                    break;

                case EventKind.Repaired:
                    if (v == null) break;
                    v.Hull = Mathf.Min(v.MaxHull, v.Hull + e.Amount);
                    board.UpdateBars(v, Battle.Get(v.Id));
                    Floater(v.Root.position, $"+{e.Amount} HULL", Skin.Good, 24);
                    yield return Wait(0.2f);
                    break;

                case EventKind.ReactorStrain:
                    if (v != null) Floater(v.Root.position + Vector3.up * 0.8f, "REACTOR STRAIN", Skin.Bad, 28);
                    yield return Wait(0.2f);
                    break;

                case EventKind.CardAdded:
                    Floater(new Vector3(BoardCentre.x, BoardCentre.y - 8.5f, 0f), e.Text.Contains("Fire") ? "FIRE ON DECK ADDED" : e.Text.Contains("Breach") ? "HULL BREACH ADDED" : "JUNK ADDED", Skin.Warn, 24);
                    break;

                case EventKind.IntentCancelled:
                    if (v != null) Floater(v.Root.position, "INTENT LOST", Skin.Good, 22);
                    yield return Wait(0.15f);
                    break;

                case EventKind.Buff:
                    if (v != null && e.Text.Contains("marked")) Floater(v.Root.position, "MARKED", Skin.Brass, 22);
                    else if (v != null && e.Text.Contains("screens")) Floater(v.Root.position, "SCREENING", Skin.Cyan, 20);
                    break;

                case EventKind.Ordinals:
                    ordinalsText.text = Battle.Ordinals.ToString();
                    break;
            }
        }
    }
}
