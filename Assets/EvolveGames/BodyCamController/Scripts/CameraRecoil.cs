using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EvolveGames
{
    public class CameraRecoil : MonoBehaviour
    {
        [Header("CameraRecoil")]
        [SerializeField]
        RecoilMultipler[] RecoilMultiplers;
        [HideInInspector] public float recoilX = 2f;
        [HideInInspector] public float recoilY = 1f;
        [HideInInspector] public float recoilZ = 0.5f;

        [HideInInspector] public float snappiness = 6f;
        [HideInInspector] public float returnSpeed = 2f;

        [HideInInspector] public float multipler = 2f;
        [HideInInspector] public float multipler_return = 2f;

        Vector3 currentRotation;
        Vector3 targetRotation;

        void Update()
        {
            targetRotation = Vector3.Lerp(targetRotation, Vector3.zero, returnSpeed * Time.deltaTime);
            currentRotation = Vector3.Slerp(currentRotation, targetRotation, snappiness * Time.deltaTime);
            transform.localRotation = Quaternion.Euler(currentRotation);
        }

        public void FireRecoil()
        {
            targetRotation += new Vector3(Random.Range(-recoilX, recoilX / 4f), Random.Range(-recoilY, recoilY / 4f), Random.Range(-recoilZ, recoilZ / 4f));
            foreach (var r in RecoilMultiplers)
            {
                r.recoilX = recoilX * multipler;
                r.recoilY = recoilY * multipler;
                r.recoilZ = recoilZ * multipler;

                r.returnSpeed = returnSpeed * multipler_return;
                r.snappiness = snappiness;
                r.FireRecoil();
            }
        }
    }
}