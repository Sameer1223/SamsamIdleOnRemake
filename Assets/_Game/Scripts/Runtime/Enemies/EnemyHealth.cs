using System;
using UnityEngine;

namespace SamsamIdleOn.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHealth = 50;

        public event Action<EnemyHealth> Died;
        public event Action<EnemyHealth> HealthChanged;

        private bool hasDied;

        public int CurrentHealth { get; private set; }

        public int MaxHealth => maxHealth;

        public bool IsAlive => !hasDied;

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
            Die();
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
                Die();
                Destroy(gameObject);
            }
        }

        public void Die()
        {
            if (hasDied)
            {
                return;
            }

            hasDied = true;
            Died?.Invoke(this);
        }
    }
}
