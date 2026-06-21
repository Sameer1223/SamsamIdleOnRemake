using SamsamIdleOn.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SamsamIdleOn.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class PowerStrikeButtonView : MonoBehaviour
    {
        [SerializeField] private PlayerCombatClick2D playerCombat;
        [SerializeField] private bool autoFindPlayerCombat = true;
        [SerializeField] private Image buttonBackground;
        [SerializeField] private Image cooldownOverlay;
        [SerializeField] private TMP_Text cooldownLabel;
        [SerializeField] private Color cooldownColor = new(0.35f, 0.35f, 0.35f, 1f);

        private Button button;
        private Color readyBackgroundColor = Color.white;

        private void Awake()
        {
            button = GetComponent<Button>();

            if (buttonBackground == null)
            {
                buttonBackground = GetComponent<Image>();
            }

            if (buttonBackground != null)
            {
                readyBackgroundColor = buttonBackground.color;
            }

            HookButton();
            ResolvePlayerCombat();
            Refresh();
        }

        private void OnEnable()
        {
            HookButton();
            ResolvePlayerCombat();
            Refresh();
        }

        private void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(ActivatePowerStrike);
            }
        }

        private void Update()
        {
            if (playerCombat == null && autoFindPlayerCombat)
            {
                ResolvePlayerCombat();
            }

            Refresh();
        }

        public void ActivatePowerStrike()
        {
            ResolvePlayerCombat();
            playerCombat?.ActivatePowerStrike();
            Refresh();
        }

        private void HookButton()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            button.onClick.RemoveListener(ActivatePowerStrike);
            button.onClick.AddListener(ActivatePowerStrike);
        }

        private void ResolvePlayerCombat()
        {
            if (!autoFindPlayerCombat)
            {
                return;
            }

            if (playerCombat == null || !playerCombat.isActiveAndEnabled)
            {
                playerCombat = FindAnyObjectByType<PlayerCombatClick2D>();
            }
        }

        private void Refresh()
        {
            float cooldownRemaining = playerCombat != null
                ? playerCombat.PowerStrikeCooldownRemaining
                : 0f;
            float cooldownDuration = playerCombat != null
                ? playerCombat.PowerStrikeCooldownDuration
                : 0f;
            bool isCoolingDown = cooldownRemaining > 0.05f;

            if (buttonBackground != null)
            {
                buttonBackground.color = isCoolingDown ? cooldownColor : readyBackgroundColor;
            }

            if (cooldownOverlay != null)
            {
                cooldownOverlay.gameObject.SetActive(isCoolingDown);
                cooldownOverlay.color = cooldownColor;

                if (cooldownOverlay.type == Image.Type.Filled && cooldownDuration > 0f)
                {
                    cooldownOverlay.fillAmount = Mathf.Clamp01(cooldownRemaining / cooldownDuration);
                }
            }

            if (cooldownLabel != null)
            {
                cooldownLabel.gameObject.SetActive(isCoolingDown);
                cooldownLabel.text = isCoolingDown ? Mathf.CeilToInt(cooldownRemaining).ToString() : string.Empty;
            }
        }
    }
}
