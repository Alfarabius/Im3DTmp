using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace EvolveGames
{
    public class Target : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] Material DiedMaterial;

        public void Died()
        {
            var r = GetComponentInChildren<MeshRenderer>();
            r.material = DiedMaterial;
        }
    }
}