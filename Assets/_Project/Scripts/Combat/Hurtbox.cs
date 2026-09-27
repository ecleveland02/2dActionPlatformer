using System.Collections.Generic;
using UnityEngine;

namespace Margin.Combat
{
    /// <summary>
    /// The area of a character that can be hit (spec 6.1). Not a physics collider: hit detection is a plain
    /// box overlap test done by the attacker each active frame, so it is deterministic and frame-exact.
    /// All enabled hurtboxes register themselves in a static list.
    /// </summary>
    public sealed class Hurtbox : MonoBehaviour
    {
        [SerializeField] private Faction faction = Faction.Enemy;
        [Tooltip("Box offset from this object's position (does not flip; keep hurtboxes symmetric).")]
        [SerializeField] private Vector2 offset;
        [SerializeField] private Vector2 size = new Vector2(0.6f, 1.8f);

        private static readonly List<Hurtbox> active = new List<Hurtbox>();
        private IHitReceiver receiver;

        public static IReadOnlyList<Hurtbox> Active => active;
        public Faction Faction
        {
            get => faction;
            set => faction = value;
        }
        /// <summary>The component that handles hits (found on this object or a parent, looked up on first use).</summary>
        public IHitReceiver Receiver
        {
            get
            {
                if (receiver == null) receiver = GetComponentInParent<IHitReceiver>();
                return receiver;
            }
        }

        public void Configure(Faction owner, Vector2 boxOffset, Vector2 boxSize)
        {
            faction = owner;
            offset = boxOffset;
            size = boxSize;
        }

        private void OnEnable()
        {
            if (!active.Contains(this)) active.Add(this);
        }

        private void OnDisable() => active.Remove(this);

        public AabbBox WorldBox
        {
            get
            {
                Vector3 p = transform.position;
                return new AabbBox(p.x + offset.x, p.y + offset.y, size.x, size.y);
            }
        }
    }
}
