using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(AttackState), typeof(EnemyBrain))]
public class EnemyWeaponEffects : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private Transform muzzle;
    [SerializeField] private Material flashMaterial;
    [SerializeField] private Material smokeMaterial;
    [SerializeField] private Material tracerMaterial;

    [Header("Вспышка")]
    [SerializeField, Min(0.01f)] private float flashSize = 0.4f;
    [SerializeField, Min(0.01f)] private float flashLifetime = 0.065f;

    [Header("Трассер")]
    [SerializeField, Min(1f)] private float tracerSpeed = 140f;
    [SerializeField, Min(0.01f)] private float tracerLength = 1.8f;
    [SerializeField, Min(0.001f)] private float tracerWidth = 0.025f;
    [SerializeField, Min(0.01f)] private float minimumTracerDuration = 0.08f;

    private AttackState _attack;
    private EnemyBrain _brain;
    private ParticleSystem _flash;
    private ParticleSystem _smoke;
    private LineRenderer _tracer;
    private Vector3 _shotStart;
    private Vector3 _shotDirection;
    private float _shotDistance;
    private float _tracerDuration;
    private float _tracerTime;

    private void Awake()
    {
        _attack = GetComponent<AttackState>();
        _brain = GetComponent<EnemyBrain>();

        if (flashMaterial != null)
        {
            _flash = ParticleEffectFactory.Create(transform, "MuzzleFlash", flashMaterial, 4);
            ParticleEffectFactory.SetFade(_flash, 0.05f);
        }
        if (smokeMaterial != null)
        {
            _smoke = ParticleEffectFactory.Create(transform, "MuzzleSmoke", smokeMaterial, 12);
            ParticleEffectFactory.SetFade(_smoke, 0.1f);
            var size = _smoke.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.4f, 1f, 1.8f));
        }
        if (tracerMaterial != null)
        {
            var tracerObject = new GameObject("BulletTracer");
            tracerObject.transform.SetParent(transform, false);
            _tracer = tracerObject.AddComponent<LineRenderer>();
            _tracer.sharedMaterial = tracerMaterial;
            _tracer.useWorldSpace = true;
            _tracer.positionCount = 2;
            _tracer.widthMultiplier = tracerWidth;
            _tracer.startColor = new Color(1f, 0.45f, 0.12f, 0.05f);
            _tracer.endColor = new Color(1f, 0.9f, 0.55f, 1f);
            _tracer.shadowCastingMode = ShadowCastingMode.Off;
            _tracer.receiveShadows = false;
            _tracer.lightProbeUsage = LightProbeUsage.Off;
            _tracer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _tracer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            _tracer.enabled = false;
        }
    }

    private void OnEnable() => _attack.OnShotFired += HandleShot;

    private void OnDisable()
    {
        _attack.OnShotFired -= HandleShot;
        if (_flash != null) _flash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (_smoke != null) _smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (_tracer != null) _tracer.enabled = false;
    }

    private void HandleShot(Vector3 origin, Vector3 end)
    {
        _shotStart = muzzle != null ? muzzle.position : origin;
        Vector3 direction = end - _shotStart;
        float distance = direction.magnitude;
        if (distance <= 0.001f) return;
        direction /= distance;

        if (Physics.Raycast(_shotStart, direction, out RaycastHit obstruction, distance,
            _brain.ObstacleLayer, QueryTriggerInteraction.Ignore))
            end = obstruction.point;

        if (_flash != null)
        {
            var flash = new ParticleSystem.EmitParams
            {
                position = _shotStart,
                startSize = flashSize * Random.Range(0.85f, 1.15f),
                startLifetime = flashLifetime,
                startColor = new Color(1f, 0.75f, 0.35f, 1f),
                rotation = Random.Range(0f, 360f)
            };
            _flash.Play(false);
            _flash.Emit(flash, 1);
        }
        if (_smoke != null)
        {
            _smoke.Play(false);
            for (int i = 0; i < 3; i++)
            {
                var smoke = new ParticleSystem.EmitParams
                {
                    position = _shotStart,
                    velocity = direction * 0.4f + Vector3.up * 0.35f + Random.insideUnitSphere * 0.12f,
                    startSize = Random.Range(0.08f, 0.14f),
                    startLifetime = Random.Range(0.3f, 0.55f),
                    startColor = new Color(0.5f, 0.48f, 0.42f, 0.22f)
                };
                _smoke.Emit(smoke, 1);
            }
        }

        _shotDistance = Vector3.Distance(_shotStart, end);
        _shotDirection = (end - _shotStart).normalized;
        _tracerTime = 0f;
        _tracerDuration = Mathf.Max(minimumTracerDuration, (_shotDistance + tracerLength) / tracerSpeed);
        if (_tracer != null)
        {
            _tracer.SetPosition(0, _shotStart);
            _tracer.SetPosition(1, _shotStart + _shotDirection * Mathf.Min(0.35f, _shotDistance));
            _tracer.enabled = _shotDistance > 0.001f;
        }
    }

    private void Update()
    {
        if (_tracer == null || !_tracer.enabled || PauseManager.IsPaused) return;

        _tracerTime += Time.deltaTime;
        if (_tracerTime >= _tracerDuration)
        {
            _tracer.enabled = false;
            return;
        }

        float distance = _tracerTime / _tracerDuration * (_shotDistance + tracerLength);
        _tracer.SetPosition(0, _shotStart + _shotDirection * Mathf.Clamp(distance - tracerLength, 0f, _shotDistance));
        _tracer.SetPosition(1, _shotStart + _shotDirection * Mathf.Min(distance, _shotDistance));
    }

    private void OnDrawGizmosSelected()
    {
        if (muzzle == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(muzzle.position, 0.035f);
        Gizmos.DrawRay(muzzle.position, muzzle.forward * 0.3f);
    }
}
