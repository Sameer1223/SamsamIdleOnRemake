using System;
using System.Collections.Generic;
using SamsamIdleOn.Stats;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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
        [SerializeField] private float walkableSurfaceClickTolerance = 0.35f;
        [SerializeField] private bool ignoreClicksOverUi = true;
        [SerializeField] private bool blockOnlyInteractiveUi = true;

        [Header("Movement")]
        [SerializeField] private MovementPlane movementPlane = MovementPlane.HorizontalOnly;
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float climbSpeed = 3f;
        [SerializeField] private float stoppingDistance = 0.05f;
        [SerializeField] private bool stopWhenClickIsInvalid = false;
        [SerializeField] private PlayerStats stats;

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
        [SerializeField] private bool artFacesLeftByDefault = true;
        [SerializeField] private Animator animator;
        [SerializeField] private string animatorMovingParameter = "IsMoving";
        [SerializeField] private float facingDeadZone = 0.01f;

        [Header("Debug")]
        [SerializeField] private bool logMovementDebug = true;
        [SerializeField] private float stallLogInterval = 0.75f;
        [SerializeField] private float stallMoveThreshold = 0.01f;
        [SerializeField] private bool unstickHorizontalStalls = true;
        [SerializeField] private float stallUnstickNudgeDistance = 0.08f;

        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private Vector2 destination;
        private readonly List<MovementStep> route = new();
        private readonly List<ClimbZone2D> climbZones = new();
        private readonly List<Collider2D> ignoredWalkableColliders = new();
        private Vector3 visualRootBaseScale = Vector3.one;
        private bool hasDestination;
        private bool movementEnabled = true;
        private float originalGravityScale;
        private int suppressedPointerClickFrame = -1;
        private Vector2 lastStallCheckPosition;
        private float nextStallLogTime;

        public event Action<Vector2> DestinationChanged;
        public event Action ReachedDestination;

        public bool IsMoving { get; private set; }

        public bool HasDestination => hasDestination;

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
            ResolveStats();
        }

        private void Update()
        {
            if (!movementEnabled || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            {
                return;
            }

            if (suppressedPointerClickFrame == Time.frameCount)
            {
                return;
            }

            if (IsPointerBlockedByUi(Mouse.current.position.ReadValue()))
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
            if (!movementEnabled || !hasDestination)
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
                UpdateGravityForStep(currentStep);
                UpdateWalkableCollisionForStep(currentStep);
                MoveBodyForStep(currentStep, nextDestination);
                CompleteCurrentStep();
                return;
            }

            if (TryRecoverMovementStall(currentPosition, currentStep, nextDestination, out Vector2 recoveredPosition))
            {
                currentPosition = recoveredPosition;
                nextDestination = GetStepDestination(currentPosition, currentStep);
                offset = nextDestination - currentPosition;

                if (offset.sqrMagnitude <= stoppingDistance * stoppingDistance)
                {
                    UpdateGravityForStep(currentStep);
                    UpdateWalkableCollisionForStep(currentStep);
                    MoveBodyForStep(currentStep, nextDestination);
                    CompleteCurrentStep();
                    return;
                }
            }

            float speed = currentStep.Type == MovementStepType.Vertical ? climbSpeed : CurrentMoveSpeed;
            Vector2 nextPosition = Vector2.MoveTowards(
                currentPosition,
                nextDestination,
                speed * Time.fixedDeltaTime);

            UpdateFacing(nextPosition.x - currentPosition.x);
            UpdateGravityForStep(currentStep);
            UpdateWalkableCollisionForStep(currentStep);
            MoveBodyForStep(currentStep, nextPosition);
            SetMoving(true);
        }

        public void MoveTo(Vector2 worldPosition)
        {
            if (!movementEnabled)
            {
                return;
            }

            RestoreGravity();
            RestoreWalkableCollisions();
            route.Clear();
            destination = movementPlane == MovementPlane.HorizontalOnly
                ? new Vector2(worldPosition.x, body.position.y)
                : worldPosition;

            hasDestination = true;
            DestinationChanged?.Invoke(destination);
            ResetStallWatch();
            LogMovement($"MoveTo destination={destination}, clicked={worldPosition}, plane={movementPlane}");
        }

        public bool RouteTo(Vector2 worldPosition)
        {
            if (!movementEnabled)
            {
                return false;
            }

            return SetDestinationFromClick(worldPosition, true);
        }

        public void SetMovementEnabled(bool isEnabled)
        {
            movementEnabled = isEnabled;

            if (!movementEnabled)
            {
                Stop();
            }
        }

        public void SuppressCurrentPointerClick()
        {
            suppressedPointerClickFrame = Time.frameCount;
        }

        public void Stop()
        {
            hasDestination = false;
            route.Clear();
            RestoreGravity();
            RestoreWalkableCollisions();
            SetMoving(false);
            LogMovement("Stop called. Cleared destination and route.");
        }

        public void RefreshClimbZones()
        {
            climbZones.Clear();
            climbZones.AddRange(FindObjectsByType<ClimbZone2D>(FindObjectsInactive.Exclude));
        }

        private float CurrentMoveSpeed => stats != null
            ? stats.GetValue(CharacterStatType.MoveSpeed)
            : moveSpeed;

        private void ResolveStats()
        {
            if (stats == null)
            {
                stats = GetComponent<PlayerStats>();
            }

            if (stats == null)
            {
                stats = gameObject.AddComponent<PlayerStats>();
            }
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
                if (TryGetWalkableSurfaceY(clickedWorldPosition, out float nearbySurfaceY)
                    && Mathf.Abs(clickedWorldPosition.y - nearbySurfaceY) <= walkableSurfaceClickTolerance)
                {
                    SetDestinationFromClick(new Vector2(clickedWorldPosition.x, nearbySurfaceY), false);
                    return;
                }

                if (stopWhenClickIsInvalid)
                {
                    Stop();
                }

                return;
            }

            SetDestinationFromClick(clickedWorldPosition, false);
        }

        private bool IsPointerBlockedByUi(Vector2 screenPosition)
        {
            if (!ignoreClicksOverUi || EventSystem.current == null)
            {
                return false;
            }

            if (!blockOnlyInteractiveUi)
            {
                return EventSystem.current.IsPointerOverGameObject();
            }

            PointerEventData pointerEventData = new(EventSystem.current)
            {
                position = screenPosition
            };

            List<RaycastResult> results = new();
            EventSystem.current.RaycastAll(pointerEventData, results);

            foreach (RaycastResult result in results)
            {
                if (result.gameObject != null && result.gameObject.GetComponentInParent<Selectable>() != null)
                {
                    return true;
                }
            }

            return false;
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

        private bool SetDestinationFromClick(Vector2 clickedWorldPosition, bool useCurrentLevelWhenNoSurface)
        {
            bool foundSurface = TryGetWalkableSurfaceY(clickedWorldPosition, out float surfaceY);

            if (!foundSurface)
            {
                surfaceY = useCurrentLevelWhenNoSurface
                    ? GetCurrentSurfaceY()
                    : clickedWorldPosition.y;
            }

            float dismountY = GetDismountY(surfaceY);

            if (!useClimbZones || Mathf.Abs(dismountY - body.position.y) < verticalRouteThreshold)
            {
                MoveTo(clickedWorldPosition);
                return true;
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
                ResetStallWatch();
                LogMovement(
                    $"RouteTo climb destination={destination}, clicked={clickedWorldPosition}, surfaceY={surfaceY:0.###}, dismountY={dismountY:0.###}, route={FormatRoute()}");
                return true;
            }

            if (useCurrentLevelWhenNoSurface && !foundSurface)
            {
                MoveTo(clickedWorldPosition);
                return true;
            }

            if (stopWhenClickIsInvalid)
            {
                Stop();
            }

            LogMovement(
                $"Route failed clicked={clickedWorldPosition}, foundSurface={foundSurface}, surfaceY={surfaceY:0.###}, dismountY={dismountY:0.###}, useCurrentLevelWhenNoSurface={useCurrentLevelWhenNoSurface}");
            return false;
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
            ResetStallWatch();
            LogMovement($"Rope click route destination={destination}, clicked={clickedWorldPosition}, route={FormatRoute()}");
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
                if (climbZone == null)
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
            LogMovement(
                $"Built climb route via {bestZone.name}: current={currentPosition}, currentSurfaceY={currentSurfaceY:0.###}, target={clickedWorldPosition}, surfaceY={surfaceY:0.###}, dismountY={dismountY:0.###}");
            return true;
        }

        private bool IsHeightInsideClimbZone(ClimbZone2D climbZone, float y)
        {
            Bounds bounds = climbZone.ClimbBounds;
            return y >= bounds.min.y - climbZoneHeightTolerance
                && y <= bounds.max.y + climbZoneHeightTolerance;
        }

        private bool TryGetWalkableSurfaceY(Vector2 clickedWorldPosition, out float surfaceY)
        {
            Vector2 rayOrigin = clickedWorldPosition + (Vector2.up * walkableSurfaceProbeHeight);
            RaycastHit2D[] hits = Physics2D.RaycastAll(rayOrigin, Vector2.down, walkableSurfaceProbeDistance, walkableLayers);

            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider != null && !hit.collider.isTrigger)
                {
                    surfaceY = hit.point.y;
                    return true;
                }
            }

            surfaceY = clickedWorldPosition.y;
            return false;
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
            MovementStep completedStep = route.Count > 0
                ? route[0]
                : new MovementStep(destination, movementPlane == MovementPlane.HorizontalOnly ? MovementStepType.Horizontal : MovementStepType.Free);

            if (route.Count > 0)
            {
                route.RemoveAt(0);
            }

            LogMovement($"Completed step {FormatStep(completedStep)}. Remaining route={FormatRoute()}");
            ResetStallWatch();

            if (route.Count == 0)
            {
                hasDestination = false;
                RestoreGravity();
                RestoreWalkableCollisions();
                SetMoving(false);
                LogMovement($"Reached destination {destination}.");
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

            if (step.Type == MovementStepType.Vertical
                || step.Type == MovementStepType.ClimbDismount)
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

        private void SetBodyPosition(Vector2 position)
        {
            body.position = position;
            body.linearVelocity = Vector2.zero;
            transform.position = position;
        }

        private void MoveBodyForStep(MovementStep step, Vector2 position)
        {
            if (step.Type == MovementStepType.Horizontal || step.Type == MovementStepType.ClimbDismount)
            {
                SetBodyPosition(position);
                return;
            }

            body.MovePosition(position);
        }

        private bool TryRecoverMovementStall(Vector2 currentPosition, MovementStep currentStep, Vector2 nextDestination, out Vector2 recoveredPosition)
        {
            recoveredPosition = currentPosition;

            if (Time.time < nextStallLogTime || (!logMovementDebug && !unstickHorizontalStalls))
            {
                return false;
            }

            float movedDistance = Vector2.Distance(currentPosition, lastStallCheckPosition);
            float remainingDistance = Vector2.Distance(currentPosition, nextDestination);
            bool recovered = false;

            if (movedDistance <= stallMoveThreshold && remainingDistance > stoppingDistance)
            {
                if (logMovementDebug)
                {
                    Debug.LogWarning(
                        $"{nameof(PlayerClickMovement2D)} may be stalled. step={FormatStep(currentStep)}, position={currentPosition}, nextDestination={nextDestination}, destination={destination}, remaining={remainingDistance:0.###}, movedSinceLastCheck={movedDistance:0.###}, routeCount={route.Count}, gravity={body.gravityScale:0.###}, ignoredWalkableColliders={ignoredWalkableColliders.Count}, contacts={FormatContacts()}, movementEnabled={movementEnabled}",
                        this);
                }

                if (unstickHorizontalStalls && currentStep.Type == MovementStepType.Horizontal)
                {
                    float direction = Mathf.Sign(nextDestination.x - currentPosition.x);

                    if (!Mathf.Approximately(direction, 0f))
                    {
                        float nudgeDistance = Mathf.Min(Mathf.Max(0.01f, stallUnstickNudgeDistance), remainingDistance);
                        recoveredPosition = currentPosition + Vector2.right * direction * nudgeDistance;
                        body.position = recoveredPosition;
                        recovered = true;

                        if (logMovementDebug)
                        {
                            Debug.LogWarning(
                                $"{nameof(PlayerClickMovement2D)} nudged horizontally by {nudgeDistance:0.###} to recover stall.",
                                this);
                        }
                    }
                }
            }

            lastStallCheckPosition = recoveredPosition;
            nextStallLogTime = Time.time + Mathf.Max(0.1f, stallLogInterval);
            return recovered;
        }

        private void ResetStallWatch()
        {
            if (body == null)
            {
                return;
            }

            lastStallCheckPosition = body.position;
            nextStallLogTime = Time.time + Mathf.Max(0.1f, stallLogInterval);
        }

        private void LogMovement(string message)
        {
            if (logMovementDebug)
            {
                Debug.Log($"{nameof(PlayerClickMovement2D)}: {message}", this);
            }
        }

        private string FormatRoute()
        {
            if (route.Count == 0)
            {
                return "<empty>";
            }

            List<string> steps = new(route.Count);

            foreach (MovementStep step in route)
            {
                steps.Add(FormatStep(step));
            }

            return string.Join(" -> ", steps);
        }

        private static string FormatStep(MovementStep step)
        {
            return $"{step.Type}@{step.Target}";
        }

        private string FormatContacts()
        {
            if (bodyCollider == null)
            {
                return "<no body collider>";
            }

            Collider2D[] contacts = new Collider2D[8];
            int contactCount = bodyCollider.GetContacts(contacts);

            if (contactCount <= 0)
            {
                return "<none>";
            }

            List<string> names = new(contactCount);

            for (int i = 0; i < contactCount; i++)
            {
                Collider2D contact = contacts[i];

                if (contact == null)
                {
                    continue;
                }

                names.Add($"{contact.name}(layer={LayerMask.LayerToName(contact.gameObject.layer)})");
            }

            return names.Count == 0 ? "<none>" : string.Join(", ", names);
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
            float defaultFacingMultiplier = artFacesLeftByDefault ? -1f : 1f;
            visualRoot.localScale = new Vector3(
                Mathf.Abs(visualRootBaseScale.x) * direction * defaultFacingMultiplier,
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
