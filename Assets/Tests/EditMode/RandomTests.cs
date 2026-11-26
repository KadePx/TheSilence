using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Silence.Core.Random;

namespace Silence.Tests
{
    public class RandomTests
    {
        [Test]
        public void SameSeedSameSequence()
        {
            var a = new Pcg32(42);
            var b = new Pcg32(42);
            for (int i = 0; i < 1000; i++) Assert.AreEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void MatchesReferencePcg32()
        {
            // pcg32-demo reference: seed 42, stream 54.
            var rng = new Pcg32(42, 54);
            uint[] expected = { 0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e };
            foreach (var e in expected) Assert.AreEqual(e, rng.NextUInt());
        }

        [Test]
        public void SaveAndRestoreContinuesTheSequence()
        {
            var a = new Pcg32(7);
            for (int i = 0; i < 10; i++) a.NextUInt();
            var b = Pcg32.FromState(a.State, a.Increment);
            for (int i = 0; i < 100; i++) Assert.AreEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void RangeStaysInBounds()
        {
            var rng = new Pcg32(1);
            var seen = new HashSet<int>();
            for (int i = 0; i < 2000; i++)
            {
                int v = rng.Range(-3, 4);
                Assert.That(v, Is.InRange(-3, 3));
                seen.Add(v);
            }
            Assert.AreEqual(7, seen.Count);
        }

        [Test]
        public void ShuffleIsAPermutationAndDeterministic()
        {
            var list1 = Enumerable.Range(0, 20).ToList();
            var list2 = Enumerable.Range(0, 20).ToList();
            new Pcg32(99).Shuffle(list1);
            new Pcg32(99).Shuffle(list2);
            CollectionAssert.AreEqual(list1, list2);
            CollectionAssert.AreEquivalent(Enumerable.Range(0, 20), list1);
            CollectionAssert.AreNotEqual(Enumerable.Range(0, 20), list1);
        }

        [Test]
        public void ForksAreIndependent()
        {
            var root = new Pcg32(5);
            var map = root.Fork(1);
            var loot = root.Fork(2);
            Assert.AreNotEqual(map.NextUInt(), loot.NextUInt());
        }
    }
}
