//Is blocking damage when invisible intended?

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Characters.Enemies;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Core.Stats;

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
    [Range(0f,1f)] public float glassAlpha = 0.28f;
    public Color glassTint = new Color(0.75f, 0.95f, 1f, 1f);

    [Header("Tongue tug (first shot after invis)")]
    public float tetherDuration = 0.6f;
    public float pullForce = 15f;

    // Global visibility flag so PlayerHealth (and optionally AI) can query.
    public static bool InvisibleActive { get; private set; }

    static Transform s_owner;
    static bool s_armed;           // next shot tethers
    static CamouflageRuntime s_rt; // for convenience

    public override void OnPrimaryEquipped(GameObject owner, WeaponStats activeStats)
    {
        s_owner = owner.transform;
        s_rt = owner.GetComponent<CamouflageRuntime>();
        if (!s_rt) s_rt = owner.AddComponent<CamouflageRuntime>();
        s_rt.Configure(this, owner);
    }

    public override void OnPrimaryUnequipped(GameObject owner)
    {
        if (s_rt) Object.Destroy(s_rt);
        s_rt = null; s_owner = null; s_armed = false;
        InvisibleActive = false;
    }

    public override void OnBulletSpawn(Bullet bullet, WeaponStats stats)
    {
        // If we’re armed, this shot gets a tongue effect
        if (s_armed)
        {
            bullet.gameObject.AddComponent<TongueMarker>().Init(this);
            s_armed = false; // consume
            // shooting also ends invis immediately
            if (s_rt) s_rt.EndInvis();
        }
    }

    public override void OnHitEnemy(Bullet bullet, Enemy enemy, WeaponStats stats)
    {
        var marker = bullet.GetComponent<TongueMarker>();
        if (!marker || enemy == null || s_owner == null) return;

        bullet.StartCoroutine(PullEnemy(enemy, s_owner, tetherDuration, pullForce));
    }

    IEnumerator PullEnemy(Enemy e, Transform player, float dur, float force)
    {
        float t = 0f;
        while (e != null && player != null && t < dur)
        {
            Vector3 dir = (player.position - e.transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                e.ApplyKnockback(dir.normalized, force); // reuse knockback to pull inward

            t += Time.deltaTime;
            yield return null;
        }
    }

    // Marker to tag “first shot after invis”
    class TongueMarker : MonoBehaviour
    {
        A_ChameleonCamouflage owner;
        public void Init(A_ChameleonCamouflage a) { owner = a; }
    }

    // Runtime component monitoring player damage and toggling invis
    public class CamouflageRuntime : MonoBehaviour
    {
        A_ChameleonCamouflage def;
        RunStats run;

        Renderer[] rends;
        // store original materials to restore glass swap cleanly
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
            cols  = owner.GetComponentsInChildren<Collider>(true);
            lastHP = run ? run.CurrentHP : -1f;

            // resolve optional layers (ok if not found)
            invisibleLayer = string.IsNullOrEmpty(def.invisibleLayerName) ? -1 : LayerMask.NameToLayer(def.invisibleLayerName);
            enemiesLayer   = string.IsNullOrEmpty(def.enemiesLayerName)   ? -1 : LayerMask.NameToLayer(def.enemiesLayerName);

            BuildGlassMaterial();
            RestoreOriginal(); // ensure visible on equip
        }

        void Update()
        {
            if (!run) return;
            // detect damage
            if (lastHP >= 0f && run.CurrentHP < lastHP - 1e-4f)
            {
                BeginInvis();
            }
            lastHP = run.CurrentHP;
        }

        void BeginInvis()
        {
            if (invisible) { // refresh timer only
                if (timer != null) StopCoroutine(timer);
                timer = StartCoroutine(InvisTimer());
                return;
            }

            s_armed = true; // arm the next shot
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

        // ===== Glass look =====
        void BuildGlassMaterial()
        {
            if (glassMat) return;
            // Simple transparent material that works in URP/BiRP
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

                // snapshot current per-renderer materials
                var mats = r.materials;
                originalMats.Add(mats);

                // swap to glass (same count so submeshes still draw)
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

        // ===== Layers/collisions (optional helpers) =====
        void FlipToInvisibleLayer()
        {
            if (invisibleLayer < 0) return; // optional

            var root = gameObject;
            originalLayer = root.layer;
            SetLayerRecursively(root.transform, invisibleLayer);

            // optionally ignore enemies layer collisions
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
