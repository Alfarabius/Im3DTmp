using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace EvolveGames
{
    public class PlayerMovement : MonoBehaviour
    {

        float playerHeight = 2;

        [SerializeField] Transform orientation;

        [Header("MOVEMENT")]
        [SerializeField, Range(1f, 14f)] float MoveSmooth = 6;
        [SerializeField, Range(0.05f, 2)] float RotationAmount = 0.2f;
        public float WalkingSpeed = 3f;
        public float RunningSpeed = 6f;


        //[Header("Keybinds")]
        //[SerializeField] KeyCode jumpKey = KeyCode.Space;

        [HideInInspector] public float horizontalMovement;
        [HideInInspector] public float verticalMovement;
        [HideInInspector] public float currentSpeed = 0;

        float groundDrag = 6f;
        float airDrag = 0.1f;
        float movementMultiplier = 10f;


        [SerializeField] float airMultiplaier = 0.4f;
        [Header("KEYS")]
        [SerializeField] KeyCode SprintKey = KeyCode.LeftShift;


        [HideInInspector] public bool isGrounded = false;
        Vector3 moveDirection;
        [HideInInspector] public Rigidbody rb;
        private void Start()
        {
            rb = GetComponent<Rigidbody>();

            rb.freezeRotation = true;
        }
        private void Update()
        {
            isGrounded = Physics.Raycast(transform.position, Vector3.down, playerHeight / 2 + 0.1f);

            KeyInput();
            ControlDrag();
        }
        void KeyInput()
        {
            horizontalMovement = Input.GetAxisRaw("Horizontal");
            verticalMovement = Input.GetAxisRaw("Vertical");

            float target = Input.GetKey(SprintKey) ? RunningSpeed : WalkingSpeed;
            target = horizontalMovement != 0 || verticalMovement != 0 ? target : 0;

            currentSpeed = Mathf.Lerp(currentSpeed, target, MoveSmooth * Time.deltaTime);


            moveDirection = (orientation.forward * verticalMovement + orientation.right * RotationAmount * horizontalMovement).normalized;
        }

        void ControlDrag()
        {
            if (isGrounded)
            {
                rb.linearDamping = groundDrag;
            }
            else
            {
                rb.linearDamping = airDrag;
            }
        }
        private void FixedUpdate()
        {
            MovePlayer();
        }
        void MovePlayer()
        {
            if (isGrounded)
            {
                rb.AddForce(moveDirection.normalized * currentSpeed * movementMultiplier, ForceMode.Acceleration);
            }
            else if (!isGrounded)
            {
                rb.AddForce(moveDirection.normalized * currentSpeed * movementMultiplier * airMultiplaier, ForceMode.Acceleration);
            }
        }
    }
}