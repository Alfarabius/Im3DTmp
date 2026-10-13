using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NPCBrain))]
public class NPCRunawayState : MonoBehaviour, ICharacterState
{
    // Глобальный реестр укрытий, к которым ПРЯМО СЕЙЧАС бегут другие NPC. 
    // Static делает его общим для всех экземпляров этого стейта.
    private static readonly HashSet<Transform> _globalRunningCovers = new HashSet<Transform>();

    private NPCBrain _brain;
    private float _safeTimer;
    [SerializeField] private float timeToFeelSafe = 5f; 
    [SerializeField] private float runDistance = 15f;
    [SerializeField] private NPCVoiceController voiceController;

    public Vector3 ThreatPosition { get; set; }
    
    [SerializeField] private float runSpeed = 5f;
    [SerializeField] private float walkSpeed = 2f;
    
    private bool _isSprinting;

    [Header("Система укрытий")]
    [SerializeField] private float checkOccupationRadius = 2f;
    [SerializeField] private LayerMask npcLayerMask;
    private bool _isSeekingCover;
    private Transform _currentCover;
    private List<Transform> _blacklistedCovers = new List<Transform>();

    private void Awake() => _brain = GetComponent<NPCBrain>();

    public void Enter()
    {
        _isSprinting = true;
        _isSeekingCover = false; 
        _blacklistedCovers.Clear();
        _currentCover = null;

        _brain.Agent.isStopped = false;
        _brain.Agent.speed = runSpeed;
        
        _safeTimer = timeToFeelSafe;
        voiceController.TryPanicScream();
        CalculateEscapeRoute();
    }

    public void UpdateState()
    {
        // 1. ЗРИТЕЛЬНЫЙ КОНТАКТ
        if (_brain.TrySeeAnyEnemy(out Vector3 enemyPos))
        {
            ThreatPosition = enemyPos;
            _safeTimer = timeToFeelSafe; 

            // Если пугают во время бега к укрытию - бросаем его и убегаем дальше
            if (_isSeekingCover)
            {
                _isSeekingCover = false;
                ReleaseCurrentCover();
                CalculateEscapeRoute();
            }
        }

        // 2. СТАМИНА
        if (_isSprinting)
        {
            _brain.ConsumeStamina(15f * Time.deltaTime);

            if (_brain.CurrentStamina <= 0f)
            {
                _isSprinting = false;
                _brain.Agent.speed = walkSpeed;
            }
        }
        else
        {
            _brain.RegenerateStamina(8f * Time.deltaTime);

            if (_brain.CurrentStamina >= 50f)
            {
                _isSprinting = true;
                _brain.Agent.speed = runSpeed;
            }
        }

        _brain.Anim.SetFloat("Speed", _brain.Agent.velocity.magnitude);

        // 3. ЛОГИКА БЕГСТВА И УКРЫТИЯ
        if (_safeTimer > 0f)
        {
            _safeTimer -= Time.deltaTime;

            if (!_brain.Agent.pathPending && (_brain.Agent.remainingDistance <= _brain.Agent.stoppingDistance || _brain.Agent.pathStatus == NavMeshPathStatus.PathPartial))
            {
                CalculateEscapeRoute();
            }
        }
        else 
        {
            if (!_isSeekingCover)
            {
                _isSeekingCover = true;
                FindAndSetCover();
            }
            else
            {
                // Защита от непредвиденных ситуаций (например, игрок затолкал другого NPC на точку)
                if (_currentCover != null && _brain.Agent.remainingDistance <= 3f)
                {
                    CheckCoverAvailability();
                }

                if (!_brain.Agent.pathPending && (_brain.Agent.remainingDistance <= _brain.Agent.stoppingDistance || _brain.Agent.pathStatus == NavMeshPathStatus.PathPartial))
                {
                    float randomAngle = Random.Range(100f, 180f);
                    float directionSign = Random.value > 0.5f ? 1f : -1f;
                    _brain.transform.Rotate(0, randomAngle * directionSign, 0);

                    _brain.ChangeState(GetComponent<NPCAlertedState>());
                }
            }
        }
    }

