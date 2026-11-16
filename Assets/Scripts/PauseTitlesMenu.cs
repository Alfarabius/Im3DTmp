using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PauseTitlesMenu : MonoBehaviour
{
    [SerializeField] private GameObject mainMenu;
    [SerializeField] private Transform leftLayout;
    [SerializeField] private Button buttonTemplate;
    [Tooltip("Список разработчиков и упоминаний. Этот текст можно дополнять в инспекторе.")]
    [SerializeField, TextArea(6, 30), InspectorName("Текст титров")]
    private string titlesText = "Game by Turbo Laser\nART - Anya Babushkina\nMusic - Mikhail Puchkov";

    private GameObject _panel;
    private Button _openButton;
    private Button _backButton;
    private ScrollRect _scroll;
    private TMP_Text _body;

    public bool IsOpen => _panel != null && _panel.activeSelf;

    private void Awake()
    {
        LocalizationService.EnsureInitialized();
        CreateMenu();
    }

    private void OnEnable() => ResetMenu();

    private void OnDisable() => ResetMenu();

    private void OnValidate()
    {
        if (_body != null) RefreshBody();
    }

    private void RefreshBody()
    {
        if (_body == null) return;
        _body.font = LocalizationService.InterfaceFont;
        _body.text = titlesText;
    }

    public void Show()
    {
        if (!PauseManager.IsPaused || GameCutscenes.IsPlaying) return;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        mainMenu.SetActive(false);
        _panel.SetActive(true);
        RefreshBody();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_scroll.content);
        _scroll.StopMovement();
        _scroll.verticalNormalizedPosition = 1f;
        if (EventSystem.current != null) _backButton.Select();
    }

    public void Close()
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        ResetMenu();
        if (EventSystem.current != null && _openButton != null) _openButton.Select();
    }

    public void SelectFirstControl()
    {
        if (!IsOpen || EventSystem.current == null) return;
        var selected = EventSystem.current.currentSelectedGameObject;
        if (selected != null && selected.activeInHierarchy && selected.transform.IsChildOf(_panel.transform)) return;
        _backButton.Select();
    }

    private void ResetMenu()
    {
        if (_panel != null) _panel.SetActive(false);
        if (mainMenu != null) mainMenu.SetActive(true);
    }

    private void CreateMenu()
    {
        _openButton = CreateButton("TitlesButton", leftLayout, "Titles", Show);
        _openButton.transform.SetSiblingIndex(buttonTemplate.transform.GetSiblingIndex() + 1);

        _panel = new GameObject("TitlesMenu", typeof(RectTransform));
        _panel.SetActive(false);
        _panel.transform.SetParent(transform, false);
        Place((RectTransform)_panel.transform, new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.94f));

        var heading = CreateText("Titles heading", _panel.transform, 72f);
        Place(heading.rectTransform, new Vector2(0f, 0.88f), Vector2.one);
        heading.text = "Titles";

        var scrollObject = new GameObject("Titles Scroll Rect", typeof(RectTransform), typeof(ScrollRect));
        scrollObject.transform.SetParent(_panel.transform, false);
        Place((RectTransform)scrollObject.transform, new Vector2(0f, 0.22f), new Vector2(1f, 0.84f));
        _scroll = scrollObject.GetComponent<ScrollRect>();
        _scroll.horizontal = false;
        _scroll.vertical = true;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
        _scroll.scrollSensitivity = 40f;

        var viewport = CreateImage("Viewport", scrollObject.transform, new Color(0f, 0f, 0f, 0.08f));
        viewport.gameObject.AddComponent<RectMask2D>();
        Place(viewport.rectTransform, Vector2.zero, Vector2.one);
        viewport.rectTransform.offsetMin = new Vector2(0f, 0f);
        viewport.rectTransform.offsetMax = new Vector2(-32f, 0f);
        _scroll.viewport = viewport.rectTransform;

        _body = CreateText("Developers and acknowledgements", viewport.transform, 36f);
        _body.margin = new Vector4(18f, 16f, 18f, 16f);
        var content = _body.rectTransform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        var fitter = _body.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _scroll.content = content;
        RefreshBody();

        var track = CreateImage("Vertical scrollbar", scrollObject.transform, new Color(0.8f, 0.9f, 1f, 0.12f));
        var trackRect = track.rectTransform;
        trackRect.anchorMin = new Vector2(1f, 0f);
        trackRect.anchorMax = Vector2.one;
        trackRect.pivot = new Vector2(1f, 0.5f);
        trackRect.anchoredPosition = Vector2.zero;
        trackRect.sizeDelta = new Vector2(18f, 0f);
        var handle = CreateImage("Handle", track.transform, new Color(0.8f, 0.9f, 1f, 0.8f));
        Place(handle.rectTransform, Vector2.zero, Vector2.one);
        var scrollbar = track.gameObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle.rectTransform;
        scrollbar.targetGraphic = handle;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        _scroll.verticalScrollbar = scrollbar;
        _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        _backButton = CreateButton("BackToPauseButton", _panel.transform, "Back", Close);
        var backRect = (RectTransform)_backButton.transform;
        backRect.anchorMin = backRect.anchorMax = Vector2.zero;
        backRect.pivot = Vector2.zero;
        backRect.anchoredPosition = Vector2.zero;
        backRect.sizeDelta = new Vector2(480f, 140f);
        _backButton.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnUp = scrollbar,
            selectOnRight = scrollbar
        };
        scrollbar.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnLeft = _backButton
        };
    }

    private Button CreateButton(string name, Transform parent, string caption, UnityEngine.Events.UnityAction action)
    {
        var button = Instantiate(buttonTemplate, parent);
        button.name = name;
        // Replace the template's persistent Resume handler as well as runtime listeners.
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(action);
        var label = button.GetComponentInChildren<TMP_Text>(true);
        var localized = label.GetComponent<LocalizedText>();
        if (localized == null) localized = label.gameObject.AddComponent<LocalizedText>();
        localized.SetLiteralText(caption);
        return button;
    }

    private static TMP_Text CreateText(string name, Transform parent, float size)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        var text = obj.GetComponent<TextMeshProUGUI>();
        text.font = LocalizationService.InterfaceFont;
        text.fontSize = size;
        text.color = new Color(0.8f, 0.9f, 1f);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        var image = obj.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static void Place(RectTransform rect, Vector2 minimum, Vector2 maximum)
    {
        rect.anchorMin = minimum;
        rect.anchorMax = maximum;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
