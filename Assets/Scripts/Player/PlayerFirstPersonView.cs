using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(-100)]
public class PlayerFirstPersonView : MonoBehaviour
{
    [SerializeField] private CharacterController controller;
    [SerializeField] private Camera viewCamera;
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private Transform collisionTarget;
    [SerializeField] private Transform modelRoot;
    [SerializeField] private int headLayer = 11;

    private readonly List<Mesh> _meshes = new List<Mesh>();

    private void Awake()
    {
        viewCamera.cullingMask &= ~(1 << headLayer);
        foreach (var renderer in modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            SeparateHead(renderer);
        LateUpdate();
    }

    private void LateUpdate()
    {
        Vector3 point = controller.center;
        float extent = Mathf.Max(0f, controller.height * 0.5f - controller.radius);
        float height = controller.transform.InverseTransformPoint(cameraTarget.position).y;
        point.y += Mathf.Clamp(height - point.y, -extent, extent);
        collisionTarget.position = controller.transform.TransformPoint(point);
    }

    private void SeparateHead(SkinnedMeshRenderer renderer)
    {
        Mesh source = renderer.sharedMesh;
        if (source == null) return;
        Transform[] bones = renderer.bones;
        var headBones = new bool[bones.Length];
        for (int i = 0; i < bones.Length; i++) headBones[i] = bones[i] != null && bones[i].name == "head";
        BoneWeight[] weights = source.boneWeights;
        var headVertices = new bool[weights.Length];
        for (int i = 0; i < weights.Length; i++)
        {
            BoneWeight w = weights[i];
            headVertices[i] = IsHead(w.boneIndex0, w.weight0) || IsHead(w.boneIndex1, w.weight1)
                || IsHead(w.boneIndex2, w.weight2) || IsHead(w.boneIndex3, w.weight3);
        }
        bool IsHead(int bone, float weight) => weight >= 0.5f && bone < headBones.Length && headBones[bone];

        var headTriangles = new List<int>[source.subMeshCount];
        var bodyTriangles = new List<int>[source.subMeshCount];
        int headCount = 0, bodyCount = 0;
        for (int sub = 0; sub < source.subMeshCount; sub++)
        {
            headTriangles[sub] = new List<int>();
            bodyTriangles[sub] = new List<int>();
            int[] triangles = source.GetTriangles(sub);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                bool head = headVertices[triangles[i]] || headVertices[triangles[i + 1]] || headVertices[triangles[i + 2]];
                var target = head ? headTriangles[sub] : bodyTriangles[sub];
                target.Add(triangles[i]); target.Add(triangles[i + 1]); target.Add(triangles[i + 2]);
                if (head) headCount++; else bodyCount++;
            }
        }
        if (headCount == 0) return;

        Mesh headMesh;
        if (bodyCount == 0)
        {
            renderer.gameObject.layer = headLayer;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            headMesh = source;
        }
        else
        {
            renderer.sharedMesh = CopyMesh(source, bodyTriangles, "FirstPersonBody");
            headMesh = CopyMesh(source, headTriangles, "FirstPersonHead");
            CreateRenderer(renderer, headMesh, headLayer, ShadowCastingMode.Off);
        }
        CreateRenderer(renderer, headMesh, gameObject.layer, ShadowCastingMode.ShadowsOnly);
    }

    private Mesh CopyMesh(Mesh source, List<int>[] triangles, string name)
    {
        Mesh mesh = Instantiate(source);
        mesh.name = name;
        for (int sub = 0; sub < triangles.Length; sub++) mesh.SetTriangles(triangles[sub], sub, false);
        _meshes.Add(mesh);
        return mesh;
    }

    private static void CreateRenderer(SkinnedMeshRenderer source, Mesh mesh, int layer, ShadowCastingMode shadows)
    {
        var head = new GameObject(shadows == ShadowCastingMode.ShadowsOnly ? "HeadShadow" : "Head");
        head.layer = layer;
        head.transform.SetParent(source.transform, false);
        var renderer = head.AddComponent<SkinnedMeshRenderer>();
        renderer.sharedMesh = mesh;
        renderer.bones = source.bones;
        renderer.rootBone = source.rootBone;
        renderer.localBounds = source.localBounds;
        renderer.sharedMaterials = source.sharedMaterials;
        renderer.shadowCastingMode = shadows;
        renderer.updateWhenOffscreen = source.updateWhenOffscreen;
    }

    private void OnDestroy()
    {
        foreach (Mesh mesh in _meshes) Destroy(mesh);
    }
}
