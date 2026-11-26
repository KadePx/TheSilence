using System.Linq;
using NUnit.Framework;
using Silence.Core.Battle;
using Silence.Core.Hex;

namespace Silence.Tests
{
    /// <summary>The tutorial battle: fixed opening hands, and the exact actions the tutorial asks for must all be legal.</summary>
    public class TutorialTests
    {
        [Test]
        public void DeckIsNotShuffledSoTheFirstHandIsFixed()
        {
            var b = Scenarios.Tutorial();
            b.Start();
            CollectionAssert.AreEqual(
                new[] { "lance_volley", "brace", "burn", "broadside_battery", "hard_about" },
                b.Deck.Hand.Select(c => c.Def.Id).ToArray());
        }

        [Test]
        public void OtherBattlesStillShuffle()
        {
            Assert.IsTrue(Scenarios.InterceptAtTheRing(1).ShuffleAtStart);
        }

        [Test]
        public void ThePicketNeverMovesAndAlwaysFiresABroadside()
        {
            var b = Scenarios.Tutorial();
            b.Start();
            var picket = b.Units.First(u => u.Side == Side.Enemy);
            var start = picket.Pos;
            for (int turn = 0; turn < 4; turn++)
            {
                Assert.AreEqual(IntentKind.Broadside, b.Intents.First(i => i.UnitId == picket.Id).Kind);
                b.EndTurn();
            }
            Assert.AreEqual(start, picket.Pos);
        }

        [Test]
        public void TheGuidedTurnsPlayOutAsTheTutorialSays()
        {
            var b = Scenarios.Tutorial();
            b.Start();
            var flag = b.Flagship;
            var picket = b.Units.First(u => u.Side == Side.Enemy);
            CardInstance Card(string id) => b.Deck.Hand.First(c => c.Def.Id == id);

            // Turn 1: the picket is out of its own range now but the drift carries the flagship into it.
            Assert.AreEqual(4, flag.Pos.DistanceTo(picket.Pos));
            Assert.IsTrue(b.ForecastHit(b.Intents[0]), "the broadside is on course to hit after the drift");
            Assert.IsNull(b.PlayCard(Card("lance_volley").Id, PlayTarget.At(picket.Id)));
            Assert.AreEqual(6, picket.Hull, "9 - 2 shields - 1 armor");
            Assert.IsNull(b.PlayCard(Card("brace").Id, PlayTarget.At(flag.Id)));
            Assert.AreEqual(0, b.Command);
            b.EndTurn();
            Assert.AreEqual(30, flag.Hull, "braced shields take the whole broadside");
            Assert.AreEqual(3, flag.Pos.DistanceTo(picket.Pos));

            // Turn 2: the second fixed hand; turn, fire the side guns, launch and use a dart.
            CollectionAssert.AreEquivalent(
                new[] { "hard_about", "broadside_battery", "launch_darts", "lance_volley", "vent_plasma" },
                b.Deck.Hand.Select(c => c.Def.Id).ToArray());
            Assert.IsNotNull(b.CheckPlay(Card("broadside_battery").Id, PlayTarget.At(picket.Id)), "dead ahead is not a side arc");
            Assert.IsNull(b.PlayCard(Card("hard_about").Id, PlayTarget.Turning(flag.Id, 1)));
            Assert.IsNull(b.PlayCard(Card("broadside_battery").Id, PlayTarget.At(picket.Id)));
            Assert.IsNull(b.PlayCard(Card("launch_darts").Id, PlayTarget.None));
            var dart = b.LiveUnits(Side.Player).First(u => u.Kind == UnitKind.Craft);
            var spot = b.Reachable(dart.Pos, dart.Move).First(h => h.DistanceTo(picket.Pos) == 1);
            Assert.IsNull(b.CommandCraft(dart.Id, spot, picket.Id));
            Assert.IsTrue(picket.Alive, "the picket survives the guided turns");
            b.EndTurn();

            Assert.AreEqual(3, b.Turn);
            Assert.AreEqual(BattleOutcome.Ongoing, b.Outcome);
            Assert.AreEqual(30, flag.Hull, "a light broadside against full shields does no hull damage");
        }

        [Test]
        public void TurningEitherWayStillBringsTheSideGunsToBear()
        {
            foreach (int steps in new[] { 1, -1 })
            {
                var b = Scenarios.Tutorial();
                b.Start();
                var flag = b.Flagship;
                var picket = b.Units.First(u => u.Side == Side.Enemy);
                b.PlayCard(b.Deck.Hand.First(c => c.Def.Id == "lance_volley").Id, PlayTarget.At(picket.Id));
                b.EndTurn();
                Assert.IsNull(b.PlayCard(b.Deck.Hand.First(c => c.Def.Id == "hard_about").Id, PlayTarget.Turning(flag.Id, steps)));
                Assert.IsNull(b.CheckPlay(b.Deck.Hand.First(c => c.Def.Id == "broadside_battery").Id, PlayTarget.At(picket.Id)), $"turn {steps}");
            }
        }
    }
}
