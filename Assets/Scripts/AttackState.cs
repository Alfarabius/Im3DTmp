using UnityEngine;

[RequireComponent(typeof(EnemyBrain))]
public class AttackState : MonoBehaviour, ICharacterState
{
    [SerializeField] private float combatWalkSpeed = 1.5f;
    [SerializeField, Range(0.1f, 0.9f)] private float standOffMultiplier = 0.8f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float fireAngle = 15f; 
    
    [Header("Combat Stats")]
    [SerializeField] private float timeBetweenShots = 2f;
    [SerializeField, Range(0f, 1f)] private float hitProbability = 0.6f;

    private EnemyBrain _brain;
    private float _shootTimer;

    private void Awake() => _brain = GetComponent<EnemyBrain>();

    public void Enter()
    {
        _brain.Anim.SetBool("IsAiming", true);
        _brain.Agent.speed = combatWalkSpeed; 
        _shootTimer = timeBetweenShots;
    }

    public void UpdateState()
    {
        _brain.RegenStamina();
        
        // Проверка на появление более приоритетной/близкой цели в зоне инста-агра
        _brain.CheckCloseThreats();

        if (_brain.CurrentTarget == null || !_brain.HasLineOfSight(_brain.CurrentTarget))
        {
            _brain.ChangeState(GetComponent<ChaseState>());
            return;
        }

        float distance = Vector3.Distance(transform.position, _brain.CurrentTarget.TargetTransform.position);

        if (distance > _brain.AttackDistance)
        {
            _brain.ChangeState(GetComponent<ChaseState>());
            return;
        }

        float stopDist = _brain.AttackDistance * standOffMultiplier;

        if (distance > stopDist)
        {
            _brain.Agent.isStopped = false;
            _brain.Agent.SetDestination(_brain.CurrentTarget.TargetTransform.position);
        }
        else
        {
            _brain.Agent.isStopped = true;
            _brain.Agent.velocity = Vector3.zero; 
        }

        _brain.Anim.SetFloat("Speed", _brain.Agent.velocity.magnitude);

        Vector3 direction = (_brain.CurrentTarget.TargetTransform.position - transform.position).normalized;
        direction.y = 0;
        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        // Проверка угла между направлением взгляда врага и направлением на цель
        float angleToTarget = Vector3.Angle(transform.forward, direction);
        _shootTimer -= Time.deltaTime;
        
        if (_shootTimer <= 0f && angleToTarget <= fireAngle)
        {
            _shootTimer = timeBetweenShots;
            _brain.Anim.SetTrigger("Attack");
            
            if (_brain.Audio != null && _brain.ShootSound != null)
            {
                _brain.Audio.PlayOneShot(_brain.ShootSound);
            }
            GlobalAudioEvents.OnGunshotFired?.Invoke(transform.position);

            // Формирование физического луча выстрела
            Vector3 origin = transform.position + _brain.EyeOffset;
            Vector3 targetPos = _brain.CurrentTarget.TargetTransform.position + Vector3.up * 1f;
            Vector3 shootDir = (targetPos - origin).normalized;
            float maxDist = Vector3.Distance(origin, targetPos) + 2f; 
            LayerMask hitMask = _brain.TargetLayers | _brain.ObstacleLayer;
            
            // Нанесение урона первому объекту на траектории пули
            if (Physics.Raycast(origin, shootDir, out RaycastHit hit, maxDist, hitMask))
            {
                var hitTarget = hit.collider.GetComponent<ITargetable>();
                if (hitTarget != null)
                {
                    hitTarget.TryApplyHit(hitProbability);
                    
                    if (hitTarget.Condition == TargetCondition.Dead || hitTarget.Condition == TargetCondition.HeavyWounded)
                    {
                        if (hitTarget == _brain.CurrentTarget)
                        {
                            _brain.ClearTarget();
                            _brain.ChangeState(GetComponent<PatrolState>());
                        }
                    }
                }
            }
        }
    }

    public void Exit()
    {
        _brain.Anim.SetBool("IsAiming", false);
        _brain.Agent.isStopped = false;
    }
}
