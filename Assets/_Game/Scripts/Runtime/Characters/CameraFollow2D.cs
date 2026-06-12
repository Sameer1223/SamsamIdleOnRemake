using UnityEngine;

namespace SamsamIdleOn.Characters
{
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float smoothTime = 0.15f;
        [SerializeField, Range(0f, 0.45f)] private float verticalScreenMargin = 0.25f;

        private Camera followCamera;
        private Vector3 velocity;
        private Vector3 lockedPosition;

        private void Awake()
        {
            followCamera = GetComponent<Camera>();
            lockedPosition = transform.position;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 currentPosition = transform.position;
            Vector3 desiredPosition = new Vector3(lockedPosition.x, currentPosition.y, lockedPosition.z);
            Vector3 viewportPosition = followCamera.WorldToViewportPoint(target.position);
            float lowerBound = verticalScreenMargin;
            float upperBound = 1f - verticalScreenMargin;

            if (viewportPosition.y < lowerBound || viewportPosition.y > upperBound)
            {
                desiredPosition.y = CalculateVerticalCameraPosition(currentPosition.y, viewportPosition.y, lowerBound, upperBound);
            }

            transform.position = Vector3.SmoothDamp(currentPosition, desiredPosition, ref velocity, smoothTime);
        }

        public void SetTarget(Transform nextTarget)
        {
            target = nextTarget;
        }

        private float CalculateVerticalCameraPosition(float currentY, float targetViewportY, float lowerBound, float upperBound)
        {
            float visibleWorldHeight = followCamera.orthographic
                ? followCamera.orthographicSize * 2f
                : Mathf.Max(0.01f, Mathf.Abs(target.position.z - transform.position.z));

            if (targetViewportY < lowerBound)
            {
                return currentY - ((lowerBound - targetViewportY) * visibleWorldHeight);
            }

            return currentY + ((targetViewportY - upperBound) * visibleWorldHeight);
        }
    }
}
