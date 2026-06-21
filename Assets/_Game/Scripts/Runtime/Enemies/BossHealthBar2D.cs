using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SamsamIdleOn.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class BossHealthBar2D : MonoBehaviour
    {
        [SerializeField] private string bossName = "Final Boss";
        [SerializeField] private Vector2 anchoredPosition = new(0f, -34f);
        [SerializeField] private Vector2 barSize = new(760f, 38f);
        [SerializeField] private Color backgroundColor = new(0.08f, 0.03f, 0.03f, 0.92f);
        [SerializeField] private Color fillColor = new(0.86f, 0.08f, 0.06f, 0.96f);
        [SerializeField] private Color lowHealthColor = new(1f, 0.45f, 0.05f, 0.98f);
        [SerializeField, Range(0f, 1f)] private float lowHealthThreshold = 0.3f;
        [SerializeField] private Color textColor = Color.white;
        [SerializeField, Min(8f)] private float textSize = 24f;
        [SerializeField] private int sortingOrder = 120;

        private EnemyHealth enemyHealth;
        private GameObject root;
        private Image fillImage;
        private TMP_Text valueLabel;

        private void Awake()
        {
            enemyHealth = GetComponent<EnemyHealth>();
            CreateBar();
        }

        private void OnEnable()
        {
            enemyHealth.HealthChanged += HandleHealthChanged;
            HandleHealthChanged(enemyHealth);
        }

        private void OnDisable()
        {
            if (enemyHealth != null)
            {
                enemyHealth.HealthChanged -= HandleHealthChanged;
            }
        }

        private void OnDestroy()
        {
            if (root != null)
            {
                Destroy(root);
            }
        }

        private void CreateBar()
        {
            root = new GameObject("Boss Health UI");

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();

            RectTransform rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect);

            Image background = CreateImage(rootRect, "Background", backgroundColor);
            RectTransform backgroundRect = background.rectTransform;
            backgroundRect.anchorMin = new Vector2(0.5f, 1f);
            backgroundRect.anchorMax = new Vector2(0.5f, 1f);
            backgroundRect.pivot = new Vector2(0.5f, 1f);
            backgroundRect.anchoredPosition = anchoredPosition;
            backgroundRect.sizeDelta = barSize;

            fillImage = CreateImage(backgroundRect, "Fill", fillColor);
            RectTransform fillRect = fillImage.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            valueLabel = CreateText(backgroundRect, "Value Text");
            Stretch(valueLabel.rectTransform);
        }

        private void HandleHealthChanged(EnemyHealth health)
        {
            if (fillImage == null || health.MaxHealth <= 0)
            {
                return;
            }

            float healthPercent = Mathf.Clamp01((float)health.CurrentHealth / health.MaxHealth);
            fillImage.rectTransform.anchorMax = new Vector2(healthPercent, 1f);
            fillImage.color = healthPercent <= lowHealthThreshold ? lowHealthColor : fillColor;

            if (valueLabel != null)
            {
                valueLabel.text = $"{bossName}: {health.CurrentHealth}/{health.MaxHealth}";
            }

            if (root != null)
            {
                root.SetActive(health.IsAlive);
            }
        }

        private static Image CreateImage(RectTransform parent, string objectName, Color color)
        {
            GameObject imageObject = new(objectName);
            imageObject.layer = 5;
            imageObject.transform.SetParent(parent, false);

            Image image = imageObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private TMP_Text CreateText(RectTransform parent, string objectName)
        {
            GameObject textObject = new(objectName);
            textObject.layer = 5;
            textObject.transform.SetParent(parent, false);

            TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.color = textColor;
            label.fontSize = textSize;
            label.fontStyle = FontStyles.Bold;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
