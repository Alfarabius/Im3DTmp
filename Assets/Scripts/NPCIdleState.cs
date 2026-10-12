using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(NPCBrain))]
public class NPCIdleState : MonoBehaviour, ICharacterState
{
    private NPCBrain _brain;
    [SerializeField] private float regenStaminaAmount = 10f;

    private void Awake() => _brain = GetComponent<NPCBrain>();

    public void Enter()
    {
        _brain.Anim.SetBool("IsIdle", true);
    }

    public void UpdateState()
    {
        _brain.RegenerateStamina(regenStaminaAmount * Time.deltaTime);
        
        if (_brain.TrySeeAnyEnemy(out Vector3 enemyPos))
        {
            var runawayState = GetComponent<NPCRunawayState>();
            runawayState.ThreatPosition = enemyPos;
            _brain.ChangeState(runawayState);
        }
    }

    public void Exit()
    {
        _brain.Anim.SetBool("IsIdle", false);
    }
}
