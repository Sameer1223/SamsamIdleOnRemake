using UnityEngine;

namespace SamsamIdleOn.Combat
{
    public interface ICombatTarget
    {
        bool IsTargetable { get; }

        string DisplayName { get; }

        Transform TargetTransform { get; }

        Component TargetComponent { get; }

        void ApplyHit(int damage, GameObject attacker);
    }
}
