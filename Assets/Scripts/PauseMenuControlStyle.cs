using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Selectable))]
public class PauseMenuControlStyle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private TMP_Text[] labels;
    [SerializeField] private Graphic[] symbols;

    private Image _background;
    private bool _hovered;
    private bool _selected;

    private void Awake()
    {
        var control = GetComponent<Selectable>();
        control.transition = Selectable.Transition.None;
        _background = control.targetGraphic as Image;
        if (labels == null || labels.Length == 0) labels = GetComponentsInChildren<TMP_Text>(true);
    }

    private void OnEnable()
    {
        _hovered = _selected = false;
        Refresh();
    }

    public void OnPointerEnter(PointerEventData eventData) { _hovered = true; Refresh(); }
    public void OnPointerExit(PointerEventData eventData) { _hovered = false; Refresh(); }
    public void OnSelect(BaseEventData eventData) { _selected = true; Refresh(); }
    public void OnDeselect(BaseEventData eventData) { _selected = false; Refresh(); }

    private void Refresh()
    {
        bool highlighted = _hovered || _selected;
        var ink = highlighted ? new Color(0.025f, 0.045f, 0.07f) : new Color(0.8f, 0.9f, 1f);
        if (_background != null) _background.color = highlighted ? new Color(0.25f, 1f, 0.5f) : Color.clear;
        if (labels != null)
            foreach (var label in labels) label.color = ink;
        if (symbols != null)
            foreach (var symbol in symbols) symbol.color = ink;
    }
}
