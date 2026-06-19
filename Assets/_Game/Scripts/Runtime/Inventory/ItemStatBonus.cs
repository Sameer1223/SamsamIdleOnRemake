using System;
using SamsamIdleOn.Stats;
using UnityEngine;

namespace SamsamIdleOn.Inventory
{
    [Serializable]
    public sealed class ItemStatBonus
    {
        [SerializeField] private CharacterStatType stat;
        [SerializeField] private float value;
        [SerializeField] private StatModifierKind kind = StatModifierKind.Flat;
        [SerializeField] private bool applyPerStack;

        public CharacterStatType Stat => stat;

        public float Value => value;

        public StatModifierKind Kind => kind;

        public bool ApplyPerStack => applyPerStack;
    }
}
