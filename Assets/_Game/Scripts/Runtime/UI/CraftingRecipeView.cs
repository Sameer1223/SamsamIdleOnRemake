using System.Text;
using SamsamIdleOn.Data;
using SamsamIdleOn.Inventory;
using SamsamIdleOn.Stats;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SamsamIdleOn.UI
{
    public sealed class CraftingRecipeView : MonoBehaviour
    {
        [Header("Recipe")]
        [SerializeField] private CraftingRecipeDefinition recipe;
        [SerializeField] private bool autoFindReferences = true;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private GameDataRegistry dataRegistry;

        [Header("Controls")]
        [SerializeField] private Button craftButton;

        [Header("Labels")]
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text ingredientsLabel;
        [SerializeField] private TMP_Text outputLabel;
        [SerializeField] private TMP_Text statBonusLabel;
        [SerializeField] private TMP_Text feedbackLabel;

        [Header("Options")]
        [SerializeField] private string missingIngredientPrefix = "<color=#FF7777>";
        [SerializeField] private string missingIngredientSuffix = "</color>";

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

            if (craftButton != null)
            {
                craftButton.onClick.RemoveListener(Craft);
            }
        }

        public void Craft()
        {
            ResolveReferences();

            if (inventory == null)
            {
                SetText(feedbackLabel, "Missing inventory.");
                Refresh();
                return;
            }

            if (recipe == null)
            {
                SetText(feedbackLabel, "Missing recipe.");
                Refresh();
                return;
            }

            bool crafted = inventory.Craft(recipe);
            SetText(feedbackLabel, crafted ? "Crafted." : "Missing ingredients.");
            Refresh();
        }

        public void Refresh()
        {
            ResolveReferences();

            if (recipe == null)
            {
                SetText(nameLabel, "Recipe");
                SetText(ingredientsLabel, string.Empty);
                SetText(outputLabel, string.Empty);
                SetText(statBonusLabel, string.Empty);
                SetButtonInteractable(false);
                return;
            }

            SetText(nameLabel, recipe.DisplayName);
            SetText(ingredientsLabel, BuildIngredientsText());
            SetText(outputLabel, BuildOutputText());
            SetText(statBonusLabel, BuildStatBonusText());
            SetButtonInteractable(inventory != null && inventory.CanCraft(recipe));
        }

        private string BuildIngredientsText()
        {
            StringBuilder builder = new();

            foreach (ItemStackRequirement ingredient in recipe.Ingredients)
            {
                if (string.IsNullOrWhiteSpace(ingredient.ItemId))
                {
                    continue;
                }

                int ownedCount = inventory != null ? inventory.GetItemCount(ingredient.ItemId) : 0;
                bool hasEnough = ownedCount >= ingredient.Count;
                string displayName = ResolveItemName(ingredient);
                string line = $"{displayName}: {ownedCount}/{ingredient.Count}";

                if (!hasEnough)
                {
                    line = $"{missingIngredientPrefix}{line}{missingIngredientSuffix}";
                }

                AppendLine(builder, line);
            }

            return builder.ToString();
        }

        private string BuildOutputText()
        {
            if (string.IsNullOrWhiteSpace(recipe.OutputItemId))
            {
                return string.Empty;
            }

            return recipe.OutputCount > 1
                ? $"x{recipe.OutputCount}"
                : string.Empty;
        }

        private string BuildStatBonusText()
        {
            ItemDefinition outputItem = ResolveItemDefinition(recipe.OutputItem, recipe.OutputItemId);

            if (outputItem == null || outputItem.StatBonuses.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new();

            foreach (ItemStatBonus bonus in outputItem.StatBonuses)
            {
                if (bonus == null)
                {
                    continue;
                }

                AppendLine(builder, FormatStatBonus(bonus));
            }

            return builder.ToString();
        }

        private string ResolveItemName(ItemStackRequirement ingredient)
        {
            return ResolveItemName(ingredient.Item, ingredient.ItemId);
        }

        private string ResolveItemName(ItemDefinition directDefinition, string itemId)
        {
            ItemDefinition definition = ResolveItemDefinition(directDefinition, itemId);
            return definition != null ? definition.DisplayName : itemId;
        }

        private ItemDefinition ResolveItemDefinition(ItemDefinition directDefinition, string itemId)
        {
            if (directDefinition != null)
            {
                return directDefinition;
            }

            if (dataRegistry != null && dataRegistry.TryGetDefinition(itemId, out ItemDefinition definition))
            {
                return definition;
            }

            return null;
        }

        private static string FormatStatBonus(ItemStatBonus bonus)
        {
            string value = IsPercentBonus(bonus)
                ? $"{bonus.Value * 100f:0.#}%"
                : FormatNumber(bonus.Value);

            return $"+{value} {GetDisplayName(bonus.Stat)}";
        }

        private static bool IsPercentBonus(ItemStatBonus bonus)
        {
            return bonus.Kind == StatModifierKind.AdditivePercent
                || bonus.Kind == StatModifierKind.MultiplicativePercent
                || bonus.Stat == CharacterStatType.XpGain
                || bonus.Stat == CharacterStatType.CoinGain
                || bonus.Stat == CharacterStatType.AttackSpeed
                || bonus.Stat == CharacterStatType.CritChance
                || bonus.Stat == CharacterStatType.Luck;
        }

        private static string FormatNumber(float value)
        {
            return Mathf.Approximately(value, Mathf.Round(value))
                ? Mathf.RoundToInt(value).ToString()
                : value.ToString("0.##");
        }

        private static string GetDisplayName(CharacterStatType stat)
        {
            return stat switch
            {
                CharacterStatType.MaxHealth => "Health",
                CharacterStatType.HealthRegen => "HP Regen",
                CharacterStatType.MaxMana => "Mana",
                CharacterStatType.ManaRegen => "MP Regen",
                CharacterStatType.AttackSpeed => "Atk Spd",
                CharacterStatType.MoveSpeed => "Move Speed",
                CharacterStatType.CritChance => "Crit Chance",
                CharacterStatType.CritDamage => "Crit Damage",
                CharacterStatType.XpGain => "XP Gain",
                CharacterStatType.CoinGain => "Coin Gain",
                CharacterStatType.TalentPoints => "Talent Points",
                CharacterStatType.AfkGain => "AFK Gain",
                _ => stat.ToString()
            };
        }

        private void HookButton()
        {
            if (craftButton == null)
            {
                craftButton = GetComponentInChildren<Button>();
            }

            if (craftButton == null)
            {
                return;
            }

            craftButton.onClick.RemoveListener(Craft);
            craftButton.onClick.AddListener(Craft);
        }

        private void Subscribe()
        {
            if (inventory != null)
            {
                inventory.InventoryChanged -= HandleInventoryChanged;
                inventory.InventoryChanged += HandleInventoryChanged;
            }
        }

        private void Unsubscribe()
        {
            if (inventory != null)
            {
                inventory.InventoryChanged -= HandleInventoryChanged;
            }
        }

        private void ResolveReferences()
        {
            if (!autoFindReferences)
            {
                return;
            }

            if (inventory == null)
            {
                inventory = FindAnyObjectByType<PlayerInventory>();
            }
        }

        private void HandleInventoryChanged(PlayerInventory changedInventory)
        {
            Refresh();
        }

        private void SetButtonInteractable(bool isInteractable)
        {
            if (craftButton != null)
            {
                craftButton.interactable = isInteractable;
            }
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null)
            {
                label.text = value;
            }
        }

        private static void AppendLine(StringBuilder builder, string value)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append(value);
        }
    }
}
