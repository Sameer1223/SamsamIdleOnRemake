using System.Collections;
using System.Collections.Generic;
using SamsamIdleOn.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SamsamIdleOn.Inventory
{
    public sealed class InventoryPanel : MonoBehaviour
    {
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private GameDataRegistry dataRegistry;
        [SerializeField] private List<InventorySlotView> slotViews = new();
        [SerializeField] private bool autoFindInventory = true;

        private Coroutine rebindRoutine;

        private void Awake()
        {
            ResolveInventory();
            ConfigureSlots();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            ResolveInventory();

            if (inventory != null)
            {
                inventory.InventoryChanged -= HandleInventoryChanged;
                inventory.InventoryChanged += HandleInventoryChanged;
            }

            Refresh();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            StopRebindRoutine();

            if (inventory != null)
            {
                inventory.InventoryChanged -= HandleInventoryChanged;
            }
        }

        private void Start()
        {
            ResolveInventory();
            Refresh();
        }

        public void Refresh()
        {
            ResolveInventory();
            ConfigureSlots();

            IReadOnlyList<InventorySlotData> slots = inventory != null
                ? inventory.Slots
                : System.Array.Empty<InventorySlotData>();

            for (int i = 0; i < slotViews.Count; i++)
            {
                InventorySlotData slot = i < slots.Count ? slots[i] : null;
                ItemDefinition definition = ResolveDefinition(slot);
                slotViews[i].Refresh(slot, definition);
            }
        }

        public bool MoveOrMergeSlot(int sourceIndex, int targetIndex)
        {
            return inventory != null && inventory.MoveOrMergeSlot(sourceIndex, targetIndex);
        }

        public void SetSlotsFromChildren()
        {
            slotViews.Clear();
            slotViews.AddRange(GetComponentsInChildren<InventorySlotView>(true));
            ConfigureSlots();
            Refresh();
        }

        private void ConfigureSlots()
        {
            for (int i = 0; i < slotViews.Count; i++)
            {
                if (slotViews[i] != null)
                {
                    slotViews[i].Configure(this, i);
                }
            }
        }

        private ItemDefinition ResolveDefinition(InventorySlotData slot)
        {
            if (slot == null || slot.IsEmpty || dataRegistry == null)
            {
                return null;
            }

            return dataRegistry.TryGetDefinition(slot.itemId, out ItemDefinition definition)
                ? definition
                : null;
        }

        private void ResolveInventory()
        {
            ResolveInventory(false);
        }

        private void ResolveInventory(bool force)
        {
            if (!autoFindInventory)
            {
                return;
            }

            if (force && inventory != null)
            {
                inventory.InventoryChanged -= HandleInventoryChanged;
                inventory = null;
            }

            if (inventory == null)
            {
                inventory = FindAnyObjectByType<PlayerInventory>();
            }

            if (inventory != null)
            {
                inventory.InventoryChanged -= HandleInventoryChanged;
                inventory.InventoryChanged += HandleInventoryChanged;
            }
        }

        private void HandleInventoryChanged(PlayerInventory changedInventory)
        {
            Refresh();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StopRebindRoutine();
            rebindRoutine = StartCoroutine(RebindAfterSceneLoad());
        }

        private IEnumerator RebindAfterSceneLoad()
        {
            yield return null;

            ResolveInventory(true);
            Refresh();
            rebindRoutine = null;
        }

        private void StopRebindRoutine()
        {
            if (rebindRoutine == null)
            {
                return;
            }

            StopCoroutine(rebindRoutine);
            rebindRoutine = null;
        }
    }
}
