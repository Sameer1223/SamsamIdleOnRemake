using System;

namespace SamsamIdleOn.Inventory
{
    [Serializable]
    public sealed class InventorySlotData
    {
        public string itemId = string.Empty;
        public int count;

        public bool IsEmpty => string.IsNullOrWhiteSpace(itemId) || count <= 0;

        public void Clear()
        {
            itemId = string.Empty;
            count = 0;
        }
    }
}
