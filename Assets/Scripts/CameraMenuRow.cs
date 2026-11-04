using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Selectable))]
public class CameraMenuRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    private Image _background;
    private TMP_Text[] _labels;
    private bool _hovered;
    private bool _selected;

    private void Awake()
    {
        var selectable = GetComponent<Selectable>();
        selectable.transition = Selectable.Transition.None;
        _background = selectable.targetGraphic as Image;
        _labels = GetComponentsInChildren<TMP_Text>(true);
    }

    private void OnEnable()
    {
        _hovered = _selected = false;
        Refresh();
    }

    public void OnPointerEnter(PointerEventData eventData) { _hovered = true; GetComponent<Selectable>().Select(); Refresh(); }
    public void OnPointerExit(PointerEventData eventData) { _hovered = false; Refresh(); }
    public void OnSelect(BaseEventData eventData) { _selected = true; Refresh(); }
    public void OnDeselect(BaseEventData eventData) { _selected = false; Refresh(); }

    private void Refresh()
    {
        bool highlighted = _hovered || _selected;
        if (_background != null) _background.color = highlighted ? new Color(0.25f, 1f, 0.5f) : Color.clear;
        if (_labels == null) return;
        foreach (var label in _labels)
            label.color = highlighted ? new Color(0.025f, 0.045f, 0.07f) : new Color(0.78f, 0.87f, 0.95f);
    }
}
