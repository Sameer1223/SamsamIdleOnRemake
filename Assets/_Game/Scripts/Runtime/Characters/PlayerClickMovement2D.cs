using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SamsamIdleOn.Characters
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerClickMovement2D : MonoBehaviour
    {
        public enum MovementPlane
        {
            HorizontalOnly,
            Free2D
        }

        [Header("Input")]
        [SerializeField] private Camera inputCamera;
        [SerializeField] private LayerMask walkableLayers = ~0;
        [SerializeField] private float walkableClickProbeRadius = 0.12f;
        [SerializeField] private bool ignoreClicksOverUi = true;

        [Header("Movement")]
        [SerializeField] private MovementPlane movementPlane = MovementPlane.HorizontalOnly;
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float climbSpeed = 3f;
        [SerializeField] private float stoppingDistance = 0.05f;
        [SerializeField] private bool stopWhenClickIsInvalid = false;

        [Header("Climbing")]
        [SerializeField] private bool useClimbZones = true;
        [SerializeField] private float verticalRouteThreshold = 0.5f;
        [SerializeField] private float climbZoneHeightTolerance = 0.35f;
        [SerializeField] private float upwardDismountClearance = 0.08f;
        [SerializeField] private float walkableSurfaceProbeHeight = 3f;
        [SerializeField] private float walkableSurfaceProbeDistance = 8f;
        [SerializeField] private bool refreshClimbZonesOnClick = true;
        [SerializeField] private bool disableGravityWhileClimbing = true;
        [SerializeField] private bool ignoreWalkableCollisionsOnRope = true;

        [Header("Presentation")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Animator animator;
        [SerializeField] private string animatorMovingParameter = "IsMoving";
        [SerializeField] private float facingDeadZone = 0.01f;

        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private Vector2 destination;
        private readonly List<MovementStep> route = new();
        private readonly List<ClimbZone2D> climbZones = new();
        private readonly List<Collider2D> ignoredWalkableColliders = new();
        private Vector3 visualRootBaseScale = Vector3.one;
        private bool hasDestination;
        private float originalGravityScale;

        public event Action<Vector2> DestinationChanged;
        public event Action ReachedDestination;

        public bool IsMoving { get; private set; }

        public Vector2 Destination => destination;

        private enum MovementStepType
        {
            Horizontal,
            Vertical,
            ClimbDismount,
            Free
        }

        private readonly struct MovementStep
        {
            public MovementStep(Vector2 target, MovementStepType type)
            {
                Target = target;
                Type = type;
            }

            public Vector2 Target { get; }

            public MovementStepType Type { get; }
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            originalGravityScale = body.gravityScale;

            if (inputCamera == null)
            {
                inputCamera = Camera.main;
            }

            if (visualRoot == null)
            {
                visualRoot = transform;
            }

            visualRootBaseScale = visualRoot.localScale;

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            RefreshClimbZones();
        }

        private void Update()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            {
                return;
            }

            if (ignoreClicksOverUi && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            TrySetDestinationFromPointer(Mouse.current.position.ReadValue());
        }

        private void OnDisable()
        {
            RestoreGravity();
            RestoreWalkableCollisions();
        }

        private void FixedUpdate()
        {
            if (!hasDestination)
            {
                SetMoving(false);
                return;
            }

            Vector2 currentPosition = body.position;
            MovementStep currentStep = route.Count > 0
                ? route[0]
                : new MovementStep(destination, movementPlane == MovementPlane.HorizontalOnly ? MovementStepType.Horizontal : MovementStepType.Free);

            Vector2 nextDestination = GetStepDestination(currentPosition, currentStep);

            Vector2 offset = nextDestination - currentPosition;

            if (offset.sqrMagnitude <= stoppingDistance * stoppingDistance)
            {
                body.MovePosition(nextDestination);
                CompleteCurrentStep();
                return;
            }

            float speed = currentStep.Type == MovementStepType.Vertical ? climbSpeed : moveSpeed;
            Vector2 nextPosition = Vector2.MoveTowards(
                currentPosition,
                nextDestination,
                speed * Time.fixedDeltaTime);

            UpdateFacing(nextPosition.x - currentPosition.x);
            UpdateGravityForStep(currentStep);
            UpdateWalkableCollisionForStep(currentStep);
            body.MovePosition(nextPosition);
            SetMoving(true);
        }

        public void MoveTo(Vector2 worldPosition)
        {
            RestoreWalkableCollisions();
            route.Clear();
            destination = movementPlane == MovementPlane.HorizontalOnly
                ? new Vector2(worldPosition.x, body.position.y)
                : worldPosition;

            hasDestination = true;
            DestinationChanged?.Invoke(destination);
        }

        public void RouteTo(Vector2 worldPosition)
        {
            SetDestinationFromClick(worldPosition);
        }

        public void Stop()
        {
            hasDestination = false;
            route.Clear();
            RestoreGravity();
            RestoreWalkableCollisions();
            SetMoving(false);
        }

        public void RefreshClimbZones()
        {
            climbZones.Clear();
            climbZones.AddRange(FindObjectsByType<ClimbZone2D>(FindObjectsInactive.Exclude));
        }

        private void TrySetDestinationFromPointer(Vector2 screenPosition)
        {
            if (inputCamera == null)
            {
                Debug.LogWarning($"{nameof(PlayerClickMovement2D)} needs an input camera.");
                return;
            }

            Vector3 worldPoint = inputCamera.ScreenToWorldPoint(screenPosition);
            Vector2 clickedWorldPosition = worldPoint;

            if (TrySetRopeDestinationFromClick(clickedWorldPosition))
            {
                return;
            }

            Collider2D clickedCollider = FindWalkableAtClick(clickedWorldPosition);

            if (clickedCollider == null)
            {
                if (stopWhenClickIsInvalid)
                {
                    Stop();
                }

                return;
            }

            SetDestinationFromClick(clickedWorldPosition);
        }

        private Collider2D FindWalkableAtClick(Vector2 clickedWorldPosition)
        {
            Collider2D directHit = Physics2D.OverlapPoint(clickedWorldPosition, walkableLayers);

            if (directHit != null && !directHit.isTrigger)
            {
                return directHit;
            }

            if (walkableClickProbeRadius <= 0f)
            {
                return null;
            }

            Collider2D[] nearbyHits = Physics2D.OverlapCircleAll(clickedWorldPosition, walkableClickProbeRadius, walkableLayers);

            foreach (Collider2D nearbyHit in nearbyHits)
            {
                if (nearbyHit != null && !nearbyHit.isTrigger)
                {
                    return nearbyHit;
                }
            }

            return null;
        }

        private void SetDestinationFromClick(Vector2 clickedWorldPosition)
        {
            float surfaceY = GetWalkableSurfaceY(clickedWorldPosition);
            float dismountY = GetDismountY(surfaceY);

            if (!useClimbZones || Mathf.Abs(dismountY - body.position.y) < verticalRouteThreshold)
            {
                MoveTo(clickedWorldPosition);
                return;
            }

            if (refreshClimbZonesOnClick)
            {
                RefreshClimbZones();
            }

            if (TryBuildClimbRoute(clickedWorldPosition, surfaceY, dismountY))
            {
                hasDestination = true;
                destination = new Vector2(clickedWorldPosition.x, dismountY);
                DestinationChanged?.Invoke(destination);
                return;
            }

            Stop();
        }

        private bool TrySetRopeDestinationFromClick(Vector2 clickedWorldPosition)
        {
            if (!useClimbZones)
            {
                return false;
            }

            if (refreshClimbZonesOnClick)
            {
                RefreshClimbZones();
            }

            ClimbZone2D climbZone = FindClimbZoneAtClick(clickedWorldPosition);

            if (climbZone == null)
            {
                return false;
            }

            Vector2 currentPosition = body.position;
            Vector2 ropeTarget = climbZone.GetPointAtHeight(clickedWorldPosition.y);

            route.Clear();
            route.Add(new MovementStep(climbZone.GetPointAtHeight(currentPosition.y), MovementStepType.Horizontal));
            route.Add(new MovementStep(ropeTarget, MovementStepType.Vertical));

            destination = ropeTarget;
            hasDestination = true;
            DestinationChanged?.Invoke(destination);
            return true;
        }

        private ClimbZone2D FindClimbZoneAtClick(Vector2 clickedWorldPosition)
        {
            foreach (ClimbZone2D climbZone in climbZones)
            {
                if (climbZone == null || !climbZone.ClimbBounds.Contains(clickedWorldPosition))
                {
                    continue;
                }

                return climbZone;
            }

            return null;
        }

        private bool TryBuildClimbRoute(Vector2 clickedWorldPosition, float surfaceY, float dismountY)
        {
            ClimbZone2D bestZone = null;
            float bestCost = float.MaxValue;
            Vector2 currentPosition = body.position;
            float currentSurfaceY = GetCurrentSurfaceY();

            foreach (ClimbZone2D climbZone in climbZones)
            {
                if (climbZone == null
                    || !CanConnectPlayerHeights(climbZone, currentPosition.y, currentSurfaceY, dismountY, surfaceY))
                {
                    continue;
                }

                float cost = Mathf.Abs(currentPosition.x - climbZone.CenterX)
                    + Mathf.Abs(dismountY - currentPosition.y)
                    + Mathf.Abs(clickedWorldPosition.x - climbZone.CenterX);

                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestZone = climbZone;
                }
            }

            if (bestZone == null)
            {
                return false;
            }

            route.Clear();
            route.Add(new MovementStep(bestZone.GetPointAtHeight(currentPosition.y), MovementStepType.Horizontal));
            route.Add(new MovementStep(new Vector2(bestZone.CenterX, dismountY), MovementStepType.Vertical));
            route.Add(new MovementStep(new Vector2(clickedWorldPosition.x, dismountY), MovementStepType.ClimbDismount));
            return true;
        }

        private bool CanConnectPlayerHeights(
            ClimbZone2D climbZone,
            float currentBodyY,
            float currentSurfaceY,
            float targetBodyY,
            float targetSurfaceY)
        {
            bool canReachFromCurrentHeight = IsHeightInsideClimbZone(climbZone, currentBodyY)
                || IsHeightInsideClimbZone(climbZone, currentSurfaceY);
            bool canReachTargetHeight = IsHeightInsideClimbZone(climbZone, targetBodyY)
                || IsHeightInsideClimbZone(climbZone, targetSurfaceY);

            return canReachFromCurrentHeight && canReachTargetHeight;
        }

        private bool IsHeightInsideClimbZone(ClimbZone2D climbZone, float y)
        {
            Bounds bounds = climbZone.ClimbBounds;
            return y >= bounds.min.y - climbZoneHeightTolerance
                && y <= bounds.max.y + climbZoneHeightTolerance;
        }

        private float GetWalkableSurfaceY(Vector2 clickedWorldPosition)
        {
            Vector2 rayOrigin = clickedWorldPosition + (Vector2.up * walkableSurfaceProbeHeight);
            RaycastHit2D[] hits = Physics2D.RaycastAll(rayOrigin, Vector2.down, walkableSurfaceProbeDistance, walkableLayers);

            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider != null && !hit.collider.isTrigger)
                {
                    return hit.point.y;
                }
            }

            return clickedWorldPosition.y;
        }

        private float GetDismountY(float surfaceY)
        {
            return surfaceY + GetColliderBottomOffset() + upwardDismountClearance;
        }

        private float GetColliderBottomOffset()
        {
            return bodyCollider == null
                ? 0f
                : Mathf.Max(0f, body.position.y - bodyCollider.bounds.min.y);
        }

        private float GetCurrentSurfaceY()
        {
            return body.position.y - GetColliderBottomOffset();
        }

        private Vector2 GetStepDestination(Vector2 currentPosition, MovementStep step)
        {
            return step.Type switch
            {
                MovementStepType.Horizontal => new Vector2(step.Target.x, currentPosition.y),
                MovementStepType.ClimbDismount => new Vector2(step.Target.x, currentPosition.y),
                MovementStepType.Vertical => new Vector2(currentPosition.x, step.Target.y),
                _ => step.Target
            };
        }

        private void CompleteCurrentStep()
        {
            if (route.Count > 0)
            {
                route.RemoveAt(0);
            }

            if (route.Count == 0)
            {
                hasDestination = false;
                RestoreGravity();
                RestoreWalkableCollisions();
                SetMoving(false);
                ReachedDestination?.Invoke();
            }
        }

        private void UpdateGravityForStep(MovementStep step)
        {
            if (!disableGravityWhileClimbing)
            {
                return;
            }

            body.gravityScale = step.Type == MovementStepType.Vertical || step.Type == MovementStepType.ClimbDismount
                ? 0f
                : originalGravityScale;
        }

        private void RestoreGravity()
        {
            if (disableGravityWhileClimbing)
            {
                body.gravityScale = originalGravityScale;
            }
        }

        private void UpdateWalkableCollisionForStep(MovementStep step)
        {
            if (!ignoreWalkableCollisionsOnRope || bodyCollider == null)
            {
                return;
            }

            if (step.Type == MovementStepType.Vertical)
            {
                IgnoreWalkableCollisions();
                return;
            }

            RestoreWalkableCollisions();
        }

        private void IgnoreWalkableCollisions()
        {
            if (ignoredWalkableColliders.Count > 0)
            {
                return;
            }

            Collider2D[] colliders = FindObjectsByType<Collider2D>(FindObjectsInactive.Exclude);

            foreach (Collider2D colliderToIgnore in colliders)
            {
                if (colliderToIgnore == null
                    || colliderToIgnore == bodyCollider
                    || colliderToIgnore.isTrigger
                    || !IsInLayerMask(colliderToIgnore.gameObject.layer, walkableLayers))
                {
                    continue;
                }

                Physics2D.IgnoreCollision(bodyCollider, colliderToIgnore, true);
                ignoredWalkableColliders.Add(colliderToIgnore);
            }
        }

        private void RestoreWalkableCollisions()
        {
            if (bodyCollider == null)
            {
                ignoredWalkableColliders.Clear();
                return;
            }

            foreach (Collider2D ignoredCollider in ignoredWalkableColliders)
            {
                if (ignoredCollider != null)
                {
                    Physics2D.IgnoreCollision(bodyCollider, ignoredCollider, false);
                }
            }

            ignoredWalkableColliders.Clear();
        }

        private static bool IsInLayerMask(int layer, LayerMask layerMask)
        {
            return (layerMask.value & (1 << layer)) != 0;
        }

        private void UpdateFacing(float horizontalDelta)
        {
            if (visualRoot == null || Mathf.Abs(horizontalDelta) <= facingDeadZone)
            {
                return;
            }

            float direction = horizontalDelta < 0f ? -1f : 1f;
            visualRoot.localScale = new Vector3(
                Mathf.Abs(visualRootBaseScale.x) * direction,
                visualRootBaseScale.y,
                visualRootBaseScale.z);
        }

        private void SetMoving(bool isMoving)
        {
            if (IsMoving == isMoving)
            {
                return;
            }

            IsMoving = isMoving;

            if (animator != null && !string.IsNullOrWhiteSpace(animatorMovingParameter))
            {
                animator.SetBool(animatorMovingParameter, IsMoving);
            }
        }

    }
}
