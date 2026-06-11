using UnityEngine;

namespace SamsamIdleOn.Data
{
    public abstract class Definition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;

        public string Id => id;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    }
}

