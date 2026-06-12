using SamsamIdleOn.Characters;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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

        [Header("Player")]
        [SerializeField] private PlayerClickMovement2D playerMovement;
        [SerializeField] private Transform arrivalPoint;
        [SerializeField] private float arrivalDistance = 0.12f;

        [Header("Destination")]
        [SerializeField] private DestinationMode destinationMode = DestinationMode.NextScene;
        [SerializeField] private string sceneName;
        [SerializeField] private bool wrapSceneIndex = false;

        private Collider2D portalCollider;
        private bool waitingForPlayer;

        private Vector2 ArrivalPosition => arrivalPoint != null ? arrivalPoint.position : transform.position;
        private float ArrivalX => ArrivalPosition.x;

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

            if (ignoreClicksOverUi && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
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
            playerMovement.RouteTo(ArrivalPosition);
        }

        private void HandlePlayerReachedDestination()
        {
            if (!waitingForPlayer || playerMovement == null)
            {
                return;
            }

            if (Mathf.Abs(playerMovement.transform.position.x - ArrivalX) > arrivalDistance)
            {
                return;
            }

            waitingForPlayer = false;
            LoadDestinationScene();
        }

        private void LoadDestinationScene()
        {
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
    }
}
