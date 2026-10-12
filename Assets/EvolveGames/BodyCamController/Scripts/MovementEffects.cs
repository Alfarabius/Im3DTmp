using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EvolveGames
{
    public class MovementEffects : MonoBehaviour
    {
        [Header("MOVEMENT FX")]
        [SerializeField] PlayerMovement Character;
        [SerializeField, Range(0.05f, 2)] float RotationAmount = 0.2f;
        [SerializeField, Range(1f, 20)] float RotationSmooth = 6f;
        [Header("Movement")]
        [SerializeField] bool CanMovementFX = true;
        [SerializeField, Range(0.1f, 2)] float MovementAmount = 0.5f;   
        [Header("Fall")]
        [SerializeField] bool CanFallFX = true;
        [SerializeField, Range(1, 90)] float MaximumFallFX = 30f;


        Quaternion InstallRotation;
        Vector3 MovementVector;
        private void Start()
        {
            InstallRotation = transform.localRotation;
        }

        private void Update()
        {
            float movementX = (Character.verticalMovement * RotationAmount);
            float movementZ = (-Character.horizontalMovement * RotationAmount);
            MovementVector = new Vector3(CanMovementFX ? movementX + Mathf.Max(Mathf.Min(CanFallFX ? Character.rb.linearVelocity.y : 0, MaximumFallFX), 0) * MovementAmount : movementX, 0, movementZ);
            transform.localRotation = Quaternion.Lerp(transform.localRotation, Quaternion.Euler(MovementVector + InstallRotation.eulerAngles), Time.deltaTime * RotationSmooth);
        }
    }
}