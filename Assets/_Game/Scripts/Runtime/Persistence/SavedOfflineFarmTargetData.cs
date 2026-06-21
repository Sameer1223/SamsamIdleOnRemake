using System;
using System.Collections.Generic;

namespace SamsamIdleOn.Persistence
{
    [Serializable]
    public sealed class SavedOfflineFarmTargetData
    {
        public const string EnemyTargetKind = "enemy";
        public const string MiningTargetKind = "mining";

        public string targetKind = EnemyTargetKind;
        public string displayName = string.Empty;
        public int enemyHealth;
        public float secondsPerAction;
        public int minExperienceReward;
        public int maxExperienceReward;
        public int minCoinBronzeReward;
        public int maxCoinBronzeReward;
        public List<SavedOfflineDropData> drops = new();

        public bool IsMining => string.Equals(targetKind, MiningTargetKind, StringComparison.OrdinalIgnoreCase);

        public bool IsValid => !string.IsNullOrWhiteSpace(displayName)
            && (IsMining || enemyHealth > 0);

        public void EnsureDefaults()
        {
            targetKind = string.IsNullOrWhiteSpace(targetKind) ? EnemyTargetKind : targetKind;
            drops ??= new List<SavedOfflineDropData>();
        }
    }
}
