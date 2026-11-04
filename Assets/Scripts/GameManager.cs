using System.Collections.Generic;
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
    private GameCutscenes _cutscenes;
    private PhoneCallAudio _phoneAudio;
    private readonly List<GameObject> _lootBag = new List<GameObject>();

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
        _cutscenes = GetComponent<GameCutscenes>();
        _phoneAudio = GetComponent<PhoneCallAudio>();
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
        if (_lootBag.Count == 0)
        {
            foreach (var prefab in randomLootPrefabs)
                if (prefab != null) _lootBag.Add(prefab);
            for (int i = _lootBag.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (_lootBag[i], _lootBag[j]) = (_lootBag[j], _lootBag[i]);
            }
        }
        if (_lootBag.Count == 0) return null;
        int last = _lootBag.Count - 1;
        var loot = _lootBag[last];
        _lootBag.RemoveAt(last);
        return loot;
    }

    public void StartPhoneCountdown()
    {
        if (_isGameOver || PhoneCountdownActive) return;
        PhoneTimeRemaining = phoneCountdownDuration;
        PhoneCountdownActive = true;
        if (_phoneAudio != null) _phoneAudio.PlayDialing();
    }

    private void Update()
    {
        if (!PhoneCountdownActive || _isGameOver) return;
        PhoneTimeRemaining = Mathf.Max(0f, PhoneTimeRemaining - Time.deltaTime);
        if (_phoneAudio != null) _phoneAudio.UpdateCountdown(phoneCountdownDuration - PhoneTimeRemaining);
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
        if (_phoneAudio != null) _phoneAudio.Finish(isWin);
        
        if (isWin)
        {
            if (_cutscenes == null || !_cutscenes.PlayVictory(GameSessionController.CompleteGame))
                GameSessionController.CompleteGame();
        }
        else
        {
            GameSessionController.RestartGame();
        }
    }
}
