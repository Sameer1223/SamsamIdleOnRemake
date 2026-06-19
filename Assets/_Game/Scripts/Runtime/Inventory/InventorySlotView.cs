using TMPro;
using SamsamIdleOn.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SamsamIdleOn.Inventory
{
    public sealed class InventorySlotView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text countTmpLabel;
        [SerializeField] private TMP_Text itemIdLabel;

        private InventoryPanel panel;
        private int slotIndex = -1;

        public static InventorySlotView ActiveDragSource { get; private set; }

        public int SlotIndex => slotIndex;

        public void Configure(InventoryPanel owner, int index)
        {
            panel = owner;
            slotIndex = index;
        }

        public void Refresh(InventorySlotData slot, ItemDefinition definition)
        {
            bool hasItem = slot != null && !slot.IsEmpty;

            if (iconImage != null)
            {
                iconImage.enabled = hasItem && definition != null && definition.Icon != null;
                iconImage.sprite = iconImage.enabled ? definition.Icon : null;
            }

            string countText = hasItem && slot.count > 1 ? slot.count.ToString() : string.Empty;
            SetText(countTmpLabel, countText);
            SetText(itemIdLabel, hasItem && definition == null ? slot.itemId : string.Empty);
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
            InventorySlotView source = eventData.pointerDrag != null
                ? eventData.pointerDrag.GetComponentInParent<InventorySlotView>()
                : null;

            source ??= ActiveDragSource;

            if (source != null && panel != null)
            {
                panel.MoveOrMergeSlot(source.SlotIndex, slotIndex);
                return;
            }

            HotbarItemSlotView hotbarSource = eventData.pointerDrag != null
                ? eventData.pointerDrag.GetComponentInParent<HotbarItemSlotView>()
                : null;

            hotbarSource ??= HotbarItemSlotView.ActiveDragSource;

            if (hotbarSource == null || panel == null)
            {
                return;
            }

            hotbarSource.MoveToInventorySlot(slotIndex);
        }

        private static void SetText(Text label, string value)
        {
            if (label != null)
            {
                label.text = value;
            }
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null)
            {
                label.text = value;
            }
        }
    }
}
