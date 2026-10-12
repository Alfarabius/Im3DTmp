using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(NavMeshAgent), typeof(Animator))]
public class EnemyBrain : MonoBehaviour
{
    [field: Header("Audio")]
    [field: SerializeField] public AudioSource Audio { get; private set; }
    [field: SerializeField] public AudioClip ShootSound { get; private set; }
    
    [Header("Stamina")]
    [field: SerializeField] public float MaxStamina { get; private set; } = 100f;
    [SerializeField] private float staminaDrainRate = 15f;
    [SerializeField] private float staminaRegenRate = 10f;

    public float CurrentStamina { get; private set; }
    
    [Header("Sensors")]
    [SerializeField] private float visionRadius = 20f;
    [SerializeField] private float visionAngle = 90f; 
    [SerializeField] private float omnidirectionalRadius = 8f; 
    public float CurrentOmniRadius { get; set; }
    public float BaseOmniRadius { get; private set; }
    
    [SerializeField] private float instantDetectionRadius = 3f; 
    [SerializeField] private LayerMask targetLayers; 
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private Vector3 eyeOffset = new Vector3(0, 1.5f, 0); 
    
    public LayerMask TargetLayers => targetLayers;
    public LayerMask ObstacleLayer => obstacleLayer;
    public Vector3 EyeOffset => eyeOffset;
    
    [field: SerializeField] public float AttackDistance { get; private set; } = 10f;
    
    [Header("Debug")]
    [SerializeField] private string currentStateName; 
    
    public NavMeshAgent Agent { get; private set; }
    public Animator Anim { get; private set; }
    public ITargetable CurrentTarget { get; private set; }
    
    public Vector3 GeneralPatrolArea { get; set; } 

    private ICharacterState _currentState;

    private void Awake()
    {
        BaseOmniRadius = omnidirectionalRadius;
        CurrentOmniRadius = omnidirectionalRadius;
        
        CurrentStamina = MaxStamina;
        Agent = GetComponent<NavMeshAgent>();
        Anim = GetComponent<Animator>();
        GeneralPatrolArea = transform.position; 
        
        // Убрали ChangeState отсюда
    }

    private System.Collections.IEnumerator Start()
    {
        // Ждем ровно один кадр, чтобы движок в WebGL успел построить NavMesh
        yield return null;

        // Страховка: если из-за погрешности float враг висит над сеткой, принудительно сажаем его
        if (Agent != null && !Agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                Agent.Warp(hit.position);
            }
        }

