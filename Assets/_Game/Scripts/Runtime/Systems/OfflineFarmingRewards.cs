using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using SamsamIdleOn.Combat;
using SamsamIdleOn.Core;
using SamsamIdleOn.Data;
using SamsamIdleOn.Inventory;
using SamsamIdleOn.Persistence;
using SamsamIdleOn.Stats;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SamsamIdleOn.Systems
{
    public sealed class OfflineFarmingRewards : MonoBehaviour
    {
        private static string appliedClosedTimestampThisSession = string.Empty;

        [Header("References")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private PlayerCombatClick2D playerCombat;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private GameDataRegistry dataRegistry;
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private GameObject messagePanel;

        [Header("Offline Farming")]
        [SerializeField] private bool applyOnStart = true;
        [SerializeField, Min(0f)] private float minimumOfflineSeconds = 10f;
        [SerializeField, Min(0f)] private float maxOfflineHours = 8f;
        [SerializeField, Range(0f, 1f)] private float enemyOfflineEfficiency = 0.45f;
        [SerializeField, Min(0.01f)] private float minimumSecondsPerKill = 1.5f;
        [SerializeField, Min(0f)] private float secondsBetweenTargets = 2f;
        [SerializeField, Min(1f)] private float maxAfkGainMultiplier = 2f;

        [Header("Offline Mining")]
        [SerializeField, Min(0.01f)] private float minimumSecondsPerMiningAction = 0.2f;
        [SerializeField, Range(0f, 1f)] private float miningOfflineEfficiency = 0.55f;
        [SerializeField, Min(0f)] private float miningSpeedPerStrength = 0.01f;
        [SerializeField, Min(0f)] private float miningYieldPerStrength = 0.02f;

        [Header("Message")]
        [SerializeField, Min(0f)] private float messageSeconds = 5f;
        [SerializeField] private bool createMessagePanelIfMissing = true;
        [SerializeField] private Color messagePanelColor = new(0f, 0f, 0f, 0.72f);
        [SerializeField] private Vector2 messagePanelPadding = new(32f, 24f);

        private Coroutine messageRoutine;
        private GameObject generatedMessagePanel;
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
            string closedTimestamp = gameManager.SaveData.lastClosedUtc;

            if (string.IsNullOrWhiteSpace(closedTimestamp)
                || string.Equals(appliedClosedTimestampThisSession, closedTimestamp, StringComparison.Ordinal)
                || string.Equals(gameManager.SaveData.lastOfflineRewardsUtc, closedTimestamp, StringComparison.Ordinal))
            {
                MarkAppliedForSession(closedTimestamp);
                return;
            }

            SavedOfflineFarmTargetData target = gameManager.SaveData.offlineFarmTarget;

            if (target == null || !target.IsValid)
            {
                MarkOfflineRewardsClaimed(closedTimestamp);
                return;
            }

            target.EnsureDefaults();

            float offlineSeconds = (float)gameManager.OfflineDuration.TotalSeconds;

            if (maxOfflineHours > 0f)
            {
                offlineSeconds = Mathf.Min(offlineSeconds, maxOfflineHours * 3600f);
            }

            if (offlineSeconds < minimumOfflineSeconds)
            {
                MarkOfflineRewardsClaimed(closedTimestamp);
                return;
            }

            OfflineRewardResult result = CalculateRewards(target, offlineSeconds);

            if (result.Actions <= 0)
            {
                MarkOfflineRewardsClaimed(closedTimestamp);
                return;
            }

            if (result.Experience > 0)
            {
                gameManager.AddExperience(result.Experience);
            }

            if (result.BronzeCoins > 0)
            {
                gameManager.AddBronzeCoins(result.BronzeCoins);
            }

            foreach ((string itemId, int count) in result.ItemRewards)
            {
                inventory?.AddItem(itemId, count);
            }

            MarkOfflineRewardsClaimed(closedTimestamp);
            ShowMessage(BuildMessage(target, offlineSeconds, result));
        }

        private void MarkOfflineRewardsClaimed(string closedTimestamp)
        {
            if (string.IsNullOrWhiteSpace(closedTimestamp))
            {
                hasApplied = true;
                return;
            }

            gameManager.SaveData.lastOfflineRewardsUtc = closedTimestamp;
            gameManager.SaveProgress();
            MarkAppliedForSession(closedTimestamp);
        }

        private void MarkAppliedForSession(string closedTimestamp)
        {
            if (!string.IsNullOrWhiteSpace(closedTimestamp))
            {
                appliedClosedTimestampThisSession = closedTimestamp;
            }

            hasApplied = true;
        }

        private OfflineRewardResult CalculateRewards(SavedOfflineFarmTargetData target, float offlineSeconds)
        {
            return target.IsMining
                ? CalculateMiningRewards(target, offlineSeconds)
                : CalculateEnemyRewards(target, offlineSeconds);
        }

        private OfflineRewardResult CalculateEnemyRewards(SavedOfflineFarmTargetData target, float offlineSeconds)
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
            int attacksPerKill = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(1, target.enemyHealth) / Mathf.Max(1f, averageDamage)));
            float combatSeconds = attacksPerKill / Mathf.Max(0.01f, attacksPerSecond);
            float secondsPerKill = Mathf.Max(minimumSecondsPerKill, combatSeconds + secondsBetweenTargets);
            float effectiveOfflineSeconds = offlineSeconds
                * Mathf.Clamp01(enemyOfflineEfficiency)
                * GetAfkGainMultiplier(afkGain);
            int kills = Mathf.FloorToInt(effectiveOfflineSeconds / secondsPerKill);

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

        private OfflineRewardResult CalculateMiningRewards(SavedOfflineFarmTargetData target, float offlineSeconds)
        {
            float strength = GetStat(CharacterStatType.Strength);
            float attackSpeed = GetStat(CharacterStatType.AttackSpeed);
            float luck = GetStat(CharacterStatType.Luck);
            float afkGain = GetStat(CharacterStatType.AfkGain);
            float baseSecondsPerAction = target.secondsPerAction > 0f
                ? target.secondsPerAction
                : 1f;
            float speedMultiplier = Mathf.Max(0.01f, attackSpeed)
                + Mathf.Max(0f, strength) * miningSpeedPerStrength;
            float secondsPerAction = Mathf.Max(minimumSecondsPerMiningAction, baseSecondsPerAction / Mathf.Max(0.01f, speedMultiplier));
            float effectiveOfflineSeconds = offlineSeconds
                * Mathf.Clamp01(miningOfflineEfficiency)
                * GetAfkGainMultiplier(afkGain);
            int actions = Mathf.FloorToInt(effectiveOfflineSeconds / secondsPerAction);
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
                int count = Mathf.FloorToInt(actions * chance * averageCount * (1f + Mathf.Max(0f, strength) * miningYieldPerStrength));

                if (count <= 0)
                {
                    continue;
                }

                itemRewards.TryGetValue(drop.itemId, out int currentCount);
                itemRewards[drop.itemId] = currentCount + count;
            }

            return new OfflineRewardResult(actions, 0, 0, itemRewards);
        }

        private float GetStat(CharacterStatType stat)
        {
            return playerStats != null ? playerStats.GetValue(stat) : 0f;
        }

        private float GetAfkGainMultiplier(float afkGain)
        {
            return Mathf.Clamp(1f + Mathf.Max(0f, afkGain), 1f, maxAfkGainMultiplier);
        }

        private void ShowMessage(string message)
        {
            if (messageLabel == null)
            {
                return;
            }

            if (messageRoutine != null)
            {
                StopCoroutine(messageRoutine);
            }

            EnsureMessagePanel();
            SetMessageVisible(true);
            messageRoutine = StartCoroutine(ShowMessageRoutine(message));
        }

        private IEnumerator ShowMessageRoutine(string message)
        {
            messageLabel.text = message;
            ResizeGeneratedMessagePanel();

            if (messageSeconds > 0f)
            {
                yield return new WaitForSeconds(messageSeconds);
                messageLabel.text = string.Empty;
                SetMessageVisible(false);
            }

            messageRoutine = null;
        }

        private void EnsureMessagePanel()
        {
            if (messagePanel != null || !createMessagePanelIfMissing || messageLabel == null)
            {
                return;
            }

            RectTransform labelRectTransform = messageLabel.rectTransform;

            if (labelRectTransform == null || labelRectTransform.parent == null)
            {
                return;
            }

            generatedMessagePanel = new GameObject("Offline Gains Panel");
            generatedMessagePanel.transform.SetParent(labelRectTransform.parent, false);
            generatedMessagePanel.transform.SetSiblingIndex(labelRectTransform.GetSiblingIndex());

            Image panelImage = generatedMessagePanel.AddComponent<Image>();
            panelImage.color = messagePanelColor;
            panelImage.raycastTarget = false;

            RectTransform panelRectTransform = generatedMessagePanel.GetComponent<RectTransform>();
            panelRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            panelRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            panelRectTransform.pivot = new Vector2(0.5f, 0.5f);
            panelRectTransform.sizeDelta = labelRectTransform.sizeDelta + messagePanelPadding;

            messagePanel = generatedMessagePanel;
            messagePanel.SetActive(false);
        }

        private void ResizeGeneratedMessagePanel()
        {
            if (generatedMessagePanel == null || messageLabel == null)
            {
                return;
            }

            messageLabel.ForceMeshUpdate();
            Vector2 preferredSize = messageLabel.GetPreferredValues(messageLabel.text);
            RectTransform panelRectTransform = generatedMessagePanel.GetComponent<RectTransform>();
            Bounds textBounds = messageLabel.textBounds;

            panelRectTransform.sizeDelta = new Vector2(
                Mathf.Max(messageLabel.rectTransform.sizeDelta.x, preferredSize.x) + messagePanelPadding.x,
                Mathf.Max(messageLabel.rectTransform.sizeDelta.y, preferredSize.y) + messagePanelPadding.y);

            if (textBounds.size.sqrMagnitude > 0f)
            {
                panelRectTransform.position = messageLabel.transform.TransformPoint(textBounds.center);
            }
            else
            {
                panelRectTransform.position = messageLabel.rectTransform.position;
            }
        }

        private void SetMessageVisible(bool isVisible)
        {
            if (messagePanel != null)
            {
                messagePanel.SetActive(isVisible);
            }

            if (messageLabel != null)
            {
                messageLabel.gameObject.SetActive(isVisible);
            }
        }

        private string BuildMessage(SavedOfflineFarmTargetData target, float offlineSeconds, OfflineRewardResult result)
        {
            StringBuilder builder = new();
            builder.AppendLine($"AFK gains from {FormatDuration(offlineSeconds)} away");

            if (target.IsMining)
            {
                builder.AppendLine($"{result.Actions} {target.displayName} mining actions");
            }
            else
            {
                builder.AppendLine($"{result.Actions} {target.displayName} defeated");
                builder.AppendLine($"+{result.Experience} XP");
                builder.AppendLine($"+{FormatCoins(result.BronzeCoins)}");
            }

            foreach ((string itemId, int count) in result.ItemRewards)
            {
                builder.AppendLine($"+{count} {ResolveItemDisplayName(itemId)}");
            }

            return builder.ToString().TrimEnd();
        }

        private string ResolveItemDisplayName(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return string.Empty;
            }

            if (dataRegistry != null && dataRegistry.TryGetDefinition(itemId, out ItemDefinition definition))
            {
                return definition.DisplayName;
            }

            return itemId;
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

            if (dataRegistry == null)
            {
                dataRegistry = FindAnyObjectByType<GameDataRegistry>();
            }
        }

        private readonly struct OfflineRewardResult
        {
            public OfflineRewardResult(int actions, long experience, long bronzeCoins, Dictionary<string, int> itemRewards)
            {
                Actions = actions;
                Experience = experience;
                BronzeCoins = bronzeCoins;
                ItemRewards = itemRewards;
            }

            public int Actions { get; }

            public long Experience { get; }

            public long BronzeCoins { get; }

            public Dictionary<string, int> ItemRewards { get; }
        }
    }
}
