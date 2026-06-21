using SamsamIdleOn.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SamsamIdleOn.UI
{
    public sealed class HomeScreenController : MonoBehaviour
    {
        [SerializeField] private string mainSceneName = "Main";
        [SerializeField] private GameManager gameManager;
        [SerializeField] private Button playButton;
        [SerializeField] private Button resetSaveButton;
        [SerializeField] private TMP_Text statusLabel;

        private void Awake()
        {
            BuildDefaultUiIfNeeded();
            ResolveGameManager();
            HookButtons();
        }

        private void OnDestroy()
        {
            if (playButton != null)
            {
                playButton.onClick.RemoveListener(Play);
            }

            if (resetSaveButton != null)
            {
                resetSaveButton.onClick.RemoveListener(ResetSave);
            }
        }

        public void Play()
        {
            if (gameManager != null)
            {
                gameManager.SaveProgress(false);
            }

            SceneManager.LoadScene(mainSceneName);
        }

        public void ResetSave()
        {
            ResolveGameManager();

            if (gameManager == null)
            {
                SetStatus("No GameManager found.");
                return;
            }

            gameManager.ResetSave();
            SetStatus("Save reset.");
        }

        private void HookButtons()
        {
            if (playButton != null)
            {
                playButton.onClick.RemoveListener(Play);
                playButton.onClick.AddListener(Play);
            }

            if (resetSaveButton != null)
            {
                resetSaveButton.onClick.RemoveListener(ResetSave);
                resetSaveButton.onClick.AddListener(ResetSave);
            }
        }

        private void ResolveGameManager()
        {
            if (gameManager != null)
            {
                return;
            }

            gameManager = GameManager.Instance != null
                ? GameManager.Instance
                : FindAnyObjectByType<GameManager>();
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
            }
        }

        private void BuildDefaultUiIfNeeded()
        {
            if (playButton != null && resetSaveButton != null)
            {
                return;
            }

            EnsureEventSystem();

            GameObject canvasObject = new("HomeScreenCanvas");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            Stretch(canvasRect);

            Image background = CreateImage(canvasRect, "Background", new Color(0.08f, 0.14f, 0.18f, 1f));
            Stretch(background.rectTransform);

            Image panel = CreateImage(canvasRect, "MenuPanel", new Color(0.06f, 0.08f, 0.09f, 0.92f));
            RectTransform panelRect = panel.rectTransform;
            SetRect(panelRect, Vector2.zero, new Vector2(620f, 560f));

            TMP_Text titleLabel = CreateText(panelRect, "TitleLabel", "Samsam IdleOn", 58f, FontStyles.Bold, new Color(0.98f, 0.94f, 0.72f, 1f));
            SetRect(titleLabel.rectTransform, new Vector2(0f, 166f), new Vector2(540f, 92f));

            TMP_Text nameLabel = CreateText(panelRect, "NameLabel", "Sameer", 30f, FontStyles.Normal, new Color(0.72f, 0.9f, 0.95f, 1f));
            SetRect(nameLabel.rectTransform, new Vector2(0f, 104f), new Vector2(540f, 48f));

            TMP_Text subtitleLabel = CreateText(panelRect, "SubtitleLabel", "Early-game idle adventure demo", 24f, FontStyles.Normal, new Color(0.78f, 0.82f, 0.8f, 1f));
            SetRect(subtitleLabel.rectTransform, new Vector2(0f, 55f), new Vector2(540f, 42f));

            playButton = CreateButton(panelRect, "PlayButton", "Play", new Vector2(0f, -45f), new Vector2(360f, 76f), new Color(0.32f, 0.72f, 0.52f, 1f));
            resetSaveButton = CreateButton(panelRect, "ResetSaveButton", "Reset Save", new Vector2(0f, -137f), new Vector2(360f, 58f), new Color(0.45f, 0.48f, 0.52f, 1f));

            statusLabel = CreateText(panelRect, "StatusLabel", string.Empty, 18f, FontStyles.Normal, new Color(0.82f, 0.86f, 0.84f, 1f));
            SetRect(statusLabel.rectTransform, new Vector2(0f, -214f), new Vector2(480f, 36f));

            TMP_Text devNoteLabel = CreateText(panelRect, "DevNoteLabel", "Reset Save is for development testing.", 16f, FontStyles.Normal, new Color(0.6f, 0.65f, 0.64f, 1f));
            SetRect(devNoteLabel.rectTransform, new Vector2(0f, -250f), new Vector2(480f, 30f));
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystemObject = new("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
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

        private static TMP_Text CreateText(RectTransform parent, string objectName, string text, float fontSize, FontStyles fontStyle, Color color)
        {
            GameObject textObject = new(objectName);
            textObject.layer = 5;
            textObject.transform.SetParent(parent, false);

            TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }

        private static Button CreateButton(RectTransform parent, string objectName, string labelText, Vector2 position, Vector2 size, Color color)
        {
            Image buttonImage = CreateImage(parent, objectName, color);
            SetRect(buttonImage.rectTransform, position, size);

            Button button = buttonImage.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.16f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.16f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.25f, 0.25f, 0.25f, 0.5f);
            button.colors = colors;

            TMP_Text label = CreateText(buttonImage.rectTransform, "Label", labelText, 28f, FontStyles.Bold, Color.white);
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
