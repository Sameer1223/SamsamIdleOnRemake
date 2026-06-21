using System.Collections;
using SamsamIdleOn.Characters;
using SamsamIdleOn.Combat;
using SamsamIdleOn.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace SamsamIdleOn.UI
{
    public sealed class GameplayHud : MonoBehaviour
    {
        [Header("Data Sources")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private PlayerResources playerResources;
        [SerializeField] private PlayerCombatClick2D playerCombat;
        [SerializeField] private bool autoFindReferences = true;

        [Header("Metric Labels")]
        [SerializeField] private TextMeshProUGUI hpLabel;
        [SerializeField] private TextMeshProUGUI mpLabel;
        [SerializeField] private TextMeshProUGUI xpLabel;
        [SerializeField] private TextMeshProUGUI levelLabel;
        [SerializeField] private TextMeshProUGUI coinLabel;

        [Header("Metric Sliders")]
        [SerializeField] private Slider hpSlider;
        [SerializeField] private Slider mpSlider;
        [SerializeField] private Slider xpSlider;

        [Header("Panels")]
        [SerializeField] private GameObject metricsPanel;
        [SerializeField] private GameObject attacksPanel;

        [Header("Buttons")]
        [SerializeField] private Button autoButton;
        [SerializeField] private Text autoButtonLabel;
        [SerializeField] private TMP_Text autoButtonTmpLabel;
        [SerializeField] private Button codexButton;
        [SerializeField] private Button attacksButton;

        [Header("Button Labels")]
        [SerializeField] private string autoOffText = "Auto: OFF";
        [SerializeField] private string autoOnText = "Auto: ON";

        private bool isAutoEnabled;
        private bool showingAttacks;
        private Coroutine rebindRoutine;

        private void Awake()
        {
            ResolveReferences();
            HookButtons();
            ShowMetricsPanel();
            RefreshAll();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            Subscribe();
            RefreshAll();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            StopRebindRoutine();
            Unsubscribe();
        }

        private void Start()
        {
            ResolveReferences();
            Subscribe();
            RefreshAll();
        }

        private void Update()
        {
            if (autoFindReferences && (playerHealth == null || playerResources == null || playerCombat == null))
            {
                ResolveReferences(false);
                Subscribe();
            }

            RefreshVitals();
            RefreshProgress();
        }

        [ContextMenu("Refresh HUD")]
        public void RefreshAll()
        {
            RefreshVitals();
            RefreshProgress();
            RefreshAutoLabel();
            RefreshPanelVisibility();
        }

        public void ToggleAuto()
        {
            if (playerCombat != null)
            {
                playerCombat.ToggleAutoCombat();
                isAutoEnabled = playerCombat.IsAutoCombatEnabled;
            }
            else
            {
                isAutoEnabled = !isAutoEnabled;
            }

            RefreshAutoLabel();
        }

        public void OpenCodex()
        {
        }

        public void ToggleAttacksPanel()
        {
            showingAttacks = !showingAttacks;
            RefreshPanelVisibility();
        }

        public void ShowMetricsPanel()
        {
            showingAttacks = false;
            RefreshPanelVisibility();
        }

        public void ShowAttacksPanel()
        {
            showingAttacks = true;
            RefreshPanelVisibility();
        }

        private void ResolveReferences()
        {
            ResolveReferences(false);
        }

        private void ResolveReferences(bool force)
        {
            if (!autoFindReferences)
            {
                return;
            }

            if (force)
            {
                Unsubscribe();
                gameManager = null;
                playerHealth = null;
                playerResources = null;
                playerCombat = null;
            }

            if (GameManager.Instance != null)
            {
                gameManager = GameManager.Instance;
            }
            else if (gameManager == null)
            {
                gameManager = FindAnyObjectByType<GameManager>();
            }

            if (playerHealth == null)
            {
                playerHealth = FindAnyObjectByType<PlayerHealth>();
            }

            if (playerResources == null)
            {
                playerResources = FindAnyObjectByType<PlayerResources>();
            }

            if (playerCombat == null)
            {
                playerCombat = FindAnyObjectByType<PlayerCombatClick2D>();
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StopRebindRoutine();
            rebindRoutine = StartCoroutine(RebindAfterSceneLoad());
        }

        private IEnumerator RebindAfterSceneLoad()
        {
            yield return null;

            ResolveReferences(true);
            Subscribe();
            RefreshAll();
            rebindRoutine = null;
        }

        private void StopRebindRoutine()
        {
            if (rebindRoutine == null)
            {
                return;
            }

            StopCoroutine(rebindRoutine);
            rebindRoutine = null;
        }

        private void HookButtons()
        {
            if (autoButton != null)
            {
                autoButton.onClick.RemoveListener(ToggleAuto);
                autoButton.onClick.AddListener(ToggleAuto);
            }

            if (codexButton != null)
            {
                codexButton.onClick.RemoveListener(OpenCodex);
                codexButton.onClick.AddListener(OpenCodex);
            }

            if (attacksButton != null)
            {
                attacksButton.onClick.RemoveListener(ToggleAttacksPanel);
                attacksButton.onClick.AddListener(ToggleAttacksPanel);
            }
        }

        private void Subscribe()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged -= RefreshProgress;
                gameManager.StateChanged += RefreshProgress;
                gameManager.Initialize();
            }

            if (playerHealth != null)
            {
                playerHealth.HealthChanged -= HandleHealthChanged;
                playerHealth.HealthChanged += HandleHealthChanged;
            }

            if (playerResources != null)
            {
                playerResources.ManaChanged -= HandleManaChanged;
                playerResources.ManaChanged += HandleManaChanged;
            }

            if (playerCombat != null)
            {
                playerCombat.AutoCombatChanged -= HandleAutoCombatChanged;
                playerCombat.AutoCombatChanged += HandleAutoCombatChanged;
                isAutoEnabled = playerCombat.IsAutoCombatEnabled;
            }
        }

        private void Unsubscribe()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged -= RefreshProgress;
            }

            if (playerHealth != null)
            {
                playerHealth.HealthChanged -= HandleHealthChanged;
            }

            if (playerResources != null)
            {
                playerResources.ManaChanged -= HandleManaChanged;
            }

            if (playerCombat != null)
            {
                playerCombat.AutoCombatChanged -= HandleAutoCombatChanged;
            }
        }

        private void HandleHealthChanged(PlayerHealth health)
        {
            RefreshVitals();
        }

        private void HandleManaChanged(PlayerResources resources)
        {
            RefreshVitals();
        }

        private void HandleAutoCombatChanged(bool isEnabled)
        {
            isAutoEnabled = isEnabled;
            RefreshAutoLabel();
        }

        private void RefreshVitals()
        {
            if (playerHealth != null)
            {
                SetText(hpLabel, $"{Mathf.CeilToInt(playerHealth.CurrentHealth)}/{playerHealth.MaxHealth}");
                SetSlider(hpSlider, playerHealth.CurrentHealth, playerHealth.MaxHealth);
            }

            if (playerResources != null)
            {
                SetText(mpLabel, $"{Mathf.CeilToInt(playerResources.CurrentMana)}/{playerResources.MaxMana}");
                SetSlider(mpSlider, playerResources.CurrentMana, playerResources.MaxMana);
            }
        }

        private void RefreshProgress()
        {
            if (gameManager == null || gameManager.SaveData == null)
            {
                return;
            }

            long experienceRequired = gameManager.GetExperienceRequiredForNextLevel();
            SetText(xpLabel, $"{gameManager.SaveData.experience}/{experienceRequired}");
            SetText(levelLabel, $"Lv. {gameManager.PlayerLevel}");
            SetText(coinLabel, gameManager.GetFormattedCoins());
            SetSlider(xpSlider, gameManager.SaveData.experience, experienceRequired);
        }

        private void RefreshAutoLabel()
        {
            SetText(autoButtonLabel, isAutoEnabled ? autoOnText : autoOffText);
            SetText(autoButtonTmpLabel, isAutoEnabled ? autoOnText : autoOffText);
        }

        private void RefreshPanelVisibility()
        {
            if (metricsPanel != null)
            {
                metricsPanel.SetActive(!showingAttacks);
            }

            if (attacksPanel != null)
            {
                attacksPanel.SetActive(showingAttacks);
            }
        }

        private static void SetText(Text label, string text)
        {
            if (label != null)
            {
                label.text = text;
            }
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (label != null)
            {
                label.text = text;
            }
        }

        private static void SetSlider(Slider slider, float current, float maximum)
        {
            if (slider == null)
            {
                return;
            }

            float safeMaximum = Mathf.Max(1f, maximum);
            slider.minValue = 0f;
            slider.maxValue = safeMaximum;
            slider.SetValueWithoutNotify(Mathf.Clamp(current, 0f, safeMaximum));
        }

    }
}
