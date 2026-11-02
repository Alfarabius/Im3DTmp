using UnityEngine;

[DefaultExecutionOrder(-100)]
public class PlayerFirstPersonView : MonoBehaviour
{
    [SerializeField] private CharacterController controller;
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private Transform collisionTarget;

    private void Awake() => LateUpdate();

    private void LateUpdate()
    {
        Vector3 point = controller.center;
        float extent = Mathf.Max(0f, controller.height * 0.5f - controller.radius);
        float height = controller.transform.InverseTransformPoint(cameraTarget.position).y;
        point.y += Mathf.Clamp(height - point.y, -extent, extent);
        collisionTarget.position = controller.transform.TransformPoint(point);
    }
}
