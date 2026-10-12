using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EvolveGames
{
    public class BulletImpact : MonoBehaviour
    {
        public enum Impact
        {
            Metal,
            Blood,
            Sand,
            WaterBarel,
            Stone,
            Wood
        }

        [SerializeField]
        public Impact ImpactSelect;
    }
}