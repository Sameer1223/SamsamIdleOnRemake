using SamsamIdleOn.Characters;
using SamsamIdleOn.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SamsamIdleOn.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class ScenePortal2D : MonoBehaviour
    {
        public enum DestinationMode
        {
            NextScene,
            PreviousScene,
            SceneName
        }

        [Header("Input")]
        [SerializeField] private Camera inputCamera;
        [SerializeField] private bool ignoreClicksOverUi = true;
        [SerializeField] private bool blockOnlyInteractiveUi = true;

        [Header("Player")]
        [SerializeField] private PlayerClickMovement2D playerMovement;
        [SerializeField] private Transform arrivalPoint;
        [SerializeField] private float arrivalDistance = 0.12f;
        [SerializeField] private float arrivalHeightDistance = 0.75f;
        [SerializeField] private float colliderArrivalDistance = 0.05f;

        [Header("Spawn")]
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private LayerMask spawnGroundLayers = 1 << 6;
        [SerializeField, Min(0f)] private float spawnGroundProbeHeight = 6f;
        [SerializeField, Min(0f)] private float spawnGroundProbeDistance = 12f;
        [SerializeField, Min(0f)] private float spawnGroundClearance = 0.02f;

        [Header("Destination")]
        [SerializeField] private DestinationMode destinationMode = DestinationMode.NextScene;
        [SerializeField] private string sceneName;
        [SerializeField] private bool wrapSceneIndex = false;

        private Collider2D portalCollider;
        private Collider2D playerCollider;
        private bool waitingForPlayer;

        private Vector2 ArrivalPosition => arrivalPoint != null ? arrivalPoint.position : transform.position;
        private float ArrivalX => ArrivalPosition.x;
        private float ArrivalY => ArrivalPosition.y;

        public Vector2 SpawnPosition => spawnPoint != null ? spawnPoint.position : transform.position;

        public Vector2 GetGroundedSpawnPosition(Collider2D arrivingPlayerCollider)
        {
            Vector2 basePosition = SpawnPosition;
            float playerBottomOffset = GetPlayerBottomOffset(arrivingPlayerCollider);

            if (TryFindGroundY(basePosition, out float groundY))
            {
                return new Vector2(basePosition.x, groundY + playerBottomOffset + spawnGroundClearance);
            }

            return basePosition;
        }

        private void Awake()
        {
            portalCollider = GetComponent<Collider2D>();

            if (inputCamera == null)
            {
                inputCamera = Camera.main;
            }

            if (playerMovement == null)
            {
                playerMovement = FindAnyObjectByType<PlayerClickMovement2D>();
            }

            if (playerMovement != null)
            {
                playerCollider = playerMovement.GetComponent<Collider2D>();
            }
        }

        private void OnEnable()
        {
            if (playerMovement != null)
            {
                playerMovement.ReachedDestination += HandlePlayerReachedDestination;
            }
        }

        private void OnDisable()
        {
            if (playerMovement != null)
            {
                playerMovement.ReachedDestination -= HandlePlayerReachedDestination;
            }
        }

        private void Update()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            {
                return;
            }

            if (IsPointerBlockedByUi(Mouse.current.position.ReadValue()))
            {
                return;
            }

            TryHandleClick(Mouse.current.position.ReadValue());
        }

        private void TryHandleClick(Vector2 screenPosition)
        {
            if (inputCamera == null || playerMovement == null)
            {
                return;
            }

            Vector2 worldPosition = inputCamera.ScreenToWorldPoint(screenPosition);

            if (!portalCollider.OverlapPoint(worldPosition))
            {
                return;
            }

            waitingForPlayer = true;
            playerMovement.SuppressCurrentPointerClick();

            if (!playerMovement.RouteTo(ArrivalPosition))
            {
                waitingForPlayer = false;
            }
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

            System.Collections.Generic.List<RaycastResult> results = new();
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

        private void HandlePlayerReachedDestination()
        {
            if (!waitingForPlayer || playerMovement == null)
            {
                return;
            }

            if (HasPlayerColliderArrived())
            {
                waitingForPlayer = false;
                LoadDestinationScene();
                return;
            }

            if (Mathf.Abs(playerMovement.transform.position.x - ArrivalX) > arrivalDistance)
            {
                return;
            }

            if (Mathf.Abs(playerMovement.transform.position.y - ArrivalY) > arrivalHeightDistance)
            {
                return;
            }

            waitingForPlayer = false;
            LoadDestinationScene();
        }

        private bool HasPlayerColliderArrived()
        {
            if (portalCollider == null)
            {
                return false;
            }

            if (playerCollider == null && playerMovement != null)
            {
                playerCollider = playerMovement.GetComponent<Collider2D>();
            }

            if (playerCollider == null)
            {
                return false;
            }

            ColliderDistance2D distance = portalCollider.Distance(playerCollider);
            return distance.isOverlapped || distance.distance <= colliderArrivalDistance;
        }

        private bool TryFindGroundY(Vector2 basePosition, out float groundY)
        {
            Vector2 rayOrigin = basePosition + Vector2.up * spawnGroundProbeHeight;
            float rayDistance = spawnGroundProbeHeight + spawnGroundProbeDistance;
            RaycastHit2D[] hits = Physics2D.RaycastAll(rayOrigin, Vector2.down, rayDistance, spawnGroundLayers);
            float bestDistance = float.PositiveInfinity;
            groundY = basePosition.y;
            bool foundGround = false;

            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null || hit.collider.isTrigger || hit.distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = hit.distance;
                groundY = hit.point.y;
                foundGround = true;
            }

            return foundGround;
        }

        private static float GetPlayerBottomOffset(Collider2D arrivingPlayerCollider)
        {
            if (arrivingPlayerCollider == null)
            {
                return 0f;
            }

            return Mathf.Max(0f, arrivingPlayerCollider.transform.position.y - arrivingPlayerCollider.bounds.min.y);
        }

        private void LoadDestinationScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            SceneTransitionState.SetPendingSourceScene(activeScene.buildIndex, activeScene.name);
            GameManager.Instance?.SaveProgress(false);

            if (destinationMode == DestinationMode.SceneName)
            {
                if (!string.IsNullOrWhiteSpace(sceneName))
                {
                    SceneManager.LoadScene(sceneName);
                }

                return;
            }

            int sceneCount = SceneManager.sceneCountInBuildSettings;
            int activeIndex = SceneManager.GetActiveScene().buildIndex;
            int offset = destinationMode == DestinationMode.NextScene ? 1 : -1;
            int targetIndex = activeIndex + offset;

            if (wrapSceneIndex && sceneCount > 0)
            {
                targetIndex = (targetIndex + sceneCount) % sceneCount;
            }

            if (targetIndex >= 0 && targetIndex < sceneCount)
            {
                SceneManager.LoadScene(targetIndex);
            }
        }

        public bool TargetsScene(int targetBuildIndex, string targetSceneName)
        {
            if (!TryGetDestinationScene(out int destinationBuildIndex, out string destinationSceneName))
            {
                return false;
            }

            if (targetBuildIndex >= 0 && destinationBuildIndex == targetBuildIndex)
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(targetSceneName)
                && !string.IsNullOrWhiteSpace(destinationSceneName)
                && string.Equals(destinationSceneName, targetSceneName, System.StringComparison.Ordinal);
        }

        private bool TryGetDestinationScene(out int destinationBuildIndex, out string destinationSceneName)
        {
            destinationBuildIndex = -1;
            destinationSceneName = string.Empty;

            if (destinationMode == DestinationMode.SceneName)
            {
                if (string.IsNullOrWhiteSpace(sceneName))
                {
                    return false;
                }

                destinationSceneName = sceneName;
                return true;
            }

            int sceneCount = SceneManager.sceneCountInBuildSettings;
            int activeIndex = SceneManager.GetActiveScene().buildIndex;
            int offset = destinationMode == DestinationMode.NextScene ? 1 : -1;
            int targetIndex = activeIndex + offset;

            if (wrapSceneIndex && sceneCount > 0)
            {
                targetIndex = (targetIndex + sceneCount) % sceneCount;
            }

            if (targetIndex < 0 || targetIndex >= sceneCount)
            {
                return false;
            }

            destinationBuildIndex = targetIndex;
            return true;
        }
    }
}
