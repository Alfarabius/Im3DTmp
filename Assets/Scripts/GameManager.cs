using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public static event System.Action<int> OnLootBoxOpened;

    [Header("Лутбоксы")]
    [SerializeField, Min(0)] private int activeLootBoxesCount = 10;

    [Header("Условия победы")]
    [Tooltip("Активировать победу при сборе заданного числа лутбоксов")] [SerializeField] private bool enableLootBoxWin = true;
    [Tooltip("Необходимое количество лутбоксов для победы")] [SerializeField] private int requiredLootBoxes = 7;
    [Tooltip("Активировать победу при смерти врага")] [SerializeField] private bool enableEnemyDeathWin = true;

    [Header("Условия поражения")]
    [Tooltip("Активировать поражение при смерти игрока")] [SerializeField] private bool enablePlayerDeathLoss = true;

    private int _openedLootBoxesCount;
    private bool _isGameOver;

    public int RequiredLootBoxes => requiredLootBoxes;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        InitializeLootBoxes();
    }

    private void InitializeLootBoxes()
    {
        LootBox[] lootBoxes = FindObjectsByType<LootBox>(FindObjectsSortMode.None);
        int keepCount = Mathf.Clamp(activeLootBoxesCount, 0, lootBoxes.Length);
        if (keepCount == lootBoxes.Length) return;

        for (int i = 0; i < keepCount; i++)
        {
            int randomIndex = Random.Range(i, lootBoxes.Length);
            (lootBoxes[randomIndex], lootBoxes[i]) = (lootBoxes[i], lootBoxes[randomIndex]);
        }

        for (int i = keepCount; i < lootBoxes.Length; i++)
            lootBoxes[i].gameObject.SetActive(false);
    }

    public void RegisterLootBoxOpened()
    {
        if (_isGameOver) return;

        _openedLootBoxesCount++;
        OnLootBoxOpened?.Invoke(_openedLootBoxesCount);
        
        if (enableLootBoxWin && _openedLootBoxesCount >= requiredLootBoxes)
        {
            EndGame(true);
            return;
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
            GameSessionController.CompleteGame();
        }
        else
        {
            GameSessionController.RestartGame();
        }
    }
}
