using TMPro;
using UnityEngine;

namespace SamsamIdleOn.Combat
{
    [DisallowMultipleComponent]
    public sealed class FloatingCombatText2D : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField, Min(0.05f)] private float lifetime = 0.75f;
        [SerializeField, Min(0f)] private float riseDistance = 0.75f;
        [SerializeField] private Vector2 randomHorizontalDrift = new(-0.15f, 0.15f);

        private Vector3 startPosition;
        private Vector3 drift;
        private Color startColor = Color.white;
        private float age;

        public static FloatingCombatText2D SpawnDefault(Vector3 position, string text, Color color)
        {
            GameObject textObject = new("Floating Combat Text");
            textObject.transform.position = position;

            TextMeshPro textMesh = textObject.AddComponent<TextMeshPro>();
            textMesh.alignment = TextAlignmentOptions.Center;
            textMesh.fontSize = 3f;
            textMesh.sortingOrder = 100;

            FloatingCombatText2D floatingText = textObject.AddComponent<FloatingCombatText2D>();
            floatingText.label = textMesh;
            floatingText.Play(text, color);
            return floatingText;
        }

        public void Play(string text, Color color)
        {
            ResolveLabel();

            startPosition = transform.position;
            drift = Vector3.right * Random.Range(randomHorizontalDrift.x, randomHorizontalDrift.y);
            startColor = color;
            age = 0f;

            if (label != null)
            {
                label.text = text;
                label.color = color;
            }
        }

        private void Awake()
        {
            ResolveLabel();
        }

        private void Update()
        {
            age += Time.deltaTime;
            float progress = Mathf.Clamp01(age / lifetime);

            transform.position = startPosition
                + Vector3.up * (riseDistance * progress)
                + drift * progress;

            if (label != null)
            {
                Color nextColor = startColor;
                nextColor.a = 1f - progress;
                label.color = nextColor;
            }

            if (age >= lifetime)
            {
                Destroy(gameObject);
            }
        }

        private void LateUpdate()
        {
            Camera mainCamera = Camera.main;

            if (mainCamera != null)
            {
                transform.rotation = mainCamera.transform.rotation;
            }
        }

        private void ResolveLabel()
        {
            if (label == null)
            {
                label = GetComponent<TMP_Text>();
            }
        }
    }
}
