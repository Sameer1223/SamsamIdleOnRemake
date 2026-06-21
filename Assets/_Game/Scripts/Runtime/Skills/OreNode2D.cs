using SamsamIdleOn.Combat;
using SamsamIdleOn.Core;
using SamsamIdleOn.Inventory;
using SamsamIdleOn.Persistence;
using SamsamIdleOn.Stats;
using UnityEngine;

namespace SamsamIdleOn.Skills
{
    [DisallowMultipleComponent]
    public sealed class OreNode2D : MonoBehaviour, ICombatTarget
    {
        [Header("Display")]
        [SerializeField] private string displayName = "Ore";

        [Header("Gem Drop")]
        [SerializeField] private ItemDefinition gemItem;
        [SerializeField] private string gemItemId = "gems";
        [SerializeField, Range(0f, 1f)] private float dropChance = 0.65f;
        [SerializeField, Min(1)] private int minGemCount = 1;
        [SerializeField, Min(1)] private int maxGemCount = 3;

        [Header("Stat Scaling")]
        [SerializeField, Min(0f)] private float dropChancePerLuck = 0.01f;
        [SerializeField, Min(0f)] private float extraGemCountPerStrength = 0.05f;
        [SerializeField] private bool setOfflineTargetWhenSceneLoads = true;

        public bool IsTargetable => isActiveAndEnabled;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

        public Transform TargetTransform => transform;

        public Component TargetComponent => this;

        public string GemItemId => gemItem != null ? gemItem.Id : gemItemId;

        public float DropChance => dropChance;

        public int MinGemCount => Mathf.Max(1, minGemCount);

        public int MaxGemCount => Mathf.Max(MinGemCount, maxGemCount);

        public float DropChancePerLuck => dropChancePerLuck;

        public float ExtraGemCountPerStrength => extraGemCountPerStrength;

        private void Start()
        {
            if (!setOfflineTargetWhenSceneLoads)
            {
                return;
            }

            PlayerCombatClick2D playerCombat = FindAnyObjectByType<PlayerCombatClick2D>();
            float actionsPerSecond = playerCombat != null
                ? playerCombat.GetEffectiveAttacksPerSecond()
                : 1f;
            GameManager gameManager = GameManager.Instance != null
                ? GameManager.Instance
                : FindAnyObjectByType<GameManager>();

            gameManager?.SetOfflineMiningTarget(this, actionsPerSecond);
        }

        public void ApplyHit(int damage, GameObject attacker)
        {
            string itemId = GemItemId;

            if (string.IsNullOrWhiteSpace(itemId))
            {
                return;
            }

            PlayerStats playerStats = attacker != null
                ? attacker.GetComponent<PlayerStats>()
                : FindAnyObjectByType<PlayerStats>();
            float luck = playerStats != null ? playerStats.GetValue(CharacterStatType.Luck) : 0f;
            float strength = playerStats != null ? playerStats.GetValue(CharacterStatType.Strength) : 0f;
            float effectiveDropChance = Mathf.Clamp01(dropChance + luck * dropChancePerLuck);

            if (Random.value > effectiveDropChance)
            {
                return;
            }

            int baseCount = Random.Range(MinGemCount, MaxGemCount + 1);
            int bonusCount = Mathf.FloorToInt(Mathf.Max(0f, strength) * extraGemCountPerStrength);
            int totalCount = Mathf.Max(1, baseCount + bonusCount);
            PlayerInventory inventory = attacker != null
                ? attacker.GetComponent<PlayerInventory>()
                : FindAnyObjectByType<PlayerInventory>();

            inventory?.AddItem(itemId, totalCount);
        }

        public SavedOfflineFarmTargetData CreateOfflineTargetData(float actionsPerSecond)
        {
            SavedOfflineFarmTargetData targetData = new()
            {
                targetKind = SavedOfflineFarmTargetData.MiningTargetKind,
                displayName = DisplayName,
                enemyHealth = 1,
                secondsPerAction = Mathf.Max(0.05f, 1f / Mathf.Max(0.01f, actionsPerSecond))
            };

            if (!string.IsNullOrWhiteSpace(GemItemId))
            {
                targetData.drops.Add(new SavedOfflineDropData
                {
                    itemId = GemItemId,
                    dropChance = dropChance,
                    minCount = MinGemCount,
                    maxCount = MaxGemCount
                });
            }

            return targetData;
        }

        private void OnValidate()
        {
            if (maxGemCount < minGemCount)
            {
                maxGemCount = minGemCount;
            }
        }
    }
}
