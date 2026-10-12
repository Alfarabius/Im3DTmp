using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace EvolveGames
{
    public class GunManager : MonoBehaviour
    {
        [Header("GunManager")]

        [Space, SerializeField] PlayerMovement player;
        [SerializeField] Animator animator;
        [SerializeField] GameObject MuzzleFlash;
        [SerializeField] Transform MuzzleFlashSpawn;
        [SerializeField] GameObject AmmoThrow;
        [SerializeField] Transform AmmoThrowSpawn;

        [Header("Ammo")]
        [SerializeField] Text AmmoText;
        [SerializeField] int MaxAmmo = 36;
        public int Stacks = 4;


        [Header("Automat")]
        [SerializeField] bool IsAutomat = false;
        [SerializeField] float DelayForFire = 0.2f;
        [Header("Recoil")]
        [SerializeField] CameraRecoil Recoil;
        [SerializeField] float MultiplerHands = 3f;
        [SerializeField] float MultiplerReturnHands = 3f;
        [SerializeField] float RecoilX = 2f;
        [SerializeField] float RecoilY = 1f;
        [SerializeField] float RecoilZ = 0.5f;
        [Space, SerializeField] float Snappiness = 6f;
        [SerializeField] float ReturnSpeed = 2f;


        [Header("GunRay")]
        [SerializeField] Transform GunRay;
        [SerializeField] float MaxRange = 100;

        [Header("ShootImpact")]
        [SerializeField] GameObject Metal;
        [SerializeField] GameObject Blood;
        [SerializeField] GameObject Sand;
        [SerializeField] GameObject WaterBarel;
        [SerializeField] GameObject Stone;
        [SerializeField] GameObject Wood;


        float auto_time;
        int current_ammo;
        void Start()
        {
            if (Recoil != null)
            {
                Recoil.recoilX = RecoilX;
                Recoil.recoilY = RecoilY;
                Recoil.recoilZ = RecoilZ;

                Recoil.snappiness = Snappiness;
                Recoil.returnSpeed = ReturnSpeed;
                Recoil.multipler_return = MultiplerReturnHands;
                Recoil.multipler = MultiplerHands;
            }
            current_ammo = MaxAmmo;
            UpdateAmmoText();
        }

        void Update()
        {
            animator.SetFloat("MOVE", player.currentSpeed);

            if (!IsAutomat)
            {
                if (Input.GetButtonDown("Fire1"))
                    Fire();
            }
            else
            {
                if (Input.GetButton("Fire1") && auto_time > DelayForFire)
                {
                    auto_time = 0;
                    Fire();
                }
                auto_time += Time.deltaTime;
            }


            if (Input.GetKeyDown(KeyCode.R))
            {
                if (Stacks <= 0) return;
                animator.Play("Reload");
            }
        }
        public void Reload()
        {
            Stacks--;

            current_ammo = MaxAmmo;
            UpdateAmmoText();
        }
        public void AddStack(int count)
        {
            Stacks += count;
            UpdateAmmoText();
        }
        public void UpdateAmmoText()
        {
            if (AmmoText != null) AmmoText.text = $"{Stacks} / {current_ammo}";
        }
        public void Fire()
        {
            if (current_ammo <= 0 || !GunRay) return;
            current_ammo--;

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

            if (stateInfo.IsName("Fire")) animator.Play("Fire", -1, 0f);
            else animator.Play("Fire");

            UpdateAmmoText();

            RaycastHit ray_hit;
            Ray ray = new Ray(GunRay.position, GunRay.forward);
            if (Physics.Raycast(ray, out ray_hit, MaxRange, ~0))
            {
                if (ray_hit.transform.GetComponent<BulletImpact>())
                {
                    var bullet_impact = ray_hit.transform.GetComponent<BulletImpact>();
                    GameObject impact = null;
                    switch (bullet_impact.ImpactSelect)
                    {
                        case BulletImpact.Impact.Metal:
                            impact = Metal;
                            break;
                        case BulletImpact.Impact.Sand:
                            impact = Sand;
                            break;
                        case BulletImpact.Impact.Stone:
                            impact = Stone;
                            break;
                        case BulletImpact.Impact.Blood:
                            impact = Blood;
                            break;
                        case BulletImpact.Impact.Wood:
                            impact = Wood;
                            break;
                        case BulletImpact.Impact.WaterBarel:
                            impact = WaterBarel;
                            break;
                    }

                    if (impact != null)
                    {
                        var i = Instantiate(impact, ray_hit.point, Quaternion.LookRotation(ray_hit.normal));
                        i.transform.parent = ray_hit.transform;
                        Destroy(i, 6);
                    }
                }
                if (ray_hit.transform.GetComponentInParent<HealthEnemy>())
                {
                    var health_enemy = ray_hit.transform.GetComponentInParent<HealthEnemy>();
                    health_enemy.Hit(20);
                }
            }

            var mf = Instantiate(MuzzleFlash, MuzzleFlashSpawn, true);
            mf.transform.position = MuzzleFlashSpawn.position;
            mf.transform.rotation = MuzzleFlashSpawn.rotation;
            Destroy(mf, 6);

            if (Recoil != null)
                Recoil.FireRecoil();
        }
        public void AmmoDump()
        {
            if (AmmoThrow != null && AmmoThrowSpawn != null)
            {
                var mf = Instantiate(AmmoThrow, AmmoThrowSpawn.position, AmmoThrowSpawn.rotation);
                Destroy(mf, 6);
            }
        }
    }
}