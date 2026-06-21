using System;

namespace SamsamIdleOn.Persistence
{
    [Serializable]
    public sealed class SavedEnemyKillData
    {
        public string enemyId = string.Empty;
        public int count;
    }
}
