using System.Collections;
using SamsamIdleOn.Characters;
using UnityEngine;

namespace SamsamIdleOn.Enemies
{
    [DisallowMultipleComponent]
    public sealed class BossGroundFireAttack2D : MonoBehaviour
    {
        private enum TargetMode
        {
            RandomGround,
            NearPlayer
        }

        [Header("Timing")]
        [SerializeField] private bool attackOnStart = true;
        [SerializeField, Min(0.1f)] private float firstAttackDelay = 1.5f;
        [SerializeField, Min(0.1f)] private float attackInterval = 4f;
        [SerializeField, Min(0.1f)] private float warningSeconds = 1.25f;
        [SerializeField, Min(0.1f)] private float fireLifetimeSeconds = 2.5f;

        [Header("Pattern")]
        [SerializeField] private TargetMode targetMode = TargetMode.NearPlayer;
        [SerializeField, Min(1)] private int firePatchesPerAttack = 3;
        [SerializeField] private Vector2 arenaCenter;
        [SerializeField] private Vector2 arenaSize = new(10f, 4f);
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField] private Vector2 indicatorSize = new(1.1f, 0.12f);
        [SerializeField] private Vector2 fireSize = new(1.1f, 0.35f);
        [SerializeField] private float groundOffset = 0.08f;
        [SerializeField] private float randomXSpacing = 1.1f;
        [SerializeField] private float playerAimSpread = 1.3f;
        [SerializeField, Min(1)] private int maxGroundSampleAttempts = 20;

        [Header("Damage")]
        [SerializeField, Min(0f)] private float fireDamage = 12f;
        [SerializeField, Min(0.01f)] private float damageCooldownSeconds = 0.65f;

        [Header("Visuals")]
        [SerializeField] private Sprite fireSprite;
        [SerializeField, Min(0.01f)] private float fireSpriteScale = 1f;
        [SerializeField] private Vector2 fireSpriteOffset;
        [SerializeField] private Color indicatorColor = new(1f, 0.15f, 0.05f, 0.35f);
        [SerializeField] private Color fireColor = new(1f, 0.35f, 0.02f, 0.88f);
        [SerializeField] private int sortingOrder = 80;

        private static Sprite squareSprite;

        private Coroutine attackRoutine;
        private EnemyHealth enemyHealth;
        private PlayerHealth playerHealth;

        private void Awake()
        {
            enemyHealth = GetComponent<EnemyHealth>();
        }

        private void OnEnable()
        {
            if (attackOnStart)
            {
                attackRoutine = StartCoroutine(AttackLoop());
            }
        }

