using System;
using System.Collections.Generic;
using SamsamIdleOn.Core;
using UnityEngine;

namespace SamsamIdleOn.Inventory
{
    [DisallowMultipleComponent]
    public sealed class PlayerInventory : MonoBehaviour
    {
        private const int MaxItemStack = 999;

        [SerializeField, Min(1)] private int slotCount = 24;
        [SerializeField, Min(1)] private int fallbackMaxStack = 999;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private bool loadFromSaveOnAwake = true;

        private readonly List<InventorySlotData> slots = new();

        public event Action<PlayerInventory> InventoryChanged;

        public IReadOnlyList<InventorySlotData> Slots => slots;

        private void Awake()
        {
            EnsureSlotCount();

            if (loadFromSaveOnAwake)
            {
                LoadFromSave();
            }
        }

        public bool AddItem(ItemDefinition item, int count)
        {
            return item != null && AddItem(item.Id, count, item.MaxStack);
        }

        public bool AddItem(string itemId, int count)
        {
            return AddItem(itemId, count, fallbackMaxStack);
        }

        public bool CanAcceptItem(ItemDefinition item, int count)
        {
            return item != null && CanAcceptItem(item.Id, count, item.MaxStack);
        }

        public bool CanAcceptItem(string itemId, int count)
        {
            return CanAcceptItem(itemId, count, fallbackMaxStack);
        }

        public bool RemoveItem(string itemId, int count)
        {
            if (string.IsNullOrWhiteSpace(itemId) || count <= 0 || GetItemCount(itemId) < count)
            {
                return false;
            }

            int remaining = count;

            foreach (InventorySlotData slot in slots)
            {
                if (slot.itemId != itemId || slot.IsEmpty)
                {
                    continue;
                }

                int removed = Mathf.Min(slot.count, remaining);
                slot.count -= removed;
                remaining -= removed;

                if (slot.count <= 0)
                {
                    slot.Clear();
                }

                if (remaining <= 0)
                {
                    CommitChanges();
                    return true;
                }
            }

            CommitChanges();
            return true;
        }

        public bool HasItem(string itemId, int count)
        {
            return GetItemCount(itemId) >= count;
        }

        public int GetItemCount(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return 0;
            }

            int total = 0;

            foreach (InventorySlotData slot in slots)
            {
                if (slot.itemId == itemId && !slot.IsEmpty)
                {
                    total += slot.count;
                }
            }

            return total;
        }

        public bool TryGetSlot(int index, out InventorySlotData slot)
        {
            if (!IsValidSlotIndex(index))
            {
                slot = null;
                return false;
            }

            slot = slots[index];
            return slot != null && !slot.IsEmpty;
        }

        public bool TryMoveSlotToHotbar(int sourceIndex, PlayerItemHotbar hotbar, int hotbarSlotIndex)
        {
            if (hotbar == null || !TryGetSlot(sourceIndex, out InventorySlotData sourceSlot))
            {
                return false;
            }

            InventorySlotData movedSlot = CopySlot(sourceSlot);

            if (!hotbar.TryPlaceSlot(movedSlot, hotbarSlotIndex, out InventorySlotData displacedSlot))
            {
                return false;
            }

            sourceSlot.Clear();

            if (displacedSlot != null && !displacedSlot.IsEmpty)
            {
                slots[sourceIndex].itemId = displacedSlot.itemId;
                slots[sourceIndex].count = displacedSlot.count;
            }

            CommitChanges();
            return true;
        }

