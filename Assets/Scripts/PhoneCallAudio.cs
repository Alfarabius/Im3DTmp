using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PhoneCallAudio : MonoBehaviour
{
    [SerializeField] private AudioClip dialingSound;
    [SerializeField] private AudioClip sirenSound;
    [SerializeField, Min(0f)] private float sirenStartDelay = 15f;
    [SerializeField, Min(0.1f)] private float sirenFadeDuration = 5f;
    [SerializeField, Range(0f, 1f)] private float sirenVolume = 1f;

    private AudioSource _source;
    private bool _sirenStarted;

    private void Awake()
    {
        _source = GetComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.spatialBlend = 0f;
    }

    public void PlayDialing()
    {
        StopAllCoroutines();
        _sirenStarted = false;
        _source.Stop();
        _source.ignoreListenerPause = false;
        _source.loop = false;
        _source.volume = 1f;
        _source.clip = dialingSound;
        if (dialingSound != null) _source.Play();
    }

    public void UpdateCountdown(float elapsed)
    {
        if (elapsed < sirenStartDelay || sirenSound == null) return;
        if (!_sirenStarted)
        {
            _sirenStarted = true;
            _source.clip = sirenSound;
            _source.loop = true;
            _source.volume = 0f;
            _source.Play();
        }
        _source.volume = sirenVolume * Mathf.SmoothStep(0f, 1f,
            Mathf.Clamp01((elapsed - sirenStartDelay) / sirenFadeDuration));
    }

    public void Finish(bool won)
    {
        if (won && _sirenStarted) StartCoroutine(FadeOut());
        else _source.Stop();
    }

    private IEnumerator FadeOut()
    {
        _source.ignoreListenerPause = true;
        float initialVolume = _source.volume;
        for (float elapsed = 0f; elapsed < 1f; elapsed += Time.unscaledDeltaTime)
        {
            _source.volume = initialVolume * (1f - elapsed);
            yield return null;
        }
        _source.Stop();
        _source.ignoreListenerPause = false;
    }
}
