using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(EnemyBrain), typeof(PatrolState))]
public class HuntState : MonoBehaviour, ICharacterState
{
    [Header("Охота на игрока")]
    [SerializeField] private Transform playerTransform;
    [SerializeField, Min(0.1f)] private float runSpeed = 5f;
    [Tooltip("Перевыбирать ближайшую к игроку точку во время бега. Выключено: точка фиксируется при входе.")]
    [SerializeField] private bool updatePatrolPointWhileRunning;
    [SerializeField, Min(0.1f)] private float pointUpdateInterval = 0.3f;
    [SerializeField, Min(0.1f)] private float arrivalTolerance = 0.5f;

    private EnemyBrain _brain;
    private PatrolState _patrol;
    private Transform _currentPoint;
    private float _pointUpdateTimer;

    private void Awake()
    {
        _brain = GetComponent<EnemyBrain>();
        _patrol = GetComponent<PatrolState>();
    }

    public void Enter()
    {
        _brain.ClearTarget();
        _currentPoint = null;
        _pointUpdateTimer = 0f;
        _brain.Agent.speed = runSpeed;
        _brain.Agent.isStopped = false;
        _brain.Agent.ResetPath();

        if (!SelectClosestPoint()) ReturnToPatrol();
    }

    public void UpdateState()
    {
        _brain.Anim.SetFloat("Speed", _brain.Agent.velocity.magnitude);
        // В этом состоянии стамина не расходуется и не восстанавливается.
        // Все NPC отфильтровываются до выбора цели.
        _brain.ScanForTargets(playerOnly: true);
        if (_brain.CurrentTarget != null)
        {
            float distance = Vector3.Distance(transform.position, _brain.CurrentTarget.TargetTransform.position);
            if (distance <= _brain.AttackDistance) _brain.ChangeState(GetComponent<AttackState>());
            else _brain.ChangeState(GetComponent<ChaseState>());
            return;
        }

        _pointUpdateTimer += Time.deltaTime;
        if (_currentPoint == null || (updatePatrolPointWhileRunning && _pointUpdateTimer >= pointUpdateInterval))
        {
            _pointUpdateTimer = 0f;
            if (!SelectClosestPoint())
            {
                ReturnToPatrol();
                return;
            }
        }

        if (_brain.Agent.pathPending) return;

        float arrivalDistance = Mathf.Max(_brain.Agent.stoppingDistance, arrivalTolerance);
        bool isAtDestination = _brain.Agent.remainingDistance <= arrivalDistance &&
            Vector3.Distance(transform.position, _brain.Agent.destination) <= arrivalDistance;
        bool isPathBroken = _brain.Agent.pathStatus != NavMeshPathStatus.PathComplete;
        if (isAtDestination || isPathBroken) ReturnToPatrol();
    }

    public void Exit()
    {
        _brain.Agent.isStopped = false;
        _brain.Agent.ResetPath();
    }

    private bool SelectClosestPoint()
    {
        Transform routeRoot = _patrol.RouteRoot;
        if (playerTransform == null || routeRoot == null) return false;

        Transform closestPoint = null;
        float closestDistance = float.MaxValue;
        for (int i = 0; i < routeRoot.childCount; i++)
        {
            Transform point = routeRoot.GetChild(i);
            float distance = (point.position - playerTransform.position).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPoint = point;
            }
        }

        if (closestPoint == null) return false;
        if (closestPoint == _currentPoint) return true;
        _currentPoint = closestPoint;
        return _brain.Agent.SetDestination(closestPoint.position);
    }

    private void ReturnToPatrol() => _brain.ChangeState(_patrol);
}
