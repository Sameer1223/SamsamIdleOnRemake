using System;
using SamsamIdleOn.Stats;
using TMPro;
using UnityEngine;

namespace SamsamIdleOn.UI
{
    public sealed class PlayerStatsPanel : MonoBehaviour
    {
        [Serializable]
        private struct StatLabel
        {
            [SerializeField] private CharacterStatType stat;
            [SerializeField] private TMP_Text label;
            [SerializeField] private string format;
            [SerializeField] private bool showAsPercent;

            public void Refresh(PlayerStats stats)
            {
                if (label == null || stats == null)
                {
                    return;
                }

                float value = GetDisplayValue(stats.GetValue(stat), stat);
                string valueText = showAsPercent
                    ? $"{value * 100f:0.#}%"
                    : value.ToString(string.IsNullOrWhiteSpace(format) ? "0.#" : format);

                label.text = $"{GetDisplayName(stat)}: {valueText}";
            }

            private static float GetDisplayValue(float value, CharacterStatType stat)
            {
                return stat switch
                {
                    CharacterStatType.Luck => 1f + value,
                    CharacterStatType.XpGain => 1f + value,
                    CharacterStatType.CritChance => Mathf.Clamp01(value),
                    _ => value
                };
            }

            private static string GetDisplayName(CharacterStatType stat)
            {
                return stat switch
                {
                    CharacterStatType.MaxHealth => "Health",
                    CharacterStatType.HealthRegen => "HP Regen",
                    CharacterStatType.MaxMana => "Mana",
                    CharacterStatType.ManaRegen => "MP Regen",
                    CharacterStatType.AttackSpeed => "Atk Spd",
                    CharacterStatType.CritChance => "Crit Chance",
                    CharacterStatType.CritDamage => "Crit Damage",
                    CharacterStatType.MoveSpeed => "Move Speed",
                    CharacterStatType.XpGain => "XP Gain",
                    CharacterStatType.CoinGain => "Coin Gain",
                    CharacterStatType.TalentPoints => "Talent Points",
                    CharacterStatType.AfkGain => "AFK Gain",
                    _ => stat.ToString()
                };
            }
        }

        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private bool autoFindPlayerStats = true;
        [SerializeField] private StatLabel[] labels = Array.Empty<StatLabel>();

        private void Awake()
        {
            ResolveReferences();
            Refresh();
        }

        private void OnEnable()
        {
            BindStats();
            Refresh();
        }

        private void OnDisable()
        {
            if (playerStats != null)
            {
                playerStats.StatsChanged -= Refresh;
            }
        }

        public void Refresh()
        {
            BindStats();

            foreach (StatLabel statLabel in labels)
            {
                statLabel.Refresh(playerStats);
            }
        }

        private void Update()
        {
            if (autoFindPlayerStats && playerStats == null)
            {
                BindStats();
                Refresh();
            }
        }

        private void ResolveReferences()
        {
            if (!autoFindPlayerStats || playerStats != null)
            {
                return;
            }

            playerStats = FindAnyObjectByType<PlayerStats>();
        }

        private void BindStats()
        {
            PlayerStats previousStats = playerStats;
            ResolveReferences();

            if (previousStats != null && previousStats != playerStats)
            {
                previousStats.StatsChanged -= Refresh;
            }

            if (playerStats != null)
            {
                playerStats.StatsChanged -= Refresh;
                playerStats.StatsChanged += Refresh;
            }
        }
    }
}
