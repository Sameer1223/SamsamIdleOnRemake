using System;
using SamsamIdleOn.Persistence;

namespace SamsamIdleOn.Core
{
    public sealed class GameClock
    {
        public DateTime UtcNow => DateTime.UtcNow;

        public TimeSpan GetOfflineDuration(SaveData saveData)
        {
            if (string.IsNullOrWhiteSpace(saveData.lastClosedUtc))
            {
                return TimeSpan.Zero;
            }

            if (!DateTime.TryParse(saveData.lastClosedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime lastClosedUtc))
            {
                return TimeSpan.Zero;
            }

            TimeSpan duration = UtcNow - lastClosedUtc;
            return duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
        }
    }
}

