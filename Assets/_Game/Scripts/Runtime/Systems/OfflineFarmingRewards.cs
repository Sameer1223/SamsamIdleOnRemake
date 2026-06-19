using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using SamsamIdleOn.Combat;
using SamsamIdleOn.Core;
using SamsamIdleOn.Inventory;
using SamsamIdleOn.Persistence;
using SamsamIdleOn.Stats;
using TMPro;
using UnityEngine;

namespace SamsamIdleOn.Systems
{
    public sealed class OfflineFarmingRewards : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private PlayerCombatClick2D playerCombat;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private TMP_Text messageLabel;

        [Header("Offline Farming")]
        [SerializeField] private bool applyOnStart = true;
        [SerializeField, Min(0f)] private float minimumOfflineSeconds = 10f;
        [SerializeField, Min(0f)] private float maxOfflineHours = 8f;
        [SerializeField, Min(0.01f)] private float minimumSecondsPerKill = 0.25f;
        [SerializeField, Min(0f)] private float secondsBetweenTargets = 1.5f;

        [Header("Message")]
        [SerializeField, Min(0f)] private float messageSeconds = 5f;

        private Coroutine messageRoutine;
        private bool hasApplied;

        private void Start()
        {
            ResolveReferences();

            if (applyOnStart)
            {
                ApplyOfflineRewards();
            }
        }

        [ContextMenu("Apply Offline Rewards")]
        public void ApplyOfflineRewards()
        {
            ResolveReferences();

            if (hasApplied || gameManager == null || gameManager.SaveData == null)
            {
                return;
            }

            gameManager.Initialize();
            SavedOfflineFarmTargetData target = gameManager.SaveData.offlineFarmTarget;

            if (target == null || !target.IsValid)
            {
                return;
            }

            target.EnsureDefaults();
            string closedTimestamp = gameManager.SaveData.lastClosedUtc;

            if (string.IsNullOrWhiteSpace(closedTimestamp)
                || string.Equals(gameManager.SaveData.lastOfflineRewardsUtc, closedTimestamp, StringComparison.Ordinal))
            {
                return;
            }

            float offlineSeconds = (float)gameManager.OfflineDuration.TotalSeconds;

            if (maxOfflineHours > 0f)
            {
                offlineSeconds = Mathf.Min(offlineSeconds, maxOfflineHours * 3600f);
            }

            if (offlineSeconds < minimumOfflineSeconds)
            {
                gameManager.SaveData.lastOfflineRewardsUtc = closedTimestamp;
                gameManager.SaveProgress();
                hasApplied = true;
                return;
            }

            OfflineRewardResult result = CalculateRewards(target, offlineSeconds);

            if (result.Kills <= 0)
            {
                gameManager.SaveData.lastOfflineRewardsUtc = closedTimestamp;
                gameManager.SaveProgress();
                hasApplied = true;
                return;
            }

            gameManager.AddExperience(result.Experience);
            gameManager.AddBronzeCoins(result.BronzeCoins);

            foreach ((string itemId, int count) in result.ItemRewards)
            {
                inventory?.AddItem(itemId, count);
            }

            gameManager.SaveData.lastOfflineRewardsUtc = closedTimestamp;
            gameManager.SaveProgress();
            hasApplied = true;
            ShowMessage(BuildMessage(target.displayName, offlineSeconds, result));
        }

