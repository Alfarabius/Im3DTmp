using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EvolveGames
{
    public class MoveBob : MonoBehaviour
    {
        
        [Header("MoveBob Effect")]
        [SerializeField] bool Enabled = true;
        [Space, Header("Main")]
        [SerializeField] PlayerMovement Character;
        [SerializeField, Range(0.001f, 0.05f)] float Amount = 0.00484f;
        [SerializeField, Range(10f, 30f)] float Frequency = 16.0f;
        [SerializeField, Range(100f, 10f)] float Smooth = 44.7f;
        [Header("RoationMovement")]
        [SerializeField] bool EnabledRoationMovement = true;
        [SerializeField, Range(40f, 4f)] float RoationMovementSmooth = 10.0f;
        [SerializeField, Range(1f, 20f)] float RoationMovementAmount = 3.0f;

        float InitialAmount;
        float ToggleSpeed = 1.0f;
        Vector3 StartPos;
        Vector3 StartRot;
        Vector3 FinalRot;
        private void Awake()
        {
            StartPos = transform.localPosition;
            StartRot = transform.localRotation.eulerAngles;
            InitialAmount = Amount;
        }

        private void Update()
        {
            if (!Enabled) return;
            CheckMotion();
            ResetPos();
            if (EnabledRoationMovement) transform.localRotation = Quaternion.Lerp(transform.localRotation, Quaternion.Euler(FinalRot), RoationMovementSmooth * Time.deltaTime);
        }

        private void CheckMotion()
        {
            if (Character.currentSpeed < ToggleSpeed) return;
            if (!Character.isGrounded) return;
            PlayMotion(HeadBobMotion());
        }

        private void PlayMotion(Vector3 Movement)
        {
            transform.localPosition += Movement;
            FinalRot += new Vector3(-Movement.x, -Movement.y, Movement.x) * RoationMovementAmount;
        }
        private Vector3 HeadBobMotion()
        {
            Vector3 pos = Vector3.zero;
            Amount = (Character.currentSpeed / Character.RunningSpeed) * InitialAmount;
            pos.y += Mathf.Lerp(pos.y, Mathf.Sin(Time.time * Frequency) * Amount * 1.4f, Smooth * Time.deltaTime);
            pos.x += Mathf.Lerp(pos.x, Mathf.Cos(Time.time * Frequency / 2f) * Amount * 1.6f, Smooth * Time.deltaTime);
            return pos;
        }

        private void ResetPos()
        {
            if (transform.localPosition == StartPos) return;
            transform.localPosition = Vector3.Lerp(transform.localPosition, StartPos, 1 * Time.deltaTime);
            FinalRot = Vector3.Lerp(FinalRot, StartRot, 1 * Time.deltaTime);
        }
    }
}