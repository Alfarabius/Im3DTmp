using UnityEngine;
using Unity.Cinemachine;

[DefaultExecutionOrder(1)]
public class PlayerCameraNoiseCollision : CinemachineExtension
{
    [SerializeField] private LayerMask obstacleLayers = ~((1 << 2) | (1 << 5) | (1 << 7));
    [SerializeField, Min(0.01f)] private float cameraRadius = 0.04f;
    private Vector3 _beforeNoise;

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage == CinemachineCore.Stage.Aim) _beforeNoise = state.GetCorrectedPosition();
        if (stage != CinemachineCore.Stage.Finalize) return;
        Vector3 offset = state.GetCorrectedPosition() - _beforeNoise;
        float distance = offset.magnitude;
        if (distance < 0.0001f) return;
        if (Physics.SphereCast(_beforeNoise, cameraRadius, offset / distance, out RaycastHit hit,
            distance, obstacleLayers, QueryTriggerInteraction.Ignore))
            state.PositionCorrection += offset / distance * Mathf.Max(0f, hit.distance - 0.002f) - offset;
    }
}
