using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(NPCBrain))]
public class NPCPixelation : MonoBehaviour
{
    [SerializeField] private bool enablePixelation = true;
    [SerializeField] private Transform modelRoot;
    [SerializeField] private Transform headBone;
    [SerializeField] private Material pixelationMaterial;
    [Tooltip("Размер блока в пикселях экрана")]
    [SerializeField, Range(4, 64)] private int pixelSize = 16;
    [Tooltip("Размер плейна в метрах")]
    [SerializeField] private Vector2 faceSize = new Vector2(0.65f, 0.7f);

    private static readonly int PixelSizeId = Shader.PropertyToID("_PixelSize");
    private static readonly int FaceSizeId = Shader.PropertyToID("_FaceSize");
    private NPCBrain _brain;
    private Transform _headEnd;
    private MeshRenderer _plane;
    private MaterialPropertyBlock _properties;

    private void Awake()
    {
        _brain = GetComponent<NPCBrain>();
        _properties = new MaterialPropertyBlock();
        if (headBone == null)
        {
            Transform root = modelRoot != null ? modelRoot : transform;
            foreach (Transform bone in root.GetComponentsInChildren<Transform>(true))
            {
                if (!string.Equals(bone.name, "head", System.StringComparison.OrdinalIgnoreCase) &&
                    !bone.name.EndsWith(":Head", System.StringComparison.OrdinalIgnoreCase)) continue;
                headBone = bone;
                break;
            }
        }
        if (headBone != null) _headEnd = headBone.Find("head_end");
    }

    private void LateUpdate()
    {
        bool show = enablePixelation && pixelationMaterial != null && headBone != null &&
            (_brain.Condition == TargetCondition.HeavyWounded || _brain.Condition == TargetCondition.Dead);
        if (show && _plane == null) CreatePlane();
        if (_plane == null) return;
        _plane.enabled = show;
        if (!show) return;

        _plane.transform.position = _headEnd != null
            ? (headBone.position + _headEnd.position) * 0.5f
            : headBone.position;
        _properties.SetFloat(PixelSizeId, pixelSize);
        _properties.SetVector(FaceSizeId, new Vector4(faceSize.x, faceSize.y, 0f, 0f));
        _plane.SetPropertyBlock(_properties);
    }

    private void CreatePlane()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Face Pixelation";
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        Destroy(go.GetComponent<Collider>());
        _plane = go.GetComponent<MeshRenderer>();
        _plane.sharedMaterial = pixelationMaterial;
        _plane.shadowCastingMode = ShadowCastingMode.Off;
        _plane.receiveShadows = false;
        _plane.lightProbeUsage = LightProbeUsage.Off;
        _plane.reflectionProbeUsage = ReflectionProbeUsage.Off;
        _plane.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        // The shader turns the quad toward the camera and moves it in front of the head.
        _plane.localBounds = new Bounds(Vector3.zero, Vector3.one * 4f);
    }

    private void OnDisable()
    {
        if (_plane != null) _plane.enabled = false;
    }

    private void OnDestroy()
    {
        if (_plane != null) Destroy(_plane.gameObject);
    }
}
