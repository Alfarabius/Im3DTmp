using UnityEngine;

public class CoverPoint : MonoBehaviour
{
    private void Start()
    {
        if (CoverManager.Instance != null)
            CoverManager.Instance.RegisterCoverPoint(this);
    }

    private void OnDestroy()
    {
        if (CoverManager.Instance != null)
            CoverManager.Instance.UnregisterCoverPoint(this);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.5f);
        Gizmos.DrawSphere(transform.position, 0.3f);
    }
#endif
}
