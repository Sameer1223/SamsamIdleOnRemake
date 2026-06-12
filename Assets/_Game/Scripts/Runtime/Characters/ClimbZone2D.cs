using UnityEngine;

namespace SamsamIdleOn.Characters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class ClimbZone2D : MonoBehaviour
    {
        [SerializeField] private Collider2D climbCollider;

        public Vector2 BottomPoint
        {
            get
            {
                Bounds bounds = ClimbBounds;
                return new Vector2(bounds.center.x, bounds.min.y);
            }
        }

        public Vector2 TopPoint
        {
            get
            {
                Bounds bounds = ClimbBounds;
                return new Vector2(bounds.center.x, bounds.max.y);
            }
        }

        public float CenterX => ClimbBounds.center.x;

        public Bounds ClimbBounds => climbCollider != null ? climbCollider.bounds : new Bounds(transform.position, Vector3.zero);

        private void Reset()
        {
            climbCollider = GetComponent<Collider2D>();
            climbCollider.isTrigger = true;
        }

        private void Awake()
        {
            if (climbCollider == null)
            {
                climbCollider = GetComponent<Collider2D>();
            }

            climbCollider.isTrigger = true;
        }

        public bool CanConnectHeights(float startY, float endY, float tolerance)
        {
            Bounds bounds = ClimbBounds;
            float minY = bounds.min.y - tolerance;
            float maxY = bounds.max.y + tolerance;

            return startY >= minY && startY <= maxY && endY >= minY && endY <= maxY;
        }

        public Vector2 GetPointAtHeight(float y)
        {
            Bounds bounds = ClimbBounds;
            return new Vector2(bounds.center.x, Mathf.Clamp(y, bounds.min.y, bounds.max.y));
        }
    }
}
