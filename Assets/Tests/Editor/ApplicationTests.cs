using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization.Tables;

public class ApplicationTests
{
    [TestCase(null, "en")]
    [TestCase("", "en")]
    [TestCase(" ru ", "ru")]
    [TestCase("RU", "ru")]
    [TestCase("en", "en")]
    [TestCase("de", "en")]
    [TestCase("ru-RU", "en")]
    public void LanguageFollowsPublisherContract(string input, string expected)
    {
        Assert.That(LaunchConfiguration.NormalizeLanguage(input), Is.EqualTo(expected));
    }

    [TestCase("{\"lang\":\"ru\"}", "ru")]
    [TestCase("{\"lang\":\"en\"}", "en")]
    [TestCase("{}", "en")]
    [TestCase("{\"lang\":\"de\"}", "en")]
    [TestCase("not json", "en")]
    [TestCase(null, "en")]
    public void LaunchArgumentFallsBackToEnglish(string json, string expected)
    {
        Assert.That(LaunchConfiguration.NormalizeLanguage(LaunchConfiguration.Parse(json).lang), Is.EqualTo(expected));
    }

    [Test]
    public void BothTablesContainEveryKeyAndMatchingPlaceholders()
    {
        var settings = Resources.Load<GameApplicationSettings>("GameApplicationSettings");
        Assert.That(settings, Is.Not.Null);
        Assert.That(settings.InterfaceFont, Is.Not.Null);
        Assert.That(settings.RussianTable.LocaleIdentifier.Code, Is.EqualTo("ru"));
        Assert.That(settings.EnglishTable.LocaleIdentifier.Code, Is.EqualTo("en"));
        foreach (SharedTableData.SharedTableEntry entry in settings.EnglishTable.SharedData.Entries)
        {
            Assert.That(settings.RussianTable.GetEntry(entry.Key)?.Value, Is.Not.Null.And.Not.Empty, entry.Key);
            Assert.That(settings.EnglishTable.GetEntry(entry.Key)?.Value, Is.Not.Null.And.Not.Empty, entry.Key);
        }
        StringAssert.Contains("{key}", settings.RussianTable.GetEntry("interaction.open_loot").Value);
        StringAssert.Contains("{key}", settings.EnglishTable.GetEntry("interaction.open_loot").Value);
    }

    [Test]
    public void SwitchingLanguagePersistsImmediatelyAndLaunchLanguageOverridesPreference()
    {
        const string key = LocalizationService.LanguagePreference;
        bool hadPreference = PlayerPrefs.HasKey(key);
        string oldValue = PlayerPrefs.GetString(key, "en");
        var settings = Resources.Load<GameApplicationSettings>("GameApplicationSettings");
        try
        {
            LocalizationService.Initialize(settings, "en");
            LocalizationService.SetLanguage("ru");
            Assert.That(LocalizationService.Get("menu.resume"), Is.EqualTo("Продолжить"));
            Assert.That(PlayerPrefs.GetString(key), Is.EqualTo("ru"));

            // Новый Windows-запуск без валидного lang обязан выбрать en, несмотря на сохранённый ru.
            LocalizationService.Initialize(settings, LaunchConfiguration.Parse(null).lang);
            Assert.That(LocalizationService.Language, Is.EqualTo("en"));
            Assert.That(LocalizationService.Get("menu.resume"), Is.EqualTo("Resume"));
        }
        finally
        {
            if (hadPreference) PlayerPrefs.SetString(key, oldValue);
            else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            LocalizationService.Reset();
        }
    }
}
