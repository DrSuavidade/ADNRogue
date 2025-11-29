using UnityEngine;
using System.Collections;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Characters.Enemies;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Wolf - Twin Fangs")]
public class A_WolfTwinFangs : EssenceAbility
{
    [Header("Extra Hits")]
    [Min(1)] public int extraHitCount = 1;               // 1 = double-tap
    [Range(0f, 2f)] public float extraHitDamageFactor = 0.7f;
    public float firstExtraDelay = 0.06f;
    public float betweenExtrasDelay = 0.06f;
    public float extraHitKnockback = 0f;

    [Header("VFX: Closing Mouth")]
    public bool  showMouthVFX   = true;
    public Color mouthColor     = new Color(1f, 1f, 1f, 0.95f);
    public float mouthDuration  = 0.10f;   // total close+fade time
    public float mouthWidth     = 0.7f;    // world width of the mouth
    public float mouthOpenGap   = 0.5f;    // initial gap between jaws
    public float toothDepth     = 0.18f;   // point length toward the center
    public float mouthHeightOff = 1.0f;    // height above enemy pivot

    public override void OnHitEnemy(Bullet bullet, Enemy enemy, WeaponStats stats)
    {
        if (!enemy || !bullet) return;
        if (extraHitCount <= 0 || extraHitDamageFactor <= 0f) return;

        float baseAtImpact = Mathf.Max(0f, bullet.damage);
        Vector3 impactPos = bullet.transform.position;

        enemy.StartCoroutine(DoExtraHits(enemy, baseAtImpact, impactPos));
    }

    IEnumerator DoExtraHits(Enemy target, float baseAtImpact, Vector3 impactPos)
    {
        if (firstExtraDelay > 0f)
            yield return new WaitForSeconds(firstExtraDelay);

        for (int i = 0; i < extraHitCount; i++)
        {
            if (!target) yield break;

            // Damage
            float dmg = baseAtImpact * extraHitDamageFactor;
            target.TakeDamage(dmg, false);

            if (extraHitKnockback > 0f)
            {
                Vector3 dir = (target.transform.position - impactPos); dir.y = 0f;
                if (dir.sqrMagnitude > 1e-4f) target.ApplyKnockback(dir.normalized, extraHitKnockback);
            }

            // VFX: closing mouth (two triangles top, two bottom)
            if (showMouthVFX) SpawnMouthVFX(target);

            if (i < extraHitCount - 1 && betweenExtrasDelay > 0f)
                yield return new WaitForSeconds(betweenExtrasDelay);
        }
    }

    void SpawnMouthVFX(Enemy target)
    {
        if (!target) return;
        var go = new GameObject("Wolf_MouthVFX");
        var runner = go.AddComponent<MouthVFXRunner>();
        runner.Init(target.transform, mouthDuration, mouthWidth, mouthOpenGap, toothDepth, mouthHeightOff, mouthColor);
    }

    // --- Runtime VFX: 2 triangles top + 2 triangles bottom, closing & fading ---
    class MouthVFXRunner : MonoBehaviour
    {
        Transform follow;
        float dur, t, width, openGap, depth, heightOff;
        Color col;

        MeshFilter topMF, botMF;
        MeshRenderer topMR, botMR;

        public void Init(Transform target, float duration, float mouthWidth, float mouthOpenGap, float toothDepth, float heightOffset, Color color)
        {
            follow    = target;
            dur       = Mathf.Max(0.05f, duration);
            width     = Mathf.Max(0.05f, mouthWidth);
            openGap   = Mathf.Max(0.01f, mouthOpenGap);
            depth     = Mathf.Max(0.01f, toothDepth);
            heightOff = heightOffset;
            col       = color;

            // Create top/bottom objects
            var top = new GameObject("TopJaw"); top.transform.SetParent(transform, false);
            var bot = new GameObject("BottomJaw"); bot.transform.SetParent(transform, false);

            topMF = top.AddComponent<MeshFilter>();
            botMF = bot.AddComponent<MeshFilter>();
            topMR = top.AddComponent<MeshRenderer>();
            botMR = bot.AddComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Sprites/Default"));
            topMR.material = mat; botMR.material = new Material(mat);
            topMR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            topMR.receiveShadows = false;
            botMR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            botMR.receiveShadows = false;

            // First frame build
            UpdatePose(0f);
        }

