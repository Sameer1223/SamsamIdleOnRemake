using System;
using System.Collections.Generic;
using SamsamIdleOn.Core;
using SamsamIdleOn.Persistence;
using UnityEngine;

namespace SamsamIdleOn.Stats
{
    [DisallowMultipleComponent]
    public sealed class PlayerStats : MonoBehaviour
    {
        [Header("Base Stats")]
        [SerializeField] private List<StatDefinition> baseStats = new()
        {
            CreateDefinition(CharacterStatType.MaxHealth, 100f, 1f),
            CreateDefinition(CharacterStatType.HealthRegen, 1f, 0f),
            CreateDefinition(CharacterStatType.MaxMana, 50f, 1f),
            CreateDefinition(CharacterStatType.ManaRegen, 1f, 0f),
            CreateDefinition(CharacterStatType.Strength, 0f, 0f),
            CreateDefinition(CharacterStatType.Agility, 0f, 0f),
            CreateDefinition(CharacterStatType.Luck, 0f, 0f),
            CreateDefinition(CharacterStatType.Accuracy, 0f, 0f),
            CreateDefinition(CharacterStatType.MoveSpeed, 4f, 0.1f),
            CreateDefinition(CharacterStatType.CritChance, 0.05f, 0f),
            CreateDefinition(CharacterStatType.CritDamage, 1.5f, 1f),
            CreateDefinition(CharacterStatType.Defense, 0f, 0f),
            CreateDefinition(CharacterStatType.XpGain, 0f, 0f),
            CreateDefinition(CharacterStatType.CoinGain, 0f, 0f),
            CreateDefinition(CharacterStatType.TalentPoints, 0f, 0f),
            CreateDefinition(CharacterStatType.AfkGain, 0f, 0f)
        };

        [SerializeField] private GameManager gameManager;

        private readonly List<CharacterStatModifier> runtimeModifiers = new();
        private readonly Dictionary<CharacterStatType, StatDefinition> definitionsByType = new();

        public event Action StatsChanged;

        private void Awake()
        {
            RebuildDefinitionLookup();
            ResolveGameManager();
        }

        private void OnEnable()
        {
            ResolveGameManager();

            if (gameManager != null)
            {
                gameManager.StateChanged -= HandleGameStateChanged;
                gameManager.StateChanged += HandleGameStateChanged;
            }
        }

