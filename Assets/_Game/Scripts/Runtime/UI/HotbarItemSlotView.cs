using SamsamIdleOn.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SamsamIdleOn.UI
{
    public sealed class HotbarItemSlotView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [SerializeField] private PlayerItemHotbar hotbar;
        [SerializeField, Min(0)] private int hotbarSlotIndex;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text itemIdLabel;

        public static HotbarItemSlotView ActiveDragSource { get; private set; }

        public int HotbarSlotIndex => hotbarSlotIndex;

        public PlayerItemHotbar Hotbar
        {
            get
            {
                ResolveHotbar();
                return hotbar;
            }
        }

        private void Awake()
        {
            ResolveHotbar();
            Refresh();
        }

        private void OnEnable()
        {
            ResolveHotbar();

            if (hotbar != null)
            {
                hotbar.HotbarChanged -= HandleHotbarChanged;
                hotbar.HotbarChanged += HandleHotbarChanged;
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (hotbar != null)
            {
                hotbar.HotbarChanged -= HandleHotbarChanged;
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            ActiveDragSource = this;
        }

        public void OnDrag(PointerEventData eventData)
        {
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (ActiveDragSource == this)
            {
                ActiveDragSource = null;
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            ResolveHotbar();

            InventorySlotView inventorySource = eventData.pointerDrag != null
                ? eventData.pointerDrag.GetComponentInParent<InventorySlotView>()
                : null;

            inventorySource ??= InventorySlotView.ActiveDragSource;

            if (inventorySource != null && hotbar != null)
            {
                hotbar.EquipFromInventorySlot(inventorySource.SlotIndex, hotbarSlotIndex);
                Refresh();
                return;
            }

            HotbarItemSlotView hotbarSource = eventData.pointerDrag != null
                ? eventData.pointerDrag.GetComponentInParent<HotbarItemSlotView>()
                : null;

            hotbarSource ??= ActiveDragSource;

            if (hotbarSource == null || hotbar == null)
            {
                return;
            }

            hotbar.SwapSlots(hotbarSource.HotbarSlotIndex, hotbarSlotIndex);
            Refresh();
        }

        public bool MoveToInventorySlot(int inventorySlotIndex)
        {
            ResolveHotbar();
            bool moved = hotbar != null && hotbar.MoveToInventorySlot(hotbarSlotIndex, inventorySlotIndex);
            Refresh();
            return moved;
        }

        public void Clear()
        {
            ResolveHotbar();
            hotbar?.ClearSlot(hotbarSlotIndex);
            Refresh();
        }

        public void Refresh()
        {
            ResolveHotbar();

            string itemId = hotbar != null ? hotbar.GetItemId(hotbarSlotIndex) : string.Empty;
            ItemDefinition definition = null;
            bool hasDefinition = hotbar != null && hotbar.TryResolveItemDefinition(hotbarSlotIndex, out definition);

            if (iconImage != null)
            {
                iconImage.enabled = hasDefinition && definition.Icon != null;
                iconImage.sprite = iconImage.enabled ? definition.Icon : null;
            }

            if (itemIdLabel != null)
            {
                itemIdLabel.text = !string.IsNullOrWhiteSpace(itemId) && !hasDefinition ? itemId : string.Empty;
            }
        }

        private void ResolveHotbar()
        {
            if (hotbar == null)
            {
                hotbar = FindAnyObjectByType<PlayerItemHotbar>();
            }
        }

        private void HandleHotbarChanged(PlayerItemHotbar changedHotbar)
        {
            Refresh();
        }
    }
}
