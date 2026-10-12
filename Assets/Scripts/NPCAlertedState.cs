using UnityEngine;

[RequireComponent(typeof(NPCBrain))]
public class NPCAlertedState : MonoBehaviour, ICharacterState
{
    private NPCBrain _brain;
    [SerializeField] private float regenStaminaAmount = 10f;

    private void Awake() => _brain = GetComponent<NPCBrain>();

    public void Enter()
    {
        _brain.Agent.isStopped = true;
        _brain.Anim.SetBool("IsAlerted", true);
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
        _brain.Anim.SetBool("IsAlerted", false);
    }
}
