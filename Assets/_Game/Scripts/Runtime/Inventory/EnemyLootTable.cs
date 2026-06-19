using System;
using System.Collections.Generic;
using SamsamIdleOn.Enemies;
using SamsamIdleOn.Persistence;
using SamsamIdleOn.Stats;
using UnityEngine;

namespace SamsamIdleOn.Inventory
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class EnemyLootTable : MonoBehaviour
    {
        [Serializable]
        private struct LootEntry
        {
            [SerializeField] private ItemDefinition item;
            [SerializeField] private string itemId;
            [SerializeField, Range(0f, 1f)] private float dropChance;
            [SerializeField, Min(1)] private int minCount;
            [SerializeField, Min(1)] private int maxCount;

            public string ItemId => item != null ? item.Id : itemId;

            public float DropChance => dropChance;

            public int MinCount => Mathf.Max(1, minCount);

            public int MaxCount => Mathf.Max(MinCount, maxCount);

            public int RollCount()
            {
                int min = Mathf.Max(1, minCount);
                int max = Mathf.Max(min, maxCount);
                return UnityEngine.Random.Range(min, max + 1);
            }
        }

        [SerializeField] private PlayerInventory targetInventory;
        [SerializeField] private LootEntry[] drops = Array.Empty<LootEntry>();

        private EnemyHealth health;

        public IReadOnlyList<SavedOfflineDropData> GetOfflineDrops()
        {
            List<SavedOfflineDropData> offlineDrops = new();

            foreach (LootEntry drop in drops)
            {
                if (string.IsNullOrWhiteSpace(drop.ItemId))
                {
                    continue;
                }

                offlineDrops.Add(new SavedOfflineDropData
                {
                    itemId = drop.ItemId,
                    dropChance = drop.DropChance,
                    minCount = drop.MinCount,
                    maxCount = drop.MaxCount
                });
            }

            return offlineDrops;
        }

        private void Awake()
        {
            health = GetComponent<EnemyHealth>();
        }

        private void OnEnable()
        {
            if (health == null)
            {
                health = GetComponent<EnemyHealth>();
            }

            health.Died -= HandleEnemyDied;
            health.Died += HandleEnemyDied;
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Died -= HandleEnemyDied;
            }
        }

        private void HandleEnemyDied(EnemyHealth enemy)
        {
            if (targetInventory == null)
            {
                targetInventory = FindAnyObjectByType<PlayerInventory>();
            }

            if (targetInventory == null)
            {
                return;
            }

            PlayerStats playerStats = targetInventory.GetComponent<PlayerStats>()
                ?? FindAnyObjectByType<PlayerStats>();
            float luckBonus = playerStats != null
                ? playerStats.GetValue(CharacterStatType.Luck)
                : 0f;

            foreach (LootEntry drop in drops)
            {
                float dropChance = Mathf.Clamp01(drop.DropChance + luckBonus);

                if (string.IsNullOrWhiteSpace(drop.ItemId) || UnityEngine.Random.value > dropChance)
                {
                    continue;
                }

                targetInventory.AddItem(drop.ItemId, drop.RollCount());
            }
        }
    }
}
