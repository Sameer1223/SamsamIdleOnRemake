using System;

namespace SamsamIdleOn.Persistence
{
    [Serializable]
    public sealed class SavedUpgradeLevelData
    {
        public string upgradeId = string.Empty;
        public int level;
    }
}
