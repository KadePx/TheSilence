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
        /// Pick a heading (within what the ship can turn) and a speed (one step from the current one) by where
        /// that leaves it after the drift: a clear shot is worth most, then a clear line to the target, then being
        /// at the range it likes. Sitting still without a shot costs, so a boxed-in ship works its way out.
        /// </summary>
        public static void Manoeuvre(OpenBattle b, OpenShip ship)
        {
            var target = Nearest(b, ship);
            if (target == null) return;
            int ideal = ship.Broadsider ? 4000 : 3000;
            int bestHeading = ship.Heading, bestSpeed = 0, bestScore = int.MinValue;
            for (int off = -ship.Turn; off <= ship.Turn; off += OpenMath.HeadingStep)
            {
                int h = OpenMath.Norm(ship.Heading + off);
                for (int speed = Math.Max(0, ship.Speed - 1); speed <= Math.Min(ship.MaxSpeed, ship.Speed + 1); speed++)
                {
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
                        // Stopped with nothing to shoot: bad, and less bad the more open space lies ahead, so the ship turns its way out.
                        if (speed == 0) score += OpenMath.Dist(end, b.Trace(ship, h, 3 * OpenMath.Span, out _)) / 20 - 250;
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

    /// <summary>A plain player for tests and the F9 key: shoots what it can, turns its guns toward the nearest enemy, closes when far.</summary>
    public static class OpenBot
    {
        public static void PlayTurn(OpenBattle b)
        {
            for (int guard = 0; guard < 40 && b.Outcome == OpenOutcome.Ongoing; guard++)
                if (!PlayOne(b)) break;
            b.EndTurn();
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
                    case OpenKind.Turn:
                        int limit = b.TurnLimit(c, src);
                        int diff = OpenMath.Diff(src.Heading, OpenMath.Bearing(src.Pos, near.Pos));
                        int turn = Math.Max(-limit, Math.Min(limit, diff));
                        // Better: the heading that lets the most weapon cards in hand bear on someone.
                        int bestGuns = Guns(b, src, src.Heading);
                        for (int off = -limit; off <= limit; off += OpenMath.HeadingStep)
                        {
                            int guns = Guns(b, src, OpenMath.Norm(src.Heading + off));
                            if (guns <= bestGuns) continue;
                            bestGuns = guns;
                            turn = off;
                        }
                        if (bestGuns == 0 && src.NoRoomAhead(b))
                        {
                            // Nose against a wreck: turn to the most open heading.
                            int bestRoom = 0;
                            for (int off = -limit; off <= limit; off += OpenMath.HeadingStep)
                            {
                                int room = OpenMath.Dist(src.Pos, b.Trace(src, OpenMath.Norm(src.Heading + off), 3 * OpenMath.Span, out _));
                                if (room <= bestRoom) continue;
                                bestRoom = room;
                                turn = off;
                            }
                        }
                        if (turn == 0) continue;
                        return b.Play(c.Id, OpenChoice.Facing(OpenMath.Norm(src.Heading + turn))) == null;
                    case OpenKind.Burn:
                        if (dist < 5000 && b.Sight(src.Pos, near.Pos) == null) continue;
                        return b.Play(c.Id, OpenChoice.None) == null;
                    case OpenKind.Brake:
                        if (dist > 3500 || src.Speed < 2) continue;
                        return b.Play(c.Id, OpenChoice.None) == null;
                    case OpenKind.Vent:
                        if (src.Heat < 2) continue;
                        return b.Play(c.Id, OpenChoice.None) == null;
                    case OpenKind.Shields:
                        if (dist > 7000) continue;
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
