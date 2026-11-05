using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(10000)]
[RequireComponent(typeof(PlayerBrain), typeof(PlayerInput))]
public class PlayerInventory : MonoBehaviour
{
    public const int SlotCount = 3;
    public event System.Action Changed;
    public event System.Action FirstItemPickedUp;

    [SerializeField] private Transform view;
    [SerializeField, Min(0f)] private float dropSpeed = 1.5f;
    [SerializeField] private LayerMask environmentMask = (1 << 0) | (1 << 6) | (1 << 9);
    [SerializeField] private bool reachWithRightHand = true;
    [SerializeField, Min(0f)] private float heldDistanceOffset = 0.25f;

    private readonly Collectible[] _slots = new Collectible[SlotCount];
    private PlayerBrain _brain;
    private Transform _upperArm;
    private Transform _forearm;
    private Transform _hand;
    private float _useElapsed;
    private bool _requireRelease;
    private bool _useStarted;
    private int _inputResetFrame = -1;
    private static readonly Vector3 HandAnchorPosition = new Vector3(0.24f, -0.2f, 0.6f);
    private bool _handRotationCached;
    private Quaternion _handViewRotation;
    private Vector3 _handRestLocalPosition;
    private bool _hasPickedUpItem;
    private InputAction _useAction;
    private InputAction _dropAction;
    private InputAction[] _slotActions;

    public int SelectedSlot { get; private set; } = -1;
    public float UseProgress { get; private set; }
    public Collectible SelectedItem => GetSlot(SelectedSlot);

    private void Awake()
    {
        _brain = GetComponent<PlayerBrain>();
        var actions = GetComponent<PlayerInput>().actions;
        _useAction = actions.FindAction("Player/Action", true);
        _dropAction = actions.FindAction("Player/AltAction", true);
        _slotActions = new[] { actions.FindAction("Player/Slot1", true), actions.FindAction("Player/Slot2", true), actions.FindAction("Player/Slot3", true) };
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
        if (_hand != null) _handRestLocalPosition = _hand.localPosition;
    }

