using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
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
    
    [Header("Controls")]
    [Tooltip("Ссылка на действие паузы (например, кнопка Esc)")] [SerializeField] private InputActionReference pauseAction;

    // Глобальная переменная для чувствительности мыши, которую могут читать скрипты контроллера
    public static float MouseSensitivity { get; private set; } = 1f;

    // События для переключения состояний в других системах (например, анимациях UI)
    public static event Action OnGamePaused;
    public static event Action OnGameResumed;

    private Resolution[] _resolutions;

    // Константы ключей для сохранения настроек, чтобы избежать опечаток
    private const string PrefsMouseSens = "MouseSensitivity";
    private const string PrefsMasterVol = "MasterVolume";
    private const string PrefsAmbientVol = "AmbientVolume";
    private const string PrefsSFXVol = "SFXVolume";
    private const string PrefsResolutionWidth = "ResolutionWidth";
    private const string PrefsResolutionHeight = "ResolutionHeight";

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        
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
        if (pauseAction != null)
        {
            pauseAction.action.performed -= TogglePause;
        }
        if (resumeAction != null) 
            resumeAction.action.performed -= TogglePause;
    }

    private void TogglePause(InputAction.CallbackContext context)
    {
        if (IsPaused) ResumeGame();
        else PauseGame();
    }

    public void PauseGame()
    {
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
        if (pauseMenuUI != null) pauseMenuUI.SetActive(false);

        // Блокировка и скрытие курсора
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        OnGameResumed?.Invoke();
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

        Resolution[] allResolutions = Screen.resolutions;
        List<Resolution> filteredResolutions = new List<Resolution>();
        List<string> options = new List<string>();
        
        int targetIndex = 0;
        int currentResolutionIndex = -1;
        int savedResolutionIndex = -1;
        int savedWidth = PlayerPrefs.GetInt(PrefsResolutionWidth, -1);
        int savedHeight = PlayerPrefs.GetInt(PrefsResolutionHeight, -1);

        for (int i = 0; i < allResolutions.Length; i++)
        {
            float ratio = (float)allResolutions[i].width / allResolutions[i].height;
            
            // Отсеиваем только 16:9
            if (Mathf.Abs(ratio - (16f / 9f)) < 0.05f)
            {
                filteredResolutions.Add(allResolutions[i]);
                options.Add(allResolutions[i].width + " x " + allResolutions[i].height);

                // Запоминаем индекс 1920x1080 относительно нового, отфильтрованного списка
                if (allResolutions[i].width == 1920 && allResolutions[i].height == 1080)
                {
                    targetIndex = filteredResolutions.Count - 1;
                }

                if (allResolutions[i].width == Screen.width && allResolutions[i].height == Screen.height)
                {
                    currentResolutionIndex = filteredResolutions.Count - 1;
                }

                if (allResolutions[i].width == savedWidth && allResolutions[i].height == savedHeight)
                {
                    savedResolutionIndex = filteredResolutions.Count - 1;
                }
            }
        }

        // Перезаписываем глобальный массив только нужными разрешениями
        _resolutions = filteredResolutions.ToArray();

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(options);
        resolutionDropdown.interactable = _resolutions.Length > 0;

        if (_resolutions.Length == 0) return;

        // Индекс мог измениться вместе со списком, поэтому ищем сохранённые размеры заново.
        // Если режима больше нет, выбираем текущий, затем 1920x1080 или первый доступный.
        int finalIndex = savedResolutionIndex >= 0 ? savedResolutionIndex :
            currentResolutionIndex >= 0 ? currentResolutionIndex : targetIndex;

        // Тихо устанавливаем значение в Dropdown, чтобы не вызвать сброс сохранений
        resolutionDropdown.SetValueWithoutNotify(finalIndex);
        resolutionDropdown.RefreshShownValue();
    }

    // Назначается на событие OnValueChanged в компоненте Dropdown
    public void SetResolution(int resolutionIndex)
    {
        if (_resolutions == null || resolutionIndex < 0 || resolutionIndex >= _resolutions.Length) return;

        Resolution resolution = _resolutions[resolutionIndex];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
        
        PlayerPrefs.SetInt(PrefsResolutionWidth, resolution.width);
        PlayerPrefs.SetInt(PrefsResolutionHeight, resolution.height);
        PlayerPrefs.Save();
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
