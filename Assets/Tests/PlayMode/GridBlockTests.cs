using System.Collections.Generic;
using Margin.Level;
using Margin.Physics;
using NUnit.Framework;
using UnityEngine;

namespace Margin.Tests
{
    /// <summary>World 2's moving blocks: bodies standing on them ride along; bodies in the way get pushed.</summary>
    public class GridBlockTests
    {
        private TestWorld world;
        private readonly List<Object> made = new List<Object>();

        [SetUp]
        public void SetUp() => world = new TestWorld();

        [TearDown]
        public void TearDown()
        {
            world.Destroy();
            foreach (Object o in made) if (o != null) Object.DestroyImmediate(o);
            made.Clear();
        }

        /// <summary>A 4 x 1 block whose top is at y = 0, moving right 2 cells at 60 cells/s (1 cell per tick).</summary>
        private GridBlock MakeBlock(Vector2Int to)
        {
            GameObject go = world.Box(-2f, -1f, 2f, 0f);
            var block = go.AddComponent<GridBlock>();
            block.Configure(new List<Vector2Int> { Vector2Int.zero, to }, 60f, 0, 0, false, null);
            block.ResetForRoom();
            return block;
        }

        [Test]
        public void ARider_MovesWithTheBlock()
        {
            GridBlock block = MakeBlock(new Vector2Int(2, 0));
            KinematicBody2D body = world.Body(new Vector2(0f, 0.01f));
            body.Move(Vector2.down * 0.05f);
            Assert.IsTrue(body.Collisions.Grounded);

            block.Tick();               // the block moves 1 unit right
            body.Move(Vector2.zero);    // the rider's own move this tick carries it
            Assert.AreEqual(1f, body.Position.x, 0.01f);
            Assert.IsTrue(body.Collisions.Grounded, "still standing on it");
        }

        [Test]
        public void ABodyInTheWay_IsPushed()
        {
            GridBlock block = MakeBlock(new Vector2Int(2, 0));
            KinematicBody2D body = world.Body(new Vector2(2.5f, -1f));   // beside the block's right edge, at its height
            float before = body.Position.x;
            block.Tick();
            Assert.Greater(body.Position.x, before + 0.4f, "shoved right, out of the block");
        }
    }
}
