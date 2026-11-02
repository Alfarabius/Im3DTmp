using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;

[RequireComponent(typeof(PlayerBrain))]
[DefaultExecutionOrder(-10)]
public class PlayerStaminaEffects : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private AudioSource breathingAudio;
    [SerializeField] private CinemachineBasicMultiChannelPerlin cameraNoise;
    [SerializeField] private Volume postProcessVolume;

    [Header("Истощение")]
    [SerializeField, Range(0f, 1f)] private float breathingVolume = 0.7f;
    [SerializeField, Min(1f)] private float shakeAmplitudeMultiplier = 2f;
    [SerializeField, Min(1f)] private float shakeFrequencyMultiplier = 1.25f;
    [SerializeField, Range(0f, 1f)] private float chromaticAberrationBoost = 0.35f;
    [Tooltip("Время плавного нарастания эффектов, секунды")]
    [SerializeField, Min(0.01f)] private float fadeInDuration = 0.8f;
    [Tooltip("Время плавного затухания после восстановления стамины, секунды")]
    [SerializeField, Min(0.01f)] private float fadeOutDuration = 1.2f;

    [Header("Боль — тот же AudioSource")]
    [SerializeField] private AudioClip painClip;
    [SerializeField, Range(0f, 1f)] private float painVolume = 0.8f;
    [SerializeField, Min(0.01f)] private float painFadeInDuration = 0.3f;
    [SerializeField, Min(0.01f)] private float painFadeOutDuration = 1.2f;

    private PlayerBrain _brain;
    private bool _depleted;
    private float _blend;
    private bool _effectsApplied;
    private float _baseAmplitude;
    private float _baseFrequency;
    private ChromaticAberration _chromaticAberration;
    private float _baseChromaticIntensity;
    private bool _baseChromaticActive;
    private bool _baseChromaticOverride;
    private enum VoiceMode { None, Breathing, Pain }
    private VoiceMode _voiceMode;
    private float _voiceBlend;
    private AudioClip _breathingClip;

    private void Awake()
    {
        _brain = GetComponent<PlayerBrain>();
        if (breathingAudio != null)
        {
            _breathingClip = breathingAudio.clip;
            breathingAudio.playOnAwake = false;
            breathingAudio.loop = true;
            breathingAudio.spatialBlend = 0f;
            breathingAudio.volume = 0f;
        }
    }

    private void OnEnable()
    {
        _brain.OnStaminaDepletedChanged += HandleStaminaDepleted;
        HandleStaminaDepleted(_brain.IsStaminaDepleted);
    }

    private void OnDisable()
    {
        _brain.OnStaminaDepletedChanged -= HandleStaminaDepleted;
        RestoreStaminaVisuals();
        ChangeVoiceMode(VoiceMode.None);
    }

    private void HandleStaminaDepleted(bool depleted) => _depleted = depleted;

    // LateUpdate выполняется после изменения стамины в PlayerActiveState,
    // а порядок -10 позволяет обновить шум до обычного LateUpdate камеры.
    private void LateUpdate()
    {
        if (PauseManager.IsPaused) return;

        bool wounded = _brain.Condition == TargetCondition.Wounded || _brain.Condition == TargetCondition.HeavyWounded;
        bool active = _depleted && _brain.Condition == TargetCondition.Normal;
        UpdateVoice(wounded, active);

        // Боль имеет приоритет: никакого остаточного усиления усталости у раненого.
        if (wounded)
        {
            RestoreStaminaVisuals();
            return;
        }
        float duration = active ? fadeInDuration : fadeOutDuration;
        _blend = Mathf.MoveTowards(_blend, active ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, duration));
        if (_blend <= 0f)
        {
            if (_effectsApplied) RestoreStaminaVisuals();
            return;
        }

        if (!_effectsApplied) CaptureBaseline();
        float strength = Mathf.SmoothStep(0f, 1f, _blend);

        if (cameraNoise != null)
        {
            cameraNoise.AmplitudeGain = _baseAmplitude * Mathf.Lerp(1f, shakeAmplitudeMultiplier, strength);
            cameraNoise.FrequencyGain = _baseFrequency * Mathf.Lerp(1f, shakeFrequencyMultiplier, strength);
        }

        if (_chromaticAberration != null)
        {
            _chromaticAberration.active = true;
            _chromaticAberration.intensity.overrideState = true;
            _chromaticAberration.intensity.value = Mathf.Clamp01(_baseChromaticIntensity + chromaticAberrationBoost * strength);
        }
    }

    private void CaptureBaseline()
    {
        if (cameraNoise != null)
        {
            _baseAmplitude = cameraNoise.AmplitudeGain;
            _baseFrequency = cameraNoise.FrequencyGain;
        }

        _chromaticAberration = null;
        if (postProcessVolume != null && postProcessVolume.profile != null)
            postProcessVolume.profile.TryGet(out _chromaticAberration);

        if (_chromaticAberration != null)
        {
            _baseChromaticIntensity = _chromaticAberration.intensity.value;
            _baseChromaticActive = _chromaticAberration.active;
            _baseChromaticOverride = _chromaticAberration.intensity.overrideState;
        }
        _effectsApplied = true;
    }

    private void UpdateVoice(bool wounded, bool exhausted)
    {
        if (breathingAudio == null) return;

        VoiceMode desired = wounded ? VoiceMode.Pain : exhausted ? VoiceMode.Breathing : VoiceMode.None;
        if (desired == VoiceMode.Pain && _voiceMode != VoiceMode.Pain)
            ChangeVoiceMode(VoiceMode.Pain);
        else if (_voiceMode == VoiceMode.None && desired != VoiceMode.None)
            ChangeVoiceMode(desired);

        bool active = _voiceMode != VoiceMode.None && _voiceMode == desired;
        float duration = _voiceMode == VoiceMode.Pain
            ? (active ? painFadeInDuration : painFadeOutDuration)
            : (active ? fadeInDuration : fadeOutDuration);
        _voiceBlend = Mathf.MoveTowards(_voiceBlend, active ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, duration));
        if (_voiceBlend <= 0f)
        {
            if (_voiceMode != VoiceMode.None) ChangeVoiceMode(VoiceMode.None);
            return;
        }

        float volume = _voiceMode == VoiceMode.Pain ? painVolume : breathingVolume;
        breathingAudio.volume = volume * Mathf.SmoothStep(0f, 1f, _voiceBlend);
        if (breathingAudio.clip != null && !breathingAudio.isPlaying) breathingAudio.Play();
    }

    private void ChangeVoiceMode(VoiceMode mode)
    {
        if (breathingAudio != null)
        {
            breathingAudio.Stop();
            breathingAudio.volume = 0f;
            breathingAudio.clip = mode == VoiceMode.Pain ? painClip : _breathingClip;
        }
        _voiceMode = mode;
        _voiceBlend = 0f;
    }

    private void RestoreStaminaVisuals()
    {
        if (_effectsApplied)
        {
            if (cameraNoise != null)
            {
                cameraNoise.AmplitudeGain = _baseAmplitude;
                cameraNoise.FrequencyGain = _baseFrequency;
            }
            if (_chromaticAberration != null)
            {
                _chromaticAberration.intensity.value = _baseChromaticIntensity;
                _chromaticAberration.intensity.overrideState = _baseChromaticOverride;
                _chromaticAberration.active = _baseChromaticActive;
            }
        }
        _blend = 0f;
        _effectsApplied = false;
    }
}
