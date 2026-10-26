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
        Refresh();
    }

    private void OnDisable() => LocalizationService.LanguageChanged -= Refresh;

    private void Refresh()
    {
        _text.font = LocalizationService.InterfaceFont;
        if (string.IsNullOrEmpty(key)) return; // Динамические подписи Dropdown меняют только шрифт.

        string value = LocalizationService.Get(key);
        if (bindingAction != null) value = value.Replace("{key}", bindingAction.action.GetBindingDisplayString());
        _text.text = value;
    }
}
