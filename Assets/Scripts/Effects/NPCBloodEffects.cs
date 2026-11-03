using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(NPCBrain))]
public class NPCBloodEffects : MonoBehaviour
{
    [SerializeField] private Material bloodMaterial;
    [SerializeField, Range(4, 32)] private int splatterCount = 18;
    [SerializeField, Min(0.15f)] private float trailSpacing = 0.5f;
    [SerializeField, Min(5f)] private float stainLifetime = 90f;
    [SerializeField, Min(0.1f)] private float stainSizeMultiplier = 3f;
    [SerializeField, Min(0.1f)] private float puddleSize = 0.9f;
    [SerializeField, Min(1f)] private float puddleGrowthTime = 12f;
    [SerializeField] private LayerMask groundMask = (1 << 0) | (1 << 6) | (1 << 9);

    private NPCBrain _brain;
    private ParticleSystem _splatter;
    private Transform _body;
    private Transform _puddle;
    private Vector3 _lastStainPosition;
    private float _puddleAge;
    private float _fallTimer;
    private float _groundY;
    private bool _groundFound;

    private void Awake()
    {
        _brain = GetComponent<NPCBrain>();
        _body = transform;
        _lastStainPosition = transform.position;
        foreach (Transform bone in GetComponentsInChildren<Transform>(true))
        {
            if (!bone.name.Equals("spine02", System.StringComparison.OrdinalIgnoreCase) &&
                !bone.name.Equals("spine", System.StringComparison.OrdinalIgnoreCase) &&
                !bone.name.Equals("spine_01", System.StringComparison.OrdinalIgnoreCase) &&
                !bone.name.EndsWith(":Spine", System.StringComparison.OrdinalIgnoreCase)) continue;
            _body = bone;
            break;
        }
        if (bloodMaterial == null) return;
        _splatter = BloodEffectFactory.CreateSplatter(transform, bloodMaterial);
    }

    private void OnEnable() => _brain.OnHit += HandleHit;

    private void OnDisable()
    {
        _brain.OnHit -= HandleHit;
        if (_splatter != null) _splatter.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void HandleHit(Vector3 point, Vector3 normal)
    {
        if (_splatter == null) return;
        BloodEffectFactory.EmitSplatter(_splatter, point, normal, splatterCount);
        AddStain(point, Random.Range(0.15f, 0.25f));
        _lastStainPosition = transform.position;
    }

    private void LateUpdate()
    {
        if (bloodMaterial == null || _brain.Condition == TargetCondition.Normal) return;
        if (_brain.Condition == TargetCondition.Wounded)
        {
            if ((transform.position - _lastStainPosition).sqrMagnitude < trailSpacing * trailSpacing) return;
            AddStain(transform.position + Random.insideUnitSphere * 0.08f, Random.Range(0.09f, 0.17f));
            _lastStainPosition = transform.position;
            return;
        }

        // Let the falling animation settle before placing the pool under the torso.
        _fallTimer += Time.deltaTime;
        if (_fallTimer < 1.2f) return;
        if (_puddle == null) CreatePuddle();
        _puddleAge += Time.deltaTime;
        float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_puddleAge / puddleGrowthTime));
        _puddle.localScale = Vector3.one * Mathf.Lerp(0.15f, puddleSize, progress) * stainSizeMultiplier;
    }

    private Vector3 GroundPoint(Vector3 position)
    {
        if (!_groundFound)
        {
            _groundY = Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down,
                out RaycastHit hit, 4f, groundMask, QueryTriggerInteraction.Ignore)
                ? hit.point.y : transform.position.y;
            _groundY += 0.012f;
            _groundFound = true;
        }
        position.y = _groundY;
        return position;
    }

    private void AddStain(Vector3 position, float size)
    {
        BloodEffectFactory.EmitStain(bloodMaterial, GroundPoint(position), size * stainSizeMultiplier, stainLifetime);
    }

    private void CreatePuddle()
    {
        var go = new GameObject("Blood Puddle", typeof(MeshFilter), typeof(MeshRenderer));
        _puddle = go.transform;
        _puddle.SetParent(BloodEffectFactory.GetStainsRoot(bloodMaterial), false);
        _puddle.position = GroundPoint(_body.position) + Vector3.up * 0.002f;
        _puddle.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
        go.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = bloodMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }

    private void OnDestroy()
    {
        if (_puddle != null) Destroy(_puddle.gameObject);
    }
}
