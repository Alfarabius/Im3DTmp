using System;
using System.Collections;
using UnityEngine;

// Адаптация GameApplication.cs издателя: JSON lang, exit codes 1/3/-1,
// Windows TerminateProcess. Платформенные ограничения и auto изолированы здесь.
public class GameApplication : MonoBehaviour
{
    public const int ExitCodeEarlyExit = 1;
    public const int ExitCodeCompleted = 3;
    public const int ExitCodeSystemError = -1;

    public static GameApplication Instance { get; private set; }
    public string AppliedLanguage => LocalizationService.Language;
    public string RawCommandLineArg { get; private set; } = "";
    public int? DesiredExitCode { get; private set; }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private bool _isQuitting;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        LocalizationService.Reset();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance == null) new GameObject("GameApplication").AddComponent<GameApplication>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        var settings = Resources.Load<GameApplicationSettings>("GameApplicationSettings");
        if (settings == null) throw new InvalidOperationException("GameApplicationSettings is missing from Resources.");

        LaunchConfiguration configuration;
#if UNITY_EDITOR
        RawCommandLineArg = "(editor)";
        configuration = new LaunchConfiguration { lang = settings.EditorOverrideLang };
#elif UNITY_STANDALONE_WIN
        string[] arguments = Environment.GetCommandLineArgs();
        RawCommandLineArg = arguments.Length > 1 ? arguments[1] : "";
        configuration = LaunchConfiguration.Parse(RawCommandLineArg);
#else
        // WebGL: язык сохраняется локально, командной строки лаунчера нет.
        configuration = new LaunchConfiguration { lang = PlayerPrefs.GetString(LocalizationService.LanguagePreference, "en") };
#endif
        LocalizationService.Initialize(settings, configuration.lang);
        Debug.Log("[GameApplication] Language: " + AppliedLanguage);
        Application.wantsToQuit += OnWantsToQuit;

#if DEVELOPMENT_BUILD && UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (configuration.auto == ExitCodeEarlyExit || configuration.auto == ExitCodeCompleted || configuration.auto == ExitCodeSystemError)
            StartCoroutine(AutoExitRoutine(configuration.auto));
#endif
    }

    private void OnDestroy()
    {
        Application.wantsToQuit -= OnWantsToQuit;
        if (Instance == this) Instance = null;
    }

    public void LeaveGame() => QuitWith(ExitCodeEarlyExit);
    public void CompleteGame() => QuitWith(ExitCodeCompleted);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void SimulateSystemError() => QuitWith(ExitCodeSystemError);
#endif

    private void QuitWith(int code)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (_isQuitting) return;
        _isQuitting = true;
#endif
        DesiredExitCode = code;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // Все настройки уже сохранены обработчиками изменений.
        // Не полагаемся на OnApplicationQuit/финализаторы после TerminateProcess.
        if (!TerminateProcess(GetCurrentProcess(), unchecked((uint)code)))
        {
            Debug.LogError("[GameApplication] TerminateProcess failed; using Unity quit.");
            Application.Quit(code);
        }
#else
        Debug.Log("[GameApplication] Exit code: " + code + " (test environment)");
#endif
    }

    private bool OnWantsToQuit()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        QuitWith(DesiredExitCode ?? ExitCodeEarlyExit);
#endif
        return true;
    }

    private void OnApplicationQuit()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        QuitWith(DesiredExitCode ?? ExitCodeEarlyExit);
#endif
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool TerminateProcess(IntPtr process, uint exitCode);

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();
#endif

#if DEVELOPMENT_BUILD && UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private IEnumerator AutoExitRoutine(int code)
    {
        yield return new WaitForSecondsRealtime(2f);
        QuitWith(code);
    }
#endif
}
