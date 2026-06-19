using SamsamIdleOn.Core;
using SamsamIdleOn.Stats;
using UnityEngine;
using UnityEngine.Serialization;

namespace SamsamIdleOn.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class EnemyRewards2D : MonoBehaviour
    {
        [Header("Experience")]
        [FormerlySerializedAs("experienceReward")]
        [SerializeField, Min(0)] private int minExperienceReward = 4;
        [SerializeField, Min(0)] private int maxExperienceReward = 7;

        [Header("Coins")]
        [Tooltip("Coin rewards are authored as total bronze. 140 means 1 silver and 40 bronze.")]
        [SerializeField, Min(0)] private int minCoinBronzeReward = 1;
        [SerializeField, Min(0)] private int maxCoinBronzeReward = 5;

        private EnemyHealth health;

        public int MinExperienceReward => minExperienceReward;

        public int MaxExperienceReward => maxExperienceReward;

        public int MinCoinBronzeReward => minCoinBronzeReward;

        public int MaxCoinBronzeReward => maxCoinBronzeReward;

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
            GameManager gameManager = GameManager.Instance != null
                ? GameManager.Instance
                : FindAnyObjectByType<GameManager>();

            if (gameManager == null)
            {
                return;
            }

            gameManager.Initialize();

            PlayerStats playerStats = FindAnyObjectByType<PlayerStats>();
            float xpMultiplier = playerStats != null
                ? 1f + playerStats.GetValue(CharacterStatType.XpGain)
                : 1f;
            float coinMultiplier = playerStats != null
                ? 1f + playerStats.GetValue(CharacterStatType.CoinGain)
                : 1f;
            long experienceReward = Mathf.Max(0, Mathf.RoundToInt(RollInclusive(minExperienceReward, maxExperienceReward) * xpMultiplier));
            long coinReward = Mathf.Max(0, Mathf.RoundToInt(RollInclusive(minCoinBronzeReward, maxCoinBronzeReward) * coinMultiplier));

            gameManager.AddExperience(experienceReward);
            gameManager.AddBronzeCoins(coinReward);
        }

        private void OnValidate()
        {
            if (maxExperienceReward < minExperienceReward)
            {
                maxExperienceReward = minExperienceReward;
            }

            if (maxCoinBronzeReward < minCoinBronzeReward)
            {
                maxCoinBronzeReward = minCoinBronzeReward;
            }
        }

        private static int RollInclusive(int minimum, int maximum)
        {
            int safeMinimum = Mathf.Max(0, minimum);
            int safeMaximum = Mathf.Max(safeMinimum, maximum);
            return Random.Range(safeMinimum, safeMaximum + 1);
        }
    }
}
