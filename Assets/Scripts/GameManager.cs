using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Условия победы")]
    [Tooltip("Активировать победу при сборе заданного числа лутбоксов")] [SerializeField] private bool enableLootBoxWin = true;
    [Tooltip("Необходимое количество лутбоксов для победы")] [SerializeField] private int requiredLootBoxes = 7;
    [Tooltip("Активировать победу при смерти врага")] [SerializeField] private bool enableEnemyDeathWin = true;

    [Header("Условия поражения")]
    [Tooltip("Активировать поражение при смерти игрока")] [SerializeField] private bool enablePlayerDeathLoss = true;

    private int _openedLootBoxesCount;
    private bool _isGameOver;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
    }

    public void RegisterLootBoxOpened()
    {
        if (_isGameOver || !enableLootBoxWin) return;

        _openedLootBoxesCount++;
        
        if (_openedLootBoxesCount >= requiredLootBoxes)
        {
            EndGame(true);
        }
    }

    public void ReportEnemyDeath()
    {
        if (_isGameOver || !enableEnemyDeathWin) return;
        
        EndGame(true);
    }

    public void ReportPlayerDeath()
    {
        if (_isGameOver || !enablePlayerDeathLoss) return;
        
        EndGame(false);
    }

    private void EndGame(bool isWin)
    {
        _isGameOver = true;
        
        if (isWin)
        {
            Debug.Log("Победа! Перезагрузка сцены...");
        }
        else
        {
            Debug.Log("Поражение! Перезагрузка сцены...");
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
