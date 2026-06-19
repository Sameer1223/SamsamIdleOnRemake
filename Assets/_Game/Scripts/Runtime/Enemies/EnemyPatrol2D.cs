using UnityEngine;

namespace SamsamIdleOn.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyPatrol2D : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 1.25f;
        [SerializeField] private float patrolDistance = 2f;
        [SerializeField] private float edgeProbeDistance = 0.8f;
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField] private Transform visualRoot;

        private Vector2 origin;
        private int direction = 1;
        private Rigidbody2D body;
        private Collider2D[] ownColliders;
        private Vector3 visualBaseScale = Vector3.one;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            ownColliders = GetComponentsInChildren<Collider2D>();

            if (visualRoot == null)
            {
                visualRoot = transform;
            }

            visualBaseScale = visualRoot.localScale;
        }

        private void OnEnable()
        {
            origin = transform.position;
            direction = Random.value < 0.5f ? -1 : 1;
            UpdateFacing();
        }

        private void FixedUpdate()
        {
            Vector2 currentPosition = body != null ? body.position : (Vector2)transform.position;

            if (ShouldTurnAround(currentPosition))
            {
                direction *= -1;
                UpdateFacing();
            }

            Vector2 nextPosition = currentPosition + (Vector2.right * direction * moveSpeed * Time.fixedDeltaTime);

            if (body != null)
            {
                body.MovePosition(nextPosition);
            }
            else
            {
                transform.position = nextPosition;
            }
        }

        private bool ShouldTurnAround(Vector2 currentPosition)
        {
            float distanceFromOrigin = currentPosition.x - origin.x;

            if ((direction > 0 && distanceFromOrigin >= patrolDistance)
                || (direction < 0 && distanceFromOrigin <= -patrolDistance))
            {
                return true;
            }

            Vector2 groundProbeOrigin = GetGroundProbeOrigin(currentPosition);
            RaycastHit2D[] groundHits = Physics2D.RaycastAll(groundProbeOrigin, Vector2.down, edgeProbeDistance, groundLayers);

            foreach (RaycastHit2D groundHit in groundHits)
            {
                if (groundHit.collider == null
                    || groundHit.collider.isTrigger
                    || IsOwnCollider(groundHit.collider))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private Vector2 GetGroundProbeOrigin(Vector2 currentPosition)
        {
            if (TryGetOwnBounds(out Bounds bounds))
            {
                float leadingEdgeX = bounds.center.x + (bounds.extents.x + 0.1f) * direction;
                return new Vector2(leadingEdgeX, bounds.min.y + 0.1f);
            }

            return currentPosition + new Vector2(direction * 0.45f, 0.1f);
        }

        private bool TryGetOwnBounds(out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;

            foreach (Collider2D ownCollider in ownColliders)
            {
                if (ownCollider == null || !ownCollider.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = ownCollider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(ownCollider.bounds);
                }
            }

            return hasBounds;
        }

        private bool IsOwnCollider(Collider2D colliderToCheck)
        {
            foreach (Collider2D ownCollider in ownColliders)
            {
                if (ownCollider == colliderToCheck)
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateFacing()
        {
            if (visualRoot == null)
            {
                return;
            }

            visualRoot.localScale = new Vector3(
                Mathf.Abs(visualBaseScale.x) * direction,
                visualBaseScale.y,
                visualBaseScale.z);
        }
    }
}
