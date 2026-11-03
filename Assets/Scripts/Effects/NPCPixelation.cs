using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(NPCBrain))]
public class NPCPixelation : MonoBehaviour
{
    [SerializeField] private bool enablePixelation = true;
    [SerializeField] private Transform modelRoot;
    [SerializeField] private Material pixelationMaterial;
    [Tooltip("Размер блока в пикселях экрана")]
    [SerializeField, Range(4, 64)] private int pixelSize = 16;

    private static readonly int PixelSizeId = Shader.PropertyToID("_PixelSize");
    private NPCBrain _brain;
    private Renderer[] _overlays;
    private MaterialPropertyBlock _properties;
    private bool _visible;
    private int _appliedPixelSize;

    private void Awake()
    {
        _brain = GetComponent<NPCBrain>();
        _properties = new MaterialPropertyBlock();
    }

    private void LateUpdate()
    {
        bool show = enablePixelation && pixelationMaterial != null &&
            (_brain.Condition == TargetCondition.HeavyWounded || _brain.Condition == TargetCondition.Dead);
        if (show && _overlays == null) CreateOverlays();
        if (_overlays == null || (show == _visible && pixelSize == _appliedPixelSize)) return;

        _properties.SetFloat(PixelSizeId, pixelSize);
        foreach (Renderer overlay in _overlays)
        {
            overlay.enabled = show;
            if (show) overlay.SetPropertyBlock(_properties);
        }
        _visible = show;
        _appliedPixelSize = pixelSize;
    }

    private void CreateOverlays()
    {
        var overlays = new List<Renderer>();
        Transform root = modelRoot != null ? modelRoot : transform;
        foreach (Renderer source in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!(source is SkinnedMeshRenderer) && !(source is MeshRenderer)) continue;
            var go = new GameObject("Pixelation");
            go.layer = source.gameObject.layer;
            go.transform.SetParent(source.transform, false);
            Renderer overlay;
            if (source is SkinnedMeshRenderer skin)
            {
                var copy = go.AddComponent<SkinnedMeshRenderer>();
                copy.sharedMesh = skin.sharedMesh;
                copy.bones = skin.bones;
                copy.rootBone = skin.rootBone;
                copy.localBounds = skin.localBounds;
                copy.updateWhenOffscreen = skin.updateWhenOffscreen;
                overlay = copy;
            }
            else
            {
                go.AddComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
                overlay = go.AddComponent<MeshRenderer>();
            }
            var materials = new Material[source.sharedMaterials.Length];
            for (int i = 0; i < materials.Length; i++) materials[i] = pixelationMaterial;
            overlay.sharedMaterials = materials;
            overlay.shadowCastingMode = ShadowCastingMode.Off;
            overlay.receiveShadows = false;
            overlay.lightProbeUsage = LightProbeUsage.Off;
            overlay.reflectionProbeUsage = ReflectionProbeUsage.Off;
            overlay.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            overlay.enabled = false;
            overlays.Add(overlay);
        }
        _overlays = overlays.ToArray();
    }

    private void OnDisable()
    {
        if (_overlays != null)
            foreach (Renderer overlay in _overlays)
                if (overlay != null) overlay.enabled = false;
        _visible = false;
    }
}
