using System.Linq;
using Silence.Core.Battle;

namespace Silence.Core.Run
{
    /// <summary>
    /// A plain stand-in player for whole runs: it picks a route, fights with <see cref="SimpleBot"/>, takes
    /// rewards, answers events and repairs at shipyards. Used by tests and the balance sims, not by the game.
    /// </summary>
    public static class RunBot
    {
        static readonly string[] CardPreference =
        {
            "overcharge_lances", "precision_edict", "seal_the_decks", "cold_discipline", "hold_the_line", "short_lance",
            "waymind_targeting", "escort_guns", "broadside_battery", "hard_about", "scry_the_void", "flak_lattice", "brace",
            // Concord
            "spotter_data", "outrider_torpedo", "patch_job", "council_vote", "ring_shield", "outrider_guns", "deck_guns", "long_scan", "combat_patrol",
        };

        /// <summary>Play a new run to its end and return the final state.</summary>
        public static RunState Play(long seed, RunContent c, int maxSteps = 500)
        {
            var r = RunRules.NewRun(seed, c);
            for (int step = 0; step < maxSteps && r.Phase != RunPhase.Over; step++) Step(r, c);
            return r;
        }

        /// <summary>Do the next thing the current phase asks for.</summary>
        public static void Step(RunState r, RunContent c)
        {
            switch (r.Phase)
            {
                case RunPhase.Map:
                {
                    if (r.EdictChoices.Count > 0)
                    {
                        string[] easiest = { "expend", "keep_escort", "no_standoff", "win_elite", "arrive_first", "austerity", "kill_commander" };
                        int pick = 0, best = int.MaxValue;
                        for (int i = 0; i < r.EdictChoices.Count; i++)
                        {
                            int rank = System.Array.IndexOf(easiest, c.Edict(r.EdictChoices[i]).Kind);
                            if (rank >= 0 && rank < best) { best = rank; pick = i; }
                        }
                        RunRules.AcceptEdict(r, c, pick);
                    }
                    // The Council first: mend the fleet when it is hurt, see to the wounded, otherwise bank the approval.
                    for (int i = 0; i < c.Motions.Count; i++)
                    {
                        var m = c.Motions[i];
                        bool want = (m.Hull > 0 && r.Flagship.Hull * 10 < r.Flagship.MaxHull * 7) || (m.HealOfficers && r.Officers.Count(o => o.Wounded) >= 2)
                                    || (m.Officer && r.Approval >= m.Cost + 1) || (m.Craft > 0 && r.Darts == 0);
                        if (want && RunRules.CanCallVote(r, c, m) == null) RunRules.CallVote(r, c, i);
                    }
                    var next = RunRules.Choices(r).OrderByDescending(n => RouteScore(r, c, n)).ThenBy(n => n.Id).First();
                    RunRules.Travel(r, c, next.Id);
                    break;
                }
                case RunPhase.Hail:
                {
                    // The plain player: says nothing, except at rank III where a grudge no longer matters and the extra command does.
                    // A healthy fleet meeting them again goads them into staying, to end it.
                    bool goad = r.Nemesis.Meetings > 0 && r.Nemesis.Rank < 3 && r.Flagship.Hull * 10 >= r.Flagship.MaxHull * 8 && r.Escort.Alive;
                    int reply = c.HailReplies.FindIndex(x => x.Effect == (r.Nemesis.Rank >= 3 ? "fire" : goad ? "taunt" : "silence"));
                    if (reply < 0 || RunRules.ChooseHail(r, c, reply) != null) RunRules.ChooseHail(r, c, c.HailReplies.Count - 1);
                    break;
                }
                case RunPhase.Battle:
                {
                    var b = RunRules.BuildBattle(r, c);
                    b.Start();
                    while (b.Outcome == BattleOutcome.Ongoing && b.Turn <= 80) SimpleBot.PlayTurn(b);
                    RunRules.FinishBattle(r, c, b);
                    break;
                }
                case RunPhase.Reward:
                {
                    if (r.Reward.RelicChoices.Count > 0) RunRules.ChooseRelic(r, c, 0);
                    if (r.Flagship.Hull * 10 < r.Flagship.MaxHull * 6)
                    {
                        RunRules.TakeFieldRepair(r, c);
                        break;
                    }
                    int pick = -1, best = int.MaxValue;
                    for (int i = 0; i < r.Reward.Cards.Count; i++)
                    {
                        int rank = System.Array.IndexOf(CardPreference, r.Reward.Cards[i].Card);
                        if (rank >= 0 && rank < best) { best = rank; pick = i; }
                    }
                    RunRules.TakeReward(r, pick);
                    break;
                }
                case RunPhase.Event:
                {
                    var ev = c.Event(r.EventId);
                    int choice = ev.Choices.Count - 1;
                    for (int i = 0; i < ev.Choices.Count; i++)
                    {
                        var ch = ev.Choices[i];
                        if (RunRules.CanChoose(r, ch) != null) continue;
                        bool hurts = ch.Outcomes.Any(o => (o.Kind == OutcomeKind.HullFlagship || o.Kind == OutcomeKind.HullAll) && o.Amount < 0);
                        if (hurts && r.Flagship.Hull < r.Flagship.MaxHull * 2 / 3) continue;
                        if (ch.Outcomes.Any(o => o.Kind == OutcomeKind.Ordinals && o.Amount < 0) && r.Ordinals < 12) continue;
                        choice = i;
                        break;
                    }
                    if (RunRules.ChooseEvent(r, c, choice) != null)
                        for (int i = 0; i < ev.Choices.Count && r.Phase == RunPhase.Event; i++) RunRules.ChooseEvent(r, c, i);
                    break;
                }
                case RunPhase.EventResult:
                    RunRules.CloseEvent(r);
                    break;
                case RunPhase.SectorClear:
                    RunRules.EnterNextSector(r, c, r.SectorRelics.Count > 0 ? 0 : -1);
                    break;
                case RunPhase.Shipyard:
                    if (!r.Escort.Alive) RunRules.RebuildEscort(r, c);
                    RunRules.Repair(r, c, RunRules.FlagshipIndex);
                    if (r.Escort.Alive) RunRules.Repair(r, c, RunRules.EscortIndex);
                    while (r.JunkCount() > 0 && RunRules.RemoveJunk(r, c) == null) { }
                    while (r.Darts < r.MaxDarts && r.Scrap >= c.Config.DartCost + 10 && RunRules.BuyDart(r, c) == null) { }
                    while (r.Officers.Any(o => o.Wounded) && r.Scrap >= c.Config.HealOfficerCost + 20 && RunRules.HealOfficer(r, c) == null) { }
                    if (RunRules.ConcordAhead(r) && r.Scrap >= c.Config.SabotageCost + 20) RunRules.Sabotage(r, c);
                    while (r.Ordinals <= r.MaxOrdinals - c.Config.OrdinalsPerPurchase && RunRules.BuyOrdinals(r, c) == null) { }
                    RunRules.LeaveShipyard(r, c);
                    break;
            }
        }

        static int RouteScore(RunState r, RunContent c, MapNode n)
        {
            var flag = r.Flagship;
            bool hurt = flag.Hull * 10 < flag.MaxHull * 6 || !r.Escort.Alive || r.JunkCount() >= 2;
            // A jump lane is worth its hull only when the race is being lost and the fleet can afford it.
            if (RunRules.IsJump(r, n.Id)) return !hurt && RunRules.ConcordAhead(r) ? 70 : 0;
            switch (n.Kind)
            {
                case NodeKind.Nemesis: return hurt ? 5 : 42;
                case NodeKind.Shipyard: return hurt ? 100 : 20;
                case NodeKind.Event: return 50;
                case NodeKind.Battle: return hurt ? 30 : 45;
                case NodeKind.Elite: return hurt ? 5 : 40;
                default: return 10;
            }
        }
    }
}
