using UnityEngine;
using System.Collections;
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

    [Header("Tongue tug (first shot after invis)")]
    public float tetherDuration = 0.6f;
    public float pullForce = 15f;

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
        float lastHP;
        bool invisible;
        Coroutine timer;

        public void Configure(A_ChameleonCamouflage d, GameObject owner)
        {
            def = d;
            run = owner.GetComponent<RunStats>();
            rends = owner.GetComponentsInChildren<Renderer>(true);
            lastHP = run ? run.currentHP : -1f;
            SetVisible(true);
        }

        void Update()
        {
            if (!run) return;
            // detect damage
            if (lastHP >= 0f && run.currentHP < lastHP - 1e-4f)
            {
                BeginInvis();
            }
            lastHP = run.currentHP;
        }

        void BeginInvis()
        {
            if (timer != null) StopCoroutine(timer);
            s_armed = true; // arm the next shot
            SetVisible(false);
            invisible = true;
            timer = StartCoroutine(InvisTimer());
        }

        public void EndInvis()
        {
            if (!invisible) return;
            if (timer != null) StopCoroutine(timer);
            invisible = false; s_armed = false;
            SetVisible(true);
        }

        IEnumerator InvisTimer()
        {
            yield return new WaitForSeconds(def.invisDuration);
            EndInvis();
        }

        void OnDestroy() { SetVisible(true); }

        void SetVisible(bool v)
        {
            if (rends == null) return;
            for (int i = 0; i < rends.Length; i++) if (rends[i]) rends[i].enabled = v;
        }
    }
}
