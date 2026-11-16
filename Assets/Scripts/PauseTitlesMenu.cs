using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PauseTitlesMenu : MonoBehaviour
{
    [SerializeField] private GameObject mainMenu;
    [SerializeField] private GameObject panel;
    [SerializeField] private Button openButton;
    [SerializeField] private Button backButton;
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private TMP_Text body;
    [Tooltip("Список разработчиков и упоминаний. Этот текст можно дополнять в инспекторе.")]
    [SerializeField, TextArea(6, 30), InspectorName("Текст титров")]
    private string titlesText = "Game by Turbo Laser\nART - Anya Babushkina\nMusic - Mikhail Puchkov";

    public bool IsOpen => panel != null && panel.activeSelf;

    private void Awake()
    {
        LocalizationService.EnsureInitialized();
        RefreshBody();
    }

    private void OnEnable() => ResetMenu();

    private void OnDisable() => ResetMenu();

    private void OnValidate()
    {
        if (body != null) body.text = titlesText;
    }

    private void RefreshBody()
    {
        if (body == null) return;
        body.font = LocalizationService.InterfaceFont;
        body.text = titlesText;
    }

    public void Show()
    {
        if (!PauseManager.IsPaused || GameCutscenes.IsPlaying) return;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        mainMenu.SetActive(false);
        panel.SetActive(true);
        RefreshBody();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = 1f;
        if (EventSystem.current != null) backButton.Select();
    }

    public void Close()
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        ResetMenu();
        if (EventSystem.current != null && openButton != null) openButton.Select();
    }

    public void SelectFirstControl()
    {
        if (!IsOpen || EventSystem.current == null) return;
        var selected = EventSystem.current.currentSelectedGameObject;
        if (selected != null && selected.activeInHierarchy && selected.transform.IsChildOf(panel.transform)) return;
        backButton.Select();
    }

    private void ResetMenu()
    {
        if (panel != null) panel.SetActive(false);
        if (mainMenu != null) mainMenu.SetActive(true);
    }
}
