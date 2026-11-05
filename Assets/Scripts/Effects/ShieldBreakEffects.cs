using System;
using System.Collections.Generic;
using UnityEngine;

public class ShieldBreakEffects : MonoBehaviour
{
    [SerializeField] private TextAsset[] fragments;
    [SerializeField] private Material[] materials;
    [SerializeField, Min(0.1f)] private float lifetime = 5f;
    [SerializeField, Min(0f)] private float separationSpeed = 1.8f;
    [SerializeField, Min(0f)] private float spinSpeed = 3f;

    [Serializable] private class SubmeshData { public int[] triangles; }
    [Serializable] private class MeshData
    {
        public Vector3[] vertices;
        public Vector3[] normals;
        public SubmeshData[] submeshes;
    }

    private static readonly Dictionary<TextAsset, Mesh> Meshes = new Dictionary<TextAsset, Mesh>();

    public bool Break(PlayerBrain owner, Vector3 sourceDirection)
    {
        if (fragments == null || fragments.Length != 2 || fragments[0] == null || fragments[1] == null) return false;

        var colliders = new MeshCollider[2];
        for (int i = 0; i < 2; i++)
        {
            var mesh = GetMesh(fragments[i]);
            var piece = new GameObject("Shield fragment");
            piece.layer = 2;
            piece.transform.SetPositionAndRotation(transform.position, transform.rotation);
            piece.transform.localScale = transform.lossyScale;
            piece.AddComponent<MeshFilter>().sharedMesh = mesh;
            piece.AddComponent<MeshRenderer>().sharedMaterials = materials;
            var solid = piece.AddComponent<MeshCollider>();
            solid.convex = true;
            solid.sharedMesh = mesh;
            colliders[i] = solid;
            var body = piece.AddComponent<Rigidbody>();
            body.mass = 0.4f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            float side = i == 0 ? -1f : 1f;
            body.linearVelocity = transform.right * (side * separationSpeed) - sourceDirection.normalized * 0.5f + Vector3.up * 0.35f;
            body.angularVelocity = transform.up * (side * spinSpeed) + transform.forward * (side * spinSpeed * 0.5f);
            Physics.IgnoreCollision(solid, owner.Controller, true);
            Destroy(piece, lifetime);
        }
        Physics.IgnoreCollision(colliders[0], colliders[1], true);
        Destroy(gameObject);
        return true;
    }

    private static Mesh GetMesh(TextAsset asset)
    {
        if (Meshes.TryGetValue(asset, out var cached) && cached != null) return cached;
        var data = JsonUtility.FromJson<MeshData>(asset.text);
        var mesh = new Mesh { name = asset.name, vertices = data.vertices, normals = data.normals };
        mesh.subMeshCount = data.submeshes.Length;
        for (int i = 0; i < data.submeshes.Length; i++) mesh.SetTriangles(data.submeshes[i].triangles, i);
        mesh.RecalculateBounds();
        mesh.UploadMeshData(false);
        Meshes[asset] = mesh;
        return mesh;
    }
}
