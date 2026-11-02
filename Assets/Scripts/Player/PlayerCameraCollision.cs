using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Camera))]
public class PlayerCameraCollision : MonoBehaviour
{
    [Serializable]
    private class BodyVolume
    {
        public string boneName;
        public Vector3 center;
        public Vector3 size;
        [NonSerialized] public Transform bone;
        [NonSerialized] public Matrix4x4 localToWorld;
        [NonSerialized] public Matrix4x4 worldToLocal;
        [NonSerialized] public float scale;
        [NonSerialized] public Vector3 preferredDirection;
    }

    [Header("Ссылки")]
    [SerializeField] private PlayerBrain player;
    [SerializeField] private Transform bodyRoot;

    [Header("Окружение")]
    [SerializeField] private LayerMask environmentMask = ~((1 << 2) | (1 << 5));
    [SerializeField, Min(0.005f)] private float minimumRadius = 0.04f;
    [SerializeField, Min(0.001f)] private float surfacePadding = 0.01f;
    [SerializeField, Range(1, 16)] private int solverIterations = 8;
    [SerializeField, Min(0f)] private float returnSpeed = 10f;
    [SerializeField, Min(0.1f)] private float recoveryDistance = 0.8f;

    [Header("Объёмы тела")]
    [SerializeField] private BodyVolume[] bodyVolumes = Array.Empty<BodyVolume>();

    private Camera _camera;
    private SphereCollider _probe;
    private RaycastHit[] _hits = new RaycastHit[32];
    private Collider[] _overlaps = new Collider[32];
    private readonly Vector3[] _frustumCorners = new Vector3[4];
    private Vector3 _rawPosition;
    private Vector3 _lastSafePosition;
    private Vector3 _correction;
    private bool _hasSafePosition;
    private bool _applied;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        if (player == null) player = GetComponentInParent<PlayerBrain>();
        CacheBones();