        private void OnDisable()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged -= HandleGameStateChanged;
            }
        }

        private void OnValidate()
        {
            RebuildDefinitionLookup();
        }

        public float GetValue(CharacterStatType stat)
        {
            RebuildDefinitionLookup();

            SavedStatBonusData savedBonus = GetSavedBonus(stat);
            float value = GetBaseValue(stat) + (savedBonus?.flatBonus ?? 0f);
            float additivePercent = savedBonus?.additivePercentBonus ?? 0f;
            float multiplier = 1f + (savedBonus?.multiplicativePercentBonus ?? 0f);

            foreach (CharacterStatModifier modifier in runtimeModifiers)
            {
                if (modifier.Stat != stat)
                {
                    continue;
                }

                switch (modifier.Kind)
                {
                    case StatModifierKind.Flat:
                        value += modifier.Value;
                        break;
                    case StatModifierKind.AdditivePercent:
                        additivePercent += modifier.Value;
                        break;
                    case StatModifierKind.MultiplicativePercent:
                        multiplier *= 1f + modifier.Value;
                        break;
                }
            }

            value *= 1f + additivePercent;
            value *= multiplier;
            return Mathf.Max(GetMinimumValue(stat), value);
        }

        public int GetIntValue(CharacterStatType stat)
        {
            return Mathf.RoundToInt(GetValue(stat));
        }

        public CharacterStatModifier AddModifier(
            CharacterStatType stat,
            float value,
            StatModifierKind kind = StatModifierKind.Flat,
            object source = null)
        {
            CharacterStatModifier modifier = new(stat, value, kind, source);
            runtimeModifiers.Add(modifier);
            StatsChanged?.Invoke();
            return modifier;
        }

        public void RemoveModifier(CharacterStatModifier modifier)
        {
            if (modifier != null && runtimeModifiers.Remove(modifier))
            {
                StatsChanged?.Invoke();
            }
        }

        public void RemoveModifiersFromSource(object source)
        {
            if (source == null)
            {
                return;
            }

            int removedCount = runtimeModifiers.RemoveAll(modifier => ReferenceEquals(modifier.Source, source));

            if (removedCount > 0)
            {
                StatsChanged?.Invoke();
            }
        }

        public void AddSavedFlatBonus(CharacterStatType stat, float amount)
        {
            AddSavedBonus(stat, amount, StatModifierKind.Flat);
        }

        public void AddSavedBonus(CharacterStatType stat, float amount, StatModifierKind kind)
        {
            if (Mathf.Approximately(amount, 0f))
            {
                return;
            }

            SavedStatBonusData bonus = GetOrCreateSavedBonus(stat);

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

            SaveAndNotify();
        }

        public void SetSavedFlatBonus(CharacterStatType stat, float amount)
        {
            SavedStatBonusData bonus = GetOrCreateSavedBonus(stat);
            bonus.flatBonus = amount;
            SaveAndNotify();
        }

        public float GetSavedFlatBonus(CharacterStatType stat)
        {
            return GetSavedBonus(stat)?.flatBonus ?? 0f;
        }

        public float GetSavedAdditivePercentBonus(CharacterStatType stat)
        {
            return GetSavedBonus(stat)?.additivePercentBonus ?? 0f;
        }

        public float GetSavedMultiplicativePercentBonus(CharacterStatType stat)
        {
            return GetSavedBonus(stat)?.multiplicativePercentBonus ?? 0f;
        }

        private SavedStatBonusData GetSavedBonus(CharacterStatType stat)
        {
            ResolveGameManager();

            if (gameManager == null || gameManager.SaveData == null)
            {
                return null;
            }

            gameManager.SaveData.EnsureDefaults();

            foreach (SavedStatBonusData bonus in gameManager.SaveData.statBonuses)
            {
                if (bonus != null && bonus.stat == stat)
                {
                    return bonus;
                }
            }

            return null;
        }

        private SavedStatBonusData GetOrCreateSavedBonus(CharacterStatType stat)
        {
            ResolveGameManager();

            if (gameManager == null)
            {
                throw new InvalidOperationException($"{nameof(PlayerStats)} needs a {nameof(GameManager)} before saving stat bonuses.");
            }

            gameManager.Initialize();
            gameManager.SaveData.EnsureDefaults();

            foreach (SavedStatBonusData bonus in gameManager.SaveData.statBonuses)
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

            gameManager.SaveData.statBonuses.Add(nextBonus);
            return nextBonus;
        }

        private void SaveAndNotify()
        {
            if (gameManager != null)
            {
                gameManager.SaveProgress();
            }

            StatsChanged?.Invoke();
        }

        private float GetBaseValue(CharacterStatType stat)
        {
            return definitionsByType.TryGetValue(stat, out StatDefinition definition)
                ? definition.BaseValue
                : 0f;
        }

        private float GetMinimumValue(CharacterStatType stat)
        {
            return definitionsByType.TryGetValue(stat, out StatDefinition definition)
                ? definition.MinimumValue
                : 0f;
        }

        private void RebuildDefinitionLookup()
        {
            definitionsByType.Clear();

            foreach (StatDefinition definition in baseStats)
            {
                if (definition != null)
                {
                    definitionsByType[definition.Stat] = definition;
                }
            }
        }

        private void ResolveGameManager()
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
        }

        private static StatDefinition CreateDefinition(CharacterStatType stat, float baseValue, float minimumValue)
        {
            return new StatDefinition(stat, baseValue, minimumValue);
        }

        private void HandleGameStateChanged()
        {
            StatsChanged?.Invoke();
        }
    }
}
