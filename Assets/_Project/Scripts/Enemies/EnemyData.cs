using System.Collections.Generic;
using Margin.Combat;
using Margin.Rendering;
using UnityEngine;

namespace Margin.Enemies
{
    /// <summary>Everything tunable about one enemy type (spec 9). Create via Assets > Create > Margin > Enemy Data.</summary>
    [CreateAssetMenu(fileName = "EnemyData", menuName = "Margin/Enemy Data")]
    public sealed class EnemyData : ScriptableObject
    {
        [Header("Health")]
        [Min(1)] public int maxHealth = 40;

        [Header("Movement")]
        [Tooltip("Walking speed (units/s).")]
        [Min(0f)] public float walkSpeed = 2.2f;
        [Tooltip("Frames to reach walking speed from standing.")]
        [Min(1)] public int accelerationFrames = 6;
        [Tooltip("Patrols this far left and right of where it was placed. 0 = stands still.")]
        [Min(0f)] public float patrolDistance = 2f;
        [Tooltip("Frames it waits at each end of its patrol.")]
        [Min(0)] public int patrolPauseFrames = 60;

        [Header("Senses")]
        [Tooltip("Notices the player within this horizontal distance (units).")]
        [Min(0f)] public float noticeRange = 7f;
        [Tooltip("...and within this vertical distance.")]
        [Min(0f)] public float noticeHeight = 2.5f;
        [Tooltip("Gives up and returns to patrolling when the player is farther than this.")]
        [Min(0f)] public float giveUpRange = 12f;
        [Tooltip("Frames of the 'noticed you' reaction before it approaches (a telegraph for the fight starting).")]
        [Min(0)] public int alertFrames = 24;

        [Header("Attacking")]
        [Tooltip("Starts an attack when the player is this close (units, horizontal).")]
        [Min(0f)] public float attackRange = 1.3f;
        [Tooltip("Won't start an attack when the player is closer than this (a spear can't hit point-blank).")]
        [Min(0f)] public float minAttackRange = 0f;
        [Tooltip("Backs away when the player is closer than this, to keep spacing. 0 = never backs away.")]
        [Min(0f)] public float retreatDistance = 0f;
        [Tooltip("Opening attacks, used in order. Each chains into its 'Cancels into' list on hit (enemy combos). " +
                 "Openers need 12+ frames of startup (spec 9); follow-ups only happen after a hit.")]
        public List<AttackData> attacks = new List<AttackData>();
        [Tooltip("Frames between the end of one attack (or string) and the next.")]
        [Min(0)] public int attackCooldownFrames = 50;
        [Tooltip("Stops this far behind another enemy so groups don't stack into one body.")]
        [Min(0f)] public float personalSpace = 0.9f;

        [Header("Flying (FlyingEnemy only)")]
        [Tooltip("Hovers this high above the player before diving (units).")]
        [Min(0f)] public float hoverHeight = 2.4f;
        [Tooltip("...and this far to the side.")]
        [Min(0f)] public float hoverSide = 2.2f;
        [Tooltip("Flying speed (units/s).")]
        [Min(0f)] public float flySpeed = 3.5f;
        [Tooltip("Frames to reach flying speed.")]
        [Min(1)] public int flyAccelerationFrames = 20;
        [Tooltip("Dive speed (units/s). The dive aims at where the player was when the wind-up ended.")]
        [Min(0f)] public float diveSpeed = 11f;

        [Header("Hit reactions")]
        [Tooltip("Multiplier on knockback received. 1 = normal, lower = heavier enemy.")]
        [Range(0f, 2f)] public float knockbackTaken = 1f;

        [Header("Death")]
        [Tooltip("Frames of the defeat pose before it vanishes in a burst of ink.")]
        [Min(1)] public int deathFrames = 40;

        [Header("Look")]
        [Tooltip("Optional body proportions (e.g. a bigger head), applied to the rig when the enemy is built.")]
        public StickFigureProportions proportions;
        public PoseClip idle;
        public PoseClip walk;
        public PoseClip alert;
        public PoseClip hurt;
        public PoseClip defeated;
        [Tooltip("Length of a held weapon line from the front hand (e.g. the Lancer's pencil). 0 = unarmed.")]
        [Min(0f)] public float weaponLength = 0f;
    }
}
