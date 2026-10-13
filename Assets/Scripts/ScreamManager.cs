using UnityEngine;

public class ScreamManager : MonoBehaviour
{
    public static ScreamManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private float globalCooldown = 1.5f;
    
    private float _nextScreamTime;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public bool RequestScream()
    {
        if (Time.time >= _nextScreamTime)
        {
            _nextScreamTime = Time.time + globalCooldown;
            return true;
        }
        
        return false;
    }
}
