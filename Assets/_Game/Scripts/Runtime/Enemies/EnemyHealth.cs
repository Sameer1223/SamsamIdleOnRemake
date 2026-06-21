using System;
using SamsamIdleOn.Combat;
using SamsamIdleOn.Core;
using UnityEngine;

namespace SamsamIdleOn.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyHealth : MonoBehaviour, ICombatTarget
    {
        [SerializeField, Min(1)] private int maxHealth = 50;

        public event Action<EnemyHealth> Died;
        public event Action<EnemyHealth> HealthChanged;

        private bool hasDied;

        public int CurrentHealth { get; private set; }

        public int MaxHealth => maxHealth;

        public bool IsAlive => !hasDied;

        public bool IsTargetable => IsAlive;

        public string DisplayName => name.Replace("(Clone)", string.Empty).Trim();

        public Transform TargetTransform => transform;

        public Component TargetComponent => this;

        private void Awake()
        {
            CurrentHealth = maxHealth;
            HealthChanged?.Invoke(this);
        }

        private void OnEnable()
        {
            if (!hasDied && CurrentHealth <= 0)
            {
                CurrentHealth = maxHealth;
                HealthChanged?.Invoke(this);
            }
        }

        private void OnDestroy()
        {
            Die(false);
        }

        public void TakeDamage(int damage)
        {
            if (hasDied || damage <= 0)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
            HealthChanged?.Invoke(this);

            if (CurrentHealth <= 0)
            {
                Die(true);
                Destroy(gameObject);
            }
        }

        public void ApplyHit(int damage, GameObject attacker)
        {
            TakeDamage(damage);
        }

        public void Die()
        {
            Die(true);
        }

        private void Die(bool recordKill)
        {
            if (hasDied)
            {
                return;
            }

            hasDied = true;

            if (recordKill)
            {
                GameManager gameManager = GameManager.Instance != null
                    ? GameManager.Instance
                    : FindAnyObjectByType<GameManager>();
                gameManager?.RecordEnemyKill(DisplayName);
            }

            Died?.Invoke(this);
        }
    }
}
