using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace EvolveGames
{
    public class HandsSmooth : MonoBehaviour
    {
        [Header("HandsSmooth")]
        [Space]
        [SerializeField] private bool Inverted = false;

        [Header("Position")]
        [SerializeField] float smooth = 6f;
        [SerializeField] float amount = 0.04f;
        [SerializeField] float maxAmount = 0.06f;
        [Header("Rotation")]
        [SerializeField] private float RotationSmooth = 12.0f;
        [SerializeField] private float RotationAmount = 4.0f;
        [SerializeField] private float MaxRotationAmount = 5.0f;
        [SerializeField] private float RotationMovementMultipler = 5.0f;
        [Header("Aim")]
        [SerializeField] float AimSpeed = 4f;
        [SerializeField] float AimMultipler = 20f;

        float AimRotation;
        Vector3 InstallPosition;
        Quaternion InstallRotation;
        float horizontal = 0;
        private void Start()
        {
            InstallPosition = transform.localPosition;
            InstallRotation = transform.localRotation;
        }
        private void Update()
        {
            float InputX = -Input.GetAxis("Mouse X");
            float InputY = -Input.GetAxis("Mouse Y");
            horizontal = Mathf.Lerp(horizontal, -Input.GetAxis("Horizontal"), 6 * Time.deltaTime);
            float vertical = Input.GetAxis("Vertical");

            float moveX = Mathf.Clamp(InputX * amount, -maxAmount, maxAmount);
            moveX = Inverted ? -moveX : moveX;
            float moveY = Mathf.Clamp(InputY * amount, -maxAmount, maxAmount);
            moveY = Inverted ? -moveY : moveY;

            Vector3 finalPosition = new Vector3(moveX, moveY, 0);

            transform.localPosition = Vector3.Lerp(transform.localPosition, finalPosition + InstallPosition, Time.deltaTime * smooth);

            float TiltX = Mathf.Clamp(InputX * RotationAmount, -MaxRotationAmount, MaxRotationAmount);
            TiltX = Inverted ? -TiltX * 2f : TiltX;
            float TiltY = Mathf.Clamp(InputY * RotationAmount, -MaxRotationAmount / 2f, MaxRotationAmount / 2f);
            TiltY = Inverted ? -TiltY * 2f : TiltY;

            Vector3 vector = new Vector3(Mathf.Max(vertical * 0.4f, 0) * RotationMovementMultipler, 0, horizontal * RotationMovementMultipler);
            Vector3 finalRotation = new Vector3(-TiltY, 0, TiltX + AimRotation) + vector;

            transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.Euler(finalRotation) * InstallRotation, Time.deltaTime * RotationSmooth);

            if (Input.GetButton("Fire2"))
            {
                AimRotation = EaseOutLerp(AimRotation, AimMultipler, AimSpeed * Time.deltaTime);
            }
            else
            {
                AimRotation = EaseOutLerp(AimRotation, 0f, AimSpeed * Time.deltaTime);
            }
        }
        float EaseOutLerp(float start, float end, float t)
        {
            t = 1 - Mathf.Pow(1 - t, 2);
            return Mathf.Lerp(start, end, t);
        }
    }
}