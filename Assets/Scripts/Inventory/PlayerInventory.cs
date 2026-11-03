using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(10000)]
[RequireComponent(typeof(PlayerBrain))]
public class PlayerInventory : MonoBehaviour
{
    public const int SlotCount = 3;
    public event System.Action Changed;

    [SerializeField] private Transform view;
    [SerializeField, Min(0f)] private float dropSpeed = 1.5f;
    [SerializeField] private LayerMask environmentMask = (1 << 0) | (1 << 6) | (1 << 9);
    [SerializeField] private bool reachWithRightHand = true;

    private readonly Collectible[] _slots = new Collectible[SlotCount];
    private PlayerBrain _brain;
    private Transform _upperArm;
    private Transform _forearm;
    private Transform _hand;
    private float _useElapsed;
    private bool _requireRelease;

    public int SelectedSlot { get; private set; } = -1;
    public float UseProgress { get; private set; }
    public Collectible SelectedItem => GetSlot(SelectedSlot);

    private void Awake()
    {
        _brain = GetComponent<PlayerBrain>();
        if (view == null)
        {
            var camera = GetComponentInChildren<Camera>();
            view = camera != null ? camera.transform : _brain.BodycamTarget;
        }
        foreach (Transform bone in GetComponentsInChildren<Transform>(true))
        {
            if (bone.name == "upper_arm.R") _upperArm = bone;
            else if (bone.name == "forearm.R") _forearm = bone;
            else if (bone.name == "hand.R") _hand = bone;
        }
    }

    public Collectible GetSlot(int index) => index >= 0 && index < SlotCount ? _slots[index] : null;

    public bool TryAdd(Collectible item)
    {
        if (item == null || item.Definition == null || item.IsStored) return false;
        for (int i = 0; i < SlotCount; i++)
        {
            if (_slots[i] != null) continue;
            _slots[i] = item;
            item.Store(transform);
            if (SelectedSlot == i && view != null) item.Equip(view);
            Changed?.Invoke();
            return true;
        }
        return false;
    }

    public void SelectSlot(int index)
    {
        if (index < 0 || index >= SlotCount || index == SelectedSlot) return;
        if (SelectedItem != null) SelectedItem.Store(transform);
        SelectedSlot = index;
        CancelUse();
        _requireRelease = Mouse.current != null && Mouse.current.rightButton.isPressed;
        if (SelectedItem != null && view != null) SelectedItem.Equip(view);
        Changed?.Invoke();
    }

    private bool CanInteract => !PauseManager.IsPaused && Application.isFocused &&
        Cursor.lockState == CursorLockMode.Locked &&
        (_brain.Condition == TargetCondition.Normal || _brain.Condition == TargetCondition.Wounded);

    private void Update()
    {
        if (!CanInteract)
        {
            CancelUse();
            _requireRelease = true;
            return;
        }
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) SelectSlot(0);
            else if (keyboard.digit2Key.wasPressedThisFrame) SelectSlot(1);
            else if (keyboard.digit3Key.wasPressedThisFrame) SelectSlot(2);
        }
        var mouse = Mouse.current;
        if (mouse == null) return;
        if (!mouse.rightButton.isPressed)
        {
            _requireRelease = false;
            CancelUse();
        }
        if (mouse.leftButton.wasPressedThisFrame)
        {
            DropSelected();
            return;
        }
        var item = SelectedItem;
        if (item == null || !mouse.rightButton.isPressed || _requireRelease) return;
        _useElapsed += Time.deltaTime;
        UseProgress = Mathf.Clamp01(_useElapsed / item.Definition.UseDuration);
        if (UseProgress < 1f) return;
        _requireRelease = true;
        CancelUse();
        if (item.Definition.ConsumeOnUse)
        {
            _slots[SelectedSlot] = null;
            SelectedSlot = -1;
        }
        item.Use(_brain);
        Changed?.Invoke();
    }

    private void CancelUse()
    {
        _useElapsed = 0f;
        UseProgress = 0f;
    }

    public void DropSelected()
    {
        var item = SelectedItem;
        if (item == null || view == null) return;
        Vector3 position = view.position + view.forward * 0.8f;
        if (Physics.Raycast(view.position, view.forward, out RaycastHit hit, 0.8f,
            environmentMask, QueryTriggerInteraction.Ignore)) position = hit.point - view.forward * 0.15f;
        _slots[SelectedSlot] = null;
        SelectedSlot = -1;
        CancelUse();
        item.Drop(position, view.forward * dropSpeed, _brain);
        Changed?.Invoke();
    }

    private void LateUpdate()
    {
        var item = SelectedItem;
        if (item == null || view == null) return;
        bool visible = _brain.Condition == TargetCondition.Normal || _brain.Condition == TargetCondition.Wounded;
        item.gameObject.SetActive(visible);
        if (!visible) return;
        var definition = item.Definition;
        item.transform.SetPositionAndRotation(view.position + view.rotation * definition.HeldPosition,
            view.rotation * Quaternion.Euler(definition.HeldEulerAngles));
        if (!reachWithRightHand || _upperArm == null || _forearm == null || _hand == null) return;

        Vector3 target = item.transform.TransformPoint(definition.GripOffset);
        Vector3 shoulder = _upperArm.position;
        float upperLength = Vector3.Distance(shoulder, _forearm.position);
        float lowerLength = Vector3.Distance(_forearm.position, _hand.position);
        Vector3 direction = target - shoulder;
        if (direction.sqrMagnitude < 0.0001f || upperLength < 0.001f || lowerLength < 0.001f) return;
        float distance = Mathf.Clamp(direction.magnitude, Mathf.Abs(upperLength - lowerLength) + 0.001f,
            upperLength + lowerLength - 0.001f);
        direction.Normalize();
        Vector3 bend = Vector3.ProjectOnPlane(view.right - view.up, direction).normalized;
        float along = (upperLength * upperLength + distance * distance - lowerLength * lowerLength) / (2f * distance);
        Vector3 elbow = shoulder + direction * along + bend * Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
        _upperArm.rotation = Quaternion.FromToRotation(_forearm.position - shoulder, elbow - shoulder) * _upperArm.rotation;
        _forearm.rotation = Quaternion.FromToRotation(_hand.position - _forearm.position, target - _forearm.position) * _forearm.rotation;
    }

    private void OnDisable() => CancelUse();
}
