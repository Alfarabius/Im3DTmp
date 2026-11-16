using TMPro;
using UnityEngine;
using UnityEngine.Audio;

public class PlayerDeathFeedback : MonoBehaviour
{
    [SerializeField] private AudioClip deathClip;
    [SerializeField] private AudioMixerGroup sfxMixerGroup;
    [SerializeField, Range(0f, 1f)] private float deathVolume = 0.5f;
    [SerializeField] private TMP_Text deathLabel;
    [SerializeField, Min(0.1f)] private float textFadeDuration = 2.5f;

    private bool _started;
    private float _elapsed;

    public void Play()
    {
        // Кровотечение и добивающий выстрел используют один сигнал смерти.
        if (_started) return;
        _started = true;
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
        if (!_started || deathLabel == null || !deathLabel.gameObject.activeSelf) return;
        _elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(_elapsed / Mathf.Max(0.1f, textFadeDuration));
        deathLabel.alpha = 1f - Mathf.SmoothStep(0f, 1f, progress);
        if (progress >= 1f) deathLabel.gameObject.SetActive(false);
    }
}
