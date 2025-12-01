using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Characters.Enemies;
using Geneforge.Core.Stats;

namespace Geneforge.Gameplay.Abilities.Special
{
    [CreateAssetMenu(menuName = "Geneforge/Abilities/Chameleon - Camouflage")]
    public class A_ChameleonCamouflage : EssenceAbility
    {
        [Header("Camouflage")]
        public float invisDuration = 3f;

        [Tooltip("Optional: layer to switch the player to while invisible (e.g. 'PlayerInvisible'). Leave empty to skip.")]
        public string invisibleLayerName = "PlayerInvisible";
        [Tooltip("Optional: enemies layer to ignore while invisible (e.g. 'Enemies'). Leave empty to skip.")]
        public string enemiesLayerName = "Enemies";

        [Header("Glass look")]
        [Range(0f, 1f)] public float glassAlpha = 0.28f;
        public Color glassTint = new Color(0.75f, 0.95f, 1f, 1f);

        [Header("Tongue tug (first shot after invis)")]
        public float tetherDuration = 0.6f;
        public float pullForce = 15f;
        public static bool InvisibleActive { get; private set; }

        static Transform s_owner;
        static bool s_armed;
        static CamouflageRuntime s_rt;

        public override void OnPrimaryEquipped(GameObject owner, WeaponStats activeStats)
        {
            s_owner = owner.transform;
            s_rt = owner.GetComponent<CamouflageRuntime>();
            if (!s_rt) s_rt = owner.AddComponent<CamouflageRuntime>();
            s_rt.Configure(this, owner);
        }

        public override void OnPrimaryUnequipped(GameObject owner)
        {
            if (s_rt) Destroy(s_rt);
            s_rt = null; s_owner = null; s_armed = false;
            InvisibleActive = false;
        }

        public override void OnBulletSpawn(Bullet bullet, WeaponStats stats)
        {
            if (s_armed)
            {
                bullet.gameObject.AddComponent<TongueMarker>().Init(this);
                s_armed = false;
                if (s_rt) s_rt.EndInvis();
            }
        }

        public override void OnHitEnemy(Bullet bullet, EnemyCore enemy, WeaponStats stats)
        {
            var marker = bullet.GetComponent<TongueMarker>();
            if (!marker || enemy == null || s_owner == null) return;

            bullet.StartCoroutine(PullEnemy(enemy, s_owner, tetherDuration, pullForce));
        }

        IEnumerator PullEnemy(EnemyCore e, Transform player, float dur, float force)
        {
            float t = 0f;
            while (e != null && player != null && t < dur)
            {
                Vector3 dir = (player.position - e.transform.position);
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                    e.ApplyKnockback(dir.normalized, force);

                t += Time.deltaTime;
                yield return null;
            }
        }

        class TongueMarker : MonoBehaviour
        {
            A_ChameleonCamouflage owner;
            public void Init(A_ChameleonCamouflage a) { owner = a; }
        }

        public class CamouflageRuntime : MonoBehaviour
        {
            A_ChameleonCamouflage def;
            RunStats run;

            Renderer[] rends;
            List<Material[]> originalMats;
            Material glassMat;

            Collider[] cols;
            int originalLayer = -1;
            int invisibleLayer = -1;
            int enemiesLayer = -1;

            float lastHP;
            bool invisible;
            Coroutine timer;

            public void Configure(A_ChameleonCamouflage d, GameObject owner)
            {
                def = d;
                run = owner.GetComponent<RunStats>();
                rends = owner.GetComponentsInChildren<Renderer>(true);
                cols = owner.GetComponentsInChildren<Collider>(true);
                lastHP = run ? run.CurrentHP : -1f;

                invisibleLayer = string.IsNullOrEmpty(def.invisibleLayerName) ? -1 : LayerMask.NameToLayer(def.invisibleLayerName);
                enemiesLayer = string.IsNullOrEmpty(def.enemiesLayerName) ? -1 : LayerMask.NameToLayer(def.enemiesLayerName);

                BuildGlassMaterial();
                RestoreOriginal();
            }

