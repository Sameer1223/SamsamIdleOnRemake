using System;

namespace SamsamIdleOn.Stats
{
    public enum StatModifierKind
    {
        Flat,
        AdditivePercent,
        MultiplicativePercent
    }

    [Serializable]
    public sealed class CharacterStatModifier
    {
        public CharacterStatModifier(CharacterStatType stat, float value, StatModifierKind kind, object source = null)
        {
            Stat = stat;
            Value = value;
            Kind = kind;
            Source = source;
        }

        public CharacterStatType Stat { get; }

        public float Value { get; }

        public StatModifierKind Kind { get; }

        public object Source { get; }
    }
}
