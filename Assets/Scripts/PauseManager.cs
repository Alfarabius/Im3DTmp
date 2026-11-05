using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance { get; private set; }
    
    // Глобальное состояние паузы для блокировки Update в стейт-машинах (NPCBrain, PlayerBrain)
    public static bool IsPaused { get; private set; }

    [Header("UI Setup")]
    [Tooltip("Ссылка на корневой объект панели меню паузы")] [SerializeField] private GameObject pauseMenuUI;
    [Tooltip("Выпадающий список разрешений экрана")] [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Text hideTipsLabel;
    [SerializeField] private Toggle hideTipsToggle;
    [SerializeField] private Selectable firstMenuControl;
    public static bool HideTips { get; private set; }
    
    [Header("UI Sliders")]
    [Tooltip("Слайдер чувствительности мыши")] [SerializeField] private Slider mouseSensSlider;
    [Tooltip("Слайдер общей громкости (Master)")] [SerializeField] private Slider masterVolSlider;
    [Tooltip("Слайдер громкости окружения (Ambient)")] [SerializeField] private Slider ambientVolSlider;
    [Tooltip("Слайдер громкости эффектов (SFX)")] [SerializeField] private Slider sfxVolSlider;

    [Header("Audio Setup")]
    [Tooltip("Главный Audio Mixer проекта")] [SerializeField] private AudioMixer audioMixer;

    [Header("Input Setup")]
    [Tooltip("Главный ассет с мапами ввода")] [SerializeField] private InputActionAsset inputAsset;
    [Tooltip("Название Action Map для игры")] [SerializeField] private string gameplayMapName = "Player";
    [Tooltip("Название Action Map для меню")] [SerializeField] private string uiMapName = "UI";
    [Tooltip("Название Action для обзора мышью")] [SerializeField] private string lookActionName = "Look";
    
    [Tooltip("Ссылка на действие снятия с паузы в UI")] 
    [SerializeField] private InputActionReference resumeAction;
    [SerializeField] private InputActionReference menuCancelAction;
    
    [Header("Controls")]
    [Tooltip("Ссылка на действие паузы (например, кнопка Esc)")] [SerializeField] private InputActionReference pauseAction;

    // Глобальная переменная для чувствительности мыши, которую могут читать скрипты контроллера
    public static float MouseSensitivity { get; private set; } = 1f;

    // События для переключения состояний в других системах (например, анимациях UI)
    public static event Action OnGamePaused;
    public static event Action OnGameResumed;

    private Vector2Int[] _resolutions;
    private bool _buildingResolutionOptions;
    private Coroutine _resolutionChange;
    private Vector2Int _requestedResolution;
    private int _lastPauseToggleFrame = -1;

    // Константы ключей для сохранения настроек, чтобы избежать опечаток
    private const string PrefsMouseSens = "MouseSensitivity";
    private const string PrefsMasterVol = "MasterVolume";
    private const string PrefsAmbientVol = "AmbientVolume";
    private const string PrefsSFXVol = "SFXVolume";
    private const string PrefsResolutionWidth = "ResolutionWidth";
    private const string PrefsResolutionHeight = "ResolutionHeight";
    private const string PrefsHideTips = "HideTips";

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        HideTips = PlayerPrefs.GetInt(PrefsHideTips, 0) != 0;
        if (hideTipsToggle != null) hideTipsToggle.SetIsOnWithoutNotify(HideTips);
        
        // Изначально скрываем меню паузы при загрузке сцены
        if (pauseMenuUI != null) pauseMenuUI.SetActive(false);
        
        InitializeResolutions();
    }

    private void Start()
    {
        // Инициализация настроек из PlayerPrefs должна происходить в Start, 
        // чтобы гарантировать, что все UI-элементы и AudioMixer уже проинициализированы Unity.
        LoadSettings();
    }

    private void OnEnable()
    {
        LocalizationService.LanguageChanged += RefreshTipsLabel;
        GameInput.ControlsChanged += SelectMenuControl;
        if (menuCancelAction != null)
        {
            menuCancelAction.action.Enable();
            menuCancelAction.action.performed += CancelMenu;
        }
        if (pauseAction != null)
        {
            pauseAction.action.Enable();
            pauseAction.action.performed += TogglePause;
        }
        if (resumeAction != null)
        {
            resumeAction.action.Enable();
            resumeAction.action.performed += TogglePause;
        }
    }

    private void OnDisable()
    {
        LocalizationService.LanguageChanged -= RefreshTipsLabel;
        GameInput.ControlsChanged -= SelectMenuControl;
        if (menuCancelAction != null) menuCancelAction.action.performed -= CancelMenu;
        if (pauseAction != null)
        {
            pauseAction.action.performed -= TogglePause;
        }
        if (resumeAction != null) 
            resumeAction.action.performed -= TogglePause;
    }

    private void TogglePause(InputAction.CallbackContext context)
    {
        if (GameCutscenes.IsPlaying || _lastPauseToggleFrame == Time.frameCount) return;
        _lastPauseToggleFrame = Time.frameCount;
        if (IsPaused) ResumeGame();
        else PauseGame();
    }

    private void CancelMenu(InputAction.CallbackContext context)
    {
        if (!IsPaused || !(context.control?.device is Gamepad)) return;
        if (resolutionDropdown != null && resolutionDropdown.IsExpanded) return;
        ResumeGame();
    }

    public void PauseGame()
    {
        if (GameCutscenes.IsPlaying) return;
        // Обновляем список на случай смены дисплея после запуска игры.
        InitializeResolutions();

        IsPaused = true;
        
        // Остановка времени и физики
        Time.timeScale = 0f;
        
        // Приостановка всех 3D и фоновых звуков через глобальный Listener
        AudioListener.pause = true;

        // Отключение управления персонажем и включение управления интерфейсом
        if (inputAsset != null)
        {
            inputAsset.FindActionMap(gameplayMapName)?.Disable();
            inputAsset.FindActionMap(uiMapName)?.Enable();
        }

        // Включение UI панели паузы
        if (pauseMenuUI != null) pauseMenuUI.SetActive(true);
        SelectMenuControl();
        RefreshTipsLabel();

        // Разблокировка и отображение курсора для взаимодействия с меню
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        OnGamePaused?.Invoke();
    }

    public void ResumeGame()
    {
        IsPaused = false;
        
        // Восстановление течения времени
        Time.timeScale = 1f;
        
        // Возобновление звуков
        AudioListener.pause = false;

        // Возврат управления персонажу
        if (inputAsset != null)
        {
            inputAsset.FindActionMap(uiMapName)?.Disable();
            inputAsset.FindActionMap(gameplayMapName)?.Enable();
        }

        // Скрытие UI панели паузы
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        if (pauseMenuUI != null) pauseMenuUI.SetActive(false);

        // Блокировка и скрытие курсора
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        OnGameResumed?.Invoke();
    }

    private void SelectMenuControl()
    {
        if (!IsPaused || !GameInput.UsingGamepad || pauseMenuUI == null || EventSystem.current == null) return;
        var selected = EventSystem.current.currentSelectedGameObject;
        if (selected != null && selected.activeInHierarchy && selected.transform.IsChildOf(pauseMenuUI.transform)) return;
        var first = firstMenuControl != null ? firstMenuControl : pauseMenuUI.GetComponentInChildren<Selectable>();
        if (first != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
    }

    // --- Загрузка настроек ---

    private void LoadSettings()
    {
        // 1. Чувствительность мыши (По умолчанию 1.0)
        float savedSens = PlayerPrefs.GetFloat(PrefsMouseSens, 1f);
        if (mouseSensSlider != null) mouseSensSlider.value = savedSens;
        
        // Сразу применяем сохраненную сенсу к процессору ввода при загрузке
        SetMouseSensitivity(savedSens);

        // 2. Громкость Master (По умолчанию 1.0 - максимум)
        float savedMaster = PlayerPrefs.GetFloat(PrefsMasterVol, 1f);
        if (masterVolSlider != null) masterVolSlider.value = savedMaster;
        ApplyVolume("MasterVolume", savedMaster);

        // 3. Громкость Ambient (По умолчанию 1.0)
        float savedAmbient = PlayerPrefs.GetFloat(PrefsAmbientVol, 1f);
        if (ambientVolSlider != null) ambientVolSlider.value = savedAmbient;
        ApplyVolume("AmbientVolume", savedAmbient);

        // 4. Громкость SFX (По умолчанию 1.0)
        float savedSFX = PlayerPrefs.GetFloat(PrefsSFXVol, 1f);
        if (sfxVolSlider != null) sfxVolSlider.value = savedSFX;
        ApplyVolume("SFXVolume", savedSFX);
        RestoreResolution();
    }

    // Вспомогательный метод для перевода линейного значения слайдера (0.0001 - 1) в децибелы (-80 - 0)
    private void ApplyVolume(string paramName, float sliderValue)
    {
        if (audioMixer != null)
        {
            // Защита от логарифма нуля, если слайдер случайно опустится до 0
            float clampedValue = Mathf.Clamp(sliderValue, 0.0001f, 1f);
            audioMixer.SetFloat(paramName, Mathf.Log10(clampedValue) * 20);
        }
    }

    // --- Методы основных кнопок меню ---

    public void UIResume() => ResumeGame();

    public void SetHideTips(bool value)
    {
        HideTips = value;
        PlayerPrefs.SetInt(PrefsHideTips, HideTips ? 1 : 0);
        PlayerPrefs.Save();
        RefreshTipsLabel();
    }

    private void RefreshTipsLabel()
    {
        if (hideTipsLabel == null) return;
        LocalizationService.EnsureInitialized();
        hideTipsLabel.font = LocalizationService.InterfaceFont;
        hideTipsLabel.text = LocalizationService.Get("menu.hide_tips");
        if (hideTipsToggle != null) hideTipsToggle.SetIsOnWithoutNotify(HideTips);
    }

    public void UIRestart()
    {
        GameSessionController.RestartGame();
    }

    public void UIQuit()
    {
        GameSessionController.LeaveGame();
    }

    // --- Настройки Разрешения экрана ---

    private void InitializeResolutions()
    {
        if (resolutionDropdown == null) return;
        var current = new Vector2Int(Screen.width, Screen.height);
        _resolutions = DisplayResolutions.GetSizes(Screen.resolutions, current);
        List<string> options = new List<string>();
        foreach (var size in _resolutions) options.Add(size.x + " x " + size.y);

        var selected = _resolutionChange != null ? _requestedResolution : current;
        int selectedIndex = DisplayResolutions.IndexOf(_resolutions, selected.x, selected.y);
        if (selectedIndex < 0) selectedIndex = DisplayResolutions.IndexOf(_resolutions, current.x, current.y);
        _buildingResolutionOptions = true;
        try
        {
            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(options);
            resolutionDropdown.interactable = _resolutions.Length > 1;
            if (selectedIndex >= 0) resolutionDropdown.SetValueWithoutNotify(selectedIndex);
            resolutionDropdown.RefreshShownValue();
        }
        finally
        {
            _buildingResolutionOptions = false;
        }
    }

    private void RestoreResolution()
    {
        int width = PlayerPrefs.GetInt(PrefsResolutionWidth, -1);
        int height = PlayerPrefs.GetInt(PrefsResolutionHeight, -1);
        int index = DisplayResolutions.IndexOf(_resolutions, width, height);
        if (index >= 0 && (Screen.width != width || Screen.height != height)) SetResolution(index);
    }

    // Назначается на событие OnValueChanged в компоненте Dropdown
    public void SetResolution(int resolutionIndex)
    {
        if (_buildingResolutionOptions || _resolutions == null || resolutionIndex < 0 || resolutionIndex >= _resolutions.Length) return;

        if (_resolutionChange != null) StopCoroutine(_resolutionChange);
        _requestedResolution = _resolutions[resolutionIndex];
        Screen.SetResolution(_requestedResolution.x, _requestedResolution.y, Screen.fullScreenMode);
        PlayerPrefs.SetInt(PrefsResolutionWidth, _requestedResolution.x);
        PlayerPrefs.SetInt(PrefsResolutionHeight, _requestedResolution.y);
        PlayerPrefs.Save();
        if (resolutionDropdown != null) resolutionDropdown.SetValueWithoutNotify(resolutionIndex);
        _resolutionChange = StartCoroutine(ConfirmResolution(_requestedResolution));
    }

    private IEnumerator ConfirmResolution(Vector2Int requested)
    {
        float deadline = Time.realtimeSinceStartup + 2f;
        yield return null;
        while ((Screen.width != requested.x || Screen.height != requested.y) && Time.realtimeSinceStartup < deadline)
            yield return null;

        _resolutionChange = null;
        if (Screen.width > 0 && Screen.height > 0 && (Screen.width != requested.x || Screen.height != requested.y))
        {
            PlayerPrefs.SetInt(PrefsResolutionWidth, Screen.width);
            PlayerPrefs.SetInt(PrefsResolutionHeight, Screen.height);
            PlayerPrefs.Save();
        }
        InitializeResolutions();
    }

    // --- Обработчики Слайдеров (сохранение в PlayerPrefs) ---

    // Назначается на событие OnValueChanged в компоненте Slider для Mouse Sensitivity
    public void SetMouseSensitivity(float value)
    {
        MouseSensitivity = value;
        PlayerPrefs.SetFloat(PrefsMouseSens, value);
        PlayerPrefs.Save();

        // Прямое изменение процессора Scale Vector 2 у экшена Look в New Input System
        if (inputAsset != null)
        {
            InputAction lookAction = inputAsset.FindActionMap(gameplayMapName)?.FindAction(lookActionName);
            if (lookAction != null)
            {
                // Принудительно используем инвариантную культуру для конвертации float в строку (через точку), 
                // иначе в русской локали ОС может сгенерироваться запятая "scaleVector2(x=0,5...)" и парсер Input System выдаст ошибку
                string invariantValue = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                
                // Перезаписываем параметры scaleVector2 для всех биндингов этого экшена
                lookAction.ApplyBindingOverride(new InputBinding { overrideProcessors = $"scaleVector2(x={invariantValue},y={invariantValue})" });
            }
        }
    }

    // Назначается на событие OnValueChanged в компоненте Slider для Master Volume
    public void SetMasterVolume(float value)
    {
        ApplyVolume("MasterVolume", value);
        PlayerPrefs.SetFloat(PrefsMasterVol, value);
        PlayerPrefs.Save();
    }

    // Назначается на событие OnValueChanged в компоненте Slider для Ambient Volume
    public void SetAmbientVolume(float value)
    {
        ApplyVolume("AmbientVolume", value);
        PlayerPrefs.SetFloat(PrefsAmbientVol, value);
        PlayerPrefs.Save();
    }

    // Назначается на событие OnValueChanged в компоненте Slider для SFX Volume
    public void SetSFXVolume(float value)
    {
        ApplyVolume("SFXVolume", value);
        PlayerPrefs.SetFloat(PrefsSFXVol, value);
        PlayerPrefs.Save();
    }

    // --- Настройки Языка ---

    // Назначается на событие OnClick кнопок с флагами
    public void SetLanguageRussian()
    {
        LocalizationService.SetLanguage("ru");
    }

    public void SetLanguageEnglish()
    {
        LocalizationService.SetLanguage("en");
    }
}
