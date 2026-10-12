using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace EvolveGames
{
    public class ItemChange : MonoBehaviour
    {
        [Header("ItemChange")]
        [SerializeField] GameObject[] Items;
        private int currentIndex = 0;
        [HideInInspector] public GunManager CurrentGun;

        private void Start()
        {
            foreach (var i in Items)
                i.SetActive(false);

            Items[currentIndex].SetActive(true);
            if (Items[currentIndex].GetComponent<GunManager>())
            {
                CurrentGun = Items[currentIndex].GetComponent<GunManager>();
            }
        }
        private void Update()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0)
            {
                if (Items.Length < 2)
                    return;

                //Items[currentIndex].SetActive(false);
                int pre = currentIndex;
                if (scroll > 0)
                {
                    currentIndex++;
                    if (currentIndex >= Items.Length)
                        currentIndex = 0;
                }
                else if (scroll < 0)
                {
                    currentIndex--;
                    if (currentIndex < 0)
                        currentIndex = Items.Length - 1;
                }

                StartCoroutine(ChangeHands(Items[pre], Items[currentIndex]));

                if (Items[currentIndex].GetComponent<GunManager>())
                {
                    var g = Items[currentIndex].GetComponent<GunManager>();
                    CurrentGun = g;
                    g.UpdateAmmoText();
                }
            }
        }
        IEnumerator ChangeHands(GameObject pre, GameObject newh)
        {
            if(pre.TryGetComponent<Animator>(out var a))
            {
                a.SetTrigger("HIDE");
            }
            yield return new WaitForSeconds(0.6f);
            newh.SetActive(true);
            pre.SetActive(false);
        }
        public void AddStack(int count)
        {
            if (CurrentGun) CurrentGun.AddStack(count);
        }
    }
}