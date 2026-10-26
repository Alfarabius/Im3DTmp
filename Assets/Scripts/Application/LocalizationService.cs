using System;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Tables;

// Небольшие локальные Unity String Tables загружаются из Resources до сцены.
// Addressables-каталог/асинхронная загрузка для этих двух таблиц не требуются.
public static class LocalizationService
{
    public const string LanguagePreference = "Language";
    public static string Language { get; private set; } = "en";
    public static TMP_FontAsset InterfaceFont { get; private set; }
    public static event Action LanguageChanged;

    private static GameApplicationSettings _settings;

    public static void Reset()
    {
        if (InterfaceFont != null)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(InterfaceFont);
            else UnityEngine.Object.DestroyImmediate(InterfaceFont);
        }
        LanguageChanged = null;
        _settings = null;
        InterfaceFont = null;
        Language = "en";
    }

    public static void Initialize(GameApplicationSettings settings, string language)
    {
        if (settings.RussianTable == null || settings.EnglishTable == null || settings.InterfaceFont == null)
            throw new InvalidOperationException("Application settings must contain both language tables and a UI font.");

        _settings = settings;
        Language = LaunchConfiguration.NormalizeLanguage(language);
        if (InterfaceFont != null) return;
        InterfaceFont = TMP_FontAsset.CreateFontAsset(settings.InterfaceFont);
        if (InterfaceFont == null) throw new InvalidOperationException("Unable to create the RU/EN font atlas.");
        InterfaceFont.name = "Interface RU EN";
        InterfaceFont.isMultiAtlasTexturesEnabled = true;
        // Предварительно заполняем общий динамический атлас обеими локалями.
        InterfaceFont.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789" +
            "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя" +
            " []():/.,!?+-'\"%", out string missing);
        if (!string.IsNullOrEmpty(missing)) Debug.LogWarning("[Localization] Missing font characters: " + missing);
    }

    public static void SetLanguage(string language)
    {
        Language = LaunchConfiguration.NormalizeLanguage(language);
        // Сохраняем при каждом изменении. При следующем Windows-запуске CLI снова приоритетен.
        PlayerPrefs.SetString(LanguagePreference, Language);
        PlayerPrefs.Save();
        LanguageChanged?.Invoke();
    }

    public static string Get(string key)
    {
        StringTable table = Language == "ru" ? _settings.RussianTable : _settings.EnglishTable;
        StringTableEntry entry = table.GetEntry(key) ?? _settings.EnglishTable.GetEntry(key);
        if (entry != null) return entry.Value;
        Debug.LogError("[Localization] Missing key: " + key);
        return "[" + key + "]";
    }
}
