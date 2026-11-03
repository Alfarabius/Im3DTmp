using UnityEngine;
using UnityEngine.UI;

public class InventoryHUD : MonoBehaviour
{
    [SerializeField] private Image[] slotFrames;
    [SerializeField] private Image[] itemIcons;
    [SerializeField] private Image[] useBars;
    [SerializeField] private Color selectedColor = new Color(1f, 0.65f, 0.25f);

    private PlayerInventory _inventory;

    private void OnEnable()
    {
        _inventory = FindFirstObjectByType<PlayerInventory>();
        if (_inventory != null) _inventory.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (_inventory != null) _inventory.Changed -= Refresh;
    }

    private void Refresh()
    {
        for (int i = 0; i < PlayerInventory.SlotCount; i++)
        {
            var item = _inventory != null ? _inventory.GetSlot(i) : null;
            itemIcons[i].sprite = item != null ? item.Definition.Icon : null;
            itemIcons[i].enabled = item != null && item.Definition.Icon != null;
            slotFrames[i].color = _inventory != null && _inventory.SelectedSlot == i ? selectedColor : Color.white;
        }
    }

    private void Update()
    {
        for (int i = 0; i < PlayerInventory.SlotCount; i++)
        {
            float progress = _inventory != null && _inventory.SelectedSlot == i ? _inventory.UseProgress : 0f;
            useBars[i].enabled = progress > 0f;
            useBars[i].fillAmount = progress;
        }
    }
}
