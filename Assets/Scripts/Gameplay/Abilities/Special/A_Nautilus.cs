using UnityEngine;
using System.Collections;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Characters.Enemies;
using Geneforge.Core.Stats; // RunStats


[CreateAssetMenu(menuName = "Geneforge/Abilities/Nautilus - Shell & Surge")]
public class A_Nautilus : EssenceAbility
{
    [Header("Surge")]
    public float surgeInterval = 5f;
    public float surgeRadius   = 6f;
    public float surgeDamage   = 8f;

    [Header("Surge VFX")]
    public bool  showSurgeVFX     = true;
    public float ringDuration     = 0.35f;
    public float ringLineWidth    = 0.06f;
    public int   ringSegments     = 64;
    public Color ringColor        = new Color(0.6f, 0.9f, 1f, 0.9f);

    [Header("Shell")]
    public float shellCooldown    = 30f;

    [Header("Shell VFX")]
    public bool  showShellSphere  = true;
    public float shellRadius      = 0.6f; // visual only
    public Material shellMaterial;        // optional; if null we create a transparent one
    public Color shellColor       = new Color(0.6f, 0.9f, 1f, 0.25f);

    // --- lifecycle from GunSlots ---
    public override void OnPrimaryEquipped(GameObject owner, Geneforge.Gameplay.Weapons.Stats.WeaponStats snapshot)
    {
        var rt = owner.GetComponent<NautilusRunner>();
        if (!rt) rt = owner.AddComponent<NautilusRunner>();
        rt.Boot(this, owner);
    }

    public override void OnPrimaryUnequipped(GameObject owner)
    {
        var rt = owner.GetComponent<NautilusRunner>();
        if (rt) Object.Destroy(rt);
    }

    // ------------------------------------------------------------------------
    // Runtime component attached to the (resolved) player root
    // ------------------------------------------------------------------------
    class NautilusRunner : MonoBehaviour
    {
        A_Nautilus def;
        Transform  root;             // player root (where PlayerHealth lives)
        GameObject shellViz;
        bool shellReady = true;
        RunStats run;
        float lastHealth = -1f;
        Coroutine loop;

        public void Boot(A_Nautilus d, GameObject ownerGO)
        {
            def = d;
            root = ResolvePlayerRoot(ownerGO.transform);
            run = root.GetComponent<RunStats>();
            lastHealth = (run != null) ? run.CurrentHP : -1f;

            // VFX: shell sphere (only when ready)
            EnsureShellSphere();
            SetShellVisible(def.showShellSphere && shellReady);

            // Kick the surge loop (immediate ping, then every interval)
            if (loop != null) StopCoroutine(loop);
            loop = StartCoroutine(SurgeLoop());
        }

        Transform ResolvePlayerRoot(Transform start)
        {
            var t = start;
            while (t != null)
            {
                if (t.GetComponent("PlayerHealth") != null) return t;
                t = t.parent;
            }
            return start; // fallback
        }

        void OnDestroy()
        {
            if (loop != null) StopCoroutine(loop);
            if (shellViz) Destroy(shellViz);
        }

        void Update()
        {
            if (run == null) return;                // no HP source found
            float h = run.CurrentHP;                // current HP

            if (shellReady && lastHealth >= 0f && h < lastHealth)
            {
                // Block this hit: immediately heal back the lost HP
                float delta = lastHealth - h;
                if (delta > 0f) run.Heal(delta);

                // Consume shell and start cooldown
                shellReady = false;
                SetShellVisible(false);
                StartCoroutine(RestoreShellAfter(def.shellCooldown));
                // Debug.Log($"Nautilus shell blocked {delta} damage");
            }

            // track after potential heal
            lastHealth = run.CurrentHP;
        }


        IEnumerator SurgeLoop()
        {
            // immediate surge so you see it right away
            DoSurge();

            var wait = new WaitForSeconds(Mathf.Max(0.1f, def.surgeInterval));
            while (true)
            {
                yield return wait;
                DoSurge();
            }
        }

