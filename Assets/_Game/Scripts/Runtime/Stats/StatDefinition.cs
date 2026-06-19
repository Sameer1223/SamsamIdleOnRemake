using System;
using UnityEngine;

namespace SamsamIdleOn.Stats
{
    [Serializable]
    public sealed class StatDefinition
    {
        [SerializeField] private CharacterStatType stat;
        [SerializeField] private float baseValue;
        [SerializeField] private float minimumValue;

        public StatDefinition()
        {
        }

        public StatDefinition(CharacterStatType stat, float baseValue, float minimumValue = 0f)
        {
            this.stat = stat;
            this.baseValue = baseValue;
            this.minimumValue = minimumValue;
        }

        public CharacterStatType Stat => stat;

        public float BaseValue => baseValue;

        public float MinimumValue => minimumValue;
    }
}
