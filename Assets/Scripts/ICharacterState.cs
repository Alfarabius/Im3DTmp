using UnityEngine;

// Контракт для состояний врага
public interface ICharacterState
{
    void Enter();
    void UpdateState();
    void Exit();
}

// Контракт для любой цели (Игрок или NPC)
public interface ITargetable
{
    float TargetWeight { get; }
    Transform TargetTransform { get; }
    TargetCondition Condition { get; }
    
    // Враг передает вероятность попадания, а сама цель решает, сменится ли ее состояние
    void TryApplyHit(float hitChance);

    bool IsPlayer();
}