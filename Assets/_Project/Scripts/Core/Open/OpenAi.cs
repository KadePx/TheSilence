using System;
using System.Collections.Generic;

namespace Silence.Core.Open
{
    /// <summary>
    /// Enemy ships in the open battle. Each fires once from where it sits, then turns toward where it wants to
    /// be and sets its speed. No dice: the same position always gives the same move.
    /// </summary>
    public static class OpenAi
    {
        /// <summary>The shot that would take the most hull, if any gun bears. Ties go to the flagship.</summary>
        public static bool Fire(OpenBattle b, OpenShip ship)
        {
            OpenWeapon bestWeapon = null;
            OpenShip bestTarget = null;
            int bestScore = -1;
            foreach (var w in ship.Weapons)
                foreach (var t in b.Ships)
                {
                    if (!t.Alive || t.Side == ship.Side || b.Check(ship, w, t) != Shot.Clear) continue;
                    int rest = Math.Max(0, w.Damage - t.Shields);
                    int hull = Math.Max(0, rest - t.Armor[(int)OpenMath.ArcOf(t.Pos, t.Heading, ship.Pos)]);
                    int score = hull * 4 + w.Damage + (t.Flagship ? 1 : 0);
                    if (score <= bestScore) continue;
                    bestScore = score;
                    bestWeapon = w;
                    bestTarget = t;
                }
            if (bestWeapon == null) return false;
            b.Hit(ship, bestWeapon, bestTarget);
            return true;
        }

        public static OpenShip Nearest(OpenBattle b, OpenShip ship)
        {
            OpenShip best = null;
            int bestDist = int.MaxValue;
            foreach (var t in b.Ships)
            {
                if (!t.Alive || t.Side == ship.Side) continue;
                int d = OpenMath.Dist(ship.Pos, t.Pos);
                if (d >= bestDist) continue;
                bestDist = d;
                best = t;
            }
            return best;
        }

        /// <summary>
        /// Pick a heading and speed within the same helm limits the player has (one speed step, its agility, one
        /// point less at top speed) by where that leaves it after the drift: a clear shot is worth most, then a
        /// clear line to the target, then being at the range it likes. Sitting still without a shot costs, so a
        /// boxed-in ship works its way out.
        /// </summary>
        public static void Manoeuvre(OpenBattle b, OpenShip ship)
        {
            var target = Nearest(b, ship);
            if (target == null) return;
            int ideal = ship.Broadsider ? 4000 : 3000;
            int bestHeading = ship.Heading, bestSpeed = 0, bestScore = int.MinValue;
            for (int speed = Math.Max(0, ship.Speed - 1); speed <= Math.Min(ship.MaxSpeed, ship.Speed + 1); speed++)
            {
                int limit = b.HelmTurn(ship, speed);
                for (int off = -limit; off <= limit; off += OpenMath.HeadingStep)
                {
                    int h = OpenMath.Norm(ship.Heading + off);
                    var end = b.Trace(ship, h, speed * OpenMath.Span, out var stop);
                    if (stop != Stop.None) continue; // never plan a collision
                    int score = -Math.Abs(OpenMath.Dist(end, target.Pos) - ideal) / 10;
                    int shot = 0;
                    foreach (var w in ship.Weapons)
                        foreach (var t in b.Ships)
                            if (t.Alive && t.Side != ship.Side && b.Check(end, h, w, t) == Shot.Clear) shot = Math.Max(shot, 600 + w.Damage * 10);
                    if (shot > 0) score += shot;
                    else
                    {
                        if (b.Sight(end, target.Pos) == null) score += 200;
                        // Nothing to shoot: open space ahead is worth something, so a boxed-in ship turns its way out; stopping costs.
                        score += OpenMath.Dist(end, b.Trace(ship, h, 3 * OpenMath.Span, out _)) / 10;
                        if (speed == 0) score -= 250;
                    }
                    score -= Math.Abs(off) / OpenMath.HeadingStep; // the smaller turn on a tie
                    if (score <= bestScore) continue;
                    bestScore = score;
                    bestHeading = h;
                    bestSpeed = speed;
                }
            }
            b.SetHeading(ship, bestHeading);
            b.SetSpeed(ship, bestSpeed);
        }
    }

