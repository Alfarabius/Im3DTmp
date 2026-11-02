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

    private void Awake()
    {
        _brain = GetComponent<PlayerBrain>();
        if (breathingAudio != null)
        {
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
        StopAndRestore();
    }

    private void HandleStaminaDepleted(bool depleted) => _depleted = depleted;

    // LateUpdate выполняется после изменения стамины в PlayerActiveState,
    // а порядок -10 позволяет обновить шум до обычного LateUpdate камеры.
    private void LateUpdate()
    {
        if (PauseManager.IsPaused) return;

        bool active = _depleted && _brain.Condition != TargetCondition.Dead;
        float duration = active ? fadeInDuration : fadeOutDuration;
        _blend = Mathf.MoveTowards(_blend, active ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, duration));
        if (_blend <= 0f)
        {
            if (_effectsApplied) StopAndRestore();
            return;
        }

        if (!_effectsApplied) CaptureBaseline();
        float strength = Mathf.SmoothStep(0f, 1f, _blend);

        if (breathingAudio != null)
        {
            breathingAudio.volume = breathingVolume * strength;
            if (!breathingAudio.isPlaying) breathingAudio.Play();
        }

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

    private void StopAndRestore()
    {
        if (breathingAudio != null)
        {
            breathingAudio.Stop();
            breathingAudio.volume = 0f;
        }

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