    private void OnEnable()
    {
        PauseManager.OnGamePaused += ResetUseInput;
        PauseManager.OnGameResumed += ResetUseInput;
        InputSystem.onDeviceChange += OnDeviceChange;
        ResetUseInput();
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected) ResetUseInput();
    }

    private void ResetUseInput()
    {
        CancelUse();
        _requireRelease = true;
        _inputResetFrame = Time.frameCount;
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
            bool firstPickup = !_hasPickedUpItem;
            _hasPickedUpItem = true;
            if (firstPickup)
            {
                SelectedSlot = i;
                CancelUse();
                _requireRelease = _useAction.IsPressed();
            }
            if (SelectedSlot == i && view != null) item.Equip(view);
            Changed?.Invoke();
            if (firstPickup) FirstItemPickedUp?.Invoke();
            return true;
        }
        return false;
    }

    public void SelectSlot(int index)
    {
        if (index < 0 || index >= SlotCount) return;
        if (SelectedItem != null) SelectedItem.Store(transform);
        SelectedSlot = index == SelectedSlot ? -1 : index;
        CancelUse();
        _requireRelease = _useAction.IsPressed();
        if (SelectedItem != null && view != null) SelectedItem.Equip(view);
        Changed?.Invoke();
    }

    private bool CanInteract => !PauseManager.IsPaused && !GameCutscenes.IsPlaying && Application.isFocused &&
        Cursor.lockState == CursorLockMode.Locked &&
        (_brain.Condition == TargetCondition.Normal || _brain.Condition == TargetCondition.Wounded);

    private void Update()
    {
        if (_hand != null) _hand.localPosition = _handRestLocalPosition;
        if (!CanInteract)
        {
            CancelUse();
            _requireRelease = true;
            return;
        }
        if (Time.frameCount == _inputResetFrame) return;
        for (int i = 0; i < _slotActions.Length; i++)
        {
            if (!_slotActions[i].WasPressedThisFrame()) continue;
            SelectSlot(i);
            break;
        }
        if (_requireRelease)
        {
            if (!_useAction.IsPressed() && !_useAction.WasPressedThisFrame() && !_useAction.WasReleasedThisFrame() &&
                !_dropAction.IsPressed() && !_dropAction.WasPressedThisFrame() && !_dropAction.WasReleasedThisFrame())
                _requireRelease = false;
            CancelUse();
            return;
        }
        if (_dropAction.WasPressedThisFrame())
        {
            DropSelected();
            _requireRelease = _useAction.IsPressed();
            return;
        }
        var item = SelectedItem;
        if (_useAction.WasPressedThisFrame()) _useStarted = true;
        if (!_useAction.IsPressed())
        {
            if (item != null && item.Definition.Kind == InventoryItemKind.Ball &&
                _useStarted)
                ThrowSelected();
            _requireRelease = false;
            CancelUse();
            return;
        }
        if (item == null || !_useStarted || item.Definition.Kind == InventoryItemKind.Shield) return;
        _useElapsed += Time.deltaTime;
        UseProgress = Mathf.Clamp01(_useElapsed / item.Definition.UseDuration);
        if (item.Definition.Kind == InventoryItemKind.Ball) return;
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
        _useStarted = false;
        _useElapsed = 0f;
        UseProgress = 0f;
    }

    private void ThrowSelected()
    {
        var item = SelectedItem;
        if (item == null || view == null) return;
        float impulse = Mathf.Lerp(item.Definition.MinThrowImpulse, item.Definition.MaxThrowImpulse, UseProgress);
        Vector3 position = GetDropPosition();
        _slots[SelectedSlot] = null;
        SelectedSlot = -1;
        item.Throw(position, view.forward * impulse, _brain);
        Changed?.Invoke();
    }

    public bool TryBlockShot(Vector3 sourceDirection)
    {
        var item = SelectedItem;
        if (item == null || item.Definition.Kind != InventoryItemKind.Shield || !item.gameObject.activeInHierarchy ||
            (_brain.Condition != TargetCondition.Normal && _brain.Condition != TargetCondition.Wounded)) return false;
        sourceDirection.y = 0f;
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (Vector3.Dot(forward, sourceDirection) <= 0f) return false;
        _slots[SelectedSlot] = null;
        SelectedSlot = -1;
        CancelUse();
        var breakEffects = item.GetComponent<ShieldBreakEffects>();
        if (breakEffects == null || !breakEffects.Break(_brain, sourceDirection))
            item.DropBroken(-sourceDirection.normalized * 0.5f, _brain, 5f);
        Changed?.Invoke();
        return true;
    }

    private Vector3 GetDropPosition()
    {
        Vector3 position = view.position + view.forward * 0.8f;
        if (Physics.Raycast(view.position, view.forward, out RaycastHit hit, 0.8f,
            environmentMask, QueryTriggerInteraction.Ignore)) position = hit.point - view.forward * 0.15f;
        return position;
    }

    public void DropSelected()
    {
        var item = SelectedItem;
        if (item == null || view == null) return;
        Vector3 position = GetDropPosition();
        _slots[SelectedSlot] = null;
        SelectedSlot = -1;
        CancelUse();
        item.Drop(position, view.forward * dropSpeed, _brain);
        Changed?.Invoke();
    }

    private void LateUpdate()
    {
        var item = SelectedItem;
        if (item == null || view == null)
        {
            return;
        }
        bool visible = _brain.Condition == TargetCondition.Normal || _brain.Condition == TargetCondition.Wounded;
        item.gameObject.SetActive(visible);
        if (!visible) return;
        item.UpdateHeldScale(view);
        var definition = item.Definition;
        Vector3 handPosition = HandAnchorPosition + Vector3.forward * heldDistanceOffset;
        Quaternion useRotation = Quaternion.identity;
        if (definition.Kind == InventoryItemKind.Heal && UseProgress > 0f)
        {
            float raise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(UseProgress * 4f));
            handPosition += new Vector3(-0.1f, 0.08f, 0f) * raise;
            useRotation = Quaternion.Euler(-10f * raise, 0f, 0f);
        }
        Vector3 handTarget = view.position + view.rotation * handPosition;
        Quaternion itemBasis = view.rotation * useRotation;
        item.transform.SetPositionAndRotation(handTarget + itemBasis * definition.ItemPosition,
            itemBasis * Quaternion.Euler(definition.ItemEulerAngles));
        if (!reachWithRightHand || _upperArm == null || _forearm == null || _hand == null) return;

        Vector3 target = handTarget;
        Vector3 shoulder = _upperArm.position;
        float upperLength = Vector3.Distance(shoulder, _forearm.position);
        float lowerLength = Vector3.Distance(_forearm.position, _hand.position);
        Vector3 direction = target - shoulder;
        if (direction.sqrMagnitude < 0.0001f || upperLength < 0.001f || lowerLength < 0.001f) return;
        float distance = Mathf.Clamp(direction.magnitude, Mathf.Abs(upperLength - lowerLength) + 0.001f,
            upperLength + lowerLength - 0.001f);
        direction.Normalize();
        target = shoulder + direction * distance;
        Vector3 bend = Vector3.ProjectOnPlane(transform.right - Vector3.up, direction);
        if (bend.sqrMagnitude < 0.001f) bend = Vector3.ProjectOnPlane(transform.forward, direction);
        bend.Normalize();
        float along = (upperLength * upperLength + distance * distance - lowerLength * lowerLength) / (2f * distance);
        Vector3 elbow = shoulder + direction * along + bend * Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
        _upperArm.rotation = Quaternion.FromToRotation(_forearm.position - shoulder, elbow - shoulder) * _upperArm.rotation;
        _forearm.rotation = Quaternion.FromToRotation(_hand.position - _forearm.position, target - _forearm.position) * _forearm.rotation;
        if (!_handRotationCached)
        {
            _handViewRotation = Quaternion.Inverse(view.rotation) * _hand.rotation;
            _handRotationCached = true;
        }
        _hand.rotation = view.rotation * _handViewRotation;
        _hand.position = handTarget;
    }

    private void OnDisable()
    {
        PauseManager.OnGamePaused -= ResetUseInput;
        PauseManager.OnGameResumed -= ResetUseInput;
        InputSystem.onDeviceChange -= OnDeviceChange;
        if (_hand != null) _hand.localPosition = _handRestLocalPosition;
        _handRotationCached = false;
        CancelUse();
    }
}
