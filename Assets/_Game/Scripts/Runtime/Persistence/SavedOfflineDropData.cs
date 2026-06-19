using System;

namespace SamsamIdleOn.Persistence
{
    [Serializable]
    public sealed class SavedOfflineDropData
    {
        public string itemId = string.Empty;
        public float dropChance;
        public int minCount = 1;
        public int maxCount = 1;
    }
}
