using System;
using UnityEngine;

[Serializable]
public sealed class LaunchConfiguration
{
    public string lang;
    public int auto;

    public static string NormalizeLanguage(string language)
    {
        return string.Equals(language?.Trim(), "ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en";
    }

    public static LaunchConfiguration Parse(string argument)
    {
        if (!string.IsNullOrWhiteSpace(argument))
        {
            try
            {
                var configuration = JsonUtility.FromJson<LaunchConfiguration>(argument);
                if (configuration != null) return configuration;
            }
            catch (ArgumentException exception)
            {
                Debug.LogWarning("[GameApplication] Invalid launch JSON: " + exception.Message);
            }
        }

        return new LaunchConfiguration { lang = "en" };
    }
}
