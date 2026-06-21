using SamsamIdleOn.Characters;
using SamsamIdleOn.Stats;
using System.Collections.Generic;
using UnityEngine;

namespace SamsamIdleOn.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyContactDamage2D : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float damage = 5f;
        [SerializeField, Min(0.01f)] private float damageCooldownSeconds = 1f;

        private readonly Dictionary<PlayerHealth, float> nextDamageTimesByPlayer = new();

        public void Configure(float damageAmount, float cooldownSeconds)
        {
            damage = Mathf.Max(0f, damageAmount);
            damageCooldownSeconds = Mathf.Max(0.01f, cooldownSeconds);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!other.TryGetComponent(out PlayerHealth playerHealth) || !playerHealth.IsAlive)
            {
                return;
            }

            if (nextDamageTimesByPlayer.TryGetValue(playerHealth, out float nextDamageTime)
                && Time.time < nextDamageTime)
            {
                return;
            }

            float defense = 0f;

            if (other.TryGetComponent(out PlayerStats stats))
            {
                defense = stats.GetValue(CharacterStatType.Defense);
            }

            float mitigatedDamage = Mathf.Max(1f, damage - defense);
            playerHealth.TakeDamage(mitigatedDamage);
            nextDamageTimesByPlayer[playerHealth] = Time.time + damageCooldownSeconds;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.TryGetComponent(out PlayerHealth playerHealth))
            {
                nextDamageTimesByPlayer.Remove(playerHealth);
            }
        }
    }
}
