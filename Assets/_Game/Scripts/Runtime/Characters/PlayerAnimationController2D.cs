using SamsamIdleOn.Combat;
using UnityEngine;

namespace SamsamIdleOn.Characters
{
    [DisallowMultipleComponent]
    public sealed class PlayerAnimationController2D : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private PlayerClickMovement2D movement;
        [SerializeField] private PlayerCombatClick2D combat;
        [SerializeField] private PlayerHealth health;

        [Header("Animator Parameters")]
        [SerializeField] private string movingParameter = "IsMoving";
        [SerializeField] private string attackingParameter = "IsAttacking";
        [SerializeField] private string deadParameter = "IsDead";
        [SerializeField] private string speedParameter = "Speed";

        [Header("Attack Timing")]
        [SerializeField, Min(0.01f)] private float attackClipSeconds = 0.75f;
        [SerializeField] private bool syncAttackAnimationToAttackSpeed = true;

        private int movingHash;
        private int attackingHash;
        private int deadHash;
        private int speedHash;
        private bool hasMovingParameter;
        private bool hasAttackingParameter;
        private bool hasDeadParameter;
        private bool hasSpeedParameter;

        private void Awake()
        {
            ResolveReferences();
            CacheHashes();
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (health != null)
            {
                health.Died -= HandleDied;
                health.Died += HandleDied;
                health.HealthChanged -= HandleHealthChanged;
                health.HealthChanged += HandleHealthChanged;
            }

            RefreshImmediate();
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Died -= HandleDied;
                health.HealthChanged -= HandleHealthChanged;
            }

        }

        private void Update()
        {
            if (animator == null)
            {
                return;
            }

            bool isDead = health != null && !health.IsAlive;
            bool isMoving = !isDead && movement != null && movement.IsMoving;
            bool isAttacking = !isDead && combat != null && combat.IsAttacking;

            UpdateAnimatorPlaybackSpeed(isAttacking);
            SetBool(movingHash, hasMovingParameter, isMoving);
            SetBool(attackingHash, hasAttackingParameter, isAttacking);
            SetBool(deadHash, hasDeadParameter, isDead);
            SetFloat(speedHash, hasSpeedParameter, isMoving ? 1f : 0f);
        }

        private void HandleDied(PlayerHealth playerHealth)
        {
            RefreshImmediate();
        }

        private void HandleHealthChanged(PlayerHealth playerHealth)
        {
            RefreshImmediate();
        }

        private void RefreshImmediate()
        {
            if (animator == null)
            {
                return;
            }

            bool isDead = health != null && !health.IsAlive;
            bool isMoving = !isDead && movement != null && movement.IsMoving;
            bool isAttacking = !isDead && combat != null && combat.IsAttacking;

            UpdateAnimatorPlaybackSpeed(isAttacking);
            SetBool(movingHash, hasMovingParameter, isMoving);
            SetBool(attackingHash, hasAttackingParameter, isAttacking);
            SetBool(deadHash, hasDeadParameter, isDead);
            SetFloat(speedHash, hasSpeedParameter, isMoving ? 1f : 0f);
        }

        private void ResolveReferences()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (movement == null)
            {
                movement = GetComponent<PlayerClickMovement2D>();
            }

            if (combat == null)
            {
                combat = GetComponent<PlayerCombatClick2D>();
            }

            if (health == null)
            {
                health = GetComponent<PlayerHealth>();
            }
        }

        private void CacheHashes()
        {
            movingHash = string.IsNullOrWhiteSpace(movingParameter)
                ? 0
                : Animator.StringToHash(movingParameter);
            attackingHash = string.IsNullOrWhiteSpace(attackingParameter)
                ? 0
                : Animator.StringToHash(attackingParameter);
            deadHash = string.IsNullOrWhiteSpace(deadParameter)
                ? 0
                : Animator.StringToHash(deadParameter);
            speedHash = string.IsNullOrWhiteSpace(speedParameter)
                ? 0
                : Animator.StringToHash(speedParameter);

            hasMovingParameter = HasParameter(movingHash);
            hasAttackingParameter = HasParameter(attackingHash);
            hasDeadParameter = HasParameter(deadHash);
            hasSpeedParameter = HasParameter(speedHash);
        }

        private void UpdateAnimatorPlaybackSpeed(bool isAttacking)
        {
            if (animator == null)
            {
                return;
            }

            if (!syncAttackAnimationToAttackSpeed || !isAttacking || combat == null)
            {
                animator.speed = 1f;
                return;
            }

            float attacksPerSecond = combat.GetEffectiveAttacksPerSecond();
            animator.speed = Mathf.Max(0.01f, attackClipSeconds * attacksPerSecond);
        }

        private bool HasParameter(int parameterHash)
        {
            if (animator == null || parameterHash == 0)
            {
                return false;
            }

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == parameterHash)
                {
                    return true;
                }
            }

            return false;
        }

        private void SetBool(int parameterHash, bool hasParameter, bool value)
        {
            if (hasParameter)
            {
                animator.SetBool(parameterHash, value);
            }
        }

        private void SetFloat(int parameterHash, bool hasParameter, float value)
        {
            if (hasParameter)
            {
                animator.SetFloat(parameterHash, value);
            }
        }
    }
}
