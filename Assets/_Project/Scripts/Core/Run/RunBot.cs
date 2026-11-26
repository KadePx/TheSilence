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
        };

        /// <summary>Play a new run to its end and return the final state.</summary>
        public static RunState Play(long seed, RunContent c, int maxSteps = 200)
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
                    var next = RunRules.Choices(r).OrderByDescending(n => RouteScore(r, n)).ThenBy(n => n.Id).First();
                    RunRules.Travel(r, c, next.Id);
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
                case RunPhase.Shipyard:
                    if (!r.Escort.Alive) RunRules.RebuildEscort(r, c);
                    RunRules.Repair(r, c, RunRules.FlagshipIndex);
                    if (r.Escort.Alive) RunRules.Repair(r, c, RunRules.EscortIndex);
                    while (r.JunkCount() > 0 && RunRules.RemoveJunk(r, c) == null) { }
                    while (r.Darts < r.MaxDarts && r.Scrap >= c.Config.DartCost + 10 && RunRules.BuyDart(r, c) == null) { }
                    while (r.Ordinals <= r.MaxOrdinals - c.Config.OrdinalsPerPurchase && RunRules.BuyOrdinals(r, c) == null) { }
                    RunRules.LeaveShipyard(r, c);
                    break;
            }
        }

        static int RouteScore(RunState r, MapNode n)
        {
            var flag = r.Flagship;
            bool hurt = flag.Hull * 10 < flag.MaxHull * 6 || !r.Escort.Alive || r.JunkCount() >= 2;
            switch (n.Kind)
            {
                case NodeKind.Shipyard: return hurt ? 100 : 20;
                case NodeKind.Event: return 50;
                case NodeKind.Battle: return hurt ? 30 : 45;
                case NodeKind.Elite: return hurt ? 5 : 40;
                default: return 10;
            }
        }
    }
}
