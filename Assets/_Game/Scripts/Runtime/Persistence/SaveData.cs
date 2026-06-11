using System;

namespace SamsamIdleOn.Persistence
{
    [Serializable]
    public sealed class SaveData
    {
        public int schemaVersion = 1;
        public string playerName = "Sameer";
        public int playerLevel = 1;
        public long experience;
        public long gold;
        public string currentActivityId = "idle";
        public string lastSavedUtc = string.Empty;
        public string lastClosedUtc = string.Empty;

        public static SaveData CreateNew(DateTime utcNow)
        {
            string timestamp = utcNow.ToString("O");

            return new SaveData
            {
                lastSavedUtc = timestamp,
                lastClosedUtc = timestamp
            };
        }
    }
}