        private void OnDisable()
        {
            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
                attackRoutine = null;
            }
        }

        [ContextMenu("Trigger Fire Attack")]
        public void TriggerFireAttack()
        {
            StartCoroutine(SpawnFirePattern());
        }

        private IEnumerator AttackLoop()
        {
            yield return new WaitForSeconds(firstAttackDelay);

            while (enemyHealth == null || enemyHealth.IsAlive)
            {
                yield return SpawnFirePattern();
                yield return new WaitForSeconds(attackInterval);
            }
        }

        private IEnumerator SpawnFirePattern()
        {
            for (int i = 0; i < firePatchesPerAttack; i++)
            {
                if (TryGetFirePosition(i, out Vector2 firePosition))
                {
                    StartCoroutine(SpawnTelegraphedFire(firePosition));
                }
            }

            yield return new WaitForSeconds(warningSeconds + fireLifetimeSeconds);
        }

        private IEnumerator SpawnTelegraphedFire(Vector2 position)
        {
            GameObject indicator = CreateVisual("Fire Indicator", position, indicatorColor, sortingOrder, indicatorSize, null);

            yield return new WaitForSeconds(warningSeconds);

            if (indicator != null)
            {
                Destroy(indicator);
            }

            GameObject fire = CreateFireHazard(position);
            BoxCollider2D collider = fire.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = fireSize;

            EnemyContactDamage2D contactDamage = fire.AddComponent<EnemyContactDamage2D>();
            contactDamage.Configure(fireDamage, damageCooldownSeconds);

            Destroy(fire, fireLifetimeSeconds);
        }

        private bool TryGetFirePosition(int index, out Vector2 position)
        {
            if (targetMode == TargetMode.NearPlayer
                && TryGetPlayerPosition(out Vector2 playerPosition)
                && TryGetGroundPositionNear(playerPosition.x + GetPlayerPatternOffset(index), out position))
            {
                return true;
            }

            return TryGetRandomGroundPosition(index, out position);
        }

        private bool TryGetPlayerPosition(out Vector2 position)
        {
            if (playerHealth == null || !playerHealth.IsAlive)
            {
                playerHealth = FindAnyObjectByType<PlayerHealth>();
            }

            if (playerHealth == null)
            {
                position = default;
                return false;
            }

            position = playerHealth.transform.position;
            return true;
        }

        private float GetPlayerPatternOffset(int index)
        {
            if (firePatchesPerAttack <= 1)
            {
                return 0f;
            }

            float centerIndex = (firePatchesPerAttack - 1) * 0.5f;
            return (index - centerIndex) * playerAimSpread;
        }

        private bool TryGetRandomGroundPosition(int index, out Vector2 position)
        {
            float minX = arenaCenter.x - arenaSize.x * 0.5f;
            float maxX = arenaCenter.x + arenaSize.x * 0.5f;

            for (int attempt = 0; attempt < maxGroundSampleAttempts; attempt++)
            {
                float x = Random.Range(minX, maxX);

                if (index > 0)
                {
                    x += (index - (firePatchesPerAttack - 1) * 0.5f) * randomXSpacing;
                }

                if (TryGetGroundPositionNear(x, out position))
                {
                    return true;
                }
            }

            position = default;
            return false;
        }

        private bool TryGetGroundPositionNear(float x, out Vector2 position)
        {
            float halfWidth = arenaSize.x * 0.5f;
            float clampedX = Mathf.Clamp(x, arenaCenter.x - halfWidth, arenaCenter.x + halfWidth);
            Vector2 rayOrigin = new(clampedX, arenaCenter.y + arenaSize.y * 0.5f);
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, arenaSize.y, groundLayers);

            if (hit.collider == null || hit.collider.isTrigger)
            {
                position = default;
                return false;
            }

            position = hit.point + Vector2.up * groundOffset;
            return true;
        }

        private GameObject CreateVisual(string objectName, Vector2 position, Color color, int order, Vector2 size, Sprite sprite)
        {
            GameObject visual = new(objectName);
            visual.transform.position = position;
            visual.transform.localScale = new Vector3(size.x, size.y, 1f);

            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite != null ? sprite : GetSquareSprite();
            renderer.color = color;
            renderer.sortingOrder = order;
            return visual;
        }

        private GameObject CreateFireHazard(Vector2 position)
        {
            GameObject hazard = new("Boss Fire");
            hazard.transform.position = position;

            GameObject visual = new("Fire Sprite");
            visual.transform.SetParent(hazard.transform, false);
            visual.transform.localPosition = fireSpriteOffset;
            visual.transform.localScale = Vector3.one * fireSpriteScale;

            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = fireSprite != null ? fireSprite : GetSquareSprite();
            renderer.color = fireSprite != null ? Color.white : fireColor;
            renderer.sortingOrder = sortingOrder + 1;

            if (fireSprite == null)
            {
                visual.transform.localScale = new Vector3(fireSize.x, fireSize.y, 1f);
            }

            return hazard;
        }

        private static Sprite GetSquareSprite()
        {
            if (squareSprite != null)
            {
                return squareSprite;
            }

            Texture2D texture = new(1, 1)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            squareSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            squareSprite.hideFlags = HideFlags.HideAndDontSave;
            return squareSprite;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.25f, 0.05f, 0.25f);
            Gizmos.DrawCube(arenaCenter, arenaSize);
            Gizmos.color = new Color(1f, 0.25f, 0.05f, 1f);
            Gizmos.DrawWireCube(arenaCenter, arenaSize);
        }
    }
}
