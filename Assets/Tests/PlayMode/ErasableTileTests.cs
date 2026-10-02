using Margin.Level;
using NUnit.Framework;
using UnityEngine;

namespace Margin.Tests
{
    /// <summary>World 2's erasable tiles: rubbed out after the crawler touches them, drawn back in later.</summary>
    public class ErasableTileTests
    {
        private TestWorld world;

        [SetUp]
        public void SetUp() => world = new TestWorld();

        [TearDown]
        public void TearDown() => world.Destroy();

        [Test]
        public void Touched_ErasesAfterTheDelay_ThenComesBack()
        {
            GameObject go = world.Box(0f, -1f, 1f, 0f);
            var tile = go.AddComponent<ErasableTile>();
            var box = go.GetComponent<BoxCollider2D>();

            for (int i = 0; i < 30; i++) tile.Tick();
            Assert.IsTrue(box.enabled, "untouched tiles stay");

            tile.Touch();
            for (int i = 0; i < 40 + 20; i++) tile.Tick();   // default delay 40 + fade 20
            Assert.IsFalse(box.enabled, "rubbed out: no longer solid");
            Assert.IsFalse(tile.Solid);

            for (int i = 0; i < 360; i++) tile.Tick();
            Assert.IsTrue(box.enabled, "drawn back in");
        }

        [Test]
        public void EnteringTheRoom_RestoresIt()
        {
            GameObject go = world.Box(0f, -1f, 1f, 0f);
            var tile = go.AddComponent<ErasableTile>();
            tile.Touch();
            for (int i = 0; i < 70; i++) tile.Tick();
            tile.ResetForRoom();
            Assert.IsTrue(go.GetComponent<BoxCollider2D>().enabled);
        }
    }
}
