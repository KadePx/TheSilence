using Silence.Core.Hex;

namespace Silence.Core.Battle
{
    public enum EventKind
    {
        BattleStarted, TurnStarted, PhaseChanged, IntentDeclared, IntentCancelled,
        CardDrawn, CardPlayed, CardAdded, CardsRemoved, Shuffled, Command,
        Moved, Turned, SpeedChanged, Collision, Spawned,
        Attack, Miss, Damage, Critical, Destroyed,
        ShieldsRaised, ShieldsRegen, Heat, ReactorStrain, Repaired, Buff, Ordinals,
        Victory, Defeat, Withdrawal,
        Escaped,
    }

    /// <summary>
    /// Something that happened in the battle. The view animates these; tests compare their text to check
    /// that a replay from the same seed and actions is identical.
    /// </summary>
    public sealed class BattleEvent
    {
        public int Turn;
        public BattlePhase Phase;
        public EventKind Kind;
        public int UnitId;
        public int OtherId;
        public int Amount;
        public HexCoord From;
        public HexCoord To;
        public string Text;

        public override string ToString() => $"T{Turn} {Phase} {Kind}: {Text}";
    }
}
