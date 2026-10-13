using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum TargetCondition
{
    Normal,
    Wounded,
    HeavyWounded,
    Dead
}

[RequireComponent(typeof(NavMeshAgent), typeof(Animator))]
public class NPCBrain : MonoBehaviour, ITargetable
{
    [Header("Vision Settings")]
    [SerializeField] private float visionRange = 25f;
    [SerializeField] private float visionAngle = 120f;
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private LayerMask obstacleMask;
    
    [Header("Hearing Settings")]
    [SerializeField] private float hearingThreshold = 30f;
    
    [Header("Core Settings")]
    [field: SerializeField] public NavMeshAgent Agent { get; private set; }
    [SerializeField] private float stressProbability = 1f;
    
    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip painScreamSound;
    
    [Header("Debug")]
    [SerializeField] private string currentStateName;
    [SerializeField] private float currentStaminaDebug;

    public Transform TargetTransform => transform;
    [field: SerializeField] public TargetCondition Condition { get; private set; } = TargetCondition.Normal;
    
    public float MaxStamina { get; private set; }
    public float CurrentStamina { get; private set; }
    public Animator Anim { get; private set; }

    private ICharacterState _currentState;
    
    [field: Header("Targeting")]
    [field: SerializeField] public float TargetWeight { get; set; } = 1f;
    
    private void Awake()
    {
        // GlobalAudioEvents.OnGunshotFired += HandleGunshot;
        audioSource = GetComponent<AudioSource>();
        Agent = GetComponent<NavMeshAgent>();
        Anim = GetComponent<Animator>();
        InitializeStamina();
    }
    
    private void Start()
    {
        ChangeState(GetComponent<NPCIdleState>());
    }

    private void InitializeStamina()
    {
        float s1 = Random.Range(60f, 100f);
        float s2 = Random.Range(60f, 100f);
        float s3 = Random.Range(60f, 100f);
        MaxStamina = (s1 + s2 + s3) / 3f;
        CurrentStamina = MaxStamina;
        currentStaminaDebug = CurrentStamina;
    }

    private void OnEnable() => GlobalAudioEvents.OnGunshotFired += HandleGunshot;
    
    private void OnDisable() => GlobalAudioEvents.OnGunshotFired -= HandleGunshot;

    private void Update()
    {
        currentStaminaDebug = CurrentStamina;
        if (Condition == TargetCondition.HeavyWounded || Condition == TargetCondition.Dead) return;
        
        _currentState?.UpdateState();
    }

    public void ChangeState(ICharacterState newState)
    {
        if (newState == _currentState) return;
        
        _currentState?.Exit();
        _currentState = newState;
        
        if (_currentState != null) currentStateName = _currentState.GetType().Name;
        
        _currentState?.Enter();
    }

    public void ConsumeStamina(float amount)
    {
        if (CurrentStamina > 0f)
        {
            CurrentStamina -= amount;
            if (CurrentStamina < 0f) CurrentStamina = 0f;
        }
    }
    
    public void RegenerateStamina(float amount)
    {
        if (CurrentStamina < MaxStamina) 
        {
            CurrentStamina += amount;
            if (CurrentStamina > MaxStamina) CurrentStamina = MaxStamina;
        }
    }

    private void HandleGunshot(Vector3 shooterPosition, Vector3 shootDirection)
    {
        if (Condition == TargetCondition.HeavyWounded || Condition == TargetCondition.Dead) return;

        float distance = Vector3.Distance(transform.position, shooterPosition);
        if (distance <= hearingThreshold)
        {
            var runawayState = GetComponent<NPCRunawayState>();
            runawayState.ThreatPosition = shooterPosition;
            ChangeState(runawayState);
        }
    }
    
    public bool TrySeeAnyEnemy(out Vector3 detectedEnemyPosition)
    {
        detectedEnemyPosition = Vector3.zero;
        Collider[] enemiesInRange = Physics.OverlapSphere(transform.position, visionRange, enemyMask);

        foreach (var enemyCol in enemiesInRange)
        {
            Vector3 dirToEnemy = (enemyCol.transform.position - transform.position).normalized;
            float distanceToEnemy = Vector3.Distance(transform.position, enemyCol.transform.position);

            if (Vector3.Angle(transform.forward, dirToEnemy) < visionAngle / 2f)
            {
                Vector3 origin = transform.position + Vector3.up; 
                if (!Physics.Raycast(origin, dirToEnemy, distanceToEnemy, obstacleMask))
                {
                    detectedEnemyPosition = enemyCol.transform.position;
                    return true;
                }
            }
        }
        return false;
    }

    public void TryApplyHit(float hitProbability)
    {
        if (Condition == TargetCondition.Dead) return;

        if (Random.value <= hitProbability) ApplyHit();
        else HandleNearMiss();
    }

    private void ApplyHit()
    {
        switch (Condition)
        {
            case TargetCondition.Normal:
                Condition = TargetCondition.Wounded;
                MaxStamina = 0f;
                CurrentStamina = 0f;
                Anim.SetBool("IsWounded", true); 
                break;
            case TargetCondition.Wounded:
                Condition = TargetCondition.HeavyWounded;
                
                // ИСПРАВЛЕНИЕ: Принудительно гасим текущий стейт
                _currentState?.Exit();
                _currentState = null;
                currentStateName = "HeavyWounded State";
                
                Agent.isStopped = true;
                Agent.velocity = Vector3.zero; // Жестко убиваем инерцию для чистого перехода
                Anim.SetTrigger("HeavyWounded");
                
                audioSource.clip = painScreamSound;
                audioSource.loop = true;
                audioSource.Play();
                
                break;
            case TargetCondition.HeavyWounded:
                Condition = TargetCondition.Dead;
                currentStateName = "Dead State";
                Anim.SetTrigger("Dead");
                
                gameObject.GetComponent<CapsuleCollider>().enabled = false;
                
                audioSource.Stop();
                
                break;
        }
    }

    private void HandleNearMiss()
    {
        if (Condition == TargetCondition.HeavyWounded || Condition == TargetCondition.Dead) return;
        
        if (Random.value <= stressProbability)
        {
            ChangeState(GetComponent<NPCStressedState>());
        }
    }
    
    public bool IsPlayer() => false;

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Handles.color = new Color(1f, 1f, 1f, 0.1f);
        Handles.DrawSolidArc(transform.position, Vector3.up, Vector3.forward, 360f, visionRange);

        Vector3 viewAngleLeft = Quaternion.Euler(0, -visionAngle / 2f, 0) * transform.forward;
        Vector3 viewAngleRight = Quaternion.Euler(0, visionAngle / 2f, 0) * transform.forward;

        Handles.color = new Color(1f, 0f, 0f, 0.2f);
        Handles.DrawSolidArc(transform.position, Vector3.up, viewAngleLeft, visionAngle, visionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, viewAngleLeft * visionRange);
        Gizmos.DrawRay(transform.position, viewAngleRight * visionRange);
    }
#endif
}
