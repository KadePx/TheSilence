// Balance and mechanic-usage simulation: N bot runs, with every battle's event log mined for what actually happens.
using System.Linq;
using Silence.Core.Battle;
using Silence.Core.Run;

int N = 150;
var c = Silence.Data.ContentLoader.Load();
var sb = new System.Text.StringBuilder();
var draws = new System.Collections.Generic.Dictionary<string, int>();
var plays = new System.Collections.Generic.Dictionary<string, int>();
var enc = new System.Collections.Generic.Dictionary<string, int[]>(); // fights, wins, losses, withdrawals, turns, flagHullLost, escortLost
var layerLoss = new int[12];
var layerFights = new int[12];
int results_win = 0, results_lost = 0, results_out = 0;
long turns = 0, battles = 0, enemyAttacks = 0, enemyMisses = 0, heatOver = 0, strain = 0, junkAdded = 0, crits = 0, expends = 0,
     torpHits = 0, torpLaunched = 0, dartsLaunched = 0, playerCardPlays = 0, collisions = 0, shieldAbsorbedAll = 0, dmgEvents = 0, turnsWithFullDodge = 0;
var firstBattleHullLoss = new System.Collections.Generic.List<int>();
var scrapEnd = new System.Collections.Generic.List<int>();
var deckEnd = new System.Collections.Generic.List<int>();
var nodeKinds = new System.Collections.Generic.Dictionary<string, int>();

void Inc(System.Collections.Generic.Dictionary<string, int> d, string k) { d.TryGetValue(k, out int v); d[k] = v + 1; }

for (int seed = 1; seed <= N; seed++)
{
    var r = RunRules.NewRun(seed, c);
    int fightNo = 0;
    for (int step = 0; step < 220 && r.Phase != RunPhase.Over; step++)
    {
        if (r.Phase != RunPhase.Battle) { if (r.Phase == RunPhase.Map) { } RunBot.Step(r, c); continue; }
        string id = r.PendingEncounter;
        int layer = r.Node(r.CurrentNode) != null ? r.Node(r.CurrentNode).Layer : 0;
        int hullBefore = r.Flagship.Hull;
        bool escortBefore = r.Escort.Alive;
        var b = RunRules.BuildBattle(r, c);
        b.Start();
        while (b.Outcome == BattleOutcome.Ongoing && b.Turn <= 80) SimpleBot.PlayTurn(b);
        foreach (var e in b.Log)
        {
            switch (e.Kind)
            {
                case EventKind.CardDrawn: Inc(draws, e.Text.Substring(5)); break;
                case EventKind.CardPlayed:
                {
                    string name = e.Text.Substring(5);
                    int on = name.IndexOf(" on "); if (on > 0) name = name.Substring(0, on);
                    int ex = name.IndexOf(" (Expend)"); if (ex > 0) { name = name.Substring(0, ex); expends++; }
                    if (e.Text.Contains("(Expend)") && ex <= 0) expends++;
                    Inc(plays, name); playerCardPlays++; break;
                }
                case EventKind.Attack: { var u = b.Get(e.UnitId); if (u != null && u.Side == Side.Enemy) enemyAttacks++; break; }
                case EventKind.Miss: { var u = b.Get(e.UnitId); if (u != null && u.Side == Side.Enemy) enemyMisses++; break; }
                case EventKind.ReactorStrain: strain++; break;
                case EventKind.CardAdded: junkAdded++; if (e.Text.Contains("heat cap")) heatOver++; break;
                case EventKind.Critical: crits++; break;
                case EventKind.Collision: collisions++; break;
                case EventKind.Spawned: if (e.Text.StartsWith("Torpedo")) torpLaunched++; else if (e.Text.Contains("dart")) dartsLaunched++; break;
                case EventKind.Damage: { var u = b.Get(e.UnitId); if (u != null && u.Side == Side.Player && u.IsCapital) { dmgEvents++; if (e.Amount == 0) shieldAbsorbedAll++; } break; }
            }
            if (e.Kind == EventKind.Attack && e.Text.StartsWith("Torpedo")) torpHits++;
        }
        RunRules.FinishBattle(r, c, b);
        if (!enc.TryGetValue(id, out var s)) enc[id] = s = new int[7];
        s[0]++;
        if (b.Outcome == BattleOutcome.Victory) s[1]++; else if (b.Outcome == BattleOutcome.Defeat) s[2]++; else s[3]++;
        s[4] += b.Turn;
        s[5] += System.Math.Max(0, hullBefore - r.Flagship.Hull);
        if (escortBefore && !r.Escort.Alive) s[6]++;
        turns += b.Turn; battles++;
        layerFights[System.Math.Min(11, layer)]++;
        if (b.Outcome == BattleOutcome.Defeat) layerLoss[System.Math.Min(11, layer)]++;
        if (fightNo == 0) firstBattleHullLoss.Add(hullBefore - r.Flagship.Hull);
        fightNo++;
    }
    if (r.Result == RunResult.Victory) results_win++; else if (r.Result == RunResult.Outpaced) results_out++; else results_lost++;
    scrapEnd.Add(r.Scrap);
    deckEnd.Add(r.Deck.Count);
    foreach (var n in r.Nodes.Where(n => n.Visited)) Inc(nodeKinds, n.Kind.ToString());
}

