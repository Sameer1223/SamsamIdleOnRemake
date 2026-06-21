using System;
using System.Collections.Generic;
using SamsamIdleOn.Inventory;
using SamsamIdleOn.Stats;

namespace SamsamIdleOn.Persistence
{
    [Serializable]
    public sealed class SaveData
    {
        private const int CurrentSchemaVersion = 3;

        public int schemaVersion = CurrentSchemaVersion;
        public string playerName = "Sameer";
        public int playerLevel = 1;
        public float currentHealth = -1f;
        public float currentMana = -1f;
        public long experience;
        public long gold;
        public long bronzeCoins;
        public List<InventorySlotData> inventory = new();
        public List<InventorySlotData> hotbarItems = new();
        public List<string> hotbarItemIds = new();
        public List<SavedStatBonusData> statBonuses = new();
        public List<SavedUpgradeLevelData> upgradeLevels = new();
        public List<SavedEnemyKillData> enemyKills = new();
        public string currentActivityId = "idle";
        public string lastSavedUtc = string.Empty;
        public string lastClosedUtc = string.Empty;
        public string lastOfflineRewardsUtc = string.Empty;
        public bool finalBossDefeated;
        public SavedOfflineFarmTargetData offlineFarmTarget = new();

        public static SaveData CreateNew(DateTime utcNow)
        {
            string timestamp = utcNow.ToString("O");

            SaveData saveData = new()
            {
                lastSavedUtc = timestamp,
                lastClosedUtc = timestamp
            };

            saveData.EnsureDefaults();
            return saveData;
        }

        public void EnsureDefaults()
        {
            inventory ??= new List<InventorySlotData>();
            hotbarItems ??= new List<InventorySlotData>();
            hotbarItemIds ??= new List<string>();
            statBonuses ??= new List<SavedStatBonusData>();
            upgradeLevels ??= new List<SavedUpgradeLevelData>();
            enemyKills ??= new List<SavedEnemyKillData>();
            offlineFarmTarget ??= new SavedOfflineFarmTargetData();
            offlineFarmTarget.EnsureDefaults();

            if (schemaVersion < 2)
            {
                ConvertLegacyCritChanceBonus();
            }

            if (schemaVersion < 3)
            {
                ConvertZeroBasePercentBonuses();
            }

            schemaVersion = CurrentSchemaVersion;
        }

        private void ConvertLegacyCritChanceBonus()
        {
            foreach (SavedStatBonusData bonus in statBonuses)
            {
                if (bonus == null
                    || bonus.stat != CharacterStatType.CritChance
                    || bonus.additivePercentBonus <= 0f)
                {
                    continue;
                }

                bonus.flatBonus += bonus.additivePercentBonus * 0.5f;
                bonus.additivePercentBonus = 0f;
            }
        }

        private void ConvertZeroBasePercentBonuses()
        {
            foreach (SavedStatBonusData bonus in statBonuses)
            {
                if (bonus == null
                    || bonus.additivePercentBonus <= 0f
                    || !UsesFlatBonusDisplay(bonus.stat))
                {
                    continue;
                }

                bonus.flatBonus += bonus.additivePercentBonus;
                bonus.additivePercentBonus = 0f;
            }
        }

        private static bool UsesFlatBonusDisplay(CharacterStatType stat)
        {
            return stat == CharacterStatType.Luck
                || stat == CharacterStatType.XpGain;
        }
    }
}
