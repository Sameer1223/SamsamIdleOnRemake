using System;
using System.Collections;
using SamsamIdleOn.Enemies;
using SamsamIdleOn.Inventory;
using SamsamIdleOn.Persistence;
using SamsamIdleOn.Skills;
using SamsamIdleOn.Stats;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SamsamIdleOn.Core
{
    public sealed class GameManager : MonoBehaviour
    {
        private static GameManager instance;
        private static SaveData sharedSaveData;
        private static TimeSpan sharedOfflineDuration;

        [SerializeField] private bool loadOnAwake = true;
        [SerializeField] private bool saveOnApplicationFocusLost = true;
        [SerializeField] private int baseExperienceToLevel = 100;
        [SerializeField] private float levelExperienceMultiplier = 1.07f;
        [SerializeField, Min(0f)] private float levelExperiencePower = 0.35f;
        [SerializeField, Min(0)] private int talentPointsPerLevel = 1;
        [SerializeField] private string[] preserveOfflineTargetSceneNames = { "Home" };

        private SaveManager saveManager;
        private GameClock clock;
        private bool isInitialized;
        private bool isDuplicate;
        private string pendingSpawnPointId;

        public static GameManager Instance => instance;

        public event Action StateChanged;

        public SaveData SaveData { get; private set; }

        public TimeSpan OfflineDuration { get; private set; }

        public string SavePath => saveManager?.SavePath ?? string.Empty;

        public int PlayerLevel
        {
            get
            {
                EnsureInitialized();
                return SaveData.playerLevel;
            }
        }

        public void SetPendingSpawnPoint(string spawnPointId)
        {
            pendingSpawnPointId = string.IsNullOrWhiteSpace(spawnPointId) ? string.Empty : spawnPointId;
        }

        public bool TryConsumePendingSpawnPointId(out string spawnPointId)
        {
            if (string.IsNullOrWhiteSpace(pendingSpawnPointId))
            {
                spawnPointId = string.Empty;
                return false;
            }

            spawnPointId = pendingSpawnPointId;
            pendingSpawnPointId = string.Empty;
            return true;
        }

        public string ConsumePendingSpawnPointId(string fallbackSpawnPointId = "default")
        {
            string spawnPointId = string.IsNullOrWhiteSpace(pendingSpawnPointId)
                ? fallbackSpawnPointId
                : pendingSpawnPointId;

            pendingSpawnPointId = string.Empty;
            return spawnPointId;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                isDuplicate = true;
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;

            if (loadOnAwake)
            {
                Initialize();
            }
        }

        public void Initialize()
        {
            if (isInitialized)
            {
                return;
            }

            clock = new GameClock();
            saveManager = new SaveManager(Application.persistentDataPath);

            if (sharedSaveData == null)
            {
                sharedSaveData = saveManager.LoadOrCreate(clock.UtcNow);
                sharedOfflineDuration = clock.GetOfflineDuration(sharedSaveData);
            }
            else
            {
                sharedSaveData.EnsureDefaults();
            }

            SaveData = sharedSaveData;
            OfflineDuration = sharedOfflineDuration;
            isInitialized = true;

            StateChanged?.Invoke();
        }

        public void AddExperience(long amount)
        {
            EnsureInitialized();

            if (amount <= 0)
            {
                return;
            }

            SaveData.experience += amount;
            ApplyLevelUps();
            sharedSaveData = SaveData;
            StateChanged?.Invoke();
        }

        public void AddGold(long amount)
        {
            EnsureInitialized();

            if (amount <= 0)
            {
                return;
            }

            SaveData.gold += amount;
            sharedSaveData = SaveData;
            StateChanged?.Invoke();
        }

        public void AddCoins(int goldCoins, int silverCoins, int bronzeCoins)
        {
            long totalBronze = (long)Mathf.Max(0, goldCoins) * 10000L
                + (long)Mathf.Max(0, silverCoins) * 100L
                + Mathf.Max(0, bronzeCoins);

            AddBronzeCoins(totalBronze);
        }

        public void AddBronzeCoins(long bronzeAmount)
        {
            EnsureInitialized();

            if (bronzeAmount <= 0)
            {
                return;
            }

            SaveData.bronzeCoins += bronzeAmount;
            sharedSaveData = SaveData;
            StateChanged?.Invoke();
        }

        public bool TrySpendBronzeCoins(long bronzeAmount)
        {
            EnsureInitialized();

            if (bronzeAmount <= 0)
            {
                return true;
            }

            if (SaveData.bronzeCoins < bronzeAmount)
            {
                return false;
            }

            SaveData.bronzeCoins -= bronzeAmount;
            sharedSaveData = SaveData;
            StateChanged?.Invoke();
            return true;
        }

        public (long gold, long silver, long bronze) GetCoinBreakdown()
        {
            EnsureInitialized();

            long totalBronze = Math.Max(0, SaveData.bronzeCoins);
            long goldCoins = totalBronze / 10000L;
            long silverCoins = totalBronze % 10000L / 100L;
            long bronzeCoins = totalBronze % 100L;

            return (goldCoins, silverCoins, bronzeCoins);
        }

        public string GetFormattedCoins()
        {
            (long goldCoins, long silverCoins, long bronzeCoins) = GetCoinBreakdown();
            return $"{goldCoins} Gold\n{silverCoins} Silver\n{bronzeCoins} Bronze";
        }

        public void AddSavedStatBonus(CharacterStatType stat, float amount)
        {
            AddSavedStatBonus(stat, amount, StatModifierKind.Flat);
        }

        public void AddSavedStatBonus(CharacterStatType stat, float amount, StatModifierKind kind)
        {
            EnsureInitialized();

            if (Mathf.Approximately(amount, 0f))
            {
                return;
            }

            SavedStatBonusData bonus = GetOrCreateSavedStatBonus(stat);

            switch (kind)
            {
                case StatModifierKind.Flat:
                    bonus.flatBonus += amount;
                    break;
                case StatModifierKind.AdditivePercent:
                    bonus.additivePercentBonus += amount;
                    break;
                case StatModifierKind.MultiplicativePercent:
                    bonus.multiplicativePercentBonus += amount;
                    break;
            }

            sharedSaveData = SaveData;
            StateChanged?.Invoke();
        }

        public bool TrySpendSavedStatBonus(CharacterStatType stat, float amount)
        {
            EnsureInitialized();

            if (amount <= 0f)
            {
                return true;
            }

            SavedStatBonusData bonus = GetOrCreateSavedStatBonus(stat);

            if (bonus.flatBonus < amount)
            {
                return false;
            }

            bonus.flatBonus -= amount;
            sharedSaveData = SaveData;
            StateChanged?.Invoke();
            return true;
        }

        public void SetActivity(string activityId)
        {
            EnsureInitialized();
            SaveData.currentActivityId = string.IsNullOrWhiteSpace(activityId) ? "idle" : activityId;
            sharedSaveData = SaveData;
            StateChanged?.Invoke();
        }

        public void RecordEnemyKill(string enemyId)
        {
            EnsureInitialized();
            SaveData.EnsureDefaults();
            string safeEnemyId = NormalizeEnemyId(enemyId);

            if (string.IsNullOrWhiteSpace(safeEnemyId))
            {
                return;
            }

            SavedEnemyKillData killData = GetOrCreateEnemyKillData(safeEnemyId);
            killData.count++;
            sharedSaveData = SaveData;
            StateChanged?.Invoke();
        }

        public int GetEnemyKillCount(string enemyId)
        {
            EnsureInitialized();
            SaveData.EnsureDefaults();
            string safeEnemyId = NormalizeEnemyId(enemyId);

            foreach (SavedEnemyKillData killData in SaveData.enemyKills)
            {
                if (killData != null
                    && string.Equals(NormalizeEnemyId(killData.enemyId), safeEnemyId, StringComparison.OrdinalIgnoreCase))
                {
                    return Mathf.Max(0, killData.count);
                }
            }

            return 0;
        }

        public void SetOfflineFarmTarget(EnemyHealth enemy)
        {
            EnsureInitialized();

            if (enemy == null)
            {
                return;
            }

            EnemyRewards2D rewards = enemy.GetComponent<EnemyRewards2D>();
            EnemyLootTable lootTable = enemy.GetComponent<EnemyLootTable>();
            SavedOfflineFarmTargetData targetData = new()
            {
                targetKind = SavedOfflineFarmTargetData.EnemyTargetKind,
                displayName = enemy.name.Replace("(Clone)", string.Empty).Trim(),
                enemyHealth = enemy.MaxHealth,
                minExperienceReward = rewards != null ? rewards.MinExperienceReward : 0,
                maxExperienceReward = rewards != null ? rewards.MaxExperienceReward : 0,
                minCoinBronzeReward = rewards != null ? rewards.MinCoinBronzeReward : 0,
                maxCoinBronzeReward = rewards != null ? rewards.MaxCoinBronzeReward : 0
            };

            if (lootTable != null)
            {
                foreach (SavedOfflineDropData drop in lootTable.GetOfflineDrops())
                {
                    targetData.drops.Add(drop);
                }
            }

            SaveData.offlineFarmTarget = targetData;
            sharedSaveData = SaveData;
            SaveProgress(false);
            StateChanged?.Invoke();
        }

        public void SetOfflineMiningTarget(OreNode2D ore, float actionsPerSecond)
        {
            EnsureInitialized();

            if (ore == null)
            {
                return;
            }

            SaveData.offlineFarmTarget = ore.CreateOfflineTargetData(actionsPerSecond);
            sharedSaveData = SaveData;
            SaveProgress(false);
            StateChanged?.Invoke();
        }

        public void ClearOfflineFarmTarget()
        {
            EnsureInitialized();
            SaveData.offlineFarmTarget = new SavedOfflineFarmTargetData();
            sharedSaveData = SaveData;
            SaveProgress(false);
            StateChanged?.Invoke();
        }

        public long GetExperienceRequiredForNextLevel()
        {
            EnsureInitialized();
            return GetExperienceRequiredForLevel(SaveData.playerLevel);
        }

        public bool IsFinalBossDefeated()
        {
            EnsureInitialized();
            return SaveData.finalBossDefeated;
        }

        public void MarkFinalBossDefeated()
        {
            EnsureInitialized();

            if (SaveData.finalBossDefeated)
            {
                return;
            }

            SaveData.finalBossDefeated = true;
            sharedSaveData = SaveData;
            SaveProgress(false);
            StateChanged?.Invoke();
        }

        public void SaveProgress(bool markClosed = false)
        {
            EnsureInitialized();
            sharedSaveData = SaveData;
            saveManager.Save(SaveData, clock.UtcNow, markClosed);
            StateChanged?.Invoke();
        }

        [ContextMenu("Reset Save")]
        public void ResetSave()
        {
            EnsureInitialized();
            saveManager.DeleteSave();
            SaveData = global::SamsamIdleOn.Persistence.SaveData.CreateNew(clock.UtcNow);
            sharedSaveData = SaveData;
            sharedOfflineDuration = TimeSpan.Zero;
            OfflineDuration = TimeSpan.Zero;
            StateChanged?.Invoke();
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused && isInitialized)
            {
                SaveProgress(true);
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && saveOnApplicationFocusLost && isInitialized)
            {
                SaveProgress(true);
            }
        }

        private void OnApplicationQuit()
        {
            if (isInitialized)
            {
                SaveProgress(true);
            }
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;

            if (!isDuplicate && isInitialized)
            {
                SaveProgress(false);
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!isDuplicate)
            {
                StartCoroutine(ClearOfflineTargetIfSceneHasNoActivity(scene.name));
            }
        }

        private IEnumerator ClearOfflineTargetIfSceneHasNoActivity(string sceneName)
        {
            yield return null;
            yield return null;

            if (ShouldPreserveOfflineTargetForScene(sceneName))
            {
                yield break;
            }

            if (FindAnyObjectByType<EnemySpawner2D>() != null
                || FindAnyObjectByType<OreNode2D>() != null)
            {
                yield break;
            }

            ClearOfflineFarmTarget();
        }

        private bool ShouldPreserveOfflineTargetForScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName) || preserveOfflineTargetSceneNames == null)
            {
                return false;
            }

            foreach (string preservedSceneName in preserveOfflineTargetSceneNames)
            {
                if (string.Equals(sceneName, preservedSceneName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyLevelUps()
        {
            while (SaveData.experience >= GetExperienceRequiredForLevel(SaveData.playerLevel))
            {
                SaveData.experience -= GetExperienceRequiredForLevel(SaveData.playerLevel);
                SaveData.playerLevel++;
                AddSavedStatBonus(CharacterStatType.TalentPoints, talentPointsPerLevel);
            }
        }

        private SavedStatBonusData GetOrCreateSavedStatBonus(CharacterStatType stat)
        {
            EnsureInitialized();
            SaveData.EnsureDefaults();

            foreach (SavedStatBonusData bonus in SaveData.statBonuses)
            {
                if (bonus != null && bonus.stat == stat)
                {
                    return bonus;
                }
            }

            SavedStatBonusData nextBonus = new()
            {
                stat = stat
            };

            SaveData.statBonuses.Add(nextBonus);
            return nextBonus;
        }

        private SavedEnemyKillData GetOrCreateEnemyKillData(string enemyId)
        {
            foreach (SavedEnemyKillData killData in SaveData.enemyKills)
            {
                if (killData != null
                    && string.Equals(NormalizeEnemyId(killData.enemyId), enemyId, StringComparison.OrdinalIgnoreCase))
                {
                    return killData;
                }
            }

            SavedEnemyKillData nextData = new()
            {
                enemyId = enemyId
            };

            SaveData.enemyKills.Add(nextData);
            return nextData;
        }

        private static string NormalizeEnemyId(string enemyId)
        {
            return string.IsNullOrWhiteSpace(enemyId)
                ? string.Empty
                : enemyId.Replace("(Clone)", string.Empty).Trim();
        }

        private long GetExperienceRequiredForLevel(int level)
        {
            int safeLevel = Mathf.Max(1, level);
            float scaledRequirement = baseExperienceToLevel
                * Mathf.Pow(levelExperienceMultiplier, safeLevel - 1)
                * Mathf.Pow(safeLevel, levelExperiencePower);

            return Math.Max(1, Mathf.RoundToInt(scaledRequirement));
        }

        private void EnsureInitialized()
        {
            if (!isInitialized)
            {
                Initialize();
            }
        }
    }
}
