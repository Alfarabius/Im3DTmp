using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
public class Collectible : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float flightDuration = 0.8f;
    [SerializeField, Min(0f)] private float flightHeight = 1f;
    [SerializeField] private InventoryItemDefinition itemDefinition;
    [SerializeField, Min(0f)] private float pickupCooldown = 0.8f;
    [SerializeField] private UnityEvent<PlayerBrain> onUsed;

    private SphereCollider _trigger;
    private Rigidbody _body;
    private Collider _solid;
    private LayerMask _playerLayer = 1 << 7;
    private Vector3 _worldScale;
    private PlayerBrain _dropper;
    private float _pickupAfter;
    private Vector3 _origin;
    private Vector3 _destination;
    private Quaternion _rotation;
    private float _elapsed;
    private bool _flying;
    private bool _ready;
    private bool _thrown;
    public InventoryItemDefinition Definition => itemDefinition;
    public bool IsStored { get; private set; }

    private void Awake()
    {
        _trigger = GetComponent<SphereCollider>();
        _trigger.isTrigger = true;
        _ready = true;
        _trigger.enabled = true;
        _body = GetComponent<Rigidbody>();
        _body.useGravity = false;
        _body.isKinematic = true;
        _worldScale = transform.lossyScale;
        _solid = GetComponent<BoxCollider>();
        if (_solid == null)
        {
            var renderers = GetComponentsInChildren<Renderer>();
            Bounds bounds = new Bounds(transform.position, Vector3.one * 0.2f);
            if (renderers.Length > 0)
            {
                bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            }
            if (itemDefinition != null && itemDefinition.Kind == InventoryItemKind.Ball)
            {
                var sphere = gameObject.AddComponent<SphereCollider>();
                sphere.center = transform.InverseTransformPoint(bounds.center);
                sphere.radius = Mathf.Max(bounds.extents.x / _worldScale.x,
                    Mathf.Max(bounds.extents.y / _worldScale.y, bounds.extents.z / _worldScale.z));
                _solid = sphere;
            }
            else
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.center = transform.InverseTransformPoint(bounds.center);
                box.size = new Vector3(bounds.size.x / _worldScale.x, bounds.size.y / _worldScale.y, bounds.size.z / _worldScale.z);
                _solid = box;
            }
        }
        _solid.enabled = false;
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
        if (!_flying)
        {
            if (IsStored || !_ready) return;
            if (_dropper != null)
            {
                Vector3 scale = transform.lossyScale;
                float diameter = _trigger.radius * 2f * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
                var bounds = new Bounds(transform.TransformPoint(_trigger.center), Vector3.one * diameter);
                if (!_dropper.Controller.bounds.Intersects(bounds)) ClearDropper();
            }
            if (Time.time >= _pickupAfter) _trigger.enabled = true;
            return;
        }
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
        if (!_ready || IsStored || Time.time < _pickupAfter) return;
        var player = other.GetComponentInParent<PlayerBrain>();
        if (player == null || player == _dropper || ((1 << player.gameObject.layer) & _playerLayer) == 0) return;
        var inventory = player.GetComponent<PlayerInventory>();
        if (inventory != null) inventory.TryAdd(this);
    }

    public void Store(Transform parent)
    {
        ClearDropper();
        IsStored = true;
        _ready = false;
        _flying = false;
        _trigger.enabled = false;
        _solid.enabled = false;
        if (!_body.isKinematic)
        {
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
        }
        _body.collisionDetectionMode = CollisionDetectionMode.Discrete;
        _body.isKinematic = true;
        _body.useGravity = false;
        _thrown = false;
        transform.SetParent(parent, true);
        gameObject.SetActive(false);
    }

    public void Equip(Transform anchor)
    {
        transform.SetParent(anchor, false);
        Vector3 scale = anchor.lossyScale;
        transform.localScale = new Vector3(_worldScale.x / scale.x, _worldScale.y / scale.y, _worldScale.z / scale.z) * itemDefinition.HeldScale;
        gameObject.SetActive(true);
    }

    public void Drop(Vector3 position, Vector3 velocity, PlayerBrain owner)
    {
        _thrown = itemDefinition.Kind == InventoryItemKind.Ball;
        transform.SetParent(null, true);
        transform.localScale = _worldScale;
        transform.position = position;
        IsStored = false;
        _ready = true;
        _pickupAfter = Time.time + pickupCooldown;
        _trigger.enabled = false;
        _solid.enabled = true;
        _body.isKinematic = false;
        _body.useGravity = true;
        _body.linearVelocity = velocity;
        _body.angularVelocity = Random.insideUnitSphere;
        _dropper = owner;
        Physics.IgnoreCollision(_solid, owner.Controller, true);
        gameObject.SetActive(true);
    }

    public void Throw(Vector3 position, Vector3 impulse, PlayerBrain owner)
    {
        Drop(position, Vector3.zero, owner);
        _body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _body.AddForce(impulse, ForceMode.Impulse);
        _thrown = true;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!_thrown || itemDefinition.Kind != InventoryItemKind.Ball) return;
        var enemy = collision.collider.GetComponentInParent<EnemyBrain>();
        if (enemy == null) return;
        _thrown = false;
        enemy.Stun(itemDefinition.StunDuration);
    }

    private void ClearDropper()
    {
        if (_dropper != null) Physics.IgnoreCollision(_solid, _dropper.Controller, false);
        _dropper = null;
    }

    public void Use(PlayerBrain player)
    {
        if (itemDefinition.Kind == InventoryItemKind.Heal) player.Heal();
        else if (itemDefinition.Kind == InventoryItemKind.Phone && GameManager.Instance != null)
            GameManager.Instance.StartPhoneCountdown();
        onUsed?.Invoke(player);
        if (itemDefinition.ConsumeOnUse) Destroy(gameObject);
    }
}