        public bool TryPlaceSlotFromHotbar(InventorySlotData movedSlot, int targetIndex, out InventorySlotData displacedSlot)
        {
            displacedSlot = null;

            if (!IsValidSlotIndex(targetIndex) || movedSlot == null || movedSlot.IsEmpty)
            {
                return false;
            }

            InventorySlotData targetSlot = slots[targetIndex];
            int safeMaxStack = GetSafeMaxStack(fallbackMaxStack);

            if (TryFindSlotWithItem(movedSlot.itemId, out int existingIndex, out InventorySlotData existingSlot)
                && existingIndex != targetIndex)
            {
                if (targetSlot != null && !targetSlot.IsEmpty)
                {
                    return false;
                }

                if (existingSlot.count + movedSlot.count > safeMaxStack)
                {
                    return false;
                }

                existingSlot.count += movedSlot.count;
                CommitChanges();
                return true;
            }

            if (targetSlot == null || targetSlot.IsEmpty)
            {
                slots[targetIndex].itemId = movedSlot.itemId;
                slots[targetIndex].count = Mathf.Min(ClampStackCount(movedSlot.count), safeMaxStack);
                CommitChanges();
                return true;
            }

            if (targetSlot.itemId == movedSlot.itemId)
            {
                if (targetSlot.count + movedSlot.count > safeMaxStack)
                {
                    return false;
                }

                targetSlot.count += movedSlot.count;
                CommitChanges();
                return true;
            }

            displacedSlot = CopySlot(targetSlot);
            targetSlot.itemId = movedSlot.itemId;
            targetSlot.count = Mathf.Min(ClampStackCount(movedSlot.count), safeMaxStack);
            CommitChanges();
            return true;
        }

        public bool MoveOrMergeSlot(int sourceIndex, int targetIndex)
        {
            if (!IsValidSlotIndex(sourceIndex) || !IsValidSlotIndex(targetIndex) || sourceIndex == targetIndex)
            {
                return false;
            }

            InventorySlotData source = slots[sourceIndex];
            InventorySlotData target = slots[targetIndex];

            if (source.IsEmpty)
            {
                return false;
            }

            if (target.IsEmpty)
            {
                target.itemId = source.itemId;
                target.count = ClampStackCount(source.count);
                source.Clear();
                CommitChanges();
                return true;
            }

            if (target.itemId == source.itemId)
            {
                int maxStack = GetSafeMaxStack(fallbackMaxStack);
                int availableSpace = Mathf.Max(0, maxStack - target.count);

                if (availableSpace <= 0)
                {
                    return false;
                }

                int moved = Mathf.Min(source.count, availableSpace);
                target.count += moved;
                source.count -= moved;

                if (source.count <= 0)
                {
                    source.Clear();
                }

                CommitChanges();
                return true;
            }

            (slots[sourceIndex], slots[targetIndex]) = (slots[targetIndex], slots[sourceIndex]);
            CommitChanges();
            return true;
        }

        public bool CanCraft(CraftingRecipeDefinition recipe)
        {
            if (recipe == null || string.IsNullOrWhiteSpace(recipe.OutputItemId))
            {
                return false;
            }

            foreach (ItemStackRequirement ingredient in recipe.Ingredients)
            {
                if (!HasItem(ingredient.ItemId, ingredient.Count))
                {
                    return false;
                }
            }

            return CanAcceptItem(recipe.OutputItemId, recipe.OutputCount, recipe.OutputItem != null ? recipe.OutputItem.MaxStack : fallbackMaxStack);
        }

        public bool Craft(CraftingRecipeDefinition recipe)
        {
            if (!CanCraft(recipe))
            {
                return false;
            }

            foreach (ItemStackRequirement ingredient in recipe.Ingredients)
            {
                RemoveItem(ingredient.ItemId, ingredient.Count);
            }

            return AddItem(recipe.OutputItemId, recipe.OutputCount, recipe.OutputItem != null ? recipe.OutputItem.MaxStack : fallbackMaxStack);
        }

        public void LoadFromSave()
        {
            ResolveGameManager();

            if (gameManager == null || gameManager.SaveData == null)
            {
                return;
            }

            slots.Clear();

            foreach (InventorySlotData savedSlot in gameManager.SaveData.inventory)
            {
                slots.Add(new InventorySlotData
                {
                    itemId = savedSlot.itemId,
                    count = ClampStackCount(savedSlot.count)
                });
            }

            EnsureSlotCount();
            EnforceSingleStackPerItem();
            WriteToSave();
            InventoryChanged?.Invoke(this);
        }

