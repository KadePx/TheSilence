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
            RefreshTutorial();

            while (logCursor < Battle.Log.Count)
            {
                var e = Battle.Log[logCursor++];
                AddLog(e);
                if (e.Kind == EventKind.PhaseChanged) ShowPhase(e.Text);
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

        /// <summary>Guns or a lance firing from one point to another. A miss uses it too, with somewhere else to land.</summary>
        IEnumerator Volley(UnitView v, Vector3 from, Vector3 to, string text, int amount)
        {
            bool player = v.Side == Side.Player;
            if (text.Contains("Lance") || text.Contains("lance") || text.Contains("Mass Driver") || text.Contains("Survey Driver") || text.Contains("Cutting Beam") || text.Contains("rakes"))
            {
                Sfx.Play("sfx_lance");
                yield return board.Beam(from, to, player ? Skin.Cyan : Skin.Hex("ffc46a"), amount >= 9 ? 0.34f : 0.2f, 0.3f * Pace);
            }
            else if (text.Contains("storm"))
            {
                Sfx.Play("sfx_launch");
                yield return board.Shots(from, to, Skin.Text, 4, 0.3f * Pace);
            }
            else
            {
                Sfx.Play(v.Capital ? "sfx_battery" : "sfx_shot", v.Capital ? 1f : 0.6f);
                yield return board.Shots(from, to, player ? Skin.Hex("c8f6ff") : Skin.Hex("ffc46a"), v.Capital ? 4 : 2, (v.Capital ? 0.26f : 0.18f) * Pace);
            }
        }

        int shakes;

        /// <summary>Jolt the board by a few whole pixels. The interface is on its own canvas and stays put.</summary>
        IEnumerator Shake(int pixels, float seconds)
        {
            if (cam == null) yield break;
            shakes++;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                float fade = 1f - t / seconds;
                int n = Mathf.FloorToInt(t * 40f);
                float x = Mathf.Round(pixels * fade * ((n % 2 == 0) ? 1f : -1f));
                float y = Mathf.Round(pixels * fade * ((n % 3 == 0) ? -1f : (n % 3 == 1) ? 1f : 0f));
                cam.transform.position = new Vector3(x / 32f, y / 32f, -10f);
                yield return null;
            }
            if (--shakes == 0) cam.transform.position = new Vector3(0f, 0f, -10f);
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
                    var to = other.Root.position;
                    if (v.Kind == UnitKind.Torpedo)
                    {
                        Sfx.Play("sfx_torpedo");
                        yield return board.MoveTo(v, other.Pos, 0.12f * Pace);
                        board.Explosion(to, false);
                    }
                    else yield return Volley(v, v.Root.position, to, e.Text, e.Amount);
                    break;
                }

                case EventKind.Damage:
                {
                    if (v == null) break;
                    int hullBefore = v.Hull;
                    v.Hull = Mathf.Max(0, v.Hull - e.Amount);
                    board.UpdateBars(v, Battle.Get(v.Id));
                    // The blow lands on the side of the hull that faces whoever fired, not on its middle.
                    Vector2 away = other != null && other != v ? (Vector2)(v.Root.position - other.Root.position) : Vector2.zero;
                    var at = v.Root.position - (Vector3)(away.normalized * (v.Capital ? 0.7f : 0.2f));
                    if (e.Absorbed > 0)
                    {
                        Sfx.Play("sfx_shield", 0.8f);
                        board.ShieldFlare(at, v.Capital && e.Amount == 0);
                    }
                    if (e.Amount > 0)
                    {
                        Floater(v.Root.position + Vector3.up * (v.Capital ? 0.9f : 0.3f), "-" + e.Amount, Skin.Bad, v.Capital ? 34 : 24);
                        if (e.Absorbed > 0) Floater(v.Root.position + Vector3.down * 1.9f, $"SHIELDS TOOK {e.Absorbed}", Skin.Cyan, 16);
                        Sfx.Play("sfx_hit", 0.8f);
                        StartCoroutine(board.Flash(v, Skin.Bad, 0.25f));
                        board.HitSpark(at);
                        if (v.Capital)
                        {
                            StartCoroutine(board.BarLoss(v, hullBefore));
                            StartCoroutine(board.Recoil(v, away, Mathf.Clamp(e.Amount / 2f, 2f, 6f), 0.3f));
                            if (e.Amount >= 5) StartCoroutine(Shake(e.Amount >= 9 ? 3 : 2, 0.2f));
                        }
                    }
                    else if (e.Absorbed > 0) Floater(v.Root.position, "SHIELDS HOLD", Skin.Cyan, 20);
                    else
                    {
                        // Nothing got through and no shield was up: the plate turned it.
                        Floater(v.Root.position, "ARMOR HOLDS", Skin.Dim, 20);
                        Sfx.Play("sfx_hit", 0.35f);
                        StartCoroutine(board.Burst(at, Skin.Text, 0.5f, 0.18f));
                    }
                    yield return Wait(0.2f);
                    break;
                }

                case EventKind.Destroyed:
                    if (v == null) break;
                    Sfx.Play(v.Capital ? "sfx_explosion" : "sfx_pop", v.Capital ? 1f : 0.7f);
                    board.Explosion(v.Root.position, v.Capital);
                    if (v.Capital) StartCoroutine(Shake(4, 0.35f));
                    board.Remove(v.Id);
                    yield return Wait(v.Capital ? 0.45f : 0.12f);
                    break;

                case EventKind.Escaped:
                    if (v == null) break;
                    Floater(v.Root.position + Vector3.up * 0.8f, "BREAKS OFF", Skin.Warn, 28);
                    Sfx.Play("sfx_jump");
                    yield return Wait(0.5f);
                    board.Remove(v.Id);
                    yield return Wait(0.2f);
                    break;

                case EventKind.Critical:
                    if (v != null) Floater(v.Root.position + Vector3.up * 0.8f, "CRITICAL", Skin.Warn, 28);
                    yield return Wait(0.15f);
                    break;

                case EventKind.Miss:
                {
                    if (v == null) break;
                    if (e.Text.Contains("point defence"))
                    {
                        // A craft's run that the target's guns broke up.
                        if (other != null)
                        {
                            board.ShieldFlare(Vector3.Lerp(other.Root.position, v.Root.position, 0.4f), false);
                            Floater(other.Root.position, "DRIVEN OFF", Skin.Good, 20);
                        }
                        Sfx.Play("sfx_shield", 0.6f);
                        yield return Wait(0.2f);
                        break;
                    }
                    if (!e.Text.Contains("misses"))
                    {
                        Floater(v.Root.position, "NO TARGET", Skin.Dim, 16);
                        yield return Wait(0.05f);
                        break;
                    }
                    // The guns still fire: the shot goes where the target was meant to be, and past it.
                    string why = e.Text.Contains("arc") ? "OUT OF ARC" : e.Text.Contains("range") ? "OUT OF RANGE" : e.Text.Contains("blocked") ? "BLOCKED" : "NO TARGET";
                    if (other != null)
                    {
                        var from = v.Root.position;
                        var aim = other.Root.position;
                        var line = (Vector2)(aim - from);
                        var along = line.normalized;
                        var wide = new Vector2(-along.y, along.x) * ((e.UnitId + Battle.Turn) % 2 == 0 ? 1f : -1f);
                        Vector3 end;
                        if (why == "BLOCKED") end = from + (Vector3)(line * 0.5f);
                        else if (why == "OUT OF RANGE") end = from + (Vector3)(along * Mathf.Min(line.magnitude - 1.6f, line.magnitude * 0.7f));
                        else end = aim + (Vector3)(wide * 1.5f + along * 2.5f);
                        yield return Volley(v, from, end, e.Text, 0);
                        if (why == "BLOCKED") StartCoroutine(board.Burst(end, Skin.Warn, 0.6f, 0.2f));
                        Floater(other.Root.position, "MISS", Skin.Good, 24);
                    }
                    Floater(v.Root.position, why, Skin.Dim, 16);
                    yield return Wait(0.25f);
                    break;
                }

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
