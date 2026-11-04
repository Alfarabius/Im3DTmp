using UnityEngine;

[RequireComponent(typeof(EnemyBrain))]
public class StunState : MonoBehaviour, ICharacterState
{
    private EnemyBrain _brain;
    private float _remaining;

    private void Awake() => _brain = GetComponent<EnemyBrain>();

    public void SetDuration(float duration) => _remaining = Mathf.Max(0.1f, duration);

    public void Enter()
    {
        if (_brain.Agent.isActiveAndEnabled && _brain.Agent.isOnNavMesh)
        {
            _brain.Agent.isStopped = true;
            _brain.Agent.velocity = Vector3.zero;
        }
        _brain.Anim.SetFloat("Speed", 0f);
        _brain.Anim.ResetTrigger("Attack");
        _brain.Anim.SetBool("IsStunned", true);
    }

    public void UpdateState()
    {
        _remaining -= Time.deltaTime;
        if (_remaining <= 0f) _brain.FinishStun();
    }

    public void Exit()
    {
        _brain.Anim.SetBool("IsStunned", false);
        if (_brain.Agent.isActiveAndEnabled && _brain.Agent.isOnNavMesh) _brain.Agent.isStopped = false;
    }
}
