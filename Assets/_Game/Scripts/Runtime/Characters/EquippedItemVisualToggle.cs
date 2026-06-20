using SamsamIdleOn.Inventory;
using UnityEngine;

namespace SamsamIdleOn.Characters
{
    public sealed class EquippedItemVisualToggle : MonoBehaviour
    {
        [SerializeField] private PlayerItemHotbar hotbar;
        [SerializeField] private GameObject visual;
        [SerializeField] private string requiredItemId = "gem_sword";

        private void Awake()
        {
            ResolveReferences();
            Refresh();
        }

        private void OnEnable()
        {
            ResolveReferences();

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

        public void Refresh()
        {
            if (visual == null)
            {
                return;
            }

            visual.SetActive(IsRequiredItemEquipped());
        }

        private void HandleHotbarChanged(PlayerItemHotbar changedHotbar)
        {
            Refresh();
        }

        private bool IsRequiredItemEquipped()
        {
            if (hotbar == null || string.IsNullOrWhiteSpace(requiredItemId))
            {
                return false;
            }

            for (int i = 0; i < hotbar.SlotCount; i++)
            {
                if (string.Equals(hotbar.GetItemId(i), requiredItemId, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void ResolveReferences()
        {
            if (hotbar == null)
            {
                hotbar = GetComponentInParent<PlayerItemHotbar>() ?? FindAnyObjectByType<PlayerItemHotbar>();
            }
        }
    }
}
