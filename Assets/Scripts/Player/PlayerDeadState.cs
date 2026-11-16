using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PlayerBrain))]
public class PlayerDeadState : MonoBehaviour, ICharacterState
{
    [Header("Настройки смерти")]
    [Tooltip("Время в секундах, которое дается на проигрывание анимации смерти перед вызовом поражения")]
    [SerializeField] private float deathDelay = 2.5f;

    private PlayerBrain _brain;

    private void Awake() => _brain = GetComponent<PlayerBrain>();

    public void Enter()
    {
        GetComponent<PlayerDeathFeedback>()?.Play();
        _brain.Anim.CrossFade("Dead", 0.25f);
        StartCoroutine(ReportDeathWithDelay());
    }

    public void UpdateState()
    {
        // Игрок мертв. Обновление логики остановлено.
    }

    public void Exit() 
    { 
        StopAllCoroutines();
    }

    private IEnumerator ReportDeathWithDelay()
    {
        var feedback = GetComponent<PlayerDeathFeedback>();
        float feedbackRemaining = feedback != null ? feedback.RemainingTime : 0f;
        yield return new WaitForSeconds(Mathf.Max(deathDelay, feedbackRemaining));
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ReportPlayerDeath();
        }
    }
}
