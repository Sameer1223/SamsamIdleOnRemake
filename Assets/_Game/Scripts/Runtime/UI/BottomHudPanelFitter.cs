using UnityEngine;

namespace SamsamIdleOn.UI
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class BottomHudPanelFitter : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxHeight = 132f;
        [SerializeField, Range(0.01f, 0.5f)] private float screenHeightPercent = 0.16f;
        [SerializeField] private bool capByScreenPercent = true;

        private RectTransform rectTransform;

        private void OnEnable()
        {
            ApplyLayout();
        }

        private void OnValidate()
        {
            ApplyLayout();
        }

        private void LateUpdate()
        {
            ApplyLayout();
        }

        [ContextMenu("Use Current Height As Max")]
        public void UseCurrentHeightAsMax()
        {
            EnsureReferences();
            maxHeight = Mathf.Max(1f, rectTransform.rect.height);
            ApplyLayout();
        }

        private void ApplyLayout()
        {
            EnsureReferences();

            float targetHeight = maxHeight;

            if (capByScreenPercent)
            {
                float canvasScale = GetCanvasScale();
                float percentHeight = (Screen.height * screenHeightPercent) / canvasScale;
                targetHeight = Mathf.Min(maxHeight, percentHeight);
            }

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.right;
            rectTransform.pivot = new Vector2(0.5f, 0f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = new Vector2(0f, targetHeight);
        }

        private void EnsureReferences()
        {
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }
        }

        private float GetCanvasScale()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            return canvas == null ? 1f : Mathf.Max(0.0001f, canvas.scaleFactor);
        }
    }
}
