using UnityEngine;

namespace SamsamIdleOn.Utilities
{
    [DefaultExecutionOrder(-1000)]
    public sealed class WindowAspectRatioGuard : MonoBehaviour
    {
        [Header("Startup")]
        [SerializeField] private bool applyOnAwake = true;
        [SerializeField] private bool forceWindowed = true;
        [SerializeField, Min(1)] private int startupWidth = 1280;
        [SerializeField, Min(1)] private int startupHeight = 720;

        [Header("Aspect Ratio")]
        [SerializeField, Min(1)] private int aspectWidth = 16;
        [SerializeField, Min(1)] private int aspectHeight = 9;
        [SerializeField] private bool enforceWhileRunning = true;
        [SerializeField, Min(0.05f)] private float resizeCheckInterval = 0.2f;
        [SerializeField] private bool enforceInEditor;

        [Header("Minimum Size")]
        [SerializeField, Min(1)] private int minimumWidth = 1280;
        [SerializeField, Min(1)] private int minimumHeight = 720;

        private int lastWidth;
        private int lastHeight;
        private float nextResizeCheckTime;

        private void Awake()
        {
            if (!ShouldRun())
            {
                return;
            }

            if (applyOnAwake)
            {
                SetWindowSize(SnapToAspect(startupWidth, startupHeight, true));
            }

            lastWidth = Screen.width;
            lastHeight = Screen.height;
        }

        private void Update()
        {
            if (!ShouldRun() || !enforceWhileRunning || Time.unscaledTime < nextResizeCheckTime)
            {
                return;
            }

            nextResizeCheckTime = Time.unscaledTime + resizeCheckInterval;

            if (Screen.width == lastWidth && Screen.height == lastHeight)
            {
                return;
            }

            bool widthChangedMore = Mathf.Abs(Screen.width - lastWidth) >= Mathf.Abs(Screen.height - lastHeight);
            Vector2Int correctedSize = SnapToAspect(Screen.width, Screen.height, widthChangedMore);
            SetWindowSize(correctedSize);
        }

        [ContextMenu("Apply Window Settings")]
        public void ApplyWindowSettings()
        {
            SetWindowSize(SnapToAspect(startupWidth, startupHeight, true));
        }

        private bool ShouldRun()
        {
            return !Application.isEditor || enforceInEditor;
        }

        private Vector2Int SnapToAspect(int width, int height, bool preserveWidth)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);

            float aspect = (float)aspectWidth / aspectHeight;

            if (preserveWidth)
            {
                height = Mathf.RoundToInt(width / aspect);
            }
            else
            {
                width = Mathf.RoundToInt(height * aspect);
            }

            if (width < minimumWidth || height < minimumHeight)
            {
                width = minimumWidth;
                height = Mathf.RoundToInt(width / aspect);

                if (height < minimumHeight)
                {
                    height = minimumHeight;
                    width = Mathf.RoundToInt(height * aspect);
                }
            }

            return new Vector2Int(width, height);
        }

        private void SetWindowSize(Vector2Int size)
        {
            FullScreenMode mode = forceWindowed ? FullScreenMode.Windowed : Screen.fullScreenMode;
            Screen.SetResolution(size.x, size.y, mode);
            lastWidth = size.x;
            lastHeight = size.y;
        }
    }
}
