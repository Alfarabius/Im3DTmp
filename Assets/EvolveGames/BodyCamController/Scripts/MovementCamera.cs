using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace EvolveGames
{
    public class MovementCamera : MonoBehaviour
    {

        [SerializeField] Transform cameraPosition;
        void Update()
        {
            transform.position = cameraPosition.position;
        }
    }
}