using System;
using SamsamIdleOn.Core;
using SamsamIdleOn.Stats;
using UnityEngine;

namespace SamsamIdleOn.Characters
{
    [DisallowMultipleComponent]
    public sealed class PlayerResources : MonoBehaviour
    {
        [Header("Mana")]
        [SerializeField, Min(1)] private int maxMana = 50;
        [SerializeField, Min(0f)] private float manaRegenPerSecond = 1f;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private PlayerStats stats;

        public event Action<PlayerResources> ManaChanged;

        public float CurrentMana { get; private set; }

        public int MaxMana => stats != null
            ? stats.GetIntValue(CharacterStatType.MaxMana)
            : maxMana;

        public float ManaRegenPerSecond
        {
            get => stats != null
                ? stats.GetValue(CharacterStatType.ManaRegen)
                : manaRegenPerSecond;
            set => manaRegenPerSecond = Mathf.Max(0f, value);
        }

        private void Awake()
        {
            ResolveGameManager();
            ResolveStats();
            LoadResources();
        }

        private void OnEnable()
        {
            ResolveStats();

            if (stats != null)
            {
                stats.StatsChanged -= HandleStatsChanged;
                stats.StatsChanged += HandleStatsChanged;
            }
        }

        private void OnDisable()
        {
            if (stats != null)
            {
                stats.StatsChanged -= HandleStatsChanged;
            }
        }

        private void Update()
        {
            float currentMaxMana = MaxMana;
            float currentRegen = ManaRegenPerSecond;

            if (currentRegen <= 0f || CurrentMana >= currentMaxMana)
            {
                return;
            }

            CurrentMana = Mathf.Min(currentMaxMana, CurrentMana + currentRegen * Time.deltaTime);
            WriteManaToSave();
            ManaChanged?.Invoke(this);
        }

        public bool TrySpendMana(float amount)
        {
            if (amount <= 0f)
            {
                return true;
            }

            if (CurrentMana < amount)
            {
                return false;
            }

            CurrentMana -= amount;
            WriteManaToSave();
            ManaChanged?.Invoke(this);
            return true;
        }

        public void RestoreMana(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            CurrentMana = Mathf.Min(MaxMana, CurrentMana + amount);
            WriteManaToSave();
            ManaChanged?.Invoke(this);
        }

        public void ResetResources()
        {
            CurrentMana = MaxMana;
            WriteManaToSave();
            ManaChanged?.Invoke(this);
        }

        private void LoadResources()
        {
            if (gameManager != null && gameManager.SaveData != null && gameManager.SaveData.currentMana >= 0f)
            {
                CurrentMana = Mathf.Clamp(gameManager.SaveData.currentMana, 0f, MaxMana);
            }
            else
            {
                CurrentMana = MaxMana;
                WriteManaToSave();
            }

            ManaChanged?.Invoke(this);
        }

        private void WriteManaToSave()
        {
            ResolveGameManager();

            if (gameManager != null && gameManager.SaveData != null)
            {
                gameManager.SaveData.currentMana = CurrentMana;
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

        private void ResolveStats()
        {
            if (stats == null)
            {
                stats = GetComponent<PlayerStats>();
            }

            if (stats == null)
            {
                stats = gameObject.AddComponent<PlayerStats>();
            }
        }

        private void HandleStatsChanged()
        {
            CurrentMana = Mathf.Clamp(CurrentMana, 0f, MaxMana);
            WriteManaToSave();
            ManaChanged?.Invoke(this);
        }
    }
}
