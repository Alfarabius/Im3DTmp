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
    
    public float CurrentStamina { get; set; }
    public Transform TargetTransform => transform;

    private ICharacterState _currentState;
    
    [field: Header("Targeting")]
    [field: SerializeField] public float TargetWeight { get; set; } = 1.5f;

    private void Awake()
    {
        CurrentStamina = MaxStamina;
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

    private void ApplyHit()
    {
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
}
