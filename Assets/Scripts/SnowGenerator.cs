using UnityEngine;

public class SnowGenerator : MonoBehaviour
{
    [Header("Визуал")]
    [Tooltip("Создай URP/Particles/Unlit материал, поставь Surface Type: Transparent и закинь сюда")]
    [SerializeField] private Material snowMaterial;

    [Header("Позиционирование")]
    [Tooltip("Смещение зоны генерации относительно камеры")]
    [SerializeField] private Vector3 spawnOffset = new Vector3(0, 20f, 0f);
    
    [Tooltip("Размер области над камерой, где рождается снег (Ширина, Высота, Глубина)")]
    [SerializeField] private Vector3 spawnAreaSize = new Vector3(40f, 1f, 40f);

    [Header("Настройки снежинок")]
    [Tooltip("Максимальное количество частиц на сцене одновременно")]
    [SerializeField] private int maxParticles = 2000;
    
    [Tooltip("Количество новых снежинок в секунду")]
    [SerializeField] private float emissionRate = 150f;
    
    [Tooltip("Время жизни одной снежинки в секундах (должно хватать, чтобы долететь до земли)")]
    [SerializeField] private float particleLifetime = 10f;
    
    [Tooltip("Размер снежинки (X = минимум, Y = максимум)")]
    [SerializeField] private Vector2 particleSize = new Vector2(0.05f, 0.15f);

    [Header("Гравитация и Ветер")]
    [Tooltip("Скорость падения вниз по оси Y (X = минимум, Y = максимум). Должна быть отрицательной!")]
    [SerializeField] private Vector2 fallSpeedY = new Vector2(-5f, -3f);
    
    [Tooltip("Снос ветром по оси X (X = минимум, Y = максимум)")]
    [SerializeField] private Vector2 windSpeedX = new Vector2(0f, 0f);
    
    [Tooltip("Снос ветром по оси Z (X = минимум, Y = максимум)")]
    [SerializeField] private Vector2 windSpeedZ = new Vector2(0f, 0f);

    [Header("Турбулентность (Завихрения)")]
    [Tooltip("Сила хаотичного покачивания (чем больше, тем сильнее метель)")]
    [SerializeField] private float noiseStrength = 0.1f;
    
    [Tooltip("Частота смены направления покачивания")]
    [SerializeField] private float noiseFrequency = 0.5f;

    private void Start()
    {
        // На случай, если ты забыл сгенерировать снег в редакторе, 
        // скрипт создаст его при запуске игры (но после выхода он пропадет)
        if (transform.Find("OptimizedSnowSystem") == null)
        {
            GenerateSnow();
        }
    }

    // Эта строчка создает кнопку в контекстном меню скрипта в Инспекторе
    [ContextMenu("Сгенерировать Снег (Сохранится на сцене)")]
    public void GenerateSnow()
    {
        // ЖЕСТКАЯ ПРОВЕРКА: Если объект уже существует, отменяем создание
        if (transform.Find("OptimizedSnowSystem") != null)
        {
            Debug.Log("Снегопад уже существует на этой камере! Пропускаем создание.");
            return;
        }

        if (snowMaterial == null)
        {
            Debug.LogError("Ты не назначил материал снега в инспекторе камеры!");
            return;
        }

        GameObject snow = new GameObject("OptimizedSnowSystem");
        snow.transform.SetParent(transform);
        snow.transform.localPosition = spawnOffset;
        snow.transform.localRotation = Quaternion.identity;

        ParticleSystem ps = snow.AddComponent<ParticleSystem>();
        ParticleSystemRenderer psRenderer = snow.GetComponent<ParticleSystemRenderer>();

        psRenderer.material = snowMaterial;
        psRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        psRenderer.receiveShadows = false;

        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World; 
        main.startLifetime = particleLifetime; 
        main.startSpeed = 0f; 
        main.startSize = new ParticleSystem.MinMaxCurve(particleSize.x, particleSize.y);
        main.maxParticles = maxParticles; 

        var emission = ps.emission;
        emission.rateOverTime = emissionRate;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = spawnAreaSize; 

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.x = new ParticleSystem.MinMaxCurve(windSpeedX.x, windSpeedX.y);
        velocity.y = new ParticleSystem.MinMaxCurve(fallSpeedY.x, fallSpeedY.y);
        velocity.z = new ParticleSystem.MinMaxCurve(windSpeedZ.x, windSpeedZ.y);
        
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = noiseStrength;
        noise.frequency = noiseFrequency;
        
        // В редакторе Play() не сработает автоматически, система запустится только при старте игры
        if (Application.isPlaying)
        {
            ps.Play();
        }
    }
}
