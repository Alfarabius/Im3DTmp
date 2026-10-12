using UnityEngine;

[RequireComponent(typeof(PlayerBrain))]
public class PlayerHeavyWoundedState : MonoBehaviour, ICharacterState
{
    [SerializeField] private float timeToDie = 10f;
    private PlayerBrain _brain;
    private float _timer;
    private Vector3 _velocity;
    private const float Gravity = -9.81f;

    private void Awake() => _brain = GetComponent<PlayerBrain>();

    public void Enter()
    {
        _timer = timeToDie;
        
        if (_brain.BodycamTarget != null && _brain.HeadBone != null)
        {
            _brain.BodycamTarget.SetParent(_brain.HeadBone, true);
        }
        
        // Жесткий запуск анимации тяжелого ранения
        _brain.Anim.Play("HeavyWounded");
    }

    public void UpdateState()
    {
        // Оставляем только гравитацию, чтобы Игрок не завис в воздухе
        if (!_brain.Controller.isGrounded)
        {
            _velocity.y += Gravity * Time.deltaTime;
            _brain.Controller.Move(_velocity * Time.deltaTime);
        }

        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            // Таймер вышел — наносим урон, который переведет в состояние Dead
            _brain.TryApplyHit(1f);
        }
    }

    public void Exit() { }
}
