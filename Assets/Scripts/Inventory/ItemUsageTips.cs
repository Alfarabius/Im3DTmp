using TMPro;
using UnityEngine;

public class ItemUsageTips : MonoBehaviour
{
    [SerializeField] private CanvasGroup panel;
    [SerializeField] private TMP_Text text;
    [SerializeField, Min(0.1f)] private float displayDuration = 10f;
    [SerializeField, Min(0.1f)] private float fadeDuration = 1.5f;

    private PlayerInventory _inventory;
    private float _age;
    private bool _visible;

    private void OnEnable()
    {
        _inventory = FindFirstObjectByType<PlayerInventory>();
        if (_inventory != null) _inventory.FirstItemPickedUp += Show;
        LocalizationService.LanguageChanged += RefreshText;
        panel.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        if (_inventory != null) _inventory.FirstItemPickedUp -= Show;
        LocalizationService.LanguageChanged -= RefreshText;
        panel.gameObject.SetActive(false);
    }

    private void Show()
    {
        _age = 0f;
        _visible = !PauseManager.HideTips;
        RefreshText();
    }

    private void RefreshText()
    {
        LocalizationService.EnsureInitialized();
        text.font = LocalizationService.InterfaceFont;
        text.outlineWidth = 0.18f;
        text.outlineColor = Color.black;
        text.text = LocalizationService.Get("inventory.tips");
    }

    private void Update()
    {
        if (!_visible) return;
        if (PauseManager.HideTips)
        {
            _visible = false;
            panel.gameObject.SetActive(false);
            return;
        }
        bool playing = !PauseManager.IsPaused && !GameCutscenes.IsPlaying;
        panel.gameObject.SetActive(playing);
        if (!playing) return;
        _age += Time.deltaTime;
        float fade = Mathf.Clamp01((_age - Mathf.Max(0f, displayDuration - fadeDuration)) / fadeDuration);
        panel.alpha = 1f - Mathf.SmoothStep(0f, 1f, fade);
        if (_age < displayDuration) return;
        _visible = false;
        panel.gameObject.SetActive(false);
    }
}
