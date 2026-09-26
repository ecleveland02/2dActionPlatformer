using UnityEngine;

namespace Margin.Level
{
    /// <summary>
    /// Temporary camera for the movement gym: keeps the target centered.
    /// Replaced by Cinemachine (look-ahead, dead zones, room confiners) when rooms arrive in Milestone 5.
    /// Runs in LateUpdate so it follows the interpolated (smooth) player position.
    /// </summary>
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        private void LateUpdate()
        {
            if (target == null) return;
            Vector3 p = target.position;
            transform.position = new Vector3(p.x, p.y, transform.position.z);
        }
    }
}
