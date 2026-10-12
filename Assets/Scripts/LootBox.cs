using UnityEngine;
using UnityEngine.InputSystem;

public class LootBox : MonoBehaviour
{
    [Header("Настройки взаимодействия")]
    [Tooltip("Ссылка на действие взаимодействия (Action из New Input System)")]
    [SerializeField] private InputActionReference interactAction;
    [Tooltip("Слой игрока для проверки (выбери слой Player)")]
    [SerializeField] private LayerMask playerLayer;
    
    [Header("Визуал и UI")]
    [Tooltip("Ссылка на UI подсказку (например, иконка F), которая лежит внутри коробки")]
    [SerializeField] private GameObject promptUI;
    [Tooltip("Рендерер коробки для управления свечением")]
    [SerializeField] private MeshRenderer boxRenderer;
    
    [Header("Свечение (Emission)")]
    [Tooltip("Базовый цвет свечения (HDR)")]
    [SerializeField] private Color emissionColor = Color.yellow;
    [Tooltip("Скорость пульсации")]
    [SerializeField] private float pulseSpeed = 2f;
    [Tooltip("Максимальная яркость свечения")]
    [SerializeField] private float maxEmissionIntensity = 2f;

    [Header("Лут")]
    [Tooltip("Префаб лута, который появится при открытии")]
    [SerializeField] private GameObject lootPrefab;
    [Tooltip("Точка спавна лута (можно оставить пустой, тогда заспавнится над коробкой)")]
    [SerializeField] private Transform spawnPoint;

    private MaterialPropertyBlock _propBlock;
    private bool _isPlayerInZone;
    private bool _isOpened;
    private Collider _triggerCollider;

    private void Awake()
    {
        // Ищем среди всех коллайдеров на объекте тот, который помечен как триггер (твоя зона обнаружения)
        Collider[] colliders = GetComponents<Collider>();
        foreach (var col in colliders)
        {
            if (col.isTrigger)
            {
                _triggerCollider = col;
                break;
            }
        }

        // Инициализируем блок свойств материала для оптимизированной смены цвета без разрыва батчинга
        _propBlock = new MaterialPropertyBlock();
        
        if (promptUI != null) promptUI.SetActive(false);
    }

    private void Update()
    {
        if (_isOpened) return;

        // Фаза ожидания: Плавная пульсация свечения
        if (boxRenderer != null && !_isPlayerInZone)
        {
            float emission = Mathf.PingPong(Time.time * pulseSpeed, maxEmissionIntensity);
            SetEmission(emissionColor * emission);
        }

        // Взаимодействие через New Input System (проверяем, что Action назначен и нажат в этом кадре)
        if (_isPlayerInZone && interactAction != null && interactAction.action.WasPressedThisFrame())
        {
            OpenBox();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Проверяем, что объект не открыт и слой вошедшего объекта совпадает со слоем игрока
        if (_isOpened || ((1 << other.gameObject.layer) & playerLayer) == 0) return;

        _isPlayerInZone = true;
        if (promptUI != null) promptUI.SetActive(true);
        
        // Фаза фокуса: Фиксируем свечение на максимуме
        SetEmission(emissionColor * maxEmissionIntensity);
    }

    private void OnTriggerExit(Collider other)
    {
        if (_isOpened || ((1 << other.gameObject.layer) & playerLayer) == 0) return;

        _isPlayerInZone = false;
        if (promptUI != null) promptUI.SetActive(false);
    }

    private void OpenBox()
    {
        _isOpened = true;
        
        // Отключаем именно триггер, оставляя физический коллайдер рабочим
        if (_triggerCollider != null)
        {
            _triggerCollider.enabled = false; 
        }
        
        if (promptUI != null) promptUI.SetActive(false);
        
        // Отключаем свечение навсегда
        SetEmission(Color.black);

        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position + Vector3.up;
        if (lootPrefab != null)
        {
            Instantiate(lootPrefab, spawnPos, Quaternion.identity);
        }
        
        if (GameManager.Instance != null) GameManager.Instance.RegisterLootBoxOpened();
    }

    private void SetEmission(Color color)
    {
        if (boxRenderer == null) return;
        
        // Читаем текущие свойства, меняем цвет свечения и применяем обратно
        boxRenderer.GetPropertyBlock(_propBlock);
        _propBlock.SetColor("_EmissionColor", color);
        boxRenderer.SetPropertyBlock(_propBlock);
    }
}
