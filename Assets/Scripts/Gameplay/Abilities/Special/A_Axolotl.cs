using UnityEngine;
using System.Collections;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Core.Stats;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Axolotl - Mitotic Split")]
public class A_AxolotlMitoticSplit : EssenceAbility
{
    [Header("Damage Reduction")]
    [Range(0f, 0.9f)] public float drAt2x = 0.10f;
    [Range(0f, 0.9f)] public float drAt4x = 0.20f;

    [Header("Echo visuals")]
    public int echoRingCount2x = 2;
    public int echoRingCount4x = 4;
    public float orbitRadius = 0.8f;
    public float orbitSpeed = 120f; // deg/sec
    public float echoScale = 0.9f;

    static SplitRuntime s_rt;

    public override void OnPrimaryEquipped(GameObject owner, WeaponStats activeStats)
    {
        s_rt = owner.GetComponent<SplitRuntime>();
        if (!s_rt) s_rt = owner.AddComponent<SplitRuntime>();
        s_rt.Configure(this, owner);
    }

    public override void OnPrimaryUnequipped(GameObject owner)
    {
        if (s_rt) Object.Destroy(s_rt);
        s_rt = null;
    }

    // Runtime behaviour on the player
    public class SplitRuntime : MonoBehaviour
    {
        A_AxolotlMitoticSplit def;
        RunStats run;
        int stage = 1; // 1,2,4
        float lastHP;

        // echoes
        GameObject[] echoes;

        void OnDestroy() { ClearEchoes(); }

        public void Configure(A_AxolotlMitoticSplit d, GameObject owner)
        {
            def = d;
            run = owner.GetComponent<RunStats>();
            lastHP = run ? run.currentHP : -1f;
            UpdateStage();
        }

        void Update()
        {
            if (!run) return;

            UpdateStage();

            // “soft DR” by healing back a fraction of recent damage
            if (lastHP >= 0f && run.currentHP < lastHP)
            {
                float delta = lastHP - run.currentHP;
                float dr = (stage >= 4) ? def.drAt4x : (stage >= 2 ? def.drAt2x : 0f);
                if (dr > 0f)
                {
                    float heal = delta * dr / (1f - dr); // makes effective damage reduced by dr
                    run.currentHP = Mathf.Min(run.maxHP, run.currentHP + heal);
                }
            }
            lastHP = run.currentHP;

            // orbit echoes
            if (echoes != null && echoes.Length > 0)
            {
                float t = Time.time;
                for (int i = 0; i < echoes.Length; i++)
                {
                    if (!echoes[i]) continue;
                    float ang = (t * def.orbitSpeed + (360f / echoes.Length) * i) * Mathf.Deg2Rad;
                    Vector3 off = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * def.orbitRadius;
                    echoes[i].transform.position = transform.position + off;
                }
            }
        }

        void UpdateStage()
        {
            int newStage = 1;
            float frac = (run && run.maxHP > 0f) ? (run.currentHP / run.maxHP) : 1f;
            if (frac <= 0.2f) newStage = 4;
            else if (frac <= 0.5f) newStage = 2;

            if (newStage == stage) return;

            stage = newStage;
            RebuildEchoes();
        }

        void RebuildEchoes()
        {
            ClearEchoes();
            int count = (stage >= 4) ? def.echoRingCount4x : (stage >= 2 ? def.echoRingCount2x : 0);
            if (count <= 0) return;

            echoes = new GameObject[count];
            for (int i = 0; i < count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "AxolotlEcho";
                Object.Destroy(go.GetComponent<Collider>());
                go.transform.localScale = Vector3.one * def.echoScale;
                var r = go.GetComponent<Renderer>();
                if (r) r.material = new Material(Shader.Find("Standard")) { color = new Color(1f, 0.7f, 0.9f, 0.6f) };
                echoes[i] = go;
            }
        }

        void ClearEchoes()
        {
            if (echoes == null) return;
            for (int i = 0; i < echoes.Length; i++) if (echoes[i]) Object.Destroy(echoes[i]);
            echoes = null;
        }
    }
}
