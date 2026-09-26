using UnityEngine;

namespace Margin.Physics
{
    /// <summary>What a KinematicBody2D touched during its last Move().</summary>
    public struct CollisionState
    {
        public bool Grounded;
        public bool WasGrounded;
        /// <summary>True only on the tick the body touched down.</summary>
        public bool JustLanded;
        /// <summary>Surface normal of the ground (straight up on flat ground). Only valid when Grounded.</summary>
        public Vector2 GroundNormal;
        public bool OnOneWay;
        public bool HitCeiling;
        public bool HitWallLeft;
        public bool HitWallRight;
    }
}