    /// <summary>
    /// A plain player for tests and the F9 key. It sets each ship's helm to bring the most guns in hand to bear
    /// (and to end the drift at a fighting range, clear of rocks), then fires what it can and ends the turn.
    /// </summary>
    public static class OpenBot
    {
        public static void PlayTurn(OpenBattle b)
        {
            foreach (var s in b.Ships)
                if (s.Alive && s.Side == OpenSide.Player && b.Outcome == OpenOutcome.Ongoing) Steer(b, s);
            for (int guard = 0; guard < 40 && b.Outcome == OpenOutcome.Ongoing; guard++)
                if (!PlayOne(b)) break;
            b.EndTurn();
        }

        /// <summary>The bot's helm choice for one ship (also what the screen could suggest).</summary>
        public static void Steer(OpenBattle b, OpenShip s)
        {
            var near = OpenAi.Nearest(b, s);
            if (near == null) return;
            int bestHeading = s.Heading, bestSpeed = s.Speed;
            long best = long.MinValue;
            for (int speed = b.HelmMin(s); speed <= b.HelmMax(s); speed++)
            {
                int limit = b.HelmTurn(s, speed);
                for (int off = -limit; off <= limit; off += OpenMath.HeadingStep)
                {
                    int h = OpenMath.Norm(s.StartHeading + off);
                    int guns = Guns(b, s, h);
                    long score = guns * 1000L;
                    var end = b.Trace(s, h, speed * OpenMath.Span, out var stop);
                    if (stop != Stop.None) score -= 2000;
                    score -= Math.Abs(OpenMath.Dist(end, near.Pos) - 3500) / 10;
                    if (b.Sight(end, near.Pos) == null) score += 150;
                    else if (guns == 0)
                    {
                        // Nothing to shoot and nothing in sight: do not sit behind a rock, and prefer open space
                        // ahead, so a ship nosed into a pocket backs out of it.
                        if (speed == 0) score -= 400;
                        score += OpenMath.Dist(end, b.Trace(s, h, 3 * OpenMath.Span, out _)) / 10;
                    }
                    score -= Math.Abs(off) / OpenMath.HeadingStep;
                    if (score <= best) continue;
                    best = score;
                    bestHeading = h;
                    bestSpeed = speed;
                }
            }
            b.Helm(s.Id, bestHeading, bestSpeed);
        }

        static bool PlayOne(OpenBattle b)
        {
            foreach (var c in b.Hand)
            {
                if (c.Kind != OpenKind.Fire || b.WhyNot(c) != null) continue;
                var src = b.Get(c.SourceId);
                foreach (var t in b.Ships)
                    if (t.Alive && t.Side != src.Side && b.Check(src, c.Weapon, t) == Shot.Clear)
                        return b.Play(c.Id, OpenChoice.At(t.Id)) == null;
            }
            foreach (var c in b.Hand)
            {
                if (b.WhyNot(c) != null) continue;
                var src = b.Get(c.SourceId);
                var near = OpenAi.Nearest(b, src);
                if (near == null) return false;
                int dist = OpenMath.Dist(src.Pos, near.Pos);
                switch (c.Kind)
                {
                    case OpenKind.Burn:
                        if (dist < 6000 && b.Sight(src.Pos, near.Pos) == null) continue;
                        return b.Play(c.Id, OpenChoice.None) == null;
                    case OpenKind.Vent:
                        if (src.Heat < 2) continue;
                        return b.Play(c.Id, OpenChoice.None) == null;
                    case OpenKind.Shields:
                        if (dist > 8000) continue;
                        return b.Play(c.Id, OpenChoice.None) == null;
                }
            }
            return false;
        }

        static int Guns(OpenBattle b, OpenShip src, int heading)
        {
            int n = 0;
            foreach (var c in b.Hand)
            {
                if (c.Kind != OpenKind.Fire || c.SourceId != src.Id) continue;
                foreach (var t in b.Ships)
                    if (t.Alive && t.Side != src.Side && b.Check(src.Pos, heading, c.Weapon, t) == Shot.Clear) { n++; break; }
            }
            return n;
        }
    }
}
