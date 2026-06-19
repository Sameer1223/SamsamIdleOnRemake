using System;
using System.Collections.Generic;
using SamsamIdleOn.Core;
using SamsamIdleOn.Data;
using SamsamIdleOn.Stats;
using UnityEngine;

namespace SamsamIdleOn.Inventory
{
    [DisallowMultipleComponent]
    public sealed class PlayerItemHotbar : MonoBehaviour
    {
        [SerializeField, Min(1)] private int slotCount = 2;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private GameDataRegistry dataRegistry;
        [SerializeField] private bool loadFromSaveOnAwake = true;

        private readonly List<InventorySlotData> slots = new();

        public event Action<PlayerItemHotbar> HotbarChanged;

        public int SlotCount => slots.Count;

        private void Awake()
        {
            ResolveReferences();
            EnsureSlotCount();

            if (loadFromSaveOnAwake)
            {
                LoadFromSave();
            }
            else
            {
                RebuildStatBonuses();
            }
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (inventory != null)
            {
                inventory.InventoryChanged -= HandleInventoryChanged;
                inventory.InventoryChanged += HandleInventoryChanged;
            }

            RebuildStatBonuses();
        }

        private void OnDisable()
        {
            if (inventory != null)
            {
                inventory.InventoryChanged -= HandleInventoryChanged;
            }

            if (playerStats != null)
            {
                playerStats.RemoveModifiersFromSource(this);
            }
        }

        public string GetItemId(int slotIndex)
        {
            return IsValidSlotIndex(slotIndex) && slots[slotIndex] != null ? slots[slotIndex].itemId : string.Empty;
        }

        public bool EquipFromInventorySlot(int inventorySlotIndex, int hotbarSlotIndex)
        {
            ResolveReferences();
            EnsureSlotCount();

            if (!IsValidSlotIndex(hotbarSlotIndex) || inventory == null)
            {
                return false;
            }

            return inventory.TryMoveSlotToHotbar(inventorySlotIndex, this, hotbarSlotIndex);
        }

        public bool TryPlaceSlot(InventorySlotData movedSlot, int hotbarSlotIndex, out InventorySlotData displacedSlot)
        {
            displacedSlot = null;
            ResolveReferences();
            EnsureSlotCount();

            if (!IsValidSlotIndex(hotbarSlotIndex) || movedSlot == null || movedSlot.IsEmpty)
            {
                return false;
            }

            InventorySlotData targetSlot = slots[hotbarSlotIndex];

            if (targetSlot != null && !targetSlot.IsEmpty)
            {
                displacedSlot = CopySlot(targetSlot);
            }

            slots[hotbarSlotIndex] = CopySlot(movedSlot);
            CommitChanges();
            return true;
        }

        public bool MoveToInventorySlot(int hotbarSlotIndex, int inventorySlotIndex)
        {
            ResolveReferences();
            EnsureSlotCount();

            if (!IsValidSlotIndex(hotbarSlotIndex)
                || slots[hotbarSlotIndex] == null
                || slots[hotbarSlotIndex].IsEmpty
                || inventory == null)
            {
                return false;
            }

            InventorySlotData movedSlot = CopySlot(slots[hotbarSlotIndex]);

            if (!inventory.TryPlaceSlotFromHotbar(movedSlot, inventorySlotIndex, out InventorySlotData displacedSlot))
            {
                return false;
            }

            slots[hotbarSlotIndex].Clear();

            if (displacedSlot != null && !displacedSlot.IsEmpty)
            {
                slots[hotbarSlotIndex] = CopySlot(displacedSlot);
            }

            CommitChanges();
            return true;
        }

        public bool SwapSlots(int sourceSlotIndex, int targetSlotIndex)
        {
            EnsureSlotCount();

            if (!IsValidSlotIndex(sourceSlotIndex) || !IsValidSlotIndex(targetSlotIndex) || sourceSlotIndex == targetSlotIndex)
            {
                return false;
            }

            (slots[sourceSlotIndex], slots[targetSlotIndex]) = (slots[targetSlotIndex], slots[sourceSlotIndex]);
            CommitChanges();
            return true;
        }

