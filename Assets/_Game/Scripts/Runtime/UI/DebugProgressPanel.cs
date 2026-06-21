using SamsamIdleOn.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SamsamIdleOn.UI
{
    public sealed class DebugProgressPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameManager gameManager;

        [Header("Labels")]
        [SerializeField] private Text playerNameLabel;
        [SerializeField] private Text levelLabel;
        [SerializeField] private Text experienceLabel;
        [SerializeField] private Text goldLabel;
        [SerializeField] private Text activityLabel;
        [SerializeField] private Text offlineTimeLabel;
        [SerializeField] private Text savePathLabel;

        [Header("Debug Amounts")]
        [SerializeField] private int debugExperienceAmount = 25;
        [SerializeField] private int debugGoldAmount = 10;

        private void Awake()
        {
            if (gameManager == null)
            {
                gameManager = FindAnyObjectByType<GameManager>();
            }
        }

        private void OnEnable()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged += Refresh;
                gameManager.Initialize();
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged -= Refresh;
            }
        }

        public void AddDebugExperience()
        {
            gameManager?.AddExperience(debugExperienceAmount);
        }

        public void AddDebugGold()
        {
            gameManager?.AddGold(debugGoldAmount);
        }

        public void SetIdleActivity()
        {
            gameManager?.SetActivity("idle");
        }

        public void ResetDebugSave()
        {
            gameManager?.ResetSave();
        }

        private void Refresh()
        {
            if (gameManager == null || gameManager.SaveData == null)
            {
                return;
            }

            SetText(playerNameLabel, gameManager.SaveData.playerName);
            SetText(levelLabel, $"Level {gameManager.SaveData.playerLevel}");
            SetText(experienceLabel, $"XP {gameManager.SaveData.experience}/{gameManager.GetExperienceRequiredForNextLevel()}");
            SetText(goldLabel, $"Gold {gameManager.SaveData.gold}");
            SetText(activityLabel, $"Activity {gameManager.SaveData.currentActivityId}");
            SetText(offlineTimeLabel, $"Offline {FormatDuration(gameManager.OfflineDuration)}");
            SetText(savePathLabel, gameManager.SavePath);
        }

        private static void SetText(Text label, string text)
        {
            if (label != null)
            {
                label.text = text;
            }
        }

        private static string FormatDuration(System.TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
            {
                return $"{duration.Hours}h {duration.Minutes}m";
            }

            if (duration.TotalMinutes >= 1)
            {
                return $"{duration.Minutes}m {duration.Seconds}s";
            }

            return $"{duration.Seconds}s";
        }
    }
}
