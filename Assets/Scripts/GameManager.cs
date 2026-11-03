using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public static event System.Action<int> OnLootBoxOpened;

    [Header("Лутбоксы")]
    [SerializeField, Min(0)] private int activeLootBoxesCount = 10;
    [SerializeField] private GameObject[] randomLootPrefabs;
    [SerializeField] private GameObject phonePrefab;
    [SerializeField, Min(0.1f)] private float phoneCountdownDuration = 20f;

    [Header("Условия победы")]
    [Tooltip("Номер открытого сундука, в котором появляется телефон")] [SerializeField, Min(1)] private int requiredLootBoxes = 7;
    [Tooltip("Активировать победу при смерти врага")] [SerializeField] private bool enableEnemyDeathWin = true;

    [Header("Условия поражения")]
    [Tooltip("Активировать поражение при смерти игрока")] [SerializeField] private bool enablePlayerDeathLoss = true;

    private int _openedLootBoxesCount;
    private bool _isGameOver;

    public int RequiredLootBoxes => requiredLootBoxes;
    public bool PhoneCountdownActive { get; private set; }
    public float PhoneTimeRemaining { get; private set; }

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
    }

    public GameObject GetNextLootPrefab()
    {
        if (_openedLootBoxesCount + 1 == requiredLootBoxes) return phonePrefab;
        if (randomLootPrefabs == null || randomLootPrefabs.Length == 0) return null;
        return randomLootPrefabs[Random.Range(0, randomLootPrefabs.Length)];
    }

    public void StartPhoneCountdown()
    {
        if (_isGameOver || PhoneCountdownActive) return;
        PhoneTimeRemaining = phoneCountdownDuration;
        PhoneCountdownActive = true;
    }

    private void Update()
    {
        if (!PhoneCountdownActive || _isGameOver) return;
        PhoneTimeRemaining = Mathf.Max(0f, PhoneTimeRemaining - Time.deltaTime);
        if (PhoneTimeRemaining <= 0f) EndGame(true);
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
        PhoneCountdownActive = false;
        
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
