using System;
using SamsamIdleOn.Persistence;
using UnityEngine;

namespace SamsamIdleOn.Core
{
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] private bool loadOnAwake = true;
        [SerializeField] private bool saveOnApplicationFocusLost = true;
        [SerializeField] private int baseExperienceToLevel = 100;
        [SerializeField] private float levelExperienceMultiplier = 1.35f;

        private SaveManager saveManager;
        private GameClock clock;
        private bool isInitialized;

        public event Action StateChanged;

        public SaveData SaveData { get; private set; }

        public TimeSpan OfflineDuration { get; private set; }

        public string SavePath => saveManager?.SavePath ?? string.Empty;

        private void Awake()
        {
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
            SaveData = saveManager.LoadOrCreate(clock.UtcNow);
            OfflineDuration = clock.GetOfflineDuration(SaveData);
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
            StateChanged?.Invoke();
        }

        public void SetActivity(string activityId)
        {
            EnsureInitialized();
            SaveData.currentActivityId = string.IsNullOrWhiteSpace(activityId) ? "idle" : activityId;
            StateChanged?.Invoke();
        }

        public long GetExperienceRequiredForNextLevel()
        {
            EnsureInitialized();
            return GetExperienceRequiredForLevel(SaveData.playerLevel);
        }

        public void SaveProgress(bool markClosed = false)
        {
            EnsureInitialized();
            saveManager.Save(SaveData, clock.UtcNow, markClosed);
            StateChanged?.Invoke();
        }

        public void ResetSave()
        {
            EnsureInitialized();
            saveManager.DeleteSave();
            SaveData = global::SamsamIdleOn.Persistence.SaveData.CreateNew(clock.UtcNow);
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
            if (isInitialized)
            {
                SaveProgress(false);
            }
        }

        private void ApplyLevelUps()
        {
            while (SaveData.experience >= GetExperienceRequiredForLevel(SaveData.playerLevel))
            {
                SaveData.experience -= GetExperienceRequiredForLevel(SaveData.playerLevel);
                SaveData.playerLevel++;
            }
        }

        private long GetExperienceRequiredForLevel(int level)
        {
            int safeLevel = Mathf.Max(1, level);
            float scaledRequirement = baseExperienceToLevel * Mathf.Pow(levelExperienceMultiplier, safeLevel - 1);
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