        private OfflineRewardResult CalculateRewards(SavedOfflineFarmTargetData target, float offlineSeconds)
        {
            float strength = GetStat(CharacterStatType.Strength);
            float xpGain = GetStat(CharacterStatType.XpGain);
            float coinGain = GetStat(CharacterStatType.CoinGain);
            float luck = GetStat(CharacterStatType.Luck);
            float afkGain = GetStat(CharacterStatType.AfkGain);

            float averageDamage = playerCombat != null
                ? playerCombat.GetEffectiveAverageDamage()
                : Mathf.Max(1f, strength);
            float attacksPerSecond = playerCombat != null
                ? playerCombat.GetEffectiveAttacksPerSecond()
                : 1f;
            float dps = averageDamage * attacksPerSecond;
            float secondsPerKill = Mathf.Max(minimumSecondsPerKill, target.enemyHealth / dps + secondsBetweenTargets);
            int kills = Mathf.FloorToInt(offlineSeconds / secondsPerKill * (1f + afkGain));

            float averageExperienceReward = (Mathf.Max(0, target.minExperienceReward) + Mathf.Max(target.minExperienceReward, target.maxExperienceReward)) * 0.5f;
            float averageCoinReward = (Mathf.Max(0, target.minCoinBronzeReward) + Mathf.Max(target.minCoinBronzeReward, target.maxCoinBronzeReward)) * 0.5f;
            long experience = Mathf.Max(0, Mathf.RoundToInt(kills * averageExperienceReward * (1f + xpGain)));
            long bronzeCoins = Mathf.Max(0, Mathf.RoundToInt(kills * averageCoinReward * (1f + coinGain)));
            Dictionary<string, int> itemRewards = new();

            foreach (SavedOfflineDropData drop in target.drops)
            {
                if (drop == null || string.IsNullOrWhiteSpace(drop.itemId))
                {
                    continue;
                }

                int minCount = Mathf.Max(1, drop.minCount);
                int maxCount = Mathf.Max(minCount, drop.maxCount);
                float averageCount = (minCount + maxCount) * 0.5f;
                float chance = Mathf.Clamp01(drop.dropChance + luck);
                int count = Mathf.FloorToInt(kills * chance * averageCount);

                if (count <= 0)
                {
                    continue;
                }

                itemRewards.TryGetValue(drop.itemId, out int currentCount);
                itemRewards[drop.itemId] = currentCount + count;
            }

            return new OfflineRewardResult(kills, experience, bronzeCoins, itemRewards);
        }

        private float GetStat(CharacterStatType stat)
        {
            return playerStats != null ? playerStats.GetValue(stat) : 0f;
        }

        private void ShowMessage(string message)
        {
            if (messageLabel == null)
            {
                Debug.Log(message, this);
                return;
            }

            if (messageRoutine != null)
            {
                StopCoroutine(messageRoutine);
            }

            messageRoutine = StartCoroutine(ShowMessageRoutine(message));
        }

        private IEnumerator ShowMessageRoutine(string message)
        {
            messageLabel.gameObject.SetActive(true);
            messageLabel.text = message;

            if (messageSeconds > 0f)
            {
                yield return new WaitForSeconds(messageSeconds);
                messageLabel.text = string.Empty;
                messageLabel.gameObject.SetActive(false);
            }

            messageRoutine = null;
        }

        private static string BuildMessage(string enemyName, float offlineSeconds, OfflineRewardResult result)
        {
            StringBuilder builder = new();
            builder.AppendLine($"AFK gains from {FormatDuration(offlineSeconds)} away");
            builder.AppendLine($"{result.Kills} {enemyName} defeated");
            builder.AppendLine($"+{result.Experience} XP");
            builder.AppendLine($"+{FormatCoins(result.BronzeCoins)}");

            foreach ((string itemId, int count) in result.ItemRewards)
            {
                builder.AppendLine($"+{count} {itemId}");
            }

            return builder.ToString().TrimEnd();
        }

        private static string FormatDuration(float seconds)
        {
            TimeSpan duration = TimeSpan.FromSeconds(seconds);

            if (duration.TotalHours >= 1d)
            {
                return $"{(int)duration.TotalHours}h {duration.Minutes}m";
            }

            return $"{duration.Minutes}m {duration.Seconds}s";
        }

        private static string FormatCoins(long bronzeAmount)
        {
            long safeAmount = Math.Max(0, bronzeAmount);
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

        private void ResolveReferences()
        {
            if (GameManager.Instance != null)
            {
                gameManager = GameManager.Instance;
            }
            else if (gameManager == null)
            {
                gameManager = FindAnyObjectByType<GameManager>();
            }

            gameManager?.Initialize();

            if (playerStats == null)
            {
                playerStats = FindAnyObjectByType<PlayerStats>();
            }

            if (playerCombat == null)
            {
                playerCombat = FindAnyObjectByType<PlayerCombatClick2D>();
            }

            if (inventory == null)
            {
                inventory = FindAnyObjectByType<PlayerInventory>();
            }
        }

        private readonly struct OfflineRewardResult
        {
            public OfflineRewardResult(int kills, long experience, long bronzeCoins, Dictionary<string, int> itemRewards)
            {
                Kills = kills;
                Experience = experience;
                BronzeCoins = bronzeCoins;
                ItemRewards = itemRewards;
            }

            public int Kills { get; }

            public long Experience { get; }

            public long BronzeCoins { get; }

            public Dictionary<string, int> ItemRewards { get; }
        }
    }
}
