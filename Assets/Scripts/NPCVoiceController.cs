using System.Collections.Generic;
using UnityEngine;

public class NPCVoiceController : MonoBehaviour
{
    [Header("Audio Setup")]
    [SerializeField] private AudioSource audioSource;

    [Header("Scream Settings")]
    [SerializeField] private List<AudioClip> panicScreams;
    [SerializeField, Range(0f, 1f)] private float screamProbability = 0.3f;
    [SerializeField] private float localCooldown = 15f;

    private float _nextAllowedScreamTime;
    private int _lastScreamIndex = -1;

    public void TryPanicScream()
    {
        if (panicScreams == null || panicScreams.Count == 0 || audioSource == null) return;
        
        // 1. Проверка локального кулдауна (чтобы один бот не орал каждые две секунды)
        if (Time.time < _nextAllowedScreamTime) return;

        // 2. Проверка персональной вероятности испуга
        if (Random.value > screamProbability) return;

        // 3. Запрос к глобальному менеджеру на право голоса
        if (ScreamManager.Instance != null && ScreamManager.Instance.RequestScream())
        {
            PlayRandomScream();
            _nextAllowedScreamTime = Time.time + localCooldown;
        }
    }

    private void PlayRandomScream()
    {
        if (panicScreams.Count == 1)
        {
            audioSource.PlayOneShot(panicScreams[0]);
            return;
        }

        int randomIndex = Random.Range(0, panicScreams.Count);

        // Сдвиг индекса, если рандом выдал тот же звук, что и в прошлый раз
        if (randomIndex == _lastScreamIndex)
        {
            randomIndex = (randomIndex + 1) % panicScreams.Count;
        }

        _lastScreamIndex = randomIndex;
        audioSource.PlayOneShot(panicScreams[randomIndex]);
    }
}
