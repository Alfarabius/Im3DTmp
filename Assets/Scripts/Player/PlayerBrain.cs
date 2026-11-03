using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController), typeof(Animator))]
public class PlayerBrain : MonoBehaviour, ITargetable
{
    [field: SerializeField] public CharacterController Controller { get; private set; }
    [field: SerializeField] public Animator Anim { get; private set; }
    [field: SerializeField] public Transform BodycamTarget { get; private set; }
    [field: SerializeField] public Transform HeadBone { get; private set; }

    [Header("Stats")]
    [field: SerializeField] public float MaxStamina { get; private set; } = 100f;
    [field: SerializeField] public TargetCondition Condition { get; private set; } = TargetCondition.Normal;
    
    public const float ExhaustionRecoveryThreshold = 15f;
    public bool IsStaminaDepleted { get; private set; }
    public event System.Action<bool> OnStaminaDepletedChanged;

    private float _currentStamina;
    public float CurrentStamina
    {
        get => _currentStamina;
        set
        {
            _currentStamina = value;
            // Эффект начинается только при полном истощении, а заканчивается строго выше 15.
            bool depleted = IsStaminaDepleted ? value <= ExhaustionRecoveryThreshold : value <= 0f;
            if (depleted == IsStaminaDepleted) return;
            IsStaminaDepleted = depleted;
            OnStaminaDepletedChanged?.Invoke(depleted);
        }
    }
    public Transform TargetTransform => transform;

    private ICharacterState _currentState;
    
    public event System.Action OnHitReceived;
    public event System.Action<Vector3, Vector3> OnHit;
    public event System.Action OnShotBlocked;
    private PlayerInventory _inventory;
    
    [field: Header("Targeting")]
    [field: SerializeField] public float TargetWeight { get; set; } = 1.5f;

    private void Awake()
    {
        CurrentStamina = MaxStamina;
        _inventory = GetComponent<PlayerInventory>();
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        ChangeState(GetComponent<PlayerActiveState>());
    }

    private void Update()
    {
        _currentState?.UpdateState();
    }

    public void ChangeState(ICharacterState newState)
    {
        _currentState?.Exit();
        _currentState = newState;
        _currentState?.Enter();
    }

    public void TryApplyHit(float hitProbability)
    {
        if (Condition == TargetCondition.Dead) return;

        if (Random.value <= hitProbability)
        {
            ApplyHit();
        }
    }

    public void TryApplyHit(float hitProbability, Vector3 hitPoint, Vector3 hitNormal)
    {
        TryApplyHit(hitProbability, hitPoint, hitNormal, hitPoint + hitNormal);
    }

    public void TryApplyHit(float hitProbability, Vector3 hitPoint, Vector3 hitNormal, Vector3 shooterPosition)
    {
        if (Condition == TargetCondition.Dead || Random.value > hitProbability) return;
        if (_inventory != null && _inventory.TryBlockShot(shooterPosition - transform.position))
        {
            OnShotBlocked?.Invoke();
            return;
        }
        ApplyHit();
        OnHit?.Invoke(hitPoint, hitNormal);
    }

    private void ApplyHit()
    {
        OnHitReceived?.Invoke();
        
        switch (Condition)
        {
            case TargetCondition.Normal:
                Condition = TargetCondition.Wounded;
                CurrentStamina = 0f;
                Anim.SetBool("IsWounded", true); 
                break;
            case TargetCondition.Wounded:
                Condition = TargetCondition.HeavyWounded;
                ChangeState(GetComponent<PlayerHeavyWoundedState>());
                break;
            case TargetCondition.HeavyWounded:
                Condition = TargetCondition.Dead;
                ChangeState(GetComponent<PlayerDeadState>());
                break;
        }
    }

    public void Heal()
    {
        if (Condition == TargetCondition.Dead || Condition == TargetCondition.Normal) return;

        Condition = TargetCondition.Normal;
        CurrentStamina = MaxStamina;
        Anim.SetBool("IsWounded", false);
        
        if (_currentState is PlayerHeavyWoundedState)
        {
            ChangeState(GetComponent<PlayerActiveState>());
        }
    }

    public bool IsPlayer() => true;
}