        var probeObject = new GameObject("CameraCollisionProbe")
        {
            hideFlags = HideFlags.HideAndDontSave,
            layer = 2
        };
        _probe = probeObject.AddComponent<SphereCollider>();
        _probe.isTrigger = true;
        _probe.enabled = false;
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
        RenderPipelineManager.endCameraRendering += EndCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= EndCameraRendering;
        RestoreCameraPosition();
        _hasSafePosition = false;
        _correction = Vector3.zero;
    }

    private void OnDestroy()
    {
        if (_probe != null) Destroy(_probe.gameObject);
    }

    private void CacheBones()
    {
        if (bodyRoot == null || bodyVolumes == null) return;
        var bones = new Dictionary<string, Transform>();
        foreach (Transform bone in bodyRoot.GetComponentsInChildren<Transform>(true))
            bones.TryAdd(bone.name, bone);
        foreach (BodyVolume volume in bodyVolumes)
            if (volume != null && !string.IsNullOrEmpty(volume.boneName))
                bones.TryGetValue(volume.boneName, out volume.bone);
    }

    // URP вызывает событие после Cinemachine и анимации, до расчёта видимости сцены.
    private void BeginCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
    {
        if (renderingCamera != _camera || player == null || player.Controller == null) return;
        RestoreCameraPosition();
        Physics.SyncTransforms();
        UpdateBodyVolumes();
        _rawPosition = transform.position;
        float radius = GetCameraRadius();
        _probe.radius = radius;

        Vector3 solved = FindSafePosition(_rawPosition, radius);
        Vector3 targetCorrection = solved - _rawPosition;
        Vector3 smoothCorrection = Vector3.Lerp(_correction, targetCorrection,
            1f - Mathf.Exp(-returnSpeed * Time.deltaTime));

        // Сглаживание тоже проходит проверку, чтобы не возвращать камеру сквозь препятствие.
        if (_hasSafePosition && smoothCorrection.sqrMagnitude > 0.000001f)
            solved = FindSafePosition(_rawPosition + smoothCorrection, radius);

        _correction = solved - _rawPosition;
        _lastSafePosition = solved;
        _hasSafePosition = IsFree(solved, radius);
        transform.position = solved;
        _applied = true;
    }

    private void EndCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
    {
        if (renderingCamera == _camera) RestoreCameraPosition();
    }

    private void RestoreCameraPosition()
    {
        if (!_applied) return;
        transform.position = _rawPosition;
        _applied = false;
    }

    private float GetCameraRadius()
    {
        _camera.CalculateFrustumCorners(new Rect(0f, 0f, 1f, 1f), _camera.nearClipPlane,
            Camera.MonoOrStereoscopicEye.Mono, _frustumCorners);
        float radius = minimumRadius;
        foreach (Vector3 corner in _frustumCorners) radius = Mathf.Max(radius, corner.magnitude);
        return radius + surfacePadding;
    }

    private void UpdateBodyVolumes()
    {
        foreach (BodyVolume volume in bodyVolumes)
        {
            if (volume == null || volume.bone == null) continue;
            volume.localToWorld = volume.bone.localToWorldMatrix;
            volume.worldToLocal = volume.bone.worldToLocalMatrix;
            volume.scale = GetMinimumScale(volume.bone);
            volume.preferredDirection = volume.bone.InverseTransformDirection(player.transform.forward);
        }
    }

    private Vector3 GetAnchor(Vector3 wanted)
    {
        CharacterController controller = player.Controller;
        Transform root = controller.transform;
        Vector3 scale = root.lossyScale;
        Vector3 center = root.TransformPoint(controller.center);
        float height = controller.height * Mathf.Abs(scale.y);
        float radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float extent = Mathf.Max(0f, height * 0.5f - radius);
        return center + root.up * Mathf.Clamp(Vector3.Dot(wanted - center, root.up), -extent, extent);
    }

    private Vector3 FindSafePosition(Vector3 wanted, float radius)
    {
        Vector3 anchor = GetAnchor(wanted);
        for (int i = 0; i < solverIterations; i++)
            if (!PushOutOfEnvironment(ref anchor, radius)) break;
        Vector3 candidate = SweepEnvironment(anchor, wanted, radius);

        bool previousIsFree = _hasSafePosition && IsFree(_lastSafePosition, radius);
        if (previousIsFree)
        {
            candidate = SweepEnvironment(_lastSafePosition, candidate, radius);
            candidate = ClipAgainstBody(_lastSafePosition, candidate, radius);
        }
        if (TryResolve(ref candidate, radius)) return candidate;

        // При зажатии между телом и стеной ищем ближайшую свободную точку со стороны игрока.
        Vector3 best = candidate;
        float bestDistance = float.PositiveInfinity;
        for (int shell = 1; shell <= 3; shell++)
        {
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            for (int z = -1; z <= 1; z++)
            {
                if (x == 0 && y == 0 && z == 0) continue;
                Vector3 direction = player.transform.TransformDirection(new Vector3(x, y, z).normalized);
                Vector3 sample = SweepEnvironment(anchor, wanted + direction * (recoveryDistance * shell / 3f), radius);
                if (previousIsFree)
                {
                    sample = SweepEnvironment(_lastSafePosition, sample, radius);
                    sample = ClipAgainstBody(_lastSafePosition, sample, radius);
                }
                if (!TryResolve(ref sample, radius)) continue;
                float distance = (sample - wanted).sqrMagnitude;
                if (distance >= bestDistance) continue;
                best = sample;
                bestDistance = distance;
            }
            if (!float.IsPositiveInfinity(bestDistance)) return best;
        }
        if (previousIsFree) return _lastSafePosition;
        return candidate;
    }

    private bool TryResolve(ref Vector3 position, float radius)
    {
        for (int i = 0; i < solverIterations; i++)
        {
            bool moved = PushOutOfEnvironment(ref position, radius);
            Vector3 beforeBody = position;
            if (PushOutOfBody(ref position, radius))
            {
                position = ClipAgainstBody(beforeBody, position, radius);
                position = SweepEnvironment(beforeBody, position, radius);
                moved = true;
            }
            if (!moved) return true;
        }
        return IsFree(position, radius);
    }

    private bool PushOutOfBody(ref Vector3 position, float radius)
    {
        bool moved = false;
        foreach (BodyVolume volume in bodyVolumes)
        {
            if (volume == null || volume.bone == null) continue;
            float localRadius = radius / volume.scale;
            Vector3 point = volume.worldToLocal.MultiplyPoint3x4(position) - volume.center;
            if (!CameraCollisionGeometry.PushSphereOutOfBox(point, volume.size * 0.5f, localRadius, volume.preferredDirection, out Vector3 corrected))
                continue;
            Vector3 separated = volume.localToWorld.MultiplyPoint3x4(corrected + volume.center);
            position = separated + (separated - position).normalized * 0.001f;
            moved = true;
        }
        return moved;
    }

    private Vector3 ClipAgainstBody(Vector3 start, Vector3 end, float radius)
    {
        float fraction = 1f;
        foreach (BodyVolume volume in bodyVolumes)
        {
            if (volume == null || volume.bone == null) continue;
            float localRadius = radius / volume.scale;
            Vector3 a = volume.worldToLocal.MultiplyPoint3x4(start) - volume.center;
            Vector3 b = volume.worldToLocal.MultiplyPoint3x4(end) - volume.center;
            Vector3 halfSize = volume.size * 0.5f + Vector3.one * localRadius;
            if (CameraCollisionGeometry.SegmentEntersBox(a, b, halfSize, out float entry))
                fraction = Mathf.Min(fraction, Mathf.Max(0f, entry - 0.001f));
        }
        return Vector3.Lerp(start, end, fraction);
    }

    private static float GetMinimumScale(Transform bone)
    {
        Vector3 scale = bone.lossyScale;
        return Mathf.Max(0.00001f, Mathf.Min(Mathf.Abs(scale.x), Mathf.Min(Mathf.Abs(scale.y), Mathf.Abs(scale.z))));
    }

    private bool IsFree(Vector3 position, float radius)
    {
        Vector3 probe = position;
        return !PushOutOfBody(ref probe, radius - 0.00001f) && !PushOutOfEnvironment(ref probe, radius - 0.00001f);
    }

    private bool IsEnvironment(Collider obstacle) => obstacle != null && obstacle.enabled && !obstacle.isTrigger &&
        !obstacle.transform.IsChildOf(player.transform);

    private bool PushOutOfEnvironment(ref Vector3 position, float radius)
    {
        int count;
        while ((count = Physics.OverlapSphereNonAlloc(position, radius, _overlaps, environmentMask, QueryTriggerInteraction.Ignore)) == _overlaps.Length)
            Array.Resize(ref _overlaps, _overlaps.Length * 2);

        bool moved = false;
        _probe.radius = radius;
        for (int i = 0; i < count; i++)
        {
            Collider obstacle = _overlaps[i];
            if (!IsEnvironment(obstacle)) continue;
            if (!Physics.ComputePenetration(_probe, position, Quaternion.identity, obstacle,
                obstacle.transform.position, obstacle.transform.rotation, out Vector3 direction, out float distance))
                continue;
            position += direction * (distance + 0.001f);
            moved = true;
        }
        return moved;
    }

    private Vector3 SweepEnvironment(Vector3 start, Vector3 end, float radius)
    {
        for (int step = 0; step < 4; step++)
        {
            Vector3 offset = end - start;
            float distance = offset.magnitude;
            if (distance < 0.00001f) return start;
            Vector3 direction = offset / distance;
            int count;
            while ((count = Physics.SphereCastNonAlloc(start, radius, direction, _hits, distance,
                environmentMask, QueryTriggerInteraction.Ignore)) == _hits.Length)
                Array.Resize(ref _hits, _hits.Length * 2);

            float nearest = float.PositiveInfinity;
            Vector3 normal = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                if (!IsEnvironment(_hits[i].collider) || _hits[i].distance >= nearest) continue;
                nearest = _hits[i].distance;
                normal = _hits[i].normal;
            }
            if (float.IsPositiveInfinity(nearest)) return end;
            start += direction * Mathf.Max(0f, nearest - 0.001f);
            end = start + Vector3.ProjectOnPlane(end - start, normal);
        }
        return start;
    }

    private void OnDrawGizmosSelected()
    {
        if (bodyVolumes == null) return;
        if (bodyVolumes.Length > 0 && bodyVolumes[0]?.bone == null) CacheBones();
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.color = Color.cyan;
        foreach (BodyVolume volume in bodyVolumes)
        {
            if (volume == null || volume.bone == null) continue;
            Gizmos.matrix = volume.bone.localToWorldMatrix;
            Gizmos.DrawWireCube(volume.center, volume.size);
        }
        Gizmos.matrix = previousMatrix;
    }
}
