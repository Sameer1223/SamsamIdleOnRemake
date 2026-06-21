using SamsamIdleOn.Core;
using SamsamIdleOn.Persistence;
using SamsamIdleOn.Stats;
using UnityEngine;

namespace SamsamIdleOn.Upgrades
{
    public enum UpgradeEffectKind
    {
        Auto,
        Flat,
        AdditivePercent,
        MultiplicativePercent
    }

    [CreateAssetMenu(menuName = "Samsam IdleOn/Upgrades/Stat Upgrade", fileName = "UpgradeDefinition")]
    public sealed class UpgradeDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string upgradeId;
        [SerializeField] private string displayName = "Upgrade";
        [SerializeField, TextArea] private string description;

        [Header("Effect")]
        [SerializeField] private CharacterStatType stat;
        [SerializeField] private float statBonusPerLevel = 1f;
        [SerializeField] private UpgradeEffectKind effectKind = UpgradeEffectKind.Auto;

        [Header("Cost")]
        [Tooltip("Costs are authored as total bronze. 140 means 1 silver and 40 bronze.")]
        [SerializeField, Min(0)] private long baseCostBronze = 100;
        [SerializeField, Min(1f)] private float costGrowthMultiplier = 1.25f;
        [Tooltip("0 means unlimited.")]
        [SerializeField, Min(0)] private int maxLevel;

        public string UpgradeId => string.IsNullOrWhiteSpace(upgradeId) ? name : upgradeId;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? UpgradeId : displayName;

        public string Description => description;

        public CharacterStatType Stat => stat;

        public float StatBonusPerLevel => statBonusPerLevel;

        public int MaxLevel => maxLevel;

        public bool HasMaxLevel => maxLevel > 0;

        public int GetLevel(GameManager gameManager)
        {
            SavedUpgradeLevelData data = GetSavedData(gameManager, false);
            return data != null ? Mathf.Max(0, data.level) : 0;
        }

        public long GetCurrentCost(GameManager gameManager)
        {
            int level = GetLevel(gameManager);
            double scaledCost = baseCostBronze * System.Math.Pow(costGrowthMultiplier, level);
            return System.Math.Max(0L, (long)System.Math.Ceiling(scaledCost));
        }

        public bool CanPurchase(GameManager gameManager)
        {
            return CanPurchaseWithCoins(gameManager);
        }

        public bool CanPurchaseWithCoins(GameManager gameManager)
        {
            if (gameManager == null)
            {
                return false;
            }

            gameManager.Initialize();
            int level = GetLevel(gameManager);

            if (HasMaxLevel && level >= maxLevel)
            {
                return false;
            }

            return gameManager.SaveData != null && gameManager.SaveData.bronzeCoins >= GetCurrentCost(gameManager);
        }

        public bool CanPurchaseWithTalentPoints(GameManager gameManager, int talentPointCost = 1)
        {
            if (gameManager == null)
            {
                return false;
            }

            gameManager.Initialize();
            int level = GetLevel(gameManager);

            if (HasMaxLevel && level >= maxLevel)
            {
                return false;
            }

            return gameManager.SaveData != null
                && GetTalentPointBalance(gameManager) >= Mathf.Max(1, talentPointCost);
        }

        public bool TryPurchase(GameManager gameManager, PlayerStats playerStats, out string resultMessage)
        {
            resultMessage = string.Empty;

            if (gameManager == null)
            {
                resultMessage = "Missing GameManager.";
                return false;
            }

            if (playerStats == null)
            {
                resultMessage = "Missing PlayerStats.";
                return false;
            }

            gameManager.Initialize();
            SavedUpgradeLevelData data = GetSavedData(gameManager, true);

            if (data == null)
            {
                resultMessage = "Could not create upgrade save data.";
                return false;
            }

            if (HasMaxLevel && data.level >= maxLevel)
            {
                resultMessage = $"{DisplayName} is maxed.";
                return false;
            }

            long cost = GetCurrentCost(gameManager);

            if (!gameManager.TrySpendBronzeCoins(cost))
            {
                resultMessage = $"Need {FormatCoins(cost)}.";
                return false;
            }

            data.level++;
            playerStats.AddSavedBonus(stat, statBonusPerLevel, GetModifierKind());
            resultMessage = $"{DisplayName} upgraded to Lv. {data.level}.";
            return true;
        }

        public bool TryPurchaseWithTalentPoints(
            GameManager gameManager,
            PlayerStats playerStats,
            int talentPointCost,
            out string resultMessage)
        {
            resultMessage = string.Empty;

            if (gameManager == null)
            {
                resultMessage = "Missing GameManager.";
                return false;
            }

            if (playerStats == null)
            {
                resultMessage = "Missing PlayerStats.";
                return false;
            }

            gameManager.Initialize();
            SavedUpgradeLevelData data = GetSavedData(gameManager, true);

            if (data == null)
            {
                resultMessage = "Could not create talent save data.";
                return false;
            }

            if (HasMaxLevel && data.level >= maxLevel)
            {
                resultMessage = $"{DisplayName} is maxed.";
                return false;
            }

            int safeCost = Mathf.Max(1, talentPointCost);

            if (!gameManager.TrySpendSavedStatBonus(CharacterStatType.TalentPoints, safeCost))
            {
                resultMessage = $"Need {safeCost} Talent Point{(safeCost == 1 ? string.Empty : "s")}.";
                return false;
            }

            data.level++;
            playerStats.AddSavedBonus(stat, statBonusPerLevel, GetModifierKind());
            resultMessage = $"{DisplayName} upgraded to Lv. {data.level}.";
            return true;
        }

