using UnityEngine;

public sealed class ImmersiveFootsteps : MonoBehaviour
{
    [SerializeField] private ImmersiveSimFirstPersonController controller;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] footstepClips;

    [SerializeField] private float walkStepInterval = 0.52f;
    [SerializeField] private float runStepInterval = 0.36f;
    [SerializeField] private float crouchStepInterval = 0.64f;

    [SerializeField] private float minPlanarSpeed = 0.25f;
    [SerializeField] private float pitchMin = 0.95f;
    [SerializeField] private float pitchMax = 1.08f;

    private float _timer;

    public void Awake()
    {
        _timer = 0f;
    }

    public void Update()
    {
        if (controller == null || audioSource == null) return;
        if (footstepClips == null || footstepClips.Length == 0) return;

        if (!controller.IsGrounded())
        {
            _timer = 0f;
            return;
        }

        Vector3 v = controller.GetPlanarVelocity();
        float planarSpeed = new Vector2(v.x, v.z).magnitude;

        if (planarSpeed < minPlanarSpeed)
        {
            _timer = 0f;
            return;
        }

        float interval = GetInterval(planarSpeed);

        _timer += Time.deltaTime;
        if (_timer < interval) return;

        _timer = 0f;
        PlayOne();
    }

    private float GetInterval(float planarSpeed)
    {
        if (controller.IsCrouching()) return crouchStepInterval;
        if (planarSpeed > 3.1f) return runStepInterval;
        return walkStepInterval;
    }

    private void PlayOne()
    {
        int index = Random.Range(0, footstepClips.Length);
        audioSource.pitch = Random.Range(pitchMin, pitchMax);
        audioSource.PlayOneShot(footstepClips[index]);
    }
}

