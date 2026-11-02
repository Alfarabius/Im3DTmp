using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;
using UnityEngine.Audio;

[RequireComponent(typeof(PlayerBrain))]
[RequireComponent(typeof(CinemachineImpulseSource))]
public class PlayerDamageEffects : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private Volume postProcessVolume;
    [SerializeField] private AudioSource audioSource2D; 
    [SerializeField] private AudioClip nearMissSound;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioMixerGroup sfxMixerGroup;
    
    [Header("Настройки промаха (Near Miss)")]
    [SerializeField] private float nearMissRadius = 2f;
    [SerializeField, Range(0f, 1f)] private float nearMissRedness = 0.25f;
    [SerializeField] private float rednessDecaySpeed = 2f;

    [Header("Настройки попадания (Hit)")]
    [SerializeField, Range(0f, 1f)] private float hitRedness = 0.4f;

    [Header("Звук боли")]
    [SerializeField] private AudioSource painAudio;
    [SerializeField] private AudioClip painClip;
    [SerializeField, Range(0f, 1f)] private float painVolume = 0.8f;
    [SerializeField, Min(0.01f)] private float painFadeInDuration = 0.3f;
    [SerializeField, Min(0.01f)] private float painFadeOutDuration = 1.2f;

    [Header("Покраснение при ранении")]
    [SerializeField, Range(0f, 1f)] private float minWoundRedness = 0.1f;
    [SerializeField, Range(0f, 1f)] private float maxWoundRedness = 0.3f;
    [Tooltip("Число пульсаций в секунду")]
    [SerializeField, Min(0f)] private float woundPulseFrequency = 0.8f;
    [SerializeField, Min(0.01f)] private float woundFadeInDuration = 0.3f;
    [SerializeField, Min(0.01f)] private float woundFadeOutDuration = 1.2f;

    private PlayerBrain _playerBrain;
    private CinemachineImpulseSource _impulseSource;
    private Vignette _vignette;
    private ColorAdjustments _colorAdjustments;
    private float _currentRedness;
    private float _woundBlend;
    private float _lastWoundRedness;
    private bool _painAudioActive;
    private float _painBlend;
    private AudioClip _previousClip;

    // Переменные для отложенной обработки промаха
    private bool _hasPendingNearMiss;
    private Vector3 _pendingNearMissPos;

    public bool IsUsingAudioSource(AudioSource source) => _painAudioActive && painAudio == source;

    private void Awake()
    {
        _playerBrain = GetComponent<PlayerBrain>();
        _impulseSource = GetComponent<CinemachineImpulseSource>();
        
        // Получение компонентов постпроцессинга из Volume
        if (postProcessVolume != null && postProcessVolume.profile != null)
        {
            postProcessVolume.profile.TryGet(out _vignette);
            postProcessVolume.profile.TryGet(out _colorAdjustments);
        }
    }

    private void OnEnable()
    {
        GlobalAudioEvents.OnGunshotFired += HandleGunshot;
        _playerBrain.OnHitReceived += HandleHit;
    }

    private void OnDisable()
    {
        GlobalAudioEvents.OnGunshotFired -= HandleGunshot;
        _playerBrain.OnHitReceived -= HandleHit;
        StopPainAudio();
    }

    private void Update()
    {
        UpdateRedness();
        UpdateColorAdjustments();
    }

    private void LateUpdate()
    {
        UpdatePainAudio();

        // Обработка промаха в конце кадра, чтобы физическое попадание могло его отменить
        if (_hasPendingNearMiss)
        {
            PlayNearMissSound(_pendingNearMissPos);
            _currentRedness = Mathf.Max(_currentRedness, nearMissRedness);
            
            _hasPendingNearMiss = false; 
        }
    }

    private void HandleGunshot(Vector3 origin, Vector3 direction)
    {
        if (_playerBrain.Condition == TargetCondition.Dead) return;

        Vector3 headPos = _playerBrain.HeadBone != null ? _playerBrain.HeadBone.position : transform.position;
        Vector3 toPlayer = headPos - origin;
        
        // Проверка направления пули, чтобы исключить летящие в противоположную сторону
        if (Vector3.Dot(direction, toPlayer) > 0)
        {
            float distanceToTrajectory = Vector3.Cross(direction, toPlayer).magnitude;

            if (distanceToTrajectory <= nearMissRadius)
            {
                // Регистрируем промах, ожидаем завершения кадра
                _pendingNearMissPos = origin + direction * Vector3.Dot(direction, toPlayer);
                _hasPendingNearMiss = true;
            }
        }
    }

    private void HandleHit()
    {
        // Отменяем эффект промаха, так как пуля попала в игрока
        _hasPendingNearMiss = false;

        if (audioSource2D != null && hitSound != null)
        {
            audioSource2D.PlayOneShot(hitSound);
        }

        _currentRedness = hitRedness;
        
        // Активация тряски камеры при попадании
        if (_impulseSource != null)
        {
            _impulseSource.GenerateImpulse();
        }
    }

    private void PlayNearMissSound(Vector3 position)
    {
        if (nearMissSound == null) return;
        
        // Временный объект для 3D-звука пролетающей пули
        GameObject soundObj = new GameObject("NearMissSound");
        soundObj.transform.position = position;
        
        AudioSource src = soundObj.AddComponent<AudioSource>();
        src.clip = nearMissSound;
        src.spatialBlend = 1f; 
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 1f;
        src.maxDistance = 20f;
        
        // Направление звука в заданный канал AudioMixer
        if (sfxMixerGroup != null)
        {
            src.outputAudioMixerGroup = sfxMixerGroup;
        }
        
        src.Play();
        
        Destroy(soundObj, nearMissSound.length + 0.1f);
    }

    private void UpdatePainAudio()
    {
        if (PauseManager.IsPaused || painAudio == null) return;

        bool wounded = _playerBrain.Condition == TargetCondition.Wounded || _playerBrain.Condition == TargetCondition.HeavyWounded;
        if (wounded && !_painAudioActive)
        {
            _previousClip = painAudio.clip;
            painAudio.Stop();
            painAudio.clip = painClip;
            painAudio.volume = 0f;
            painAudio.playOnAwake = false;
            painAudio.loop = true;
            painAudio.spatialBlend = 0f;
            _painAudioActive = true;
        }

        if (!_painAudioActive) return;

        float duration = wounded ? painFadeInDuration : painFadeOutDuration;
        _painBlend = Mathf.MoveTowards(_painBlend, wounded ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, duration));
        if (_painBlend <= 0f)
        {
            StopPainAudio();
            return;
        }

        painAudio.volume = painVolume * Mathf.SmoothStep(0f, 1f, _painBlend);
        if (painAudio.clip != null && !painAudio.isPlaying) painAudio.Play();
    }

    private void StopPainAudio()
    {
        if (_painAudioActive && painAudio != null)
        {
            painAudio.Stop();
            painAudio.volume = 0f;
            painAudio.clip = _previousClip;
        }
        _painAudioActive = false;
        _painBlend = 0f;
    }

    private void UpdateRedness()
    {
        if (_vignette == null) return;

        _currentRedness = Mathf.MoveTowards(_currentRedness, 0f, Time.deltaTime * rednessDecaySpeed);

        bool wounded = _playerBrain.Condition == TargetCondition.Wounded || _playerBrain.Condition == TargetCondition.HeavyWounded;
        if (wounded)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * woundPulseFrequency * 2f * Mathf.PI);
            _lastWoundRedness = Mathf.Lerp(minWoundRedness, maxWoundRedness, pulse);
        }
        float duration = wounded ? woundFadeInDuration : woundFadeOutDuration;
        _woundBlend = Mathf.MoveTowards(_woundBlend, wounded ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, duration));

        // После лечения фиксируем последнюю фазу пульса, чтобы краснота только убывала.
        float redness = Mathf.Max(_currentRedness, _lastWoundRedness * Mathf.SmoothStep(0f, 1f, _woundBlend));

        // Белый цвет нейтрален для URP Vignette: меняем только оттенок, сохраняя её форму.
        _vignette.color.value = Color.Lerp(Color.white, Color.red, redness);
    }

    private void UpdateColorAdjustments()
    {
        if (_colorAdjustments == null) return;

        float targetSaturation = 0f;

        if (_playerBrain.Condition == TargetCondition.Wounded)
        {
            targetSaturation = -30f; 
        }
        else if (_playerBrain.Condition == TargetCondition.HeavyWounded)
        {
            targetSaturation = -80f; // Выцветание почти до черно-белого
        }
        else if (_playerBrain.Condition == TargetCondition.Dead)
        {
            targetSaturation = -100f; 
        }

        // Плавная интерполяция цвета при смене состояний
        _colorAdjustments.saturation.value = Mathf.Lerp(_colorAdjustments.saturation.value, targetSaturation, Time.deltaTime * 2f);
    }
}
