// Race and nemesis simulation: 200 bot runs, with what the race and the hunting commander did in each.
// Evaluate in the editor (script_eval with file). Writes Temp/m3sim.txt.
using System.Linq;
using Silence.Core.Battle;
using Silence.Core.Run;

int N = 200;
string faction = System.IO.File.Exists("Temp/sim-faction.txt") ? System.IO.File.ReadAllText("Temp/sim-faction.txt").Trim() : "ascendancy"; // write "concord" there to sim the Concord
var c = Silence.Data.ContentLoader.Load(faction, true);
int win = 0, lost = 0, outp = 0, met = 0, killed = 0, escaped = 0, standoff = 0, atGate = 0, jumps = 0, minedRuns = 0, minedJumps = 0, relics = 0, concordAheadAtEnd = 0;
int nemFights = 0, nemWins = 0, nemLoss = 0, nemTurns = 0, rank3 = 0, lostToNem = 0, dodged = 0;
var byCommander = new System.Collections.Generic.Dictionary<string, int[]>();
long turns = 0;
var lossAt = new System.Collections.Generic.Dictionary<string, int>();
int bossWith = 0, bossWithWon = 0, bossWithout = 0, bossWithoutWon = 0; long hullAtBoss = 0; int bossN = 0;
for (int seed = 1; seed <= N; seed++)
{
    var r = RunRules.NewRun(seed, c);
    bool mined = false;
    for (int step = 0; step < 220 && r.Phase != RunPhase.Over; step++)
    {
        if (r.Phase == RunPhase.Map)
        {
            if (RunRules.LanesMined(r, c)) { mined = true; minedJumps++; }
            RunBot.Step(r, c);
            if (r.TravelNote.Contains("jump lane")) jumps++;
            continue;
        }
        if (r.Phase != RunPhase.Battle) { RunBot.Step(r, c); continue; }
        bool nem = r.PendingEncounter == RunRules.NemesisEncounterId;
        var b = RunRules.BuildBattle(r, c);
        b.Start();
        while (b.Outcome == BattleOutcome.Ongoing && b.Turn <= 80) SimpleBot.PlayTurn(b);
        if (nem)
        {
            nemFights++; nemTurns += b.Turn;
            if (!byCommander.TryGetValue(r.Nemesis.Id, out var a)) byCommander[r.Nemesis.Id] = a = new int[5];
            a[0]++;
            var ship = b.Units.Find(u => u.IsCommander);
            if (b.Outcome == BattleOutcome.Victory) { nemWins++; a[1]++; }
            if (b.Outcome == BattleOutcome.Defeat) { nemLoss++; lostToNem++; a[2]++; }
            if (ship.Escaped) { escaped++; a[3]++; } else if (!ship.Alive) { killed++; a[4]++; } else standoff++;
        }
        var encDef = RunRules.PendingEncounterDef(r, c);
        if (encDef.Tier == 3)
        {
            bool with = r.Nemesis.FellBack && r.Nemesis.Alive;
            if (with) { bossWith++; if (b.Outcome == BattleOutcome.Victory) bossWithWon++; } else { bossWithout++; if (b.Outcome == BattleOutcome.Victory) bossWithoutWon++; }
        }
        if (b.Outcome == BattleOutcome.Defeat) { string k = r.PendingEncounter  + (nem ? r.Nemesis.Rank.ToString() : ""); lossAt.TryGetValue(k, out int v); lossAt[k] = v + 1; }
        RunRules.FinishBattle(r, c, b);
    }
    turns += r.TurnsFought;
    if (r.Result == RunResult.Victory) win++; else if (r.Result == RunResult.Defeat) lost++; else outp++;
    if (r.Nemesis.Meetings > 0) met++;
    if (r.Nemesis.FellBack && r.Nemesis.Alive) atGate++;
    if (r.Nemesis.Rank >= 3) rank3++;
    if (mined) minedRuns++;
    relics += r.Relics.Count;
    if (RunRules.ConcordAhead(r)) concordAheadAtEnd++;
}
var sb = new System.Text.StringBuilder();
sb.AppendLine($"faction {faction}");
sb.AppendLine($"runs {N}: won {win} lost {lost} outpaced {outp}; avg turns {turns / (float)N:0.0}");
sb.AppendLine($"nemesis: met in {met} runs; fights {nemFights} (avg {nemTurns / (float)System.Math.Max(1, nemFights):0.0} turns): won {nemWins}, fleet lost {nemLoss}; commander killed {killed}, escaped {escaped}, standoff {standoff}; fell back alive {atGate}; reached rank III {rank3}");
foreach (var kv in byCommander) sb.AppendLine($"  {kv.Key}: fights {kv.Value[0]} won {kv.Value[1]} lost {kv.Value[2]} escaped {kv.Value[3]} killed {kv.Value[4]}");
sb.AppendLine($"race: jump lanes taken {jumps}; runs that saw mined lanes {minedRuns} (mined jumps {minedJumps}); concord ahead at end {concordAheadAtEnd}; relics per run {relics / (float)N:0.00}");
sb.AppendLine("losses at: " + string.Join(", ", lossAt.OrderByDescending(k => k.Value).Select(k => k.Key + " " + k.Value)));
sb.AppendLine($"gate fights won: {bossWithWon}/{bossWith} with the commander fallen back to the main fleet; {bossWithoutWon}/{bossWithout} otherwise");
System.IO.File.WriteAllText("Temp/m3sim.txt", sb.ToString());
return sb.ToString();
