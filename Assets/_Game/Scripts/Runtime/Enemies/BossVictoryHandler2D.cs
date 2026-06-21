using SamsamIdleOn.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SamsamIdleOn.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class BossVictoryHandler2D : MonoBehaviour
    {
        [SerializeField] private string mainSceneName = "Main";
        [SerializeField] private string titleText = "You win!";
        [SerializeField] private string buttonText = "Return to Main";
        [SerializeField] private Color panelColor = new(0f, 0f, 0f, 0.78f);

        private EnemyHealth enemyHealth;
        private bool hasWon;

        private void Awake()
        {
            enemyHealth = GetComponent<EnemyHealth>();
        }

        private void OnEnable()
        {
            enemyHealth.Died += HandleBossDied;
        }

        private void OnDisable()
        {
            if (enemyHealth != null)
            {
                enemyHealth.Died -= HandleBossDied;
            }
        }

        private void HandleBossDied(EnemyHealth _)
        {
            if (hasWon)
            {
                return;
            }

            hasWon = true;

            GameManager gameManager = GameManager.Instance != null
                ? GameManager.Instance
                : FindAnyObjectByType<GameManager>();
            gameManager?.MarkFinalBossDefeated();

            ShowVictoryPrompt();
        }

        private void ShowVictoryPrompt()
        {
            EnsureEventSystem();

            GameObject root = new("Boss Victory UI");
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 250;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();

            RectTransform rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect);

            Image overlay = CreateImage(rootRect, "Overlay", panelColor);
            Stretch(overlay.rectTransform);

            TMP_Text title = CreateText(rootRect, "Title", titleText, 72f, FontStyles.Bold);
            SetRect(title.rectTransform, new Vector2(0f, 92f), new Vector2(700f, 110f));

            Button button = CreateButton(rootRect, "ReturnButton", buttonText, new Vector2(0f, -38f), new Vector2(360f, 72f));
            button.onClick.AddListener(ReturnToMain);
            button.gameObject.AddComponent<ReturnButtonClickFallback>().Configure(button.GetComponent<RectTransform>(), mainSceneName);
        }

        private void ReturnToMain()
        {
            SceneManager.LoadScene(mainSceneName);
        }

        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = EventSystem.current != null
                ? EventSystem.current
                : FindAnyObjectByType<EventSystem>();

            if (eventSystem == null)
            {
                GameObject createdEventSystemObject = new("EventSystem");
                eventSystem = createdEventSystemObject.AddComponent<EventSystem>();
            }

            GameObject eventSystemObject = eventSystem.gameObject;
            eventSystemObject.SetActive(true);
            eventSystem.enabled = true;

            if (eventSystemObject.GetComponent<InputSystemUIInputModule>() == null)
            {
                eventSystemObject.AddComponent<InputSystemUIInputModule>();
            }

            EventSystem.current = eventSystem;
        }

        private static Image CreateImage(RectTransform parent, string objectName, Color color)
        {
            GameObject imageObject = new(objectName);
            imageObject.layer = 5;
            imageObject.transform.SetParent(parent, false);

            Image image = imageObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text CreateText(RectTransform parent, string objectName, string text, float size, FontStyles style)
        {
            GameObject textObject = new(objectName);
            textObject.layer = 5;
            textObject.transform.SetParent(parent, false);

            TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        private static Button CreateButton(RectTransform parent, string objectName, string text, Vector2 position, Vector2 size)
        {
            Image image = CreateImage(parent, objectName, new Color(0.24f, 0.62f, 0.38f, 1f));
            SetRect(image.rectTransform, position, size);

            Button button = image.gameObject.AddComponent<Button>();
            TMP_Text label = CreateText(image.rectTransform, "Label", text, 28f, FontStyles.Bold);
            Stretch(label.rectTransform);
            return button;
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        private static void SetRect(RectTransform rectTransform, Vector2 position, Vector2 size)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = size;
        }

        private sealed class ReturnButtonClickFallback : MonoBehaviour
        {
            private RectTransform buttonRect;
            private string sceneName;
            private bool isLoading;

            public void Configure(RectTransform rectTransform, string targetSceneName)
            {
                buttonRect = rectTransform;
                sceneName = targetSceneName;
            }

            private void Update()
            {
                if (isLoading || buttonRect == null || string.IsNullOrWhiteSpace(sceneName))
                {
                    return;
                }

                if (TryGetPointerPressPosition(out Vector2 screenPosition)
                    && RectTransformUtility.RectangleContainsScreenPoint(buttonRect, screenPosition, null))
                {
                    isLoading = true;
                    SceneManager.LoadScene(sceneName);
                }
            }

            private static bool TryGetPointerPressPosition(out Vector2 screenPosition)
            {
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                {
                    screenPosition = Mouse.current.position.ReadValue();
                    return true;
                }

                if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
                {
                    screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
                    return true;
                }

                screenPosition = default;
                return false;
            }
        }
    }
}
