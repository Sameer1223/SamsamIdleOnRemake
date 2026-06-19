using UnityEngine;

namespace SamsamIdleOn.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class EnemyHealthBar2D : MonoBehaviour
    {
        [SerializeField] private Vector2 localOffset = new(0f, 0.75f);
        [SerializeField, Min(0.05f)] private float width = 0.9f;
        [SerializeField, Min(0.02f)] private float height = 0.08f;
        [SerializeField] private Color backgroundColor = new(0.12f, 0.08f, 0.08f, 0.9f);
        [SerializeField] private Color fillColor = new(0.25f, 0.95f, 0.25f, 0.95f);
        [SerializeField] private Color lowHealthColor = new(0.95f, 0.2f, 0.12f, 0.95f);
        [SerializeField, Range(0f, 1f)] private float lowHealthThreshold = 0.3f;
        [SerializeField] private int sortingOrder = 100;

        private static Sprite barSprite;

        private EnemyHealth lifetime;
        private Transform root;
        private Transform fill;
        private SpriteRenderer fillRenderer;

        private void Awake()
        {
            lifetime = GetComponent<EnemyHealth>();
            CreateBar();
        }

        private void OnEnable()
        {
            lifetime.HealthChanged += HandleHealthChanged;
            HandleHealthChanged(lifetime);
        }

        private void OnDisable()
        {
            lifetime.HealthChanged -= HandleHealthChanged;
        }

        private void LateUpdate()
        {
            if (root != null)
            {
                UpdateBarTransform();
            }
        }

        private void CreateBar()
        {
            root = new GameObject("Health Bar").transform;
            root.SetParent(transform, false);
            UpdateBarTransform();

            SpriteRenderer backgroundRenderer = CreatePart("Background", root);
            backgroundRenderer.color = backgroundColor;
            backgroundRenderer.sortingOrder = sortingOrder;
            backgroundRenderer.transform.localScale = new Vector3(width, height, 1f);

            fillRenderer = CreatePart("Fill", root);
            fillRenderer.color = fillColor;
            fillRenderer.sortingOrder = sortingOrder + 1;
            fill = fillRenderer.transform;
        }

        private static SpriteRenderer CreatePart(string objectName, Transform parent)
        {
            GameObject part = new(objectName);
            part.transform.SetParent(parent, false);

            SpriteRenderer spriteRenderer = part.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = GetBarSprite();
            return spriteRenderer;
        }

        private static Sprite GetBarSprite()
        {
            if (barSprite != null)
            {
                return barSprite;
            }

            Texture2D texture = new(1, 1)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            barSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            barSprite.hideFlags = HideFlags.HideAndDontSave;
            return barSprite;
        }

        private void HandleHealthChanged(EnemyHealth enemy)
        {
            if (fill == null || fillRenderer == null || enemy.MaxHealth <= 0)
            {
                return;
            }

            float healthPercent = Mathf.Clamp01((float)enemy.CurrentHealth / enemy.MaxHealth);
            fill.localScale = new Vector3(width * healthPercent, height, 1f);
            fill.localPosition = new Vector3((healthPercent - 1f) * width * 0.5f, 0f, -0.01f);
            fillRenderer.color = healthPercent <= lowHealthThreshold ? lowHealthColor : fillColor;
            root.gameObject.SetActive(enemy.IsAlive);
        }

        private void UpdateBarTransform()
        {
            float parentFacingSign = transform.lossyScale.x < 0f ? -1f : 1f;
            root.localPosition = new Vector3(localOffset.x * parentFacingSign, localOffset.y, 0f);
            root.localScale = new Vector3(parentFacingSign, 1f, 1f);
        }
    }
}
