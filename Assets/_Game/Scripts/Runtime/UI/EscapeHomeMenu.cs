using SamsamIdleOn.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SamsamIdleOn.UI
{
    [DisallowMultipleComponent]
    public sealed class EscapeHomeMenu : MonoBehaviour
    {
        [SerializeField] private string homeSceneName = "Home";
        [SerializeField] private GameManager gameManager;
        [SerializeField] private Color overlayColor = new(0f, 0f, 0f, 0.55f);
        [SerializeField] private Color panelColor = new(0.08f, 0.1f, 0.11f, 0.96f);
        [SerializeField] private Color homeButtonColor = new(0.3f, 0.62f, 0.48f, 1f);
        [SerializeField] private Color resumeButtonColor = new(0.34f, 0.36f, 0.39f, 1f);

        private GameObject menuRoot;
        private bool isOpen;

        private void Awake()
        {
            ResolveGameManager();
        }

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                return;
            }

            if (SceneManager.GetActiveScene().name == homeSceneName)
            {
                return;
            }

            Toggle();
        }

        public void Toggle()
        {
            if (isOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        public void Open()
        {
            EnsureMenuBuilt();
            isOpen = true;
            menuRoot.SetActive(true);
        }

        public void Close()
        {
            isOpen = false;

            if (menuRoot != null)
            {
                menuRoot.SetActive(false);
            }
        }

        public void ReturnHome()
        {
            ResolveGameManager();
            gameManager?.SaveProgress(false);
            SceneManager.LoadScene(homeSceneName);
        }

        private void EnsureMenuBuilt()
        {
            if (menuRoot != null)
            {
                return;
            }

            EnsureEventSystem();

            menuRoot = new GameObject("Escape Home Menu");
            menuRoot.transform.SetParent(transform, false);

            Canvas canvas = menuRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 220;

            CanvasScaler scaler = menuRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            menuRoot.AddComponent<GraphicRaycaster>();

            RectTransform rootRect = menuRoot.GetComponent<RectTransform>();
            Stretch(rootRect);

            Image overlay = CreateImage(rootRect, "Overlay", overlayColor);
            Stretch(overlay.rectTransform);

            Image panel = CreateImage(rootRect, "Panel", panelColor);
            SetRect(panel.rectTransform, Vector2.zero, new Vector2(460f, 300f));

            TMP_Text title = CreateText(panel.rectTransform, "Title", "Menu", 42f, FontStyles.Bold);
            SetRect(title.rectTransform, new Vector2(0f, 86f), new Vector2(360f, 64f));

            Button homeButton = CreateButton(panel.rectTransform, "HomeButton", "Home", new Vector2(0f, 5f), new Vector2(260f, 64f), homeButtonColor);
            homeButton.onClick.AddListener(ReturnHome);

            Button resumeButton = CreateButton(panel.rectTransform, "ResumeButton", "Resume", new Vector2(0f, -75f), new Vector2(260f, 54f), resumeButtonColor);
            resumeButton.onClick.AddListener(Close);

            menuRoot.SetActive(false);
        }

        private void ResolveGameManager()
        {
            if (gameManager == null)
            {
                gameManager = GameManager.Instance != null
                    ? GameManager.Instance
                    : FindAnyObjectByType<GameManager>();
            }
        }

        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = EventSystem.current != null
                ? EventSystem.current
                : FindAnyObjectByType<EventSystem>();

            if (eventSystem == null)
            {
                GameObject eventSystemObject = new("EventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
            }

            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
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

        private static TMP_Text CreateText(RectTransform parent, string objectName, string text, float fontSize, FontStyles fontStyle)
        {
            GameObject textObject = new(objectName);
            textObject.layer = 5;
            textObject.transform.SetParent(parent, false);

            TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        private static Button CreateButton(RectTransform parent, string objectName, string text, Vector2 position, Vector2 size, Color color)
        {
            Image image = CreateImage(parent, objectName, color);
            SetRect(image.rectTransform, position, size);

            Button button = image.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.16f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.16f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            TMP_Text label = CreateText(image.rectTransform, "Label", text, 26f, FontStyles.Bold);
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
    }
}