    public void Exit()
    {
        _brain.Agent.isStopped = true;
        _brain.Anim.SetFloat("Speed", 0f);
        
        // Снимаем логическую бронь. Теперь NPC стоит на точке физически, 
        // и новые бегущие боты увидят его через OverlapSphere в FindAndSetCover.
        ReleaseCurrentCover();
    }

    private void ReleaseCurrentCover()
    {
        if (_currentCover != null)
        {
            _globalRunningCovers.Remove(_currentCover);
            _currentCover = null;
        }
    }

    private void CalculateEscapeRoute()
    {
        Vector3 runDirection = (transform.position - ThreatPosition).normalized;
        bool pathFound = false;
        NavMeshPath path = new NavMeshPath();

        float[] angles = { 0f, 45f, -45f, 90f, -90f };

        foreach (float angle in angles)
        {
            Vector3 rotatedDirection = Quaternion.Euler(0, angle, 0) * runDirection;
            Vector3 targetPos = transform.position + rotatedDirection * runDistance;

            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                _brain.Agent.CalculatePath(hit.position, path);
                
                if (path.status == NavMeshPathStatus.PathComplete)
                {
                    _brain.Agent.SetPath(path);
                    pathFound = true;
                    break;
                }
            }
        }

        if (!pathFound)
        {
            for (int i = 0; i < 5; i++) 
            {
                Vector3 randomDir = Random.insideUnitSphere * (runDistance * 0.5f);
                randomDir += transform.position;
                
                if (NavMesh.SamplePosition(randomDir, out NavMeshHit randomHit, 2f, NavMesh.AllAreas))
                {
                    _brain.Agent.CalculatePath(randomHit.position, path);
                    if (path.status == NavMeshPathStatus.PathComplete)
                    {
                        _brain.Agent.SetPath(path);
                        break;
                    }
                }
            }
        }
    }

    private void FindAndSetCover()
    {
        while (true)
        {
            // Сливаем в один список точки, которые не подходят конкретно нам, и те, к которым уже бегут другие
            List<Transform> exclusions = new List<Transform>(_blacklistedCovers);
            exclusions.AddRange(_globalRunningCovers);

            Transform potentialCover = CoverManager.Instance.GetBestCover(transform.position, ThreatPosition, exclusions);

            // Если точек вообще не осталось - сдаемся и встаем в боевую стойку
            if (potentialCover == null)
            {
                _brain.ChangeState(GetComponent<NPCAlertedState>());
                return;
            }

            // Проверяем, не стоит ли на найденной точке кто-то прямо сейчас (в другом стейте)
            bool isOccupiedPhysically = false;
            Collider[] colliders = Physics.OverlapSphere(potentialCover.position, checkOccupationRadius, npcLayerMask);
            
            foreach (var col in colliders)
            {
                if (col.gameObject != this.gameObject)
                {
                    isOccupiedPhysically = true;
                    break;
                }
            }

            if (isOccupiedPhysically)
            {
                // Если там уже кто-то стоит, кидаем в локальный блеклист и ищем заново
                _blacklistedCovers.Add(potentialCover);
                continue; 
            }

            // Точка свободна физически и никто к ней не бежит. Бронируем её!
            _currentCover = potentialCover;
            _globalRunningCovers.Add(_currentCover);
            _brain.Agent.SetDestination(_currentCover.position);
            break;
        }
    }

    private void CheckCoverAvailability()
    {
        Collider[] hitColliders = Physics.OverlapSphere(_currentCover.position, checkOccupationRadius, npcLayerMask);
        foreach (var col in hitColliders)
        {
            if (col.gameObject != this.gameObject)
            {
                _blacklistedCovers.Add(_currentCover);
                ReleaseCurrentCover();
                FindAndSetCover(); 
                return;
            }
        }
    }
}
