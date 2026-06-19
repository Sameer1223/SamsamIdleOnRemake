using System;
using System.Collections.Generic;
using SamsamIdleOn.Inventory;

namespace SamsamIdleOn.Persistence
{
    [Serializable]
    public sealed class SaveData
    {
        public int schemaVersion = 1;
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
        public string currentActivityId = "idle";
        public string lastSavedUtc = string.Empty;
        public string lastClosedUtc = string.Empty;
        public string lastOfflineRewardsUtc = string.Empty;
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
            offlineFarmTarget ??= new SavedOfflineFarmTargetData();
            offlineFarmTarget.EnsureDefaults();
        }
    }
}
