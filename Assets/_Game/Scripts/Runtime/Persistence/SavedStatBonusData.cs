using System;
using SamsamIdleOn.Stats;

namespace SamsamIdleOn.Persistence
{
    [Serializable]
    public sealed class SavedStatBonusData
    {
        public CharacterStatType stat;
        public float flatBonus;
        public float additivePercentBonus;
        public float multiplicativePercentBonus;
    }
}
