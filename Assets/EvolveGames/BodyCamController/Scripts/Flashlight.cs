using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EvolveGames
{
    public class Flashlight : MonoBehaviour
    {
        [Header("Flashlight")]
        [SerializeField] bool FlashlightOnStart = false;
        [SerializeField] GameObject FlashlightObject;
        [SerializeField] AudioSource Audio;
        [Header("Key")]
        [SerializeField] KeyCode ChangeKey = KeyCode.F;

        private void Start()
        {
            if(FlashlightOnStart)
            {
                FlashlightObject.SetActive(true);
                if (Audio) Audio.Play();
                return;
            }
            FlashlightObject.SetActive(false);
        }
        private void Update()
        {
            if (Input.GetKeyDown(ChangeKey))
            {
                FlashlightObject.SetActive(!FlashlightObject.activeInHierarchy);
                if (Audio) Audio.Play();
            }
        }

    }
}