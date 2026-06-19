using System;
using System.Collections.Generic;

namespace SamsamIdleOn.Persistence
{
    [Serializable]
    public sealed class SavedOfflineFarmTargetData
    {
        public string displayName = string.Empty;
        public int enemyHealth;
        public int minExperienceReward;
        public int maxExperienceReward;
        public int minCoinBronzeReward;
        public int maxCoinBronzeReward;
        public List<SavedOfflineDropData> drops = new();

        public bool IsValid => enemyHealth > 0 && !string.IsNullOrWhiteSpace(displayName);

        public void EnsureDefaults()
        {
            drops ??= new List<SavedOfflineDropData>();
        }
    }
}
