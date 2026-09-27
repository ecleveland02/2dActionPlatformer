using System.Collections.Generic;
using Margin.Abilities;
using Margin.Level;
using Margin.Physics;
using Margin.Player;
using NUnit.Framework;
using UnityEngine;

namespace Margin.Tests
{
    /// <summary>
    /// Rooms (spec 11): walking through a door switches rooms behind a fade and arrives moving in; pits cost health
    /// and put you back; respawns go to the checkpoint's room; ink pots set the respawn point and heal.
    /// Room A spans x -10..10 with a door on its right edge; room B sits at x 90..110 with a door on its left.
    /// </summary>
    public class RoomTests
    {
        private TestWorld world;
        private FakeInput input;
        private PlayerController player;
        private PlayerHealth health;
        private LevelDirector director;
        private LevelSettings settings;
        private Room roomA, roomB;
        private readonly List<Object> made = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            world = new TestWorld();
            input = new FakeInput();
            MovementData data = Track(ScriptableObject.CreateInstance<MovementData>());
            settings = Track(ScriptableObject.CreateInstance<LevelSettings>());
            settings.fadeOutFrames = 4;
            settings.fadeInFrames = 4;
            settings.entryWalkFrames = 6;
            settings.pitFadeOutFrames = 4;
            settings.pitDamage = 15;
            settings.respawnFadeInFrames = 4;

            world.Box(-10, -1, 10, 0);
            world.Box(90, -1, 110, 0);
            roomA = MakeRoom("A", 0f);
            roomB = MakeRoom("B", 100f);
            RoomDoor right = MakeDoor(roomA, RoomDoor.Side.Right, new Vector2(10.5f, 1.5f), new Vector2(-1.5f, -0.55f));
            RoomDoor left = MakeDoor(roomB, RoomDoor.Side.Left, new Vector2(89.5f, 1.5f), new Vector2(1.5f, -0.55f));
            RoomDoor.Link(right, left);

            KinematicBody2D body = world.Body(new Vector2(0f, 0.05f));
            player = body.gameObject.AddComponent<PlayerController>();
            player.Configure(data, input, new AbilityUnlocks());
            health = body.gameObject.AddComponent<PlayerHealth>();

            director = Track(new GameObject("Director")).AddComponent<LevelDirector>();
            director.Configure(settings, roomA, player);
            director.Begin();
            for (int i = 0; i < 5; i++) Step();
        }

        [TearDown]
        public void TearDown()
        {
            world.Destroy();
            foreach (Object o in made) if (o != null) Object.DestroyImmediate(o);
            made.Clear();
        }

        private T Track<T>(T o) where T : Object { made.Add(o); return o; }

        private Room MakeRoom(string title, float x)
        {
            var go = Track(new GameObject("Room " + title));
            go.transform.position = new Vector3(x, 0f, 0f);
            var room = go.AddComponent<Room>();
            room.Configure(title, new Rect(-10f, -2f, 20f, 12f));
            return room;
        }

        private static RoomDoor MakeDoor(Room room, RoomDoor.Side side, Vector2 at, Vector2 arrival)
        {
            var go = new GameObject("Door " + side);
            go.transform.SetParent(room.transform, false);
            go.transform.position = at;
            var door = go.AddComponent<RoomDoor>();
            door.Configure(side, new Vector2(1f, 4f), arrival);
            return door;
        }

        private void Step()
        {
            input.Clock.Advance();
            player.Tick();
            health.Tick();
            director.Tick();
        }

        private void StepUntil(System.Func<bool> condition, int max, string what)
        {
            for (int i = 0; i < max && !condition(); i++) Step();
            Assert.IsTrue(condition(), what);
        }

        [Test]
        public void StartsInTheStartRoom_WithTheOthersSwitchedOff()
        {
            Assert.AreEqual(roomA, director.CurrentRoom);
            Assert.IsTrue(roomA.gameObject.activeSelf);
            Assert.IsFalse(roomB.gameObject.activeSelf);
        }

        [Test]
        public void WalkingThroughADoor_SwitchesRooms_AndArrivesWalkingIn()
        {
            input.Move = Vector2.right;
            StepUntil(() => director.InTransition, 200, "walking right should reach the door");
            Assert.AreEqual(roomA, director.CurrentRoom, "still in A while the screen fades");
            Assert.IsTrue(health.Protected, "can't be hit during a transition");

            StepUntil(() => director.CurrentRoom == roomB, settings.fadeOutFrames + 1, "switches when the screen is covered");
            Assert.AreEqual(1f, director.Fade, 1e-5f);
            Assert.IsFalse(roomA.gameObject.activeSelf);
            Assert.IsTrue(roomB.gameObject.activeSelf);
            Assert.AreEqual(91f, player.Body.Position.x, 1e-4f, "at the arrival point");
            Assert.Greater(player.Velocity.x, 0f, "already walking in");
            Assert.AreEqual(1, player.Facing);

            input.Move = Vector2.zero;
            StepUntil(() => !director.InTransition, 30, "the transition ends");
            Assert.IsFalse(health.Protected);
            Assert.AreEqual(0f, director.Fade);
        }

        [Test]
        public void FallingIntoAPit_CostsHealth_AndPutsYouBack()
        {
            Vector2 start = player.Body.Position;
            input.Move = Vector2.left;
            StepUntil(() => player.Body.Position.x < -11f, 200, "walk off the left edge");
            input.Move = Vector2.zero;
            StepUntil(() => director.InTransition, 300, "falling below the room is a pit");
            Assert.AreEqual(100 - settings.pitDamage, health.Health.Current);

            StepUntil(() => director.Fade >= 1f || !director.InTransition, 10, "fades out");
            Step();
            Assert.AreEqual(start.x, player.Body.Position.x, 0.05f, "back where the room was entered");
            Assert.Greater(player.Body.Position.y, -1f);
            StepUntil(() => !director.InTransition, 30, "fades back in");
            Assert.AreEqual(roomA, director.CurrentRoom);
        }

        [Test]
        public void Respawn_ReturnsToTheCheckpointRoom()
        {
            input.Move = Vector2.right;
            StepUntil(() => director.CurrentRoom == roomB, 300, "go to B");
            input.Move = Vector2.zero;
            StepUntil(() => !director.InTransition, 30, "arrive");

            health.Respawn();   // same as dying (or Restart from the pause menu)
            Step();
            Assert.AreEqual(roomA, director.CurrentRoom, "no ink pot touched: back to the start room");
            Assert.IsFalse(roomB.gameObject.activeSelf);
            Assert.AreEqual(0f, player.Body.Position.x, 0.5f);
        }

        [Test]
        public void InkPot_SetsTheRespawnPoint_AndRestoresHealth()
        {
            var pot = new GameObject("Ink Pot");
            pot.transform.SetParent(roomA.transform, false);
            pot.transform.position = new Vector3(5f, 0f, 0f);
            Checkpoint checkpoint = pot.AddComponent<Checkpoint>();

            health.TakeHazardDamage(30, "test");
            Assert.AreEqual(70, health.Health.Current);

            input.Move = Vector2.right;
            for (int i = 0; i < 200 && health.Health.Current < 100; i++)
            {
                Step();
                checkpoint.Tick();
            }
            Assert.AreEqual(100, health.Health.Current, "touching the pot heals");
            Assert.AreEqual(checkpoint.RespawnPoint, health.SpawnPoint);
            Assert.AreEqual(roomA, director.CheckpointRoom);
            Assert.IsTrue(checkpoint.IsCurrent);
        }
    }
}
