using System;
using UnityEngine;

namespace SamsamIdleOn.Inventory
{
    [Serializable]
    public struct ItemStackRequirement
    {
        [SerializeField] private ItemDefinition item;
        [SerializeField] private string itemId;
        [SerializeField, Min(1)] private int count;

        public ItemDefinition Item => item;

        public string ItemId => item != null ? item.Id : itemId;

        public int Count => Mathf.Max(1, count);
    }
}
