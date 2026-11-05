using UnityEngine;
using UnityEngine.Serialization;

public enum InventoryItemKind { Placeholder, Ball, Shield, Heal, Phone }

[CreateAssetMenu(menuName = "Game/Inventory Item")]
public class InventoryItemDefinition : ScriptableObject
{
    [SerializeField] private string displayName;
    [SerializeField] private InventoryItemKind kind;
    [SerializeField] private Sprite icon;
    [SerializeField, Min(0.1f)] private float useDuration = 2f;
    [SerializeField] private bool consumeOnUse;
    [Header("Предмет в руке")]
    [Tooltip("Смещение только предмета от кисти в метрах: X вправо, Y вверх, Z вперёд. Положение кисти не меняется.")]
    [SerializeField, InspectorName("Положение предмета")] private Vector3 itemPosition;
    [Tooltip("Поворот только предмета в градусах. Положение и поворот кисти не меняются.")]
    [FormerlySerializedAs("heldEulerAngles")]
    [SerializeField, InspectorName("Поворот предмета")] private Vector3 itemEulerAngles = new Vector3(-8f, 0f, -12f);
    [Tooltip("Масштаб только предмета при удержании.")]
    [FormerlySerializedAs("heldScale")]
    [SerializeField, Min(0.01f), InspectorName("Масштаб предмета")] private float itemScale = 0.45f;
    [Header("Бросок")]
    [SerializeField, Min(0f)] private float minThrowImpulse = 3f;
    [SerializeField, Min(0f)] private float maxThrowImpulse = 18f;
    [SerializeField, Min(0.1f)] private float stunDuration = 5f;
    [SerializeField, Min(0.1f)] private float thrownLifetime = 5f;

    public string DisplayName => displayName;
    public InventoryItemKind Kind => kind;
    public Sprite Icon => icon;
    public float UseDuration => useDuration;
    public bool ConsumeOnUse => consumeOnUse;
    public Vector3 ItemPosition => itemPosition;
    public Vector3 ItemEulerAngles => itemEulerAngles;
    public float ItemScale => itemScale;
    public float MinThrowImpulse => minThrowImpulse;
    public float MaxThrowImpulse => maxThrowImpulse;
    public float StunDuration => stunDuration;
    public float ThrownLifetime => thrownLifetime;
}
