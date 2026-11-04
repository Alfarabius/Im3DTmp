using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(11000)]
public class InventoryHUD : MonoBehaviour
{
    [SerializeField] private Image[] slotFrames;
    [SerializeField] private Image[] itemIcons;
    [SerializeField] private Image[] useBars;
    [SerializeField] private Image[] useTracks;
    [SerializeField] private Image ballAimPoint;
    [SerializeField] private Color selectedColor = new Color(0.25f, 1f, 0.5f);
    [SerializeField] private Color idleColor = new Color(0.78f, 0.87f, 0.95f);

    private PlayerInventory _inventory;

    private void OnEnable()
    {
        _inventory = FindFirstObjectByType<PlayerInventory>();
        if (_inventory != null) _inventory.Changed += Refresh;
        foreach (var bar in useBars)
        {
            bar.type = Image.Type.Filled;
            bar.fillMethod = Image.FillMethod.Horizontal;
            bar.fillOrigin = 0;
            bar.preserveAspect = false;
            bar.fillAmount = 0f;
        }
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
            slotFrames[i].color = _inventory != null && _inventory.SelectedSlot == i ? selectedColor : idleColor;
        }
    }

    private void LateUpdate()
    {
        for (int i = 0; i < PlayerInventory.SlotCount; i++)
        {
            float progress = _inventory != null && _inventory.SelectedSlot == i ? _inventory.UseProgress : 0f;
            var item = _inventory != null ? _inventory.GetSlot(i) : null;
            bool selected = _inventory != null && _inventory.SelectedSlot == i && item != null &&
                item.Definition.Kind != InventoryItemKind.Shield;
            useTracks[i].enabled = selected;
            useBars[i].enabled = selected;
            useBars[i].fillAmount = progress;
        }
        var held = _inventory != null ? _inventory.SelectedItem : null;
        ballAimPoint.gameObject.SetActive(held != null && held.Definition.Kind == InventoryItemKind.Ball &&
            held.gameObject.activeInHierarchy && !PauseManager.IsPaused && !GameCutscenes.IsPlaying);
    }
}
