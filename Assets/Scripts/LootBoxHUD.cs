using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LootBoxHUD : MonoBehaviour
{
    public static LootBoxHUD Instance { get; private set; }

    [SerializeField] private TMP_Text openPrompt;
    [SerializeField] private TMP_Text counter;
    [SerializeField, Min(0f)] private float displayDuration = 5f;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.5f;

    private readonly HashSet<LootBox> _nearby = new HashSet<LootBox>();
    private float _counterAge;
    private bool _counterVisible;
    private int _lastPhoneSecond = -1;

    private void Awake()
    {
        openPrompt.gameObject.SetActive(false);
        counter.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        Instance = this;
        GameManager.OnLootBoxOpened += ShowCounter;
    }

    private void OnDisable()
    {
        GameManager.OnLootBoxOpened -= ShowCounter;
        if (Instance == this) Instance = null;
        _nearby.Clear();
        openPrompt.gameObject.SetActive(false);
        counter.gameObject.SetActive(false);
        _counterVisible = false;
    }

    public void SetNearby(LootBox box, bool nearby)
    {
        if (nearby) _nearby.Add(box);
        else _nearby.Remove(box);
        openPrompt.gameObject.SetActive(_nearby.Count > 0);
    }

    private void ShowCounter(int openedCount)
    {
        if (GameManager.Instance == null || GameManager.Instance.PhoneCountdownActive) return;
        counter.text = $"{openedCount}/{GameManager.Instance.RequiredLootBoxes}";
        counter.alpha = 1f;
        counter.gameObject.SetActive(true);
        _counterAge = 0f;
        _counterVisible = true;
    }

    private void Update()
    {
        var manager = GameManager.Instance;
        if (manager != null && manager.PhoneCountdownActive)
        {
            int seconds = Mathf.CeilToInt(manager.PhoneTimeRemaining);
            if (seconds != _lastPhoneSecond)
            {
                counter.SetText("{0}", seconds);
                _lastPhoneSecond = seconds;
            }
            counter.alpha = 1f;
            counter.gameObject.SetActive(true);
            _counterVisible = false;
            return;
        }
        if (!_counterVisible) return;
        _counterAge += Time.deltaTime;
        counter.alpha = 1f - Mathf.Clamp01((_counterAge - displayDuration) / fadeDuration);
        if (_counterAge < displayDuration + fadeDuration) return;
        _counterVisible = false;
        counter.gameObject.SetActive(false);
    }
}