sb.AppendLine($"RUNS {N}: won {results_win} lost {results_lost} outpaced {results_out}");
sb.AppendLine($"battles {battles}, avg turns {turns / (double)battles:0.0}, card plays per turn {playerCardPlays / (double)turns:0.00}");
sb.AppendLine($"enemy attacks landed {enemyAttacks}, missed {enemyMisses} ({100.0 * enemyMisses / System.Math.Max(1, enemyAttacks + enemyMisses):0}% dodged)");
sb.AppendLine($"hits on player capitals {dmgEvents}, fully absorbed {shieldAbsorbedAll} ({100.0 * shieldAbsorbedAll / System.Math.Max(1, dmgEvents):0}%)");
sb.AppendLine($"per battle: heat-cap junk {heatOver / (double)battles:0.00}, reactor strain {strain / (double)battles:0.00}, junk added {junkAdded / (double)battles:0.00}, crits {crits / (double)battles:0.00}, collisions {collisions / (double)battles:0.00}, expends {expends / (double)battles:0.00}");
sb.AppendLine($"torpedoes launched {torpLaunched}, hit {torpHits}; darts launched {dartsLaunched}");
sb.AppendLine($"first battle flagship hull loss avg {firstBattleHullLoss.Average():0.0}, max {firstBattleHullLoss.Max()}");
sb.AppendLine($"scrap at end avg {scrapEnd.Average():0}, deck size at end avg {deckEnd.Average():0.0}");
sb.AppendLine("nodes visited: " + string.Join(", ", nodeKinds.Select(k => $"{k.Key} {k.Value / (double)N:0.0}")));
sb.AppendLine("ENCOUNTER fights win% loss% withdraw% avgTurns flagHullLost escortLost%");
foreach (var k in enc.OrderBy(k => k.Key))
{
    var s = k.Value;
    sb.AppendLine($"  {k.Key,-14} {s[0],4} {100 * s[1] / s[0],3}% {100 * s[2] / s[0],3}% {100 * s[3] / s[0],3}% {s[4] / (double)s[0],5:0.0} {s[5] / (double)s[0],5:0.0} {100 * s[6] / s[0],3}%");
}
sb.AppendLine("LAYER loss rate: " + string.Join(" ", Enumerable.Range(0, 10).Where(i => layerFights[i] > 0).Select(i => $"L{i}:{100 * layerLoss[i] / layerFights[i]}%({layerFights[i]})")));
sb.AppendLine("CARD draws plays play%");
foreach (var k in draws.OrderByDescending(k => k.Value))
{
    plays.TryGetValue(k.Key, out int p);
    sb.AppendLine($"  {k.Key,-20} {k.Value,6} {p,6} {100 * p / k.Value,3}%");
}
System.IO.File.WriteAllText("Temp/sim.txt", sb.ToString());
return sb.ToString();
