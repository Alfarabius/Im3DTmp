using System.Collections;
using UnityEngine;

[RequireComponent(typeof(LootBox))]
public class LootBoxEffects : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private Transform confettiOrigin;
    [SerializeField] private Material confettiMaterial;
    [SerializeField] private AudioSource openingAudio;

    [Header("Крышка")]
    [SerializeField] private Transform top;
    [SerializeField] private Vector3 slideOffset = new Vector3(0.75f, 0f, 0f);
    [SerializeField, Min(0.01f)] private float slideDuration = 0.25f;
    [Tooltip("Максимальное ожидание приземления крышки перед появлением содержимого.")]
    [SerializeField, Min(0.1f)] private float landingTimeout = 2f;
    [SerializeField] private LayerMask groundMask = (1 << 0) | (1 << 6) | (1 << 9);

    [Header("Конфетти")]
    [SerializeField, Range(8, 128)] private int confettiCount = 64;
    [SerializeField, Min(0.005f)] private float confettiSize = 0.045f;
    [SerializeField] private Color[] confettiColors =
    {
        new Color(1f, 0.22f, 0.22f), new Color(1f, 0.75f, 0.12f),
        new Color(0.15f, 0.8f, 1f), new Color(0.3f, 1f, 0.45f),
        new Color(0.85f, 0.3f, 1f), new Color(1f, 0.5f, 0.8f)
    };

    private LootBox _lootBox;
    private ParticleSystem _confetti;

    private void Awake()
    {
        _lootBox = GetComponent<LootBox>();
        if (confettiMaterial == null) return;

        _confetti = ParticleEffectFactory.Create(transform, "Confetti", confettiMaterial, Mathf.Clamp(confettiCount, 1, 128));
        var main = _confetti.main;
        main.startSize3D = true;
        main.startRotation3D = true;
        main.gravityModifier = 0.28f;
        var rotation = _confetti.rotationOverLifetime;
        rotation.enabled = true;
        rotation.separateAxes = true;
        rotation.x = new ParticleSystem.MinMaxCurve(-5f, 5f);
        rotation.y = new ParticleSystem.MinMaxCurve(-4f, 4f);
        rotation.z = new ParticleSystem.MinMaxCurve(-7f, 7f);
        var velocity = _confetti.limitVelocityOverLifetime;
        velocity.enabled = true;
        velocity.limit = 2.5f;
        velocity.dampen = 0.15f;
        var renderer = _confetti.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        ParticleEffectFactory.SetFade(_confetti, 0.65f);
    }

    private void OnEnable() => _lootBox.OnOpened += HandleOpened;

    private void OnDisable()
    {
        _lootBox.OnOpened -= HandleOpened;
        if (_confetti != null) _confetti.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public void PlayOpening(System.Action onCompleted)
    {
        if (openingAudio != null && openingAudio.clip != null) openingAudio.Play();
        StartCoroutine(AnimateTop(onCompleted));
    }

    private IEnumerator AnimateTop(System.Action onCompleted)
    {
        if (top != null)
        {
            Vector3 start = top.position;
            Vector3 offset = transform.TransformVector(slideOffset);
            Vector3 slideEnd = start + offset;
            var renderer = top.GetComponentInChildren<Renderer>();
            Bounds bounds = renderer != null ? renderer.bounds : new Bounds(start, Vector3.zero);
            Vector3 rayOrigin = bounds.center + offset + Vector3.up * 0.25f;
            float floorY = Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 5f,
                groundMask, QueryTriggerInteraction.Ignore) ? hit.point.y : transform.position.y;

            for (float elapsed = 0f; elapsed < slideDuration;)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / slideDuration);
                top.position = Vector3.Lerp(start, slideEnd, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            top.position = slideEnd;

            top.SetParent(null, true);
            top.gameObject.layer = 2;
            var solid = top.GetComponent<Collider>();
            if (solid == null)
            {
                var box = top.gameObject.AddComponent<BoxCollider>();
                var filter = top.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    box.center = filter.sharedMesh.bounds.center;
                    box.size = filter.sharedMesh.bounds.size;
                }
                solid = box;
            }
            solid.isTrigger = false;
            var body = top.GetComponent<Rigidbody>();
            if (body == null) body = top.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = false;
            body.useGravity = true;
            body.mass = 0.3f;
            body.angularDamping = 0.8f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.linearVelocity = offset.normalized * 0.6f;
            body.angularVelocity = Vector3.Cross(Vector3.up, offset.normalized) * 2.3f;
            var player = FindFirstObjectByType<PlayerBrain>();
            if (player != null) Physics.IgnoreCollision(solid, player.Controller, true);

            float maxFallTime = Mathf.Max(landingTimeout, 0.1f);
            for (float elapsed = 0f; elapsed < maxFallTime; elapsed += Time.deltaTime)
            {
                if (elapsed >= 0.1f && solid.bounds.min.y <= floorY + 0.04f &&
                    Mathf.Abs(body.linearVelocity.y) < 0.5f) break;
                yield return null;
            }
        }
        onCompleted?.Invoke();
    }

    private void HandleOpened()
    {
        if (_confetti == null) return;

        Vector3 origin = confettiOrigin != null ? confettiOrigin.position : transform.position + Vector3.up;
        _confetti.Play(false);
        int count = Mathf.Clamp(confettiCount, 1, 128);
        for (int i = 0; i < count; i++)
        {
            Vector2 spread = Random.insideUnitCircle;
            var particle = new ParticleSystem.EmitParams
            {
                position = origin,
                velocity = new Vector3(spread.x * 1.8f, Random.Range(2.6f, 4.2f), spread.y * 1.8f),
                startLifetime = Random.Range(2.2f, 3.1f),
                startSize3D = new Vector3(confettiSize, confettiSize * Random.Range(0.4f, 0.7f), confettiSize),
                rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f)),
                startColor = confettiColors != null && confettiColors.Length > 0
                    ? confettiColors[Random.Range(0, confettiColors.Length)] : Color.white
            };
            _confetti.Emit(particle, 1);
        }
    }
}
