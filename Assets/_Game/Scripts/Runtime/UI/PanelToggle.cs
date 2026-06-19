using TMPro;
using UnityEngine;

namespace SamsamIdleOn.UI
{
    public sealed class PanelToggle : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private bool showOnStart;

        [Header("Optional Label")]
        [SerializeField] private TMP_Text tmpLabel;
        [SerializeField] private string hiddenText = "Inventory";
        [SerializeField] private string shownText = "Close";

        private void Awake()
        {
            SetVisible(showOnStart);
        }

        public void Toggle()
        {
            SetVisible(panel == null || !panel.activeSelf);
        }

        public void Show()
        {
            SetVisible(true);
        }

        public void Hide()
        {
            SetVisible(false);
        }

        public void SetVisible(bool isVisible)
        {
            if (panel != null)
            {
                panel.SetActive(isVisible);
            }

            RefreshLabel(isVisible);
        }

        private void RefreshLabel(bool isVisible)
        {
            string text = isVisible ? shownText : hiddenText;

            if (tmpLabel != null)
            {
                tmpLabel.text = text;
            }
        }
    }
}
