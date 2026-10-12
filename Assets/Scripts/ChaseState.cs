using UnityEngine;

[RequireComponent(typeof(EnemyBrain))]
public class ChaseState : MonoBehaviour, ICharacterState
{
    [SerializeField] private float chaseRunSpeed = 5f;
    [SerializeField] private float exhaustedWalkSpeed = 2f;
    [SerializeField] private float targetMemoryTime = 3f; 

    private EnemyBrain _brain;
    private float _timeSinceLastSeen;
    private Vector3 _lastKnownPosition;
    private bool _investigating;
    private bool _isRunning;

    private void Awake() => _brain = GetComponent<EnemyBrain>();

    public void Enter()
    {
        // Раздуваем чутье в бою (например, до 20f, чтобы слышать маневры цели)
        _brain.CurrentOmniRadius = 20f;
        
        _investigating = false;
        _timeSinceLastSeen = 0f;
        
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

        // Проверка на появление более приоритетной/близкой цели в зоне инста-агра
        _brain.CheckCloseThreats();

        if (_brain.CurrentTarget != null)
        {
            _investigating = false;
            
            // Если цель видна, обновляем ее позицию и сбрасываем таймер памяти
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
                // Цель скрылась из виду, увеличиваем таймер памяти и идем к последней точке
                _timeSinceLastSeen += Time.deltaTime;
                _brain.Agent.SetDestination(_lastKnownPosition);
                
                _brain.ScanForTargets();
                
                if (_timeSinceLastSeen >= targetMemoryTime)
                {
                    _brain.ClearTarget();
                }
            }
        }
        else 
        {
            // Поиск в последней известной позиции после истечения таймера
            if (!_investigating)
            {
                _investigating = true;
                _brain.Agent.SetDestination(_lastKnownPosition);
            }

            if (!_brain.Agent.pathPending && _brain.Agent.remainingDistance <= _brain.Agent.stoppingDistance)
            {
                _brain.GeneralPatrolArea = _lastKnownPosition;
                _brain.ChangeState(GetComponent<PatrolState>());
            }
        }
    }

    public void Exit()
    {
        // Возвращаем базовый слух при потере цели
        _brain.CurrentOmniRadius = _brain.BaseOmniRadius;
    }
}