        void DoSurge()
        {
            Vector3 p = root.position;

            // Damage everyone in radius
            var cols = Physics.OverlapSphere(p, def.surgeRadius, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < cols.Length; i++)
            {
                var e = cols[i].GetComponent<Enemy>();
                if (!e) continue;
                e.TakeDamage(def.surgeDamage, false);
            }

            // Ring VFX
            if (def.showSurgeVFX)
                SpawnRing(p, def.surgeRadius, def.ringDuration, def.ringLineWidth, def.ringSegments, def.ringColor);
        }

        IEnumerator RestoreShellAfter(float seconds)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, seconds));
            shellReady = true;
            SetShellVisible(def.showShellSphere);
        }

        // ------------------------------ VFX -----------------------------------
        void EnsureShellSphere()
        {
            if (!def.showShellSphere || shellViz) return;

            shellViz = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shellViz.name = "Nautilus_Shell_VFX";
            Destroy(shellViz.GetComponent<Collider>()); // visual only
            shellViz.transform.SetParent(root, false);
            shellViz.transform.localPosition = Vector3.zero;

            // Keep world radius regardless of parent scale
            float worldD = def.shellRadius * 2f;
            Vector3 pl = root.lossyScale;
            Vector3 local = new Vector3(
                pl.x > 1e-5f ? worldD / pl.x : worldD,
                pl.y > 1e-5f ? worldD / pl.y : worldD,
                pl.z > 1e-5f ? worldD / pl.z : worldD
            );
            shellViz.transform.localScale = local;

            var r = shellViz.GetComponent<Renderer>();
            Material mat = def.shellMaterial;
            if (mat == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("Standard");
                mat = new Material(sh != null ? sh : Shader.Find("Sprites/Default"));
            }
            // set color on common prop
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", def.shellColor);
            else if (mat.HasProperty("_Color")) mat.color = def.shellColor;

            r.material = new Material(mat); // instance
        }

        void SetShellVisible(bool on)
        {
            if (!shellViz) EnsureShellSphere();
            if (shellViz) shellViz.SetActive(on);
        }

        static void SpawnRing(Vector3 center, float targetRadius, float life, float width, int segments, Color color)
        {
            var go = new GameObject("Nautilus_SurgeRing_VFX");
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.positionCount = Mathf.Max(16, segments);
            lr.widthMultiplier = width;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = color; lr.endColor = color;

            go.AddComponent<RingExpander>().Init(center, targetRadius, life, lr);
        }

        class RingExpander : MonoBehaviour
        {
            Vector3 center;
            float targetR, life, t;
            LineRenderer lr;

            public void Init(Vector3 c, float r, float l, LineRenderer line)
            {
                center = c; targetR = Mathf.Max(0.01f, r); life = Mathf.Max(0.05f, l); lr = line;
            }

            void Update()
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / life);
                float r = Mathf.Lerp(0f, targetR, k);

                int n = lr.positionCount;
                for (int i = 0; i < n; i++)
                {
                    float a = (i / (float)n) * Mathf.PI * 2f;
                    lr.SetPosition(i, center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                }

                var c = lr.startColor; c.a = (1f - k) * c.a;
                lr.startColor = c; lr.endColor = c;

                if (k >= 1f) Destroy(gameObject);
            }
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
                case "Nautilus/SurgeInterval":
                    surgeInterval = Mathf.Max(0.1f, ApplyNumeric(surgeInterval, u));
                    break;

                case "Nautilus/SurgeRadius":
                    surgeRadius = Mathf.Max(0.1f, ApplyNumeric(surgeRadius, u));
                    break;

                case "Nautilus/SurgeDamage":
                    surgeDamage = Mathf.Max(0f, ApplyNumeric(surgeDamage, u));
                    break;

                case "Nautilus/ShellCooldown":
                    shellCooldown = Mathf.Max(0f, ApplyNumeric(shellCooldown, u));
                    break;
            }
        }
    }

}