        private bool AddItem(string itemId, int count, int maxStack)
        {
            if (string.IsNullOrWhiteSpace(itemId) || count <= 0)
            {
                return false;
            }

            int safeMaxStack = GetSafeMaxStack(maxStack);
            int currentCount = GetItemCount(itemId);
            int availableSpace = Mathf.Max(0, safeMaxStack - currentCount);

            if (availableSpace <= 0)
            {
                return false;
            }

            int added = Mathf.Min(count, availableSpace);

            if (TryFindSlotWithItem(itemId, out _, out InventorySlotData existingSlot))
            {
                existingSlot.count = Mathf.Min(safeMaxStack, existingSlot.count + added);
                CommitChanges();
                return added == count;
            }

            foreach (InventorySlotData slot in slots)
            {
                if (!slot.IsEmpty)
                {
                    continue;
                }

                slot.itemId = itemId;
                slot.count = added;
                CommitChanges();
                return added == count;
            }

            return false;
        }

        private bool CanAcceptItem(string itemId, int count, int maxStack)
        {
            if (string.IsNullOrWhiteSpace(itemId) || count <= 0)
            {
                return false;
            }

            int safeMaxStack = GetSafeMaxStack(maxStack);

            if (TryFindSlotWithItem(itemId, out _, out InventorySlotData existingSlot))
            {
                return existingSlot.count + count <= safeMaxStack;
            }

            foreach (InventorySlotData slot in slots)
            {
                if (slot.IsEmpty)
                {
                    return count <= safeMaxStack;
                }
            }

            return false;
        }

        private static InventorySlotData CopySlot(InventorySlotData slot)
        {
            return new InventorySlotData
            {
                itemId = slot.itemId,
                count = ClampStackCount(slot.count)
            };
        }

        private void CommitChanges()
        {
            EnsureSlotCount();
            EnforceSingleStackPerItem();
            WriteToSave();
            InventoryChanged?.Invoke(this);
        }

        private void WriteToSave()
        {
            ResolveGameManager();

            if (gameManager == null || gameManager.SaveData == null)
            {
                return;
            }

            gameManager.SaveData.inventory.Clear();

            foreach (InventorySlotData slot in slots)
            {
                gameManager.SaveData.inventory.Add(new InventorySlotData
                {
                    itemId = slot.itemId,
                    count = ClampStackCount(slot.count)
                });
            }
        }

        private void EnsureSlotCount()
        {
            int safeSlotCount = Mathf.Max(1, slotCount);

            while (slots.Count < safeSlotCount)
            {
                slots.Add(new InventorySlotData());
            }

            while (slots.Count > safeSlotCount)
            {
                slots.RemoveAt(slots.Count - 1);
            }

            for (int i = 0; i < slots.Count; i++)
            {
                slots[i] ??= new InventorySlotData();
            }
        }

        private void ResolveGameManager()
        {
            if (GameManager.Instance != null)
            {
                gameManager = GameManager.Instance;
            }
            else if (gameManager == null)
            {
                gameManager = FindAnyObjectByType<GameManager>();
            }

            gameManager?.Initialize();
        }

        private bool IsValidSlotIndex(int index)
        {
            return index >= 0 && index < slots.Count;
        }

        private static int GetSafeMaxStack(int requestedMaxStack)
        {
            return Mathf.Clamp(requestedMaxStack, 1, MaxItemStack);
        }

        private static int ClampStackCount(int count)
        {
            return Mathf.Clamp(count, 0, MaxItemStack);
        }

        private bool TryFindSlotWithItem(string itemId, out int index, out InventorySlotData slot)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlotData candidate = slots[i];

                if (candidate != null && !candidate.IsEmpty && candidate.itemId == itemId)
                {
                    index = i;
                    slot = candidate;
                    return true;
                }
            }

            index = -1;
            slot = null;
            return false;
        }

        private void EnforceSingleStackPerItem()
        {
            Dictionary<string, InventorySlotData> firstSlotsByItemId = new();

            foreach (InventorySlotData slot in slots)
            {
                if (slot == null || slot.IsEmpty)
                {
                    continue;
                }

                slot.count = ClampStackCount(slot.count);

                if (!firstSlotsByItemId.TryGetValue(slot.itemId, out InventorySlotData firstSlot))
                {
                    firstSlotsByItemId[slot.itemId] = slot;
                    continue;
                }

                int availableSpace = Mathf.Max(0, MaxItemStack - firstSlot.count);
                int moved = Mathf.Min(slot.count, availableSpace);
                firstSlot.count += moved;
                slot.Clear();
            }
        }
    }
}
