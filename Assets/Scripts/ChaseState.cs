using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(EnemyBrain))]
public class ChaseState : MonoBehaviour, ICharacterState
{
    [SerializeField] private float chaseRunSpeed = 5f;
    [SerializeField] private float exhaustedWalkSpeed = 2f;
    [SerializeField] private float targetMemoryTime = 3f; 
    
    [Header("Защита от застревания")]
    [SerializeField] private float maxInvestigationTime = 8f; // Максимальное время топтания на точке

    private EnemyBrain _brain;
    private float _timeSinceLastSeen;
    private float _investigationTimer;
    private Vector3 _lastKnownPosition;
    private bool _investigating;
    private bool _isRunning;

    private void Awake() => _brain = GetComponent<EnemyBrain>();

    public void Enter()
    {
        _brain.CurrentOmniRadius = 20f;
        
        _investigating = false;
        _timeSinceLastSeen = 0f;
        _investigationTimer = 0f;
        
        _isRunning = _brain.CurrentStamina > 0f;
        _brain.Agent.speed = _isRunning ? chaseRunSpeed : exhaustedWalkSpeed;
    }

    public void UpdateState()
    {
        _brain.Anim.SetFloat("Speed", _brain.Agent.velocity.magnitude);
        
        if (_isRunning)
        {
            _brain.DrainStamina();
            if (_brain.CurrentStamina <= 0f)
            {
                _isRunning = false;
                _brain.Agent.speed = exhaustedWalkSpeed;
            }
        }
        else
        {
            _brain.RegenStamina();
            if (_brain.CurrentStamina >= _brain.MaxStamina * 0.5f)
            {
                _isRunning = true;
                _brain.Agent.speed = chaseRunSpeed;
            }
        }

        _brain.CheckCloseThreats();

        if (_brain.CurrentTarget != null)
        {
            _investigating = false;
            
            if (_brain.HasLineOfSight(_brain.CurrentTarget))
            {
                _timeSinceLastSeen = 0f;
                _lastKnownPosition = _brain.CurrentTarget.TargetTransform.position;
                _brain.Agent.SetDestination(_lastKnownPosition);

                float distance = Vector3.Distance(transform.position, _brain.CurrentTarget.TargetTransform.position);
                if (distance <= _brain.AttackDistance)
                {
                    _brain.ChangeState(GetComponent<AttackState>());
                }
            }
            else
            {
                _timeSinceLastSeen += Time.deltaTime;
                
                // SetDestination убран отсюда, чтобы не вызывать его каждый кадр.
                // Маршрут к _lastKnownPosition уже был проложен в последний кадр видимости.
                
                if (_timeSinceLastSeen >= targetMemoryTime)
                {
                    _brain.ClearTarget();
                }
            }
        }
        else 
        {
            if (!_investigating)
            {
                _investigating = true;
                _investigationTimer = 0f;
                _brain.Agent.SetDestination(_lastKnownPosition);
            }

            _investigationTimer += Time.deltaTime;

            if (!_brain.Agent.pathPending)
            {
                // Проверяем: дошли до точки ИЛИ путь оборван/недостижим
                bool isAtDestination = _brain.Agent.remainingDistance <= _brain.Agent.stoppingDistance;
                bool isPathBroken = _brain.Agent.pathStatus == NavMeshPathStatus.PathPartial || _brain.Agent.pathStatus == NavMeshPathStatus.PathInvalid;
                bool isTimedOut = _investigationTimer >= maxInvestigationTime;

                if (isAtDestination || isPathBroken || isTimedOut)
                {
                    _brain.GeneralPatrolArea = _lastKnownPosition;
                    _brain.ChangeState(GetComponent<PatrolState>());
                }
            }
        }
    }

    public void Exit()
    {
        _brain.CurrentOmniRadius = _brain.BaseOmniRadius;
    }
}
