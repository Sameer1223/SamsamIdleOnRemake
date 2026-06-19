using SamsamIdleOn.Data;
using System.Collections.Generic;
using UnityEngine;

namespace SamsamIdleOn.Inventory
{
    [CreateAssetMenu(menuName = "Samsam IdleOn/Inventory/Item Definition", fileName = "ItemDefinition")]
    public sealed class ItemDefinition : Definition
    {
        [SerializeField] private Sprite icon;
        [SerializeField, Min(1)] private int maxStack = 999;
        [SerializeField, TextArea] private string description;
        [SerializeField] private List<ItemStatBonus> statBonuses = new();

        public Sprite Icon => icon;

        public int MaxStack => maxStack;

        public string Description => description;

        public IReadOnlyList<ItemStatBonus> StatBonuses => statBonuses;
    }
}
