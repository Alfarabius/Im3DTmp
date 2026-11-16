using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    [SerializeField] private string key;
    [Tooltip("Необязательная кнопка для подстановки {key} в подсказку.")]
    [SerializeField] private InputActionReference bindingAction;

    private TMP_Text _text;

    private void Awake() => _text = GetComponent<TMP_Text>();

    private void OnEnable()
    {
        LocalizationService.LanguageChanged += Refresh;
        GameInput.ControlsChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        LocalizationService.LanguageChanged -= Refresh;
        GameInput.ControlsChanged -= Refresh;
    }

    private void Refresh()
    {
        LocalizationService.EnsureInitialized();
        if (_text == null) _text = GetComponent<TMP_Text>();
        _text.font = LocalizationService.InterfaceFont;
        if (string.IsNullOrEmpty(key)) return; // Динамические подписи Dropdown меняют только шрифт.

        string value = LocalizationService.Get(key);
        if (bindingAction != null) value = value.Replace("{key}", GameInput.GetButtonLabel(bindingAction.action));
        _text.text = value;
    }
}