        // Только теперь, когда агент гарантированно на сетке, запускаем логику
        ChangeState(GetComponent<PatrolState>());
    }

    private void Update() => _currentState?.UpdateState();

    public void ChangeState(ICharacterState newState)
    {
        _currentState?.Exit();
        _currentState = newState;
        
        if (_currentState != null) 
            currentStateName = _currentState.GetType().Name;
            
        _currentState?.Enter();
    }

    public bool HasLineOfSight(ITargetable target)
    {
        if (target == null) return false;
        
        Vector3 origin = transform.position + eyeOffset;
        Vector3 targetPos = target.TargetTransform.position + Vector3.up * 1f; 
        Vector3 dirToTarget = (targetPos - origin).normalized;
        float dist = Vector3.Distance(origin, targetPos);
        
        // Текущая цель использует динамический радиус, остальные — всегда базовый
        float activeOmniRadius = (target == CurrentTarget) ? CurrentOmniRadius : BaseOmniRadius;
        
        if (dist > activeOmniRadius * target.TargetWeight)
        {
            if (Vector3.Angle(transform.forward, dirToTarget) > visionAngle / 2f) 
                return false;
        }

        return !Physics.Raycast(origin, dirToTarget, dist, obstacleLayer);
    }

    public void ScanForTargets()
    {
        CurrentTarget = null;
        List<ITargetable> visibleTargets = GetVisibleTargets();
        
        foreach (var target in visibleTargets)
        {
            Vector3 origin = transform.position + eyeOffset;
            Vector3 targetPos = target.TargetTransform.position + Vector3.up * 1f; 
            
            if (Vector3.Distance(origin, targetPos) <= instantDetectionRadius * target.TargetWeight)
            {
                CurrentTarget = target;
                return; 
            }
        }
        
        if (visibleTargets.Count > 0)
        {
            CurrentTarget = SelectTargetByWeight(visibleTargets);
        }
    }

    // Быстрое сканирование только ближней зоны без обнуления цели
    public void CheckCloseThreats()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, instantDetectionRadius * 2f, targetLayers);
        foreach (var hit in hits)
        {
            var target = hit.GetComponent<ITargetable>();
            if (target != null && target != CurrentTarget && target.Condition != TargetCondition.Dead)
            {
                Vector3 origin = transform.position + eyeOffset;
                Vector3 targetPos = target.TargetTransform.position + Vector3.up * 1f;
                
                if (Vector3.Distance(origin, targetPos) <= instantDetectionRadius * target.TargetWeight)
                {
                    if (HasLineOfSight(target)) 
                    {
                        CurrentTarget = target;
                        return;
                    }
                }
            }
        }
    }

    public void ClearTarget()
    {
        CurrentTarget = null;
    }

    private List<ITargetable> GetVisibleTargets()
    {
        List<ITargetable> validTargets = new List<ITargetable>();
        Collider[] hits = Physics.OverlapSphere(transform.position, visionRadius, targetLayers);

        foreach (var hit in hits)
        {
            var target = hit.GetComponent<ITargetable>();
            
            if (target != null && target.Condition != TargetCondition.Dead)
            {
                float dist = Vector3.Distance(transform.position, target.TargetTransform.position);
                
                if (dist <= visionRadius * target.TargetWeight && HasLineOfSight(target))
                {
                    validTargets.Add(target);
                }
            }
        }

        return validTargets;
    }

    private ITargetable SelectTargetByWeight(List<ITargetable> targets)
    {
        List<float> weights = new List<float>();
        float totalWeight = 0f;

        foreach (var target in targets)
        {
            float distance = Vector3.Distance(transform.position, target.TargetTransform.position);
            float weight = (1f / Mathf.Max(distance, 0.1f)) * target.TargetWeight;

            weights.Add(weight);
            totalWeight += weight;
        }

        float randomVal = Random.Range(0f, totalWeight);
        float currentSum = 0f;

        for (int i = 0; i < targets.Count; i++)
        {
            currentSum += weights[i];
            if (randomVal <= currentSum)
            {
                return targets[i];
            }
        }
        
        return targets[targets.Count - 1]; 
    }
    
    public void DrainStamina()
    {
        CurrentStamina -= staminaDrainRate * Time.deltaTime;
        CurrentStamina = Mathf.Clamp(CurrentStamina, 0f, MaxStamina);
    }

    public void RegenStamina()
    {
        CurrentStamina += staminaRegenRate * Time.deltaTime;
        CurrentStamina = Mathf.Clamp(CurrentStamina, 0f, MaxStamina);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector3 origin = transform.position + eyeOffset;
        float halfAngle = visionAngle / 2f;

        Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
        Gizmos.DrawWireSphere(origin, instantDetectionRadius);

        Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.5f);
        Gizmos.DrawWireSphere(origin, omnidirectionalRadius);

        Gizmos.color = new Color(1f, 1f, 1f, 0.1f);
        Gizmos.DrawWireSphere(origin, visionRadius);

        float coneLength = visionRadius * Mathf.Cos(halfAngle * Mathf.Deg2Rad);
        float coneRadius = visionRadius * Mathf.Sin(halfAngle * Mathf.Deg2Rad);
        Vector3 coneCenter = origin + transform.forward * coneLength;

        Handles.color = new Color(1f, 0f, 0f, 0.5f);
        Handles.DrawWireDisc(coneCenter, transform.forward, coneRadius);

        Vector3 upEdge = coneCenter + transform.up * coneRadius;
        Vector3 downEdge = coneCenter - transform.up * coneRadius;
        Vector3 leftEdge = coneCenter - transform.right * coneRadius;
        Vector3 rightEdge = coneCenter + transform.right * coneRadius;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(origin, upEdge);
        Gizmos.DrawLine(origin, downEdge);
        Gizmos.DrawLine(origin, leftEdge);
        Gizmos.DrawLine(origin, rightEdge);
    }
#endif
}
