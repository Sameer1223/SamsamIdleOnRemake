using SamsamIdleOn.Core;
using SamsamIdleOn.Stats;
using SamsamIdleOn.Upgrades;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SamsamIdleOn.UI
{
    public enum UpgradePurchaseCurrency
    {
        Coins,
        TalentPoints
    }

    public sealed class UpgradeButtonView : MonoBehaviour
    {
        [Header("Upgrade")]
        [SerializeField] private UpgradeDefinition upgrade;
        [SerializeField] private UpgradePurchaseCurrency purchaseCurrency = UpgradePurchaseCurrency.Coins;
        [SerializeField, Min(1)] private int talentPointCost = 1;
        [SerializeField] private bool autoFindReferences = true;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private PlayerStats playerStats;

        [Header("Controls")]
        [SerializeField] private Button purchaseButton;

        [Header("Labels")]
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private TMP_Text effectLabel;
        [SerializeField] private TMP_Text costLabel;
        [SerializeField] private TMP_Text feedbackLabel;

        private void Awake()
        {
            ResolveReferences();
            HookButton();
            Refresh();
        }

        private void OnEnable()
        {
            ResolveReferences();
            HookButton();
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();

            if (purchaseButton != null)
            {
                purchaseButton.onClick.RemoveListener(Purchase);
            }
        }

        public void Purchase()
        {
            ResolveReferences();

            if (upgrade == null)
            {
                SetText(feedbackLabel, "Missing upgrade.");
                Refresh();
                return;
            }

            string message;

            if (purchaseCurrency == UpgradePurchaseCurrency.TalentPoints)
            {
                upgrade.TryPurchaseWithTalentPoints(gameManager, playerStats, talentPointCost, out message);
            }
            else
            {
                upgrade.TryPurchase(gameManager, playerStats, out message);
            }

            SetText(feedbackLabel, message);

            Refresh();
        }

        public void Refresh()
        {
            ResolveReferences();

            if (upgrade == null)
            {
                SetText(nameLabel, "Upgrade");
                SetText(descriptionLabel, string.Empty);
                SetText(levelLabel, string.Empty);
                SetText(effectLabel, string.Empty);
                SetText(costLabel, string.Empty);
                SetButtonInteractable(false);
                return;
            }

            SetText(nameLabel, upgrade.DisplayName);
            SetText(descriptionLabel, upgrade.Description);
            SetText(levelLabel, upgrade.GetLevelText(gameManager));
            SetText(effectLabel, upgrade.GetEffectText());
            SetText(costLabel, GetCostText());
            SetButtonInteractable(CanPurchase());
        }

        private string GetCostText()
        {
            return purchaseCurrency == UpgradePurchaseCurrency.TalentPoints
                ? upgrade.GetTalentPointCostText(gameManager, talentPointCost)
                : upgrade.GetCostText(gameManager);
        }

        private bool CanPurchase()
        {
            return purchaseCurrency == UpgradePurchaseCurrency.TalentPoints
                ? upgrade.CanPurchaseWithTalentPoints(gameManager, talentPointCost)
                : upgrade.CanPurchase(gameManager);
        }

        private void HookButton()
        {
            if (purchaseButton == null)
            {
                purchaseButton = GetComponentInChildren<Button>();
            }

            if (purchaseButton == null)
            {
                return;
            }

            purchaseButton.onClick.RemoveListener(Purchase);
            purchaseButton.onClick.AddListener(Purchase);
        }

        private void Subscribe()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged -= Refresh;
                gameManager.StateChanged += Refresh;
            }

            if (playerStats != null)
            {
                playerStats.StatsChanged -= Refresh;
                playerStats.StatsChanged += Refresh;
            }
        }

        private void Unsubscribe()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged -= Refresh;
            }

            if (playerStats != null)
            {
                playerStats.StatsChanged -= Refresh;
            }
        }

        private void ResolveReferences()
        {
            if (!autoFindReferences)
            {
                return;
            }

            if (GameManager.Instance != null)
            {
                gameManager = GameManager.Instance;
            }
            else if (gameManager == null)
            {
                gameManager = FindAnyObjectByType<GameManager>();
            }

            if (playerStats == null)
            {
                playerStats = FindAnyObjectByType<PlayerStats>();
            }
        }

        private void SetButtonInteractable(bool isInteractable)
        {
            if (purchaseButton != null)
            {
                purchaseButton.interactable = isInteractable;
            }
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null)
            {
                label.text = value;
            }
        }
    }
}
