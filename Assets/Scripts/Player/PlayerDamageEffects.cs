using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;

[RequireComponent(typeof(PlayerBrain))]
[RequireComponent(typeof(CinemachineImpulseSource))]
public class PlayerDamageEffects : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private Volume postProcessVolume;
    [SerializeField] private AudioSource audioSource2D; 
    [SerializeField] private AudioClip nearMissSound;
    [SerializeField] private AudioClip hitSound;

    [Header("Настройки промаха (Near Miss)")]
    [SerializeField] private float nearMissRadius = 2f;
    [SerializeField] private float vignetteMissIntensity = 0.4f;
    [SerializeField] private float vignetteDecaySpeed = 2f;

    [Header("Настройки попадания (Hit)")]
    [SerializeField] private float vignetteHitIntensity = 0.6f;

    private PlayerBrain _playerBrain;
    private CinemachineImpulseSource _impulseSource;
    private Vignette _vignette;
    private ColorAdjustments _colorAdjustments;
    private float _currentVignetteTarget;
    
    // Переменные для отложенной обработки промаха
    private bool _hasPendingNearMiss;
    private Vector3 _pendingNearMissPos;

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
    }

    private void Update()
    {
        UpdateVignette();
        UpdateColorAdjustments();
    }

    private void LateUpdate()
    {
        // Обработка промаха в конце кадра, чтобы физическое попадание могло его отменить
        if (_hasPendingNearMiss)
        {
            PlayNearMissSound(_pendingNearMissPos);
            _currentVignetteTarget = Mathf.Max(_currentVignetteTarget, vignetteMissIntensity);
            
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

        _currentVignetteTarget = vignetteHitIntensity;
        
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
        src.Play();
        
        Destroy(soundObj, nearMissSound.length + 0.1f);
    }

    private void UpdateVignette()
    {
        if (_vignette == null) return;

        // Плавное затухание от вспышек промаха или попадания
        if (_currentVignetteTarget > 0)
        {
            _currentVignetteTarget -= Time.deltaTime * vignetteDecaySpeed;
            _currentVignetteTarget = Mathf.Max(0, _currentVignetteTarget);
        }

        float finalIntensity = _currentVignetteTarget;

        // Наложение перманентной пульсации при различных стадиях ранения
        if (_playerBrain.Condition == TargetCondition.Wounded)
        {
            finalIntensity = Mathf.Max(finalIntensity, 0.2f + Mathf.Sin(Time.time * 3f) * 0.05f);
        }
        else if (_playerBrain.Condition == TargetCondition.HeavyWounded)
        {
            finalIntensity = Mathf.Max(finalIntensity, 0.4f + Mathf.Sin(Time.time * 5f) * 0.1f);
        }

        _vignette.intensity.value = finalIntensity;
        _vignette.color.value = Color.red; 
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
