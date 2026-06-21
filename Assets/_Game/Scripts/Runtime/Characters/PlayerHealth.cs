using System;
using System.Collections;
using SamsamIdleOn.Core;
using SamsamIdleOn.Stats;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SamsamIdleOn.Characters
{
    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField, Min(1)] private int maxHealth = 100;
        [SerializeField, Min(0f)] private float healthRegenPerSecond = 1f;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private PlayerStats stats;

        [Header("Death")]
        [SerializeField] private string startingSceneName = "Main";
        [SerializeField, Min(0f)] private float deathPromptSeconds = 2f;
        [SerializeField] private string deathMessage = "You died. Returning to base.";
        [SerializeField, Range(0f, 1f)] private float deathScreenDimAlpha = 0.55f;

        [Header("Debug")]
        [SerializeField] private bool testDeathOnStart;

        private bool isDead;
        private Coroutine deathRoutine;
        private int lastKnownMaxHealth;

        public event Action<PlayerHealth> HealthChanged;
        public event Action<PlayerHealth> Died;

        public float CurrentHealth { get; private set; }

        public int MaxHealth => stats != null
            ? stats.GetIntValue(CharacterStatType.MaxHealth)
            : maxHealth;

        public float HealthRegenPerSecond
        {
            get => stats != null
                ? stats.GetValue(CharacterStatType.HealthRegen)
                : healthRegenPerSecond;
            set => healthRegenPerSecond = Mathf.Max(0f, value);
        }

        public bool IsAlive => !isDead;

        private void Awake()
        {
            ResolveGameManager();
            ResolveStats();
            LoadStats();
            lastKnownMaxHealth = MaxHealth;

            if (testDeathOnStart)
            {
                KillForTesting();
            }
        }

        private void OnEnable()
        {
            ResolveStats();

            if (stats != null)
            {
                stats.StatsChanged -= HandleStatsChanged;
                stats.StatsChanged += HandleStatsChanged;
            }
        }

        private void OnDisable()
        {
            if (stats != null)
            {
                stats.StatsChanged -= HandleStatsChanged;
            }
        }

        private void Update()
        {
            float currentMaxHealth = MaxHealth;
            float currentRegen = HealthRegenPerSecond;

            if (isDead || currentRegen <= 0f || CurrentHealth >= currentMaxHealth)
            {
                return;
            }

            CurrentHealth = Mathf.Min(currentMaxHealth, CurrentHealth + currentRegen * Time.deltaTime);
            WriteHealthToSave();
            HealthChanged?.Invoke(this);
        }

        public void TakeDamage(float damage)
        {
            if (isDead || damage <= 0f)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
            WriteHealthToSave();
            HealthChanged?.Invoke(this);

            if (CurrentHealth <= 0f)
            {
                Die();
            }
        }

        public void Heal(float amount)
        {
            if (isDead || amount <= 0f)
            {
                return;
            }

            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
            WriteHealthToSave();
            HealthChanged?.Invoke(this);
        }

        public void ResetStats()
        {
            isDead = false;
            CurrentHealth = MaxHealth;
            GetComponent<PlayerClickMovement2D>()?.SetMovementEnabled(true);
            WriteHealthToSave();
            HealthChanged?.Invoke(this);
        }

        [ContextMenu("Test Death")]
        public void KillForTesting()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning($"{nameof(PlayerHealth)} test death only runs in Play Mode.", this);
                return;
            }

            CurrentHealth = 0f;
            WriteHealthToSave();
            HealthChanged?.Invoke(this);
            Die();
        }

        private void LoadStats()
        {
            isDead = false;

            if (gameManager != null && gameManager.SaveData != null && gameManager.SaveData.currentHealth >= 0f)
            {
                CurrentHealth = Mathf.Clamp(gameManager.SaveData.currentHealth, 0f, MaxHealth);
            }
            else
            {
                CurrentHealth = MaxHealth;
                WriteHealthToSave();
            }

            lastKnownMaxHealth = MaxHealth;
            HealthChanged?.Invoke(this);
        }

        private void WriteHealthToSave()
        {
            ResolveGameManager();

            if (gameManager != null && gameManager.SaveData != null)
            {
                gameManager.SaveData.currentHealth = CurrentHealth;
            }
        }

        private void ResolveGameManager()
        {
            if (GameManager.Instance != null)
            {
                gameManager = GameManager.Instance;
            }
            else if (gameManager == null)
            {
                gameManager = FindAnyObjectByType<GameManager>();
            }

            gameManager?.Initialize();
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

        private void HandleStatsChanged()
        {
            int currentMaxHealth = MaxHealth;

            if (!isDead && currentMaxHealth > lastKnownMaxHealth)
            {
                CurrentHealth += currentMaxHealth - lastKnownMaxHealth;
            }

            CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, currentMaxHealth);
            lastKnownMaxHealth = currentMaxHealth;
            WriteHealthToSave();
            HealthChanged?.Invoke(this);
        }

        private void Die()
        {
            if (isDead)
            {
                return;
            }

            isDead = true;
            PlayerClickMovement2D movement = GetComponent<PlayerClickMovement2D>();
            movement?.Stop();
            movement?.SetMovementEnabled(false);
            Died?.Invoke(this);

            if (deathRoutine != null)
            {
                StopCoroutine(deathRoutine);
            }

            deathRoutine = StartCoroutine(ShowDeathPromptAndReturnToStart());
        }

        private IEnumerator ShowDeathPromptAndReturnToStart()
        {
            CreateDeathPrompt();

            if (deathPromptSeconds > 0f)
            {
                yield return new WaitForSeconds(deathPromptSeconds);
            }

            ResetStats();
            GetComponent<PlayerResources>()?.ResetResources();
            SceneManager.LoadScene(startingSceneName);
        }

        private void CreateDeathPrompt()
        {
            Canvas canvas = new GameObject("Death Prompt Canvas").AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            CanvasScaler scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            canvas.gameObject.AddComponent<GraphicRaycaster>();

            Image dim = new GameObject("Death Screen Dim").AddComponent<Image>();
            dim.transform.SetParent(canvas.transform, false);
            dim.color = new Color(0f, 0f, 0f, deathScreenDimAlpha);
            dim.raycastTarget = true;

            RectTransform dimRectTransform = dim.rectTransform;
            dimRectTransform.anchorMin = Vector2.zero;
            dimRectTransform.anchorMax = Vector2.one;
            dimRectTransform.offsetMin = Vector2.zero;
            dimRectTransform.offsetMax = Vector2.zero;

            Text text = new GameObject("Death Prompt").AddComponent<Text>();
            text.transform.SetParent(canvas.transform, false);
            text.text = deathMessage;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 42;

            RectTransform rectTransform = text.rectTransform;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
