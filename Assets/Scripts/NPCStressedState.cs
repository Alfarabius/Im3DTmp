using UnityEngine;

[RequireComponent(typeof(NPCBrain))]
public class NPCStressedState : MonoBehaviour, ICharacterState
{
    private NPCBrain _brain;
    private float _stressTimer;
    [SerializeField] private float cooldownTime = 4f;

    private void Awake() => _brain = GetComponent<NPCBrain>();

    public void Enter()
    {
        _brain.Agent.isStopped = true;
        _brain.Anim.SetBool("IsStressed", true);
        
        _stressTimer = cooldownTime;
    }

    public void UpdateState()
    {
        _stressTimer -= Time.deltaTime;

        if (_stressTimer <= 0f)
        {
            _brain.ChangeState(GetComponent<NPCAlertedState>());
        }
    }

    public void Exit()
    {
        _brain.Anim.SetBool("IsStressed", false);
    }
}
