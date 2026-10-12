using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace EvolveGames
{
    public class HealthEnemy : MonoBehaviour
    {
        [Header("HealthEnemy")]
        [SerializeField] int Health = 100;

        [Space, SerializeField] UnityEvent AfterDie;
        int MaxHealth;
        private void Start()
        {
            MaxHealth = Health;
        }
        public void Hit(int count)
        {
            Health -= count < Health ? count : Health;
            if (Health == 0) AfterDie.Invoke();
        }
        public void Heal(int count)
        {
            Health += count > (MaxHealth - Health) ? (MaxHealth - Health) : count;
        }
    }
}