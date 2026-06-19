using System.Collections.Generic;
using SamsamIdleOn.Data;
using UnityEngine;

namespace SamsamIdleOn.Inventory
{
    [CreateAssetMenu(menuName = "Samsam IdleOn/Inventory/Crafting Recipe", fileName = "CraftingRecipe")]
    public sealed class CraftingRecipeDefinition : Definition
    {
        [SerializeField] private List<ItemStackRequirement> ingredients = new();
        [SerializeField] private ItemDefinition outputItem;
        [SerializeField] private string outputItemId;
        [SerializeField, Min(1)] private int outputCount = 1;

        public IReadOnlyList<ItemStackRequirement> Ingredients => ingredients;

        public ItemDefinition OutputItem => outputItem;

        public string OutputItemId => outputItem != null ? outputItem.Id : outputItemId;

        public int OutputCount => outputCount;
    }
}
