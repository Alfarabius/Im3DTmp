using TMPro;
using UnityEngine;
using UnityEngine.Audio;

public class PlayerDeathFeedback : MonoBehaviour
{
    [SerializeField] private AudioClip deathClip;
    [SerializeField] private AudioMixerGroup sfxMixerGroup;
    [SerializeField, Range(0f, 1f)] private float deathVolume = 0.5f;
    [SerializeField] private TMP_Text deathLabel;
    [SerializeField, Min(0f)] private float bleedingFeedbackDelay = 3f;
    [SerializeField, Min(0.1f)] private float textFadeDuration = 5f;

    private bool _started;
    private bool _feedbackVisible;
    private float _elapsed;
    private float _startDelay;

    public float RemainingTime => _started
        ? Mathf.Max(0f, _startDelay + Mathf.Max(0.1f, textFadeDuration) - _elapsed) : 0f;

    public void Play(bool bleeding = false)
    {
        // Кровотечение и добивающий выстрел используют один сигнал смерти.
        if (_started) return;
        _started = true;
        _startDelay = bleeding ? Mathf.Max(0f, bleedingFeedbackDelay) : 0f;
        if (_startDelay <= 0f) BeginFeedback();
    }

    private void BeginFeedback()
    {
        _feedbackVisible = true;
        if (deathLabel != null)
        {
            deathLabel.alpha = 1f;
            deathLabel.gameObject.SetActive(true);
        }

        if (deathClip == null) return;
        var sound = new GameObject("Player death sound", typeof(AudioSource));
        // Не обрываем короткий звук при перезапуске сцены после прямого убийства.
        DontDestroyOnLoad(sound);
        var source = sound.GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.outputAudioMixerGroup = sfxMixerGroup;
        source.volume = deathVolume;
        source.clip = deathClip;
        source.Play();
        Destroy(sound, deathClip.length);
    }

    private void Update()
    {
        if (!_started) return;
        _elapsed += Time.deltaTime;
        if (!_feedbackVisible)
        {
            if (_elapsed < _startDelay) return;
            BeginFeedback();
        }
        if (deathLabel == null || !deathLabel.gameObject.activeSelf) return;
        float progress = Mathf.Clamp01((_elapsed - _startDelay) / Mathf.Max(0.1f, textFadeDuration));
        deathLabel.alpha = 1f - Mathf.SmoothStep(0f, 1f, progress);
        if (progress >= 1f) deathLabel.gameObject.SetActive(false);
    }
}
