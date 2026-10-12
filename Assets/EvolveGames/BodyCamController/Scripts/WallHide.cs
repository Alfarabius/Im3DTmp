using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EvolveGames
{
    public class WallHide : MonoBehaviour
    {

        [Header("WallsHide")]
        [SerializeField] Transform StartDetect;
        [SerializeField] bool CanHide = true;
        [SerializeField] float SmoothHide = 6;
        [SerializeField] float MaxHide = 1.4f;
        [SerializeField] float RayLength = 1.5f;

        Vector3 InitHandsPos;
        Vector3 TargetHidePos;
        Quaternion InitHandsRot;
        Quaternion TargetHideRot;
        private float hideProgress = 0.0f;

        void Start()
        {
            InitHandsPos = transform.localPosition;
            InitHandsRot = transform.localRotation;

            TargetHidePos = InitHandsPos;
            TargetHideRot = InitHandsRot;
        }

        void Update()
        {
            if (!CanHide) return;
            Debug.DrawRay(StartDetect.position, StartDetect.forward, Color.red, RayLength);

            RaycastHit[] hit = Physics.RaycastAll(StartDetect.position, StartDetect.forward, RayLength);
            bool hit_b = false;
            foreach (var h in hit)
            {
                if (h.transform.GetComponent<BulletImpact>())
                {
                    if (h.distance < MaxHide)
                    {
                        hideProgress = Mathf.Clamp01(1 - (h.distance / MaxHide));

                        TargetHidePos = InitHandsPos + new Vector3(-0.1f * hideProgress, -0.2f * hideProgress, 0);
                        TargetHideRot = Quaternion.Euler(InitHandsRot.eulerAngles.x + (40f * (hideProgress)), InitHandsRot.eulerAngles.y - (10f * hideProgress), InitHandsRot.eulerAngles.z);
                    }

                    hit_b = true;
                    break;
                }
            }

            if (!hit_b)
            {
                hideProgress = Mathf.Lerp(hideProgress, 0f, SmoothHide * Time.deltaTime);
                TargetHidePos = InitHandsPos;
                TargetHideRot = InitHandsRot;
            }

            transform.localPosition = Vector3.Lerp(transform.localPosition, TargetHidePos, SmoothHide * Time.deltaTime);
            transform.localRotation = Quaternion.Lerp(transform.localRotation, TargetHideRot, SmoothHide * Time.deltaTime);
        }
    }
}