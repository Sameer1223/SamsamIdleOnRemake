using SamsamIdleOn.Characters;
using System;
using SamsamIdleOn.Enemies;
using SamsamIdleOn.Stats;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace SamsamIdleOn.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerClickMovement2D))]
    public sealed class PlayerCombatClick2D : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private Camera inputCamera;
        [SerializeField] private LayerMask enemyLayers = ~0;
        [SerializeField] private bool ignoreClicksOverUi = true;
        [SerializeField] private bool blockOnlyInteractiveUi = true;

        [Header("Attack")]
        [SerializeField] private float attackRange = 1.25f;
        [SerializeField] private float attackPointInset = 0.85f;
        [SerializeField] private float clickProbeRadius = 0.2f;
        [SerializeField] private float screenBoundsPadding = 8f;
        [SerializeField] private float rerouteCooldown = 0.6f;
        [SerializeField] private float pursuitResumeBuffer = 0.25f;
        [SerializeField] private float minimumRerouteDistance = 0.35f;
        [SerializeField, Min(1)] private int attackDamage = 10;
        [SerializeField, Min(0.01f)] private float attacksPerSecond = 1f;
        [SerializeField] private PlayerStats stats;
        [SerializeField] private PlayerResources resources;

        [Header("Power Strike")]
        [SerializeField, Min(0f)] private float powerStrikeManaCost = 15f;
        [SerializeField, Min(1f)] private float powerStrikeDamageMultiplier = 2f;
        [SerializeField, Min(0f)] private float powerStrikeCooldownSeconds = 4f;
        [FormerlySerializedAs("autoUsePowerStrike")]
        [SerializeField] private bool usePowerStrikeDuringAutoCombat = true;

        [Header("Feedback")]
        [SerializeField] private Vector3 combatTextOffset = new(0f, 1.25f, 0f);
        [SerializeField] private Color damageTextColor = new(1f, 0.95f, 0.55f, 1f);
        [SerializeField] private Color critTextColor = new(1f, 0.35f, 0.2f, 1f);
        [SerializeField] private Color missTextColor = new(0.75f, 0.85f, 1f, 1f);
        [SerializeField] private Color powerStrikeTextColor = new(0.45f, 0.85f, 1f, 1f);
        [SerializeField] private Color powerStrikeDamageTextColor = new(0.35f, 1f, 0.95f, 1f);

        [Header("Auto Combat")]
        [SerializeField] private bool autoCombatEnabled;
        [SerializeField, Min(0f)] private float autoTargetRefreshSeconds = 0.25f;
        [SerializeField, Min(0f)] private float maxAutoTargetDistance = 0f;

        private PlayerClickMovement2D movement;
        private PlayerHealth playerHealth;
        private ICombatTarget pendingTarget;
        private readonly Collider2D[] enemyClickResults = new Collider2D[16];
        private float lastRouteTime = -999f;
        private float nextAttackTime;
        private float nextAutoTargetRefreshTime;
        private Vector2 lastAttackRoutePosition;
        private bool hasLastAttackRoutePosition;
        private float nextPowerStrikeTime;
        private bool powerStrikeArmed;

        public event Action<bool> AutoCombatChanged;

        public bool IsAutoCombatEnabled => autoCombatEnabled;
        public bool IsPowerStrikeArmed => powerStrikeArmed;
        public float PowerStrikeManaCost => powerStrikeManaCost;
        public float PowerStrikeCooldownRemaining => Mathf.Max(0f, nextPowerStrikeTime - Time.time);
        public float PowerStrikeCooldownDuration => powerStrikeCooldownSeconds;

        public bool IsAttacking => pendingTarget != null
            && pendingTarget.IsTargetable
            && (playerHealth == null || playerHealth.IsAlive)
            && IsInAttackRange(pendingTarget);

        public float GetEffectiveAverageDamage()
        {
            ResolveStats();

            float strength = stats != null ? stats.GetValue(CharacterStatType.Strength) : 0f;
            float critChance = stats != null ? stats.GetValue(CharacterStatType.CritChance) : 0f;
            float critDamage = stats != null ? stats.GetValue(CharacterStatType.CritDamage) : 1f;
            float damage = Mathf.Max(1f, attackDamage + strength);
            return damage * (1f + Mathf.Clamp01(critChance) * (Mathf.Max(1f, critDamage) - 1f));
        }

        public float GetEffectiveAttacksPerSecond()
        {
            ResolveStats();

            float attackSpeedMultiplier = stats != null ? stats.GetValue(CharacterStatType.AttackSpeed) : 1f;
            return Mathf.Max(0.01f, attacksPerSecond * Mathf.Max(0.01f, attackSpeedMultiplier));
        }

        private void Awake()
        {
            movement = GetComponent<PlayerClickMovement2D>();
            playerHealth = GetComponent<PlayerHealth>();
            ResolveStats();
            ResolveResources();

            if (inputCamera == null)
            {
                inputCamera = Camera.main;
            }
        }

        private void OnEnable()
        {
        }

        private void OnDisable()
        {
            pendingTarget = null;
        }

        private void Update()
        {
            if (playerHealth != null && !playerHealth.IsAlive)
            {
                pendingTarget = null;
                return;
            }

            if (inputCamera == null)
            {
                inputCamera = Camera.main;
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                HandlePointerClick(Mouse.current.position.ReadValue());
            }

            UpdateAutoTargeting();
            UpdatePendingAttack();
        }

        public void ToggleAutoCombat()
        {
            SetAutoCombatEnabled(!autoCombatEnabled);
        }

        public void SetAutoCombatEnabled(bool isEnabled)
        {
            if (autoCombatEnabled == isEnabled)
            {
                return;
            }

            autoCombatEnabled = isEnabled;
            nextAutoTargetRefreshTime = 0f;

            if (!autoCombatEnabled)
            {
                pendingTarget = null;
            }

            AutoCombatChanged?.Invoke(autoCombatEnabled);
        }

        private void HandlePointerClick(Vector2 screenPosition)
        {
            if (inputCamera == null || IsPointerBlockedByUi(screenPosition))
            {
                return;
            }

            ICombatTarget target = FindClickedTarget(screenPosition);

            if (target == null)
            {
                pendingTarget = null;
                return;
            }

            movement.SuppressCurrentPointerClick();
            StartAttacking(target);
            UpdatePendingAttack(true);
        }

        private void StartAttacking(ICombatTarget target)
        {
            pendingTarget = target;
            nextAttackTime = 0f;
            hasLastAttackRoutePosition = false;
        }

        private void UpdateAutoTargeting()
        {
            if (!autoCombatEnabled || pendingTarget != null || Time.time < nextAutoTargetRefreshTime)
            {
                return;
            }

            nextAutoTargetRefreshTime = Time.time + autoTargetRefreshSeconds;
            ICombatTarget target = FindNearestAutoTarget();

            if (target == null)
            {
                return;
            }

            StartAttacking(target);
            UpdatePendingAttack(true);
        }

        private ICombatTarget FindNearestAutoTarget()
        {
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude);
            ICombatTarget bestTarget = null;
            float bestSqrDistance = float.MaxValue;
            Vector2 playerPosition = transform.position;
            float maxSqrDistance = maxAutoTargetDistance > 0f
                ? maxAutoTargetDistance * maxAutoTargetDistance
                : float.PositiveInfinity;

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is not ICombatTarget target
                    || !target.IsTargetable
                    || target.TargetComponent == null
                    || !IsInLayerMask(target.TargetComponent.gameObject.layer, enemyLayers))
                {
                    continue;
                }

                float sqrDistance = ((Vector2)target.TargetTransform.position - playerPosition).sqrMagnitude;

                if (sqrDistance > maxSqrDistance || sqrDistance >= bestSqrDistance)
                {
                    continue;
                }

                bestSqrDistance = sqrDistance;
                bestTarget = target;
            }

            return bestTarget;
        }

        private ICombatTarget FindClickedTarget(Vector2 screenPosition)
        {
            ICombatTarget targetByBounds = FindTargetByScreenBounds(screenPosition);

            if (targetByBounds != null)
            {
                return targetByBounds;
            }

            Vector2 worldPosition = inputCamera.ScreenToWorldPoint(screenPosition);
            ContactFilter2D filter = CreateEnemyContactFilter();
            int hitCount = Physics2D.OverlapPoint(worldPosition, filter, enemyClickResults);
            ICombatTarget target2D = FindTargetInHits(hitCount);

            if (target2D != null)
            {
                return target2D;
            }

            if (clickProbeRadius > 0f)
            {
                hitCount = Physics2D.OverlapCircle(worldPosition, clickProbeRadius, filter, enemyClickResults);
                target2D = FindTargetInHits(hitCount);

                if (target2D != null)
                {
                    return target2D;
                }
            }

            Ray ray = inputCamera.ScreenPointToRay(screenPosition);
            int layerMask = enemyLayers.value == 0 ? Physics.DefaultRaycastLayers : enemyLayers.value;

            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, layerMask, QueryTriggerInteraction.Collide))
            {
                return hit.collider.GetComponentInParent<ICombatTarget>();
            }

            return null;
        }

        private bool IsPointerBlockedByUi(Vector2 screenPosition)
        {
            if (!ignoreClicksOverUi || EventSystem.current == null)
            {
                return false;
            }

            if (!blockOnlyInteractiveUi)
            {
                return EventSystem.current.IsPointerOverGameObject();
            }

            PointerEventData pointerEventData = new(EventSystem.current)
            {
                position = screenPosition
            };

            System.Collections.Generic.List<RaycastResult> results = new();
            EventSystem.current.RaycastAll(pointerEventData, results);

            foreach (RaycastResult result in results)
            {
                if (result.gameObject != null && result.gameObject.GetComponentInParent<Selectable>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        private ContactFilter2D CreateEnemyContactFilter()
        {
            ContactFilter2D filter = new()
            {
                useLayerMask = true,
                useTriggers = true
            };

            filter.SetLayerMask(enemyLayers.value == 0 ? Physics2D.AllLayers : enemyLayers);
            return filter;
        }

        private ICombatTarget FindTargetInHits(int hitCount)
        {
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = enemyClickResults[i];

                if (hit == null)
                {
                    continue;
                }

                ICombatTarget target = hit.GetComponentInParent<ICombatTarget>();

                if (target != null && target.IsTargetable)
                {
                    return target;
                }
            }

            return null;
        }

        private void UpdatePendingAttack(bool forceRoute = false)
        {
            if (pendingTarget == null || !pendingTarget.IsTargetable)
            {
                pendingTarget = null;
                return;
            }

            float distanceToTarget = GetDistanceToTarget(pendingTarget);

            if (distanceToTarget <= attackRange)
            {
                if (movement.HasDestination)
                {
                    movement.Stop();
                }

                if (Time.time >= nextAttackTime)
                {
                    TryAutoActivatePowerStrike();
                    Hit(pendingTarget);
                }

                return;
            }

            if (!forceRoute && movement.HasDestination)
            {
                return;
            }

            Vector2 attackPosition = GetAttackPosition(pendingTarget);
            bool canRefreshRoute = Time.time >= lastRouteTime + rerouteCooldown;
            bool movedEnoughToReroute = !hasLastAttackRoutePosition
                || Vector2.Distance(attackPosition, lastAttackRoutePosition) >= minimumRerouteDistance;
            bool outsideResumeBuffer = distanceToTarget > attackRange + pursuitResumeBuffer;

            if (forceRoute || (canRefreshRoute && (movedEnoughToReroute || outsideResumeBuffer)))
            {
                lastRouteTime = Time.time;
                bool routeAccepted = movement.RouteTo(attackPosition);
                lastAttackRoutePosition = attackPosition;
                hasLastAttackRoutePosition = routeAccepted;
            }
        }

        public bool ActivatePowerStrike()
        {
            if (powerStrikeArmed)
            {
                return true;
            }

            if (!CanActivatePowerStrike())
            {
                ShowPowerStrikeStatusText(GetPowerStrikeFailureText());
                return false;
            }

            resources.TrySpendMana(powerStrikeManaCost);
            nextPowerStrikeTime = Time.time + powerStrikeCooldownSeconds;
            powerStrikeArmed = true;
            ShowPowerStrikeStatusText("2x Ready");
            return true;
        }

        public void ActivatePowerStrikeButton()
        {
            ActivatePowerStrike();
        }

        private bool CanActivatePowerStrike()
        {
            ResolveResources();

            return resources != null
                && Time.time >= nextPowerStrikeTime
                && resources.CurrentMana >= powerStrikeManaCost;
        }

        private string GetPowerStrikeFailureText()
        {
            ResolveResources();

            if (PowerStrikeCooldownRemaining > 0.05f)
            {
                return $"CD {Mathf.CeilToInt(PowerStrikeCooldownRemaining)}s";
            }

            if (resources == null)
            {
                return "No MP";
            }

            return $"No MP {Mathf.FloorToInt(resources.CurrentMana)}/{Mathf.CeilToInt(powerStrikeManaCost)}";
        }

        private void TryAutoActivatePowerStrike()
        {
            if (autoCombatEnabled && usePowerStrikeDuringAutoCombat && CanActivatePowerStrike())
            {
                ActivatePowerStrike();
            }
        }

        private bool IsInAttackRange(ICombatTarget target)
        {
            return GetDistanceToTarget(target) <= attackRange;
        }

        private float GetDistanceToTarget(ICombatTarget target)
        {
            Vector2 playerPosition = transform.position;

            if (TryGetTargetBounds(target, out Bounds bounds))
            {
                Vector2 closestPoint = bounds.ClosestPoint(playerPosition);
                return Vector2.Distance(playerPosition, closestPoint);
            }

            return Vector2.Distance(playerPosition, target.TargetTransform.position);
        }

        private Vector2 GetAttackPosition(ICombatTarget target)
        {
            float directionFromTarget = transform.position.x <= target.TargetTransform.position.x ? -1f : 1f;
            float horizontalOffset = Mathf.Max(0.1f, attackRange * attackPointInset);
            return (Vector2)target.TargetTransform.position + (Vector2.right * directionFromTarget * horizontalOffset);
        }

        private ICombatTarget FindTargetByScreenBounds(Vector2 screenPosition)
        {
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude);
            ICombatTarget closestTarget = null;
            float closestSqrDistance = float.MaxValue;

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is not ICombatTarget target
                    || !target.IsTargetable
                    || target.TargetComponent == null
                    || !IsInLayerMask(target.TargetComponent.gameObject.layer, enemyLayers))
                {
                    continue;
                }

                if (!TryGetTargetBounds(target, out Bounds bounds)
                    || !TryGetScreenRect(bounds, out Rect screenRect))
                {
                    continue;
                }

                screenRect.xMin -= screenBoundsPadding;
                screenRect.xMax += screenBoundsPadding;
                screenRect.yMin -= screenBoundsPadding;
                screenRect.yMax += screenBoundsPadding;

                if (!screenRect.Contains(screenPosition))
                {
                    continue;
                }

                float sqrDistance = ((Vector2)screenRect.center - screenPosition).sqrMagnitude;

                if (sqrDistance < closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
                    closestTarget = target;
                }
            }

            return closestTarget;
        }

        private static bool TryGetTargetBounds(ICombatTarget target, out Bounds bounds)
        {
            bool hasBounds = false;
            Component targetComponent = target.TargetComponent;
            bounds = new Bounds(target.TargetTransform.position, Vector3.zero);

            foreach (Collider2D targetCollider in targetComponent.GetComponentsInChildren<Collider2D>())
            {
                if (targetCollider == null || !targetCollider.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = targetCollider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(targetCollider.bounds);
                }
            }

            foreach (Renderer renderer in targetComponent.GetComponentsInChildren<Renderer>())
            {
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private bool TryGetScreenRect(Bounds bounds, out Rect screenRect)
        {
            if (inputCamera == null)
            {
                screenRect = default;
                return false;
            }

            Vector3 screenMin = inputCamera.WorldToScreenPoint(new Vector3(bounds.min.x, bounds.min.y, bounds.center.z));
            Vector3 screenMax = inputCamera.WorldToScreenPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.center.z));

            float minX = Mathf.Min(screenMin.x, screenMax.x);
            float maxX = Mathf.Max(screenMin.x, screenMax.x);
            float minY = Mathf.Min(screenMin.y, screenMax.y);
            float maxY = Mathf.Max(screenMin.y, screenMax.y);

            screenRect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return screenRect.width > 0f && screenRect.height > 0f;
        }

        private static bool IsInLayerMask(int layer, LayerMask layerMask)
        {
            return layerMask.value == 0 || (layerMask.value & (1 << layer)) != 0;
        }

        private void Hit(ICombatTarget target)
        {
            ResolveStats();

            if (TryRollDodge(target))
            {
                ShowMissText(target);
                nextAttackTime = Time.time + (1f / GetEffectiveAttacksPerSecond());
                return;
            }

            bool usedPowerStrike = powerStrikeArmed;
            int damage = RollDamage(out bool didCrit);
            target.ApplyHit(damage, gameObject);
            ShowDamageText(target, damage, didCrit, usedPowerStrike);
            powerStrikeArmed = false;
            nextAttackTime = Time.time + (1f / GetEffectiveAttacksPerSecond());

            if (!target.IsTargetable)
            {
                pendingTarget = null;
            }
        }

        private bool TryRollDodge(ICombatTarget target)
        {
            if (target?.TargetComponent == null)
            {
                return false;
            }

            EnemyEvasion2D evasion = target.TargetComponent.GetComponentInParent<EnemyEvasion2D>();
            return evasion != null && evasion.RollDodge(stats);
        }

        private void ShowDamageText(ICombatTarget target, int damage, bool didCrit, bool usedPowerStrike)
        {
            string text = usedPowerStrike ? $"2x {damage}" : damage.ToString();
            Color color = usedPowerStrike ? powerStrikeDamageTextColor : didCrit ? critTextColor : damageTextColor;
            FloatingCombatText2D.SpawnDefault(GetCombatTextPosition(target), text, color);
        }

        private void ShowMissText(ICombatTarget target)
        {
            FloatingCombatText2D.SpawnDefault(GetCombatTextPosition(target), "Miss", missTextColor);
        }

        private void ShowPowerStrikeStatusText(string text)
        {
            FloatingCombatText2D.SpawnDefault(transform.position + combatTextOffset, text, powerStrikeTextColor);
        }

        private Vector3 GetCombatTextPosition(ICombatTarget target)
        {
            if (target != null && TryGetTargetBounds(target, out Bounds bounds))
            {
                return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z) + combatTextOffset;
            }

            return target != null
                ? target.TargetTransform.position + combatTextOffset
                : transform.position + combatTextOffset;
        }

        private int RollDamage(out bool didCrit)
        {
            ResolveStats();

            float strength = stats != null ? stats.GetValue(CharacterStatType.Strength) : 0f;
            float critChance = stats != null ? stats.GetValue(CharacterStatType.CritChance) : 0f;
            float critDamage = stats != null ? stats.GetValue(CharacterStatType.CritDamage) : 1f;
            float damage = Mathf.Max(1f, attackDamage + strength);

            didCrit = UnityEngine.Random.value < Mathf.Clamp01(critChance);

            if (powerStrikeArmed)
            {
                damage *= powerStrikeDamageMultiplier;
            }

            if (didCrit)
            {
                damage *= Mathf.Max(1f, critDamage);
            }

            return Mathf.Max(1, Mathf.RoundToInt(damage));
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

        private void ResolveResources()
        {
            if (resources == null || resources.gameObject != gameObject)
            {
                resources = GetComponent<PlayerResources>();
            }
        }
    }
}
