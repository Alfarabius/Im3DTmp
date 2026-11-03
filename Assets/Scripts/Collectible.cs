using UnityEngine;

[RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
public class Collectible : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float flightDuration = 0.8f;
    [SerializeField, Min(0f)] private float flightHeight = 1f;

    private SphereCollider _trigger;
    private LayerMask _playerLayer;
    private Vector3 _origin;
    private Vector3 _destination;
    private Quaternion _rotation;
    private float _elapsed;
    private bool _flying;
    private bool _ready;
    private bool _collected;

    private void Awake()
    {
        _trigger = GetComponent<SphereCollider>();
        _trigger.isTrigger = true;
        _trigger.enabled = false;
        var body = GetComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;
    }

    public void Launch(Vector3 origin, Vector3 destination, LayerMask playerLayer)
    {
        _origin = origin;
        _destination = destination;
        _playerLayer = playerLayer;
        _rotation = transform.rotation;
        _elapsed = 0f;
        _ready = false;
        _flying = true;
        _trigger.enabled = false;
        transform.position = origin;
    }

    private void Update()
    {
        if (!_flying) return;
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / flightDuration);
        transform.position = Vector3.Lerp(_origin, _destination, t) + Vector3.up * (4f * flightHeight * t * (1f - t));
        transform.rotation = Quaternion.AngleAxis(360f * t, Vector3.up) * _rotation;
        if (t < 1f) return;
        _flying = false;
        _ready = true;
        _trigger.enabled = true;
    }

    private void OnTriggerEnter(Collider other) => TryCollect(other);

    private void OnTriggerStay(Collider other) => TryCollect(other);

    private void TryCollect(Collider other)
    {
        if (!_ready || _collected) return;
        var player = other.GetComponentInParent<PlayerBrain>();
        if (player == null || ((1 << player.gameObject.layer) & _playerLayer) == 0) return;
        _collected = true;
        _trigger.enabled = false;
        Destroy(gameObject);
    }
}