            void Update()
            {
                if (!run) return;
                if (lastHP >= 0f && run.CurrentHP < lastHP - 1e-4f)
                {
                    BeginInvis();
                }
                lastHP = run.CurrentHP;
            }

            void BeginInvis()
            {
                if (invisible)
                {
                    if (timer != null) StopCoroutine(timer);
                    timer = StartCoroutine(InvisTimer());
                    return;
                }

                s_armed = true;
                ApplyGlass();
                FlipToInvisibleLayer();

                InvisibleActive = true;
                invisible = true;

                if (timer != null) StopCoroutine(timer);
                timer = StartCoroutine(InvisTimer());
            }

            public void EndInvis()
            {
                if (!invisible) return;

                if (timer != null) StopCoroutine(timer);
                invisible = false; s_armed = false;
                InvisibleActive = false;

                RestoreOriginal();
                RestoreLayer();
            }

            IEnumerator InvisTimer()
            {
                yield return new WaitForSeconds(def.invisDuration);
                EndInvis();
            }

            void OnDestroy()
            {
                InvisibleActive = false;
                RestoreOriginal();
                RestoreLayer();
            }

            void BuildGlassMaterial()
            {
                if (glassMat) return;
                var shader = Shader.Find("Sprites/Default");
                glassMat = new Material(shader);
                var c = def.glassTint; c.a = Mathf.Clamp01(def.glassAlpha);
                glassMat.color = c;
            }

            void ApplyGlass()
            {
                if (rends == null) return;
                if (originalMats == null) originalMats = new List<Material[]>(rends.Length);
                originalMats.Clear();

                for (int i = 0; i < rends.Length; i++)
                {
                    var r = rends[i];
                    if (!r) { originalMats.Add(null); continue; }

                    var mats = r.materials;
                    originalMats.Add(mats);

                    var swap = new Material[mats.Length];
                    for (int m = 0; m < swap.Length; m++) swap[m] = glassMat;
                    r.materials = swap;
                }
            }

            void RestoreOriginal()
            {
                if (rends == null) return;

                if (originalMats != null && originalMats.Count == rends.Length)
                {
                    for (int i = 0; i < rends.Length; i++)
                    {
                        var r = rends[i];
                        if (!r) continue;
                        var mats = originalMats[i];
                        if (mats != null) r.materials = mats;
                    }
                }
            }

            void FlipToInvisibleLayer()
            {
                if (invisibleLayer < 0) return;

                var root = gameObject;
                originalLayer = root.layer;
                SetLayerRecursively(root.transform, invisibleLayer);

                if (enemiesLayer >= 0)
                    Physics.IgnoreLayerCollision(invisibleLayer, enemiesLayer, true);
            }

            void RestoreLayer()
            {
                if (originalLayer < 0) return;

                SetLayerRecursively(transform, originalLayer);

                if (invisibleLayer >= 0 && enemiesLayer >= 0)
                    Physics.IgnoreLayerCollision(invisibleLayer, enemiesLayer, false);

                originalLayer = -1;
            }

            void SetLayerRecursively(Transform t, int layer)
            {
                t.gameObject.layer = layer;
                for (int i = 0; i < t.childCount; i++) SetLayerRecursively(t.GetChild(i), layer);
            }
        }
        public override void ApplyUpgrades(AbilityUpgrade[] upgrades)
        {
            if (upgrades == null) return;

            for (int i = 0; i < upgrades.Length; i++)
            {
                var u = upgrades[i];
                switch (u.key)
                {
                    case "Camouflage/InvisDuration":
                        invisDuration = Mathf.Max(0.05f, ApplyNumeric(invisDuration, u));
                        break;

                    case "Camouflage/TetherDuration":
                        tetherDuration = Mathf.Max(0f, ApplyNumeric(tetherDuration, u));
                        break;

                    case "Camouflage/PullForce":
                        pullForce = Mathf.Max(0f, ApplyNumeric(pullForce, u));
                        break;
                }
            }
        }
    }
}