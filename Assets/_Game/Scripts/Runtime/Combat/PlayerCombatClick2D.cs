using SamsamIdleOn.Characters;
using System;
using SamsamIdleOn.Core;
using SamsamIdleOn.Enemies;
using SamsamIdleOn.Stats;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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
        [SerializeField, Min(0f)] private float attackSpeedPerAgility = 0.02f;
        [SerializeField] private PlayerStats stats;

        [Header("Debug")]
        [SerializeField] private bool logCombatRoutingDebug = true;

        [Header("Auto Combat")]
        [SerializeField] private bool autoCombatEnabled;
        [SerializeField, Min(0f)] private float autoTargetRefreshSeconds = 0.25f;
        [SerializeField, Min(0f)] private float maxAutoTargetDistance = 0f;

        private PlayerClickMovement2D movement;
        private PlayerHealth playerHealth;
        private EnemyHealth pendingTarget;
        private readonly Collider2D[] enemyClickResults = new Collider2D[16];
        private float lastRouteTime = -999f;
        private float nextAttackTime;
        private float nextAutoTargetRefreshTime;
        private Vector2 lastAttackRoutePosition;
        private bool hasLastAttackRoutePosition;

        public event Action<bool> AutoCombatChanged;

        public bool IsAutoCombatEnabled => autoCombatEnabled;

        public bool IsAttacking => pendingTarget != null
            && pendingTarget.IsAlive
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

            float agility = stats != null ? stats.GetValue(CharacterStatType.Agility) : 0f;
            float attackSpeedMultiplier = 1f + Mathf.Max(0f, agility) * attackSpeedPerAgility;
            return Mathf.Max(0.01f, attacksPerSecond * attackSpeedMultiplier);
        }

        private void Awake()
        {
            movement = GetComponent<PlayerClickMovement2D>();
            playerHealth = GetComponent<PlayerHealth>();
            ResolveStats();

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

            EnemyHealth enemy = FindClickedEnemy(screenPosition);

            if (enemy == null)
            {
                pendingTarget = null;
                return;
            }

            movement.SuppressCurrentPointerClick();
            StartAttacking(enemy);
            UpdatePendingAttack(true);
        }

        private void StartAttacking(EnemyHealth enemy)
        {
            pendingTarget = enemy;
            nextAttackTime = 0f;
            hasLastAttackRoutePosition = false;

            GameManager gameManager = GameManager.Instance != null
                ? GameManager.Instance
                : FindAnyObjectByType<GameManager>();
            gameManager?.SetOfflineFarmTarget(enemy);
        }

        private void UpdateAutoTargeting()
        {
            if (!autoCombatEnabled || pendingTarget != null || Time.time < nextAutoTargetRefreshTime)
            {
                return;
            }

            nextAutoTargetRefreshTime = Time.time + autoTargetRefreshSeconds;
            EnemyHealth target = FindNearestAutoTarget();

            if (target == null)
            {
                return;
            }

            StartAttacking(target);
            UpdatePendingAttack(true);
        }

        private EnemyHealth FindNearestAutoTarget()
        {
            EnemyHealth[] enemies = FindObjectsByType<EnemyHealth>(FindObjectsInactive.Exclude);
            EnemyHealth bestTarget = null;
            float bestSqrDistance = float.MaxValue;
            Vector2 playerPosition = transform.position;
            float maxSqrDistance = maxAutoTargetDistance > 0f
                ? maxAutoTargetDistance * maxAutoTargetDistance
                : float.PositiveInfinity;

            foreach (EnemyHealth enemy in enemies)
            {
                if (enemy == null || !enemy.IsAlive || !IsInLayerMask(enemy.gameObject.layer, enemyLayers))
                {
                    continue;
                }

                float sqrDistance = ((Vector2)enemy.transform.position - playerPosition).sqrMagnitude;

                if (sqrDistance > maxSqrDistance || sqrDistance >= bestSqrDistance)
                {
                    continue;
                }

                bestSqrDistance = sqrDistance;
                bestTarget = enemy;
            }

            return bestTarget;
        }

        private EnemyHealth FindClickedEnemy(Vector2 screenPosition)
        {
            EnemyHealth enemyByBounds = FindEnemyByScreenBounds(screenPosition);

            if (enemyByBounds != null)
            {
                return enemyByBounds;
            }

            Vector2 worldPosition = inputCamera.ScreenToWorldPoint(screenPosition);
            ContactFilter2D filter = CreateEnemyContactFilter();
            int hitCount = Physics2D.OverlapPoint(worldPosition, filter, enemyClickResults);
            EnemyHealth enemy2D = FindEnemyInHits(hitCount);

            if (enemy2D != null)
            {
                return enemy2D;
            }

            if (clickProbeRadius > 0f)
            {
                hitCount = Physics2D.OverlapCircle(worldPosition, clickProbeRadius, filter, enemyClickResults);
                enemy2D = FindEnemyInHits(hitCount);

                if (enemy2D != null)
                {
                    return enemy2D;
                }
            }

            Ray ray = inputCamera.ScreenPointToRay(screenPosition);
            int layerMask = enemyLayers.value == 0 ? Physics.DefaultRaycastLayers : enemyLayers.value;

            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, layerMask, QueryTriggerInteraction.Collide))
            {
                return hit.collider.GetComponentInParent<EnemyHealth>();
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

        private EnemyHealth FindEnemyInHits(int hitCount)
        {
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = enemyClickResults[i];

                if (hit == null)
                {
                    continue;
                }

                EnemyHealth enemy = hit.GetComponentInParent<EnemyHealth>();

                if (enemy != null)
                {
                    return enemy;
                }
            }

            return null;
        }

        private void UpdatePendingAttack(bool forceRoute = false)
        {
            if (pendingTarget == null || !pendingTarget.IsAlive)
            {
                pendingTarget = null;
                return;
            }

            float distanceToTarget = GetDistanceToEnemy(pendingTarget);

            if (distanceToTarget <= attackRange)
            {
                if (movement.HasDestination)
                {
                    LogCombatRouting($"Target {pendingTarget.name} is in range. Stopping active movement route before attacking.");
                    movement.Stop();
                }

                if (Time.time >= nextAttackTime)
                {
                    Hit(pendingTarget);
                }

                return;
            }

            if (!forceRoute && movement.HasDestination)
            {
                LogCombatRouting($"Waiting for active movement route before rerouting to {pendingTarget.name}. Current destination={movement.Destination}");
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
                LogCombatRouting($"RouteTo attack target={pendingTarget.name}, attackPosition={attackPosition}, accepted={routeAccepted}, forceRoute={forceRoute}");
            }
        }

        private bool IsInAttackRange(EnemyHealth enemy)
        {
            return GetDistanceToEnemy(enemy) <= attackRange;
        }

        private float GetDistanceToEnemy(EnemyHealth enemy)
        {
            Vector2 playerPosition = transform.position;

            if (TryGetEnemyBounds(enemy, out Bounds bounds))
            {
                Vector2 closestPoint = bounds.ClosestPoint(playerPosition);
                return Vector2.Distance(playerPosition, closestPoint);
            }

            return Vector2.Distance(playerPosition, enemy.transform.position);
        }

        private Vector2 GetAttackPosition(EnemyHealth enemy)
        {
            float directionFromEnemy = transform.position.x <= enemy.transform.position.x ? -1f : 1f;
            float horizontalOffset = Mathf.Max(0.1f, attackRange * attackPointInset);
            return (Vector2)enemy.transform.position + (Vector2.right * directionFromEnemy * horizontalOffset);
        }

        private EnemyHealth FindEnemyByScreenBounds(Vector2 screenPosition)
        {
            EnemyHealth[] enemies = FindObjectsByType<EnemyHealth>(FindObjectsInactive.Exclude);
            EnemyHealth closestEnemy = null;
            float closestSqrDistance = float.MaxValue;

            foreach (EnemyHealth enemy in enemies)
            {
                if (enemy == null || !IsInLayerMask(enemy.gameObject.layer, enemyLayers))
                {
                    continue;
                }

                if (!TryGetEnemyBounds(enemy, out Bounds bounds)
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
                    closestEnemy = enemy;
                }
            }

            return closestEnemy;
        }

        private static bool TryGetEnemyBounds(EnemyHealth enemy, out Bounds bounds)
        {
            bool hasBounds = false;
            bounds = new Bounds(enemy.transform.position, Vector3.zero);

            foreach (Collider2D enemyCollider in enemy.GetComponentsInChildren<Collider2D>())
            {
                if (enemyCollider == null || !enemyCollider.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = enemyCollider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(enemyCollider.bounds);
                }
            }

            foreach (Renderer renderer in enemy.GetComponentsInChildren<Renderer>())
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

        private void Hit(EnemyHealth enemy)
        {
            int damage = RollDamage(out bool didCrit);
            enemy.TakeDamage(damage);
            Debug.Log(
                $"Player hit {enemy.name} for {damage}{(didCrit ? " CRIT" : string.Empty)}. HP: {enemy.CurrentHealth}/{enemy.MaxHealth}",
                enemy);
            nextAttackTime = Time.time + (1f / GetEffectiveAttacksPerSecond());

            if (!enemy.IsAlive)
            {
                pendingTarget = null;
            }
        }

        private void LogCombatRouting(string message)
        {
            if (logCombatRoutingDebug)
            {
                Debug.Log($"{nameof(PlayerCombatClick2D)}: {message}", this);
            }
        }

        private int RollDamage(out bool didCrit)
        {
            ResolveStats();

            float strength = stats != null ? stats.GetValue(CharacterStatType.Strength) : 0f;
            float critChance = stats != null ? stats.GetValue(CharacterStatType.CritChance) : 0f;
            float critDamage = stats != null ? stats.GetValue(CharacterStatType.CritDamage) : 1f;
            float damage = Mathf.Max(1f, attackDamage + strength);

            didCrit = UnityEngine.Random.value < Mathf.Clamp01(critChance);

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
    }
}