        void Update()
        {
            if (!follow) { Destroy(gameObject); return; }

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur); // 0..1

            UpdatePose(k);

            // Fade alpha
            float a = (1f - k) * col.a;
            var cTop = topMR.material.color; cTop.a = a; topMR.material.color = cTop;
            var cBot = botMR.material.color; cBot.a = a; botMR.material.color = cBot;

            if (k >= 1f) Destroy(gameObject);
        }

        void UpdatePose(float k)
        {
            // Place at enemy + height, face camera (full billboard)
            var cam = Camera.main;
            Vector3 pos = follow.position + Vector3.up * heightOff;
            transform.position = pos;

            if (cam)
                transform.rotation = Quaternion.LookRotation(-cam.transform.forward, cam.transform.up);

            // Current gap as it closes
            float gap = Mathf.Lerp(openGap, 0f, k);
            float halfW = width * 0.5f;

            // Build top and bottom meshes (two triangles each), on XY plane in local space
            topMF.sharedMesh = BuildJawMesh(halfW, +gap * 0.5f, +1f);
            botMF.sharedMesh = BuildJawMesh(halfW, -gap * 0.5f, -1f);

            // keep color
            topMR.material.color = col;
            botMR.material.color = col;
        }

        // upDir = +1 for top (teeth point downward), -1 for bottom (teeth point upward)
        Mesh BuildJawMesh(float halfWidth, float yBase, float upDir)
        {
            // Two isosceles triangles side-by-side to suggest "teeth"
            // Left tooth: base from -halfWidth to 0 at yBase, apex toward center by 'depth'
            // Right tooth: base from 0 to +halfWidth at yBase, apex toward center by 'depth'
            var m = new Mesh();

            Vector3 L  = new Vector3(-halfWidth, yBase, 0f);
            Vector3 C  = new Vector3(0f, yBase, 0f);
            Vector3 R  = new Vector3(+halfWidth, yBase, 0f);
            Vector3 AL = new Vector3(-halfWidth * 0.5f, yBase - depth * upDir, 0f); // left apex
            Vector3 AR = new Vector3(+halfWidth * 0.5f, yBase - depth * upDir, 0f); // right apex

            // 2 triangles: (L, C, AL) and (C, R, AR)
            m.vertices  = new[] { L, C, AL,  C, R, AR };
            m.triangles = new[] { 0,1,2,  3,4,5 };
            m.RecalculateNormals();
            return m;
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
                case "Wolf/ExtraHitCount":
                    extraHitCount = Mathf.Max(1, Mathf.RoundToInt(ApplyNumeric(extraHitCount, u)));
                    break;

                case "Wolf/ExtraHitDamageFactor":
                    extraHitDamageFactor = Mathf.Clamp(ApplyNumeric(extraHitDamageFactor, u), 0f, 3f);
                    break;

                case "Wolf/FirstExtraDelay":
                    firstExtraDelay = Mathf.Max(0f, ApplyNumeric(firstExtraDelay, u));
                    break;

                case "Wolf/BetweenExtrasDelay":
                    betweenExtrasDelay = Mathf.Max(0f, ApplyNumeric(betweenExtrasDelay, u));
                    break;

                case "Wolf/ExtraHitKnockback":
                    extraHitKnockback = Mathf.Max(0f, ApplyNumeric(extraHitKnockback, u));
                    break;

                case "Wolf/MouthDuration":
                    mouthDuration = Mathf.Max(0.01f, ApplyNumeric(mouthDuration, u));
                    break;
            }
        }
    }

}