        public void ClearSlot(int hotbarSlotIndex)
        {
            EnsureSlotCount();

            if (!IsValidSlotIndex(hotbarSlotIndex))
            {
                return;
            }

            slots[hotbarSlotIndex].Clear();
            CommitChanges();
        }

        public void LoadFromSave()
        {
            ResolveReferences();
            EnsureSlotCount();

            if (gameManager == null || gameManager.SaveData == null)
            {
                return;
            }

            gameManager.SaveData.EnsureDefaults();
            slots.Clear();

            if (gameManager.SaveData.hotbarItems.Count > 0)
            {
                foreach (InventorySlotData savedSlot in gameManager.SaveData.hotbarItems)
                {
                    slots.Add(savedSlot != null
                        ? CopySlot(savedSlot)
                        : new InventorySlotData());
                }
            }
            else
            {
                foreach (string savedItemId in gameManager.SaveData.hotbarItemIds)
                {
                    slots.Add(string.IsNullOrWhiteSpace(savedItemId)
                        ? new InventorySlotData()
                        : new InventorySlotData { itemId = savedItemId, count = 1 });
                }
            }

            EnsureSlotCount();
            RebuildStatBonuses();
            HotbarChanged?.Invoke(this);
        }

        public bool TryResolveItemDefinition(int slotIndex, out ItemDefinition definition)
        {
            definition = null;

            if (dataRegistry == null
                || !IsValidSlotIndex(slotIndex)
                || slots[slotIndex] == null
                || slots[slotIndex].IsEmpty)
            {
                return false;
            }

            return dataRegistry.TryGetDefinition(slots[slotIndex].itemId, out definition);
        }

        private void CommitChanges()
        {
            WriteToSave();
            RebuildStatBonuses();
            HotbarChanged?.Invoke(this);
        }

        private void RebuildStatBonuses()
        {
            ResolveReferences();

            if (playerStats == null)
            {
                return;
            }

            playerStats.RemoveModifiersFromSource(this);

            if (dataRegistry == null)
            {
                return;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlotData slot = slots[i];

                if (slot == null
                    || slot.IsEmpty
                    || !dataRegistry.TryGetDefinition(slot.itemId, out ItemDefinition itemDefinition))
                {
                    continue;
                }

                foreach (ItemStatBonus bonus in itemDefinition.StatBonuses)
                {
                    if (bonus == null)
                    {
                        continue;
                    }

                    float value = bonus.ApplyPerStack
                        ? bonus.Value * Mathf.Max(1, slot.count)
                        : bonus.Value;

                    playerStats.AddModifier(bonus.Stat, value, bonus.Kind, this);
                }
            }
        }

        private void WriteToSave()
        {
            ResolveReferences();

            if (gameManager == null || gameManager.SaveData == null)
            {
                return;
            }

            gameManager.SaveData.EnsureDefaults();
            gameManager.SaveData.hotbarItemIds.Clear();
            gameManager.SaveData.hotbarItems.Clear();

            foreach (InventorySlotData slot in slots)
            {
                gameManager.SaveData.hotbarItems.Add(slot != null ? CopySlot(slot) : new InventorySlotData());
                gameManager.SaveData.hotbarItemIds.Add(slot != null && !slot.IsEmpty ? slot.itemId : string.Empty);
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

        private void ResolveReferences()
        {
            if (inventory == null)
            {
                inventory = GetComponent<PlayerInventory>() ?? FindAnyObjectByType<PlayerInventory>();
            }

            if (playerStats == null)
            {
                playerStats = GetComponent<PlayerStats>() ?? FindAnyObjectByType<PlayerStats>();
            }

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

        private void HandleInventoryChanged(PlayerInventory changedInventory)
        {
            RebuildStatBonuses();
            HotbarChanged?.Invoke(this);
        }

        private bool IsValidSlotIndex(int index)
        {
            return index >= 0 && index < slots.Count;
        }

        private static InventorySlotData CopySlot(InventorySlotData slot)
        {
            return new InventorySlotData
            {
                itemId = slot.itemId,
                count = slot.count
            };
        }
    }
}
