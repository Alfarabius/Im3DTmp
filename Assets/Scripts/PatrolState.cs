using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(EnemyBrain))]
public class PatrolState : MonoBehaviour, ICharacterState
{
    [Header("Настройки патрулирования")]
    [Tooltip("Скорость перемещения при патрулировании")] [SerializeField] private float patrolSpeed = 2f;
    [Tooltip("Скорость поворота при осмотре (градусы в сек.)")] [SerializeField] private float rotationSpeed = 120f;
    [Tooltip("Интервал между проверками видимости целей")] [SerializeField] private float scanInterval = 0.3f;
    
    
    [Header("Переход к охоте")]
    [Tooltip("Время в патруле до перехода в HuntState, включая осмотр местности")]
    [SerializeField, Min(0.1f)] private float timeBeforeHunting = 10f;
    
    [SerializeField] private Transform routeRoot;
    public Transform RouteRoot => routeRoot;
    private Transform[] _patrolPoints;
    
    private EnemyBrain _brain;
    private float _scanTimer;
    private float _timeInState;
    private Transform _currentPoint;
    private readonly List<Transform> _unvisitedPoints = new List<Transform>();

    // Переменные для контроля осмотра на 360
    private bool _isLookingAround;
    private float _rotatedDegrees;

    private void Awake()
    {
        _brain = GetComponent<EnemyBrain>();
        
        if (routeRoot != null)
        {
            int childCount = routeRoot.transform.childCount;
            _patrolPoints = new Transform[childCount];
            
            for (int i = 0; i < childCount; i++)
            {
                _patrolPoints[i] = routeRoot.transform.GetChild(i);
            }
        }
    }

    private void Start()
    {
        if (_patrolPoints != null && _patrolPoints.Length > 0)
        {
            StartLookingAround();
        }
    }

    public void Enter()
    {
        _brain.Agent.speed = patrolSpeed;
        _timeInState = 0f;

        // Сбрасываем старый маршрут, чтобы не идти к убитой цели (трупу)
        if (_brain.Agent.isActiveAndEnabled)
        {
            _brain.Agent.ResetPath();
        }
        
        // Если мы возвращаемся из боя (осмотр из Start уже прошел), 
        // сразу выбираем новую точку, чтобы уйти с места убийства
        if (!_isLookingAround && _patrolPoints != null && _patrolPoints.Length > 0)
        {
            SelectNextWeightedPoint();
        }
    }

    public void UpdateState()
    {
        _timeInState += Time.deltaTime;
        if (_timeInState >= timeBeforeHunting && _brain.BeginHunt()) return;

        // Обновляем анимацию (когда агент стоит во время осмотра, velocity.magnitude равно 0)
        _brain.Anim.SetFloat("Speed", _brain.Agent.velocity.magnitude);
        _brain.RegenStamina();

        if (_patrolPoints != null && _patrolPoints.Length > 0)
        {
            // Если враг крутится - блокируем остальную логику движения
            if (_isLookingAround)
            {
                PerformLookAround();
                return; 
            }

            // Проверка достижения точки
            if (!_brain.Agent.pathPending && _brain.Agent.remainingDistance <= _brain.Agent.stoppingDistance)
            {
                // Дошли до точки — запускаем осмотр местности
                StartLookingAround();
            }
        }

        // Базовое сканирование во время движения (если мы не крутимся)
        if (!_isLookingAround)
        {
            _scanTimer += Time.deltaTime;
            if (_scanTimer >= scanInterval)
            {
                _scanTimer = 0f;
                _brain.ScanForTargets();

                if (_brain.CurrentTarget != null)
                {
                    _brain.ChangeState(GetComponent<ChaseState>());
                }
            }
        }
    }

    public void Exit()
    {
        _isLookingAround = false;
        if (_brain.Agent.isActiveAndEnabled)
        {
            // Снимаем блокировку движения на случай, если вышли из стейта во время поворота
            _brain.Agent.isStopped = false; 
            _brain.Agent.ResetPath();
        }
    }

    private void StartLookingAround()
    {
        _isLookingAround = true;
        _rotatedDegrees = 0f;
        
        if (_brain.Agent.isActiveAndEnabled)
        {
            // Останавливаем движение по NavMesh
            _brain.Agent.isStopped = true; 
        }
    }

    private void PerformLookAround()
    {
        // Вращаем объект
        float step = rotationSpeed * Time.deltaTime;
        transform.Rotate(0, step, 0);
        _rotatedDegrees += step;

        // Принудительно сканируем окружение по таймеру во время вращения
        _scanTimer += Time.deltaTime;
        if (_scanTimer >= scanInterval)
        {
            _scanTimer = 0f;
            _brain.ScanForTargets();

            // Если во время осмотра найдена цель - прерываем всё и атакуем
            if (_brain.CurrentTarget != null)
            {
                _isLookingAround = false;
                if (_brain.Agent.isActiveAndEnabled) _brain.Agent.isStopped = false;
                
                _brain.ChangeState(GetComponent<ChaseState>());
                return;
            }
        }

        // Если развернулись на 360 - заканчиваем осмотр и идем к следующей точке
        if (_rotatedDegrees >= 360f)
        {
            _isLookingAround = false;
            
            if (_brain.Agent.isActiveAndEnabled)
            {
                _brain.Agent.isStopped = false;
            }
            
            SelectNextWeightedPoint();
        }
    }

    private void SelectNextWeightedPoint()
    {
        if (_unvisitedPoints.Count == 0)
        {
            ResetUnvisitedPoints();
        }

        Transform bestPoint = null;
        float highestScore = -1f;

        for (int i = 0; i < _unvisitedPoints.Count; i++)
        {
            Transform point = _unvisitedPoints[i];
            if (point == null || point == _currentPoint) continue;

            float distance = Vector3.Distance(transform.position, point.position);
            float weightScore = 1f / Mathf.Max(distance, 0.1f);

            if (weightScore > highestScore)
            {
                highestScore = weightScore;
                bestPoint = point;
            }
        }

        if (bestPoint == null && _unvisitedPoints.Count > 0)
        {
            bestPoint = _unvisitedPoints[0];
        }

        if (bestPoint != null)
        {
            _currentPoint = bestPoint;
            _unvisitedPoints.Remove(bestPoint);
            _brain.Agent.SetDestination(bestPoint.position);
        }
    }

    private void ResetUnvisitedPoints()
    {
        _unvisitedPoints.Clear();
        for (int i = 0; i < _patrolPoints.Length; i++)
        {
            if (_patrolPoints[i] != null)
            {
                _unvisitedPoints.Add(_patrolPoints[i]);
            }
        }
    }

}