        public string GetEffectText()
        {
            return $"+{FormatEffectValue(statBonusPerLevel, GetModifierKind(), stat)} {GetDisplayName(stat)}";
        }

        public string GetLevelText(GameManager gameManager)
        {
            int level = GetLevel(gameManager);
            return HasMaxLevel ? $"Lv. {level}/{maxLevel}" : $"Lv. {level}";
        }

        public string GetCostText(GameManager gameManager)
        {
            if (HasMaxLevel && GetLevel(gameManager) >= maxLevel)
            {
                return "Max";
            }

            return FormatCoins(GetCurrentCost(gameManager));
        }

        public string GetTalentPointCostText(GameManager gameManager, int talentPointCost = 1)
        {
            if (HasMaxLevel && GetLevel(gameManager) >= maxLevel)
            {
                return "Max";
            }

            int safeCost = Mathf.Max(1, talentPointCost);
            return $"{safeCost} Talent Point{(safeCost == 1 ? string.Empty : "s")}";
        }

        private SavedUpgradeLevelData GetSavedData(GameManager gameManager, bool createIfMissing)
        {
            if (gameManager == null)
            {
                return null;
            }

            gameManager.Initialize();

            if (gameManager.SaveData == null)
            {
                return null;
            }

            gameManager.SaveData.EnsureDefaults();

            foreach (SavedUpgradeLevelData data in gameManager.SaveData.upgradeLevels)
            {
                if (data != null && data.upgradeId == UpgradeId)
                {
                    return data;
                }
            }

            if (!createIfMissing)
            {
                return null;
            }

            SavedUpgradeLevelData nextData = new()
            {
                upgradeId = UpgradeId
            };

            gameManager.SaveData.upgradeLevels.Add(nextData);
            return nextData;
        }

        private static string FormatCoins(long bronzeAmount)
        {
            long safeAmount = System.Math.Max(0, bronzeAmount);
            long gold = safeAmount / 10000L;
            long silver = safeAmount % 10000L / 100L;
            long bronze = safeAmount % 100L;

            if (gold > 0)
            {
                return $"{gold}g {silver}s {bronze}b";
            }

            if (silver > 0)
            {
                return $"{silver}s {bronze}b";
            }

            return $"{bronze}b";
        }

        private static float GetTalentPointBalance(GameManager gameManager)
        {
            gameManager.SaveData.EnsureDefaults();

            foreach (SavedStatBonusData bonus in gameManager.SaveData.statBonuses)
            {
                if (bonus != null && bonus.stat == CharacterStatType.TalentPoints)
                {
                    return bonus.flatBonus;
                }
            }

            return 0f;
        }

        private static string FormatNumber(float value)
        {
            return Mathf.Approximately(value, Mathf.Round(value))
                ? Mathf.RoundToInt(value).ToString()
                : value.ToString("0.##");
        }

        private StatModifierKind GetModifierKind()
        {
            return effectKind switch
            {
                UpgradeEffectKind.Flat => StatModifierKind.Flat,
                UpgradeEffectKind.AdditivePercent => StatModifierKind.AdditivePercent,
                UpgradeEffectKind.MultiplicativePercent => StatModifierKind.MultiplicativePercent,
                _ => GetAutomaticModifierKind(stat)
            };
        }

        private static StatModifierKind GetAutomaticModifierKind(CharacterStatType stat)
        {
            return stat switch
            {
                CharacterStatType.MoveSpeed => StatModifierKind.AdditivePercent,
                CharacterStatType.AttackSpeed => StatModifierKind.AdditivePercent,
                _ => StatModifierKind.Flat
            };
        }

        private static string FormatEffectValue(float value, StatModifierKind kind, CharacterStatType stat)
        {
            if (kind == StatModifierKind.AdditivePercent || kind == StatModifierKind.MultiplicativePercent || IsPercentDisplayStat(stat))
            {
                return $"{value * 100f:0.#}%";
            }

            return FormatNumber(value);
        }

        private static bool IsPercentDisplayStat(CharacterStatType stat)
        {
            return stat == CharacterStatType.XpGain
                || stat == CharacterStatType.CoinGain
                || stat == CharacterStatType.AttackSpeed
                || stat == CharacterStatType.CritChance
                || stat == CharacterStatType.Luck
                || stat == CharacterStatType.AfkGain;
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

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(upgradeId))
            {
                upgradeId = name;
            }

            if (costGrowthMultiplier < 1f)
            {
                costGrowthMultiplier = 1f;
            }
        }
    }
}
