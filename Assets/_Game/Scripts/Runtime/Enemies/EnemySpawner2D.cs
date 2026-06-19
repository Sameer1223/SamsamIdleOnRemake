using System.Collections.Generic;
using UnityEngine;

namespace SamsamIdleOn.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemySpawner2D : MonoBehaviour
    {
        [Header("Enemy")]
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private Transform enemyParent;

        [Header("Population")]
        [SerializeField, Min(0)] private int initialSpawnCount = 6;
        [SerializeField, Min(0)] private int respawnThreshold = 2;
        [SerializeField, Min(1)] private int respawnBatchCount = 4;

        [Header("Ground Sampling")]
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField] private Vector2 spawnAreaCenter;
        [SerializeField] private Vector2 spawnAreaSize = new Vector2(10f, 6f);
        [SerializeField] private float groundOffset = 0.05f;
        [SerializeField, Min(1)] private int maxSpawnAttempts = 24;

        private readonly List<EnemyHealth> aliveEnemies = new();

        private void Start()
        {
            SpawnBatch(initialSpawnCount);
        }

        private void OnDisable()
        {
            foreach (EnemyHealth enemy in aliveEnemies)
            {
                if (enemy != null)
                {
                    enemy.Died -= HandleEnemyDied;
                }
            }

            aliveEnemies.Clear();
        }

        private void HandleEnemyDied(EnemyHealth enemy)
        {
            if (enemy != null)
            {
                enemy.Died -= HandleEnemyDied;
            }

            aliveEnemies.Remove(enemy);

            if (aliveEnemies.Count <= respawnThreshold)
            {
                SpawnBatch(respawnBatchCount);
            }
        }

        [ContextMenu("Spawn Respawn Batch")]
        public void SpawnRespawnBatch()
        {
            SpawnBatch(respawnBatchCount);
        }

        private void SpawnBatch(int count)
        {
            if (enemyPrefab == null)
            {
                Debug.LogWarning($"{nameof(EnemySpawner2D)} on {name} cannot spawn because no enemy prefab is assigned.", this);
                return;
            }

            if (count <= 0)
            {
                return;
            }

            int spawnedCount = 0;

            for (int i = 0; i < count; i++)
            {
                if (TryFindSpawnPoint(out Vector2 spawnPoint))
                {
                    SpawnEnemy(spawnPoint);
                    spawnedCount++;
                }
            }

            if (spawnedCount == 0)
            {
                Debug.LogWarning(
                    $"{nameof(EnemySpawner2D)} on {name} found no ground spawn points. Check groundLayers and spawnArea.",
                    this);
            }
        }

        private void SpawnEnemy(Vector2 spawnPoint)
        {
            GameObject enemyObject = Instantiate(enemyPrefab, spawnPoint, Quaternion.identity, enemyParent);
            AlignEnemyToGround(enemyObject, spawnPoint.y);
            EnemyHealth enemyHealth = enemyObject.GetComponent<EnemyHealth>();

            if (enemyHealth == null)
            {
                enemyHealth = enemyObject.AddComponent<EnemyHealth>();
            }

            if (enemyObject.GetComponent<EnemyHealthBar2D>() == null)
            {
                enemyObject.AddComponent<EnemyHealthBar2D>();
            }

            if (enemyObject.GetComponent<EnemyContactDamage2D>() == null)
            {
                enemyObject.AddComponent<EnemyContactDamage2D>();
            }

            enemyHealth.Died += HandleEnemyDied;
            aliveEnemies.Add(enemyHealth);
        }

        private void AlignEnemyToGround(GameObject enemyObject, float groundY)
        {
            Bounds bounds = default;
            bool hasBounds = false;

            foreach (Collider2D enemyCollider in enemyObject.GetComponentsInChildren<Collider2D>())
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

            if (!hasBounds)
            {
                return;
            }

            float verticalAdjustment = groundY + groundOffset - bounds.min.y;
            enemyObject.transform.position += Vector3.up * verticalAdjustment;
        }

        private bool TryFindSpawnPoint(out Vector2 spawnPoint)
        {
            Vector2 min = spawnAreaCenter - (spawnAreaSize * 0.5f);
            Vector2 max = spawnAreaCenter + (spawnAreaSize * 0.5f);

            for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
            {
                float x = Random.Range(min.x, max.x);
                Vector2 rayOrigin = new Vector2(x, max.y);
                RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, spawnAreaSize.y, groundLayers);

                if (hit.collider == null || hit.collider.isTrigger)
                {
                    continue;
                }

                spawnPoint = hit.point;
                return true;
            }

            spawnPoint = transform.position;
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.55f, 0.15f, 0.35f);
            Gizmos.DrawCube(spawnAreaCenter, spawnAreaSize);
            Gizmos.color = new Color(1f, 0.55f, 0.15f, 1f);
            Gizmos.DrawWireCube(spawnAreaCenter, spawnAreaSize);
        }
    }
}
