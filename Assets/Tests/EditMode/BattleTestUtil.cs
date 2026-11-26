using System.Linq;
using Silence.Core.Battle;
using Silence.Core.Hex;

namespace Silence.Tests
{
    /// <summary>Small battles for rule tests: empty deck, cards handed out directly.</summary>
    static class T
    {
        /// <summary>Plain target: hull 20, no shields, no armor, no AI, Speed 0.</summary>
        public static UnitSpec Dummy(int hull = 20, int shields = 0, int armor = 0)
            => UnitSpec.Capital("Dummy", hull, armor, armor, armor, armor, shields, 0, 0, 0, 2);

        /// <summary>Flagship at (2,3) facing E, Speed 0; escort at (2,5) facing E, Speed 0. Not started.</summary>
        public static Battle Fleet(out Unit flag, out Unit escort, ulong seed = 1)
        {
            var b = new Battle(seed);
            flag = b.AddUnit(PaperUnits.LanceCruiser, Side.Player, 2, 3, HexDirection.E, 0, flagship: true);
            escort = b.AddUnit(PaperUnits.Blade, Side.Player, 2, 5, HexDirection.E, 0);
            return b;
        }

        /// <summary>Put a card straight into the hand (battle must be started).</summary>
        public static CardInstance Give(Battle b, CardDef def, Unit source)
        {
            var c = b.AddCard(def, source?.Id ?? -1);
            b.Deck.DrawPile.Remove(c);
            b.Deck.DrawPile.Insert(0, c);
            b.DrawCards(1);
            return c;
        }

        public static HexCoord At(int col, int row) => HexCoord.FromOffset(col, row);

        public static bool Logged(Battle b, EventKind kind) => b.Log.Any(e => e.Kind == kind);

        public static string LogText(Battle b) => string.Join("\n", b.Log.Select(e => e.ToString()));
    }
}
