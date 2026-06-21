using SamsamIdleOn.Stats;
using UnityEngine;

namespace SamsamIdleOn.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyEvasion2D : MonoBehaviour
    {
        [SerializeField] private bool canDodge = true;
        [SerializeField, Range(0f, 1f)] private float baseDodgeChance = 0.1f;
        [SerializeField, Min(0f)] private float dodgeReductionPerAccuracy = 0.01f;
        [SerializeField, Range(0f, 1f)] private float minimumDodgeChance = 0f;
        [SerializeField, Range(0f, 1f)] private float maximumDodgeChance = 0.95f;

        public float GetDodgeChance(PlayerStats attackerStats)
        {
            if (!canDodge)
            {
                return 0f;
            }

            float accuracy = attackerStats != null
                ? attackerStats.GetValue(CharacterStatType.Accuracy)
                : 0f;
            float dodgeChance = baseDodgeChance - Mathf.Max(0f, accuracy) * dodgeReductionPerAccuracy;
            float low = Mathf.Min(minimumDodgeChance, maximumDodgeChance);
            float high = Mathf.Max(minimumDodgeChance, maximumDodgeChance);
            return Mathf.Clamp(dodgeChance, low, high);
        }

        public bool RollDodge(PlayerStats attackerStats)
        {
            return Random.value < GetDodgeChance(attackerStats);
        }
    }
}
