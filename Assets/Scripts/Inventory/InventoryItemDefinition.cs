using UnityEngine;

[CreateAssetMenu(menuName = "Game/Inventory Item")]
public class InventoryItemDefinition : ScriptableObject
{
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;
    [SerializeField, Min(0.1f)] private float useDuration = 2f;
    [SerializeField] private bool consumeOnUse;
    [SerializeField] private Vector3 heldPosition = new Vector3(0.24f, -0.27f, 0.55f);
    [SerializeField] private Vector3 heldEulerAngles = new Vector3(-8f, 0f, -12f);
    [SerializeField, Min(0.01f)] private float heldScale = 0.45f;
    [SerializeField] private Vector3 gripOffset = new Vector3(0f, 0.08f, 0f);

    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public float UseDuration => useDuration;
    public bool ConsumeOnUse => consumeOnUse;
    public Vector3 HeldPosition => heldPosition;
    public Vector3 HeldEulerAngles => heldEulerAngles;
    public float HeldScale => heldScale;
    public Vector3 GripOffset => gripOffset;
}
