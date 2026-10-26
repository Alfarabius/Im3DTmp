using UnityEngine;
using UnityEngine.Localization.Tables;

[CreateAssetMenu(menuName = "Game/Application Settings")]
public class GameApplicationSettings : ScriptableObject
{
    [Tooltip("Язык для прямого запуска сцены в Editor. В Windows используется аргумент лаунчера.")]
    [SerializeField] private string editorOverrideLang = "ru";
    [SerializeField] private StringTable russianTable;
    [SerializeField] private StringTable englishTable;
    [SerializeField] private Font interfaceFont;

    public string EditorOverrideLang => editorOverrideLang;
    public StringTable RussianTable => russianTable;
    public StringTable EnglishTable => englishTable;
    public Font InterfaceFont => interfaceFont;
}
