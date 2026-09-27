using Margin.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Margin.Tests
{
    /// <summary>The drawn weapon: katana parts, exact tip, grip angle.</summary>
    public class WeaponLineTests
    {
        private GameObject root;
        private StickFigureRig rig;
        private WeaponLine weapon;
        private WeaponLook look;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Figure");
            rig = root.AddComponent<StickFigureRig>();
            rig.Build();
            var go = new GameObject("Weapon");
            go.transform.SetParent(root.transform, false);
            weapon = go.AddComponent<WeaponLine>();
            weapon.Rig = rig;
            weapon.Length = 0.9f;
            look = ScriptableObject.CreateInstance<WeaponLook>();
            look.twoHanded = false;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(look);
        }

        private void Draw() => weapon.SendMessage("LateUpdate");

        private Vector3 ExpectedTip()
        {
            Vector3 hand = rig.HandPosition(front: true);
            return hand + WeaponLine.WeaponDirection(rig) * 0.9f;
        }

        [Test]
        public void Katana_DrawsBladeGuardHandleAndScabbard()
        {
            weapon.Look = look;
            rig.ApplyPose(new FigurePose { shoulderFront = 90f });
            Draw();
            Assert.AreEqual(1 + 3 + 2, weapon.Lines.Length, "Blade outline + fill, guard, handle + scabbard outline and collar.");
            Assert.Greater(weapon.Line.positionCount, 2, "The blade is a curve, not a straight line.");
        }

        [Test]
        public void Tip_IsExactlyHandPlusDirectionTimesLength_ForEveryStyle()
        {
            rig.ApplyPose(new FigurePose { shoulderFront = 70f, elbowFront = 20f });
            foreach (WeaponStyle style in new[] { WeaponStyle.Plain, WeaponStyle.Katana, WeaponStyle.Pencil })
            {
                look.style = style;
                weapon.Look = look;
                Draw();
                Assert.Less(Vector3.Distance(ExpectedTip(), weapon.TipPosition), 1e-4f, style.ToString());
            }
        }

        [Test]
        public void Grip_TurnsTheWeaponAwayFromTheForearm()
        {
            rig.ApplyPose(new FigurePose { shoulderFront = 90f });   // forearm level, pointing forward (+x)
            Vector3 straight = WeaponLine.WeaponDirection(rig);
            rig.ApplyPose(new FigurePose { shoulderFront = 90f, grip = 30f });
            Vector3 turned = WeaponLine.WeaponDirection(rig);
            Assert.AreEqual(30f, Vector3.Angle(straight, turned), 0.5f);
            Assert.Less(turned.y, straight.y, "+ grip turns a forward-pointing blade clockwise (down), matching the tracer.");
        }
    }
}
