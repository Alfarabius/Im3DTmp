using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class CoverManager : MonoBehaviour
{
    public static CoverManager Instance { get; private set; }

    [Header("Настройки безопасности")]
    [Tooltip("Минимально допустимое расстояние от изгибов маршрута до ThreatPosition")]
    [SerializeField] private float safePathRadius = 5f; 

    private List<CoverPoint> _coverPoints = new List<CoverPoint>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void RegisterCoverPoint(CoverPoint point)
    {
        if (!_coverPoints.Contains(point)) _coverPoints.Add(point);
    }

    public void UnregisterCoverPoint(CoverPoint point)
    {
        _coverPoints.Remove(point);
    }

    // blacklistedPoints - точки, которые NPC обнаружил как "занятые"
    public Transform GetBestCover(Vector3 seekerPosition, Vector3 threatPosition, List<Transform> blacklistedPoints = null)
    {
        Transform bestSafeCover = null;
        Transform bestFallbackCover = null;
        
        float minDistToSafeCover = float.MaxValue;
        float maxDistToThreatForFallback = 0f; // Для опасного пути ищем ту точку, что хотя бы дальше всех от врага

        float currentDistToThreat = Vector3.Distance(seekerPosition, threatPosition);
        NavMeshPath path = new NavMeshPath();

        foreach (var point in _coverPoints)
        {
            Transform coverTransform = point.transform;
            
            // Пропускаем точки из локального черного списка NPC
            if (blacklistedPoints != null && blacklistedPoints.Contains(coverTransform)) 
                continue;

            float coverDistToThreat = Vector3.Distance(coverTransform.position, threatPosition);

            // ПРАВИЛО 1: Точка не должна быть ближе к врагу, чем сам NPC (отсекаем бег вперед)
            if (coverDistToThreat <= currentDistToThreat) 
                continue;

            // Считаем маршрут
            if (NavMesh.CalculatePath(seekerPosition, coverTransform.position, NavMesh.AllAreas, path))
            {
                // Игнорируем недостижимые точки
                if (path.status != NavMeshPathStatus.PathComplete) 
                    continue;

                bool isRouteSafe = true;

                // ПРАВИЛО 2: Проверка углов маршрута
                foreach (Vector3 corner in path.corners)
                {
                    if (Vector3.Distance(corner, threatPosition) < safePathRadius)
                    {
                        isRouteSafe = false;
                        break;
                    }
                }

                // ПРАВИЛО 3: Распределение
                if (isRouteSafe)
                {
                    // Безопасный путь: выбираем ближайшую к NPC точку, чтобы быстрее добежать
                    float distToSeeker = Vector3.Distance(seekerPosition, coverTransform.position);
                    if (distToSeeker < minDistToSafeCover)
                    {
                        minDistToSafeCover = distToSeeker;
                        bestSafeCover = coverTransform;
                    }
                }
                else
                {
                    // Опасный путь (фоллбек): выбираем ту точку, которая физически дальше всего от врага
                    if (coverDistToThreat > maxDistToThreatForFallback)
                    {
                        maxDistToThreatForFallback = coverDistToThreat;
                        bestFallbackCover = coverTransform;
                    }
                }
            }
        }

        // Если нашли идеальную безопасную - отдаем её. Если нет - бежим по опасному, но дальнему пути.
        return bestSafeCover != null ? bestSafeCover : bestFallbackCover;
    }
}
