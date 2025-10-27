using UnityEngine;
using System.Collections;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Characters.Enemies;
using UnityEngine.AI;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Bee - Honeycomb")]
public class A_BeeHoneycomb : EssenceAbility
{
    [Header("Sticky Stacks")]
    public float slowPerStack = 0.07f; // 7% per stack
    public int   maxStacks = 6;
    public float stackDuration = 4f;

    [Header("Pop (at max stacks)")]
    public float rootDuration = 1.25f;
    public float cooldownAfterPop = 10f; // NEW: no stacking during this time

    [Header("Puddle")]
    public float puddleRadius = 3f;
    public float puddleDuration = 4f;
    public float puddleSlow = 0.35f;

    [Header("Stack Indicator (hex wedges)")]
    public bool   showIndicator = true;
    public float indicatorHeightFactor = 0.9f;
    public float  indicatorHeightOffset = 0.15f;
    public float indicatorRadius = 0.4f;
    public float  indicatorWorldScale = 1f;
    public Color  wedgeColor = new Color(1f, 0.85f, 0.2f, 0.85f); // honey yellow
    public Color  wedgeOffColor = new Color(1f, 1f, 1f, 0.08f);   // faint/off

    public override void OnHitEnemy(Bullet bullet, Enemy enemy, WeaponStats stats)
    {
        var st = enemy.GetComponent<StickyStatus>();
        if (!st) st = enemy.gameObject.AddComponent<StickyStatus>();
        st.Apply(this, enemy);
    }

    // --- Status on enemies ---
    public class StickyStatus : MonoBehaviour
    {
        A_BeeHoneycomb def;
        Enemy enemy;
        NavMeshAgent agent;

        int stacks = 0;
        float[] expiry; // ring buffer per stack

        bool rooted; float rootEnd;

        // cooldown state
        bool  onCooldown = false;
        float cooldownEndsAt = 0f;

        // indicator
        Transform indicator;
        MeshRenderer[] wedgeRenderers; // length 6
        bool indicatorVisible = false;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        public void Apply(A_BeeHoneycomb d, Enemy e)
        {
            def = d; enemy = e;
            if (expiry == null || expiry.Length != def.maxStacks) expiry = new float[def.maxStacks];

            // if we're cooling down, ignore new stacks
            if (onCooldown)
            {
                if (Time.time >= cooldownEndsAt) { onCooldown = false; } else { return; }
            }

            // add/refresh a stack
            if (stacks < def.maxStacks) stacks++;
            // put/refresh the newest stack expiry
            expiry[stacks - 1] = Time.time + def.stackDuration;

            RecomputeSlow();
            UpdateIndicator();

            // reached cap -> root + puddle + POP stacks + start cooldown
            if (stacks >= def.maxStacks)
            {
                StartCoroutine(RootCoroutine(def.rootDuration));
                SpawnPuddle();
                PopStacks(); // clear stacks right away
                StartCooldown(def.cooldownAfterPop);
            }
        }

        void Update()
        {
            if (indicator) UpdateIndicatorTransform();
            // expire stacks
            if (stacks > 0 && !onCooldown)
            {
                int before = stacks;
                float now = Time.time;
                for (int i = stacks - 1; i >= 0; i--)
                {
                    if (expiry[i] <= now)
                    {
                        // shift down
                        for (int j = i; j < stacks - 1; j++) expiry[j] = expiry[j + 1];
                        stacks--;
                    }
                }
                if (stacks != before)
                {
                    RecomputeSlow();
                    UpdateIndicator();
                }
            }

            // end root
            if (rooted && Time.time >= rootEnd) Unroot();

            // end cooldown (indicator will re-enable as we stack again)
            if (onCooldown && Time.time >= cooldownEndsAt)
            {
                onCooldown = false;
                UpdateIndicator(); // show empty hex ready to re-stack
            }

            // keep indicator billboarded
            if (indicator) BillboardFull(indicator);
        }

        void RecomputeSlow()
        {
            if (!agent) return;
            // derive base so we play nice with other slow sources
            float totalSlow = Mathf.Clamp01(stacks * def.slowPerStack);
            float baseSpeed = agent.speed / Mathf.Max(0.01f, 1f - totalSlow);
            agent.speed = baseSpeed * (1f - totalSlow);
        }

        IEnumerator RootCoroutine(float dur)
        {
            Root();
            yield return new WaitForSeconds(dur);
            Unroot();
        }

        void Root()
        {
            if (agent) agent.isStopped = true;
            rooted = true; rootEnd = Time.time + def.rootDuration;
        }

        void Unroot()
        {
            rooted = false;
            if (agent) agent.isStopped = false;
        }

        void PopStacks()
        {
            stacks = 0;
            if (expiry != null) System.Array.Clear(expiry, 0, expiry.Length);
            RecomputeSlow();
            UpdateIndicator();
        }

        void StartCooldown(float seconds)
        {
            onCooldown = true;
            cooldownEndsAt = Time.time + Mathf.Max(0f, seconds);
            SetIndicatorVisible(false);
        }

        void SpawnPuddle()
        {
            var go = new GameObject("HoneyPuddle");
            go.transform.position = transform.position;
            var rt = go.AddComponent<HoneyPuddleRuntime>();
            rt.radius = def.puddleRadius;
            rt.slow = def.puddleSlow;
            rt.duration = def.puddleDuration;
        }

        // ------------------- Indicator -------------------
        void EnsureIndicator()
        {
            if (!def.showIndicator) return;
            if (indicator) return;

            indicator = new GameObject("BeeStacks_Hex").transform;
            indicator.SetParent(transform, false);
            indicatorVisible = true;

            // Create wedges (world-space sized)
            wedgeRenderers = new MeshRenderer[6];
            for (int i = 0; i < 6; i++)
            {
                var w = new GameObject("Wedge_" + i);
                w.transform.SetParent(indicator, false);

                var mf = w.AddComponent<MeshFilter>();
                var mr = w.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                mf.sharedMesh = BuildHexWedgeMesh(def.indicatorRadius, i);
                mr.material = BuildWedgeMaterial(def.wedgeOffColor);
                wedgeRenderers[i] = mr;
            }

            // First placement/scale
            UpdateIndicatorTransform(force:true);
            SetIndicatorVisible(false); // start hidden until we get stacks/cooldown
        }

        void UpdateIndicator()
        {
            if (!def.showIndicator) return;
            EnsureIndicator();

            // Show only while we actually have stacks.
            bool shouldShow = stacks > 0;                 // <— no longer shows during cooldown
            SetIndicatorVisible(shouldShow);
            if (!shouldShow) return;

            // Light up [0..stacks-1], dim the rest
            for (int i = 0; i < 6; i++)
                if (wedgeRenderers[i])
                    SetMatColor(wedgeRenderers[i], (i < stacks) ? def.wedgeColor : def.wedgeOffColor);
        }


        // Keep it facing camera on Y, and maintain world scale/height (no squashing).
        void UpdateIndicatorTransform(bool force = false)
        {
            if (!indicator) return;

            // Height from enemy’s actual bounds
            float h = GetEnemyHeight();
            float y = h * Mathf.Max(0f, def.indicatorHeightFactor) + def.indicatorHeightOffset;
            indicator.localPosition = new Vector3(0f, y, 0f);

            // Compensate parent lossy scale so the hex stays perfect in world space
            Vector3 ls = transform.lossyScale;
            Vector3 inv = new Vector3(
                (ls.x != 0f) ? 1f / ls.x : 1f,
                (ls.y != 0f) ? 1f / ls.y : 1f,
                (ls.z != 0f) ? 1f / ls.z : 1f
            );
            float s = (def.indicatorWorldScale <= 0f) ? 1f : def.indicatorWorldScale;
            indicator.localScale = inv * s;

            BillboardFull(indicator);
        }

        void BillboardFull(Transform t)
        {
            var cam = Camera.main;
            if (!cam) return;
            // Make the indicator’s forward face the camera
            t.rotation = Quaternion.LookRotation(-cam.transform.forward, cam.transform.up);
        }

        void SetIndicatorVisible(bool vis)
        {
            if (!indicator) return;
            if (indicatorVisible == vis) return;
            indicatorVisible = vis;
            indicator.gameObject.SetActive(vis);
        }

        float GetEnemyHeight()
        {
            // Prefer CharacterController / Capsule, else renderers/colliders bounds
            var cc = GetComponent<CharacterController>();
            if (cc) return cc.height * transform.lossyScale.y;

            var cap = GetComponent<CapsuleCollider>();
            if (cap) return cap.height * transform.lossyScale.y;

            Bounds? b = null;
            var rends = GetComponentsInChildren<Renderer>();
            for (int i = 0; i < rends.Length; i++)
                b = b.HasValue ? Encaps(b.Value, rends[i].bounds) : rends[i].bounds;

            if (!b.HasValue)
            {
                var cols = GetComponentsInChildren<Collider>();
                for (int i = 0; i < cols.Length; i++)
                    b = b.HasValue ? Encaps(b.Value, cols[i].bounds) : cols[i].bounds;
            }

            return b.HasValue ? b.Value.size.y : 2f;

            static Bounds Encaps(Bounds a, Bounds add) { a.Encapsulate(add); return a; }
        }



        Mesh BuildHexWedgeMesh(float r, int wedgeIndex)
        {
            // center + two adjacent vertices on a pointy-top hex, drawn on XY plane (z = 0)
            var m = new Mesh();
            Vector3 c  = Vector3.zero;
            Vector3 v0 = HexPoint(r, wedgeIndex);
            Vector3 v1 = HexPoint(r, (wedgeIndex + 1) % 6);

            m.vertices  = new[] { c, v0, v1 };
            m.triangles = new[] { 0, 1, 2 };
            m.uv        = new[] { new Vector2(0.5f,0.5f), new Vector2(1f,0.5f), new Vector2(0.5f,1f) };
            m.RecalculateNormals();
            return m;
        }

        Vector3 HexPoint(float r, int i)
        {
            // Pointy-top hex, start at "12 o’clock", go clockwise. On XY plane.
            // Angle offset so a flat edge faces camera nicely.
            float angle = (i / 6f) * Mathf.PI * 2f - Mathf.PI / 6f;
            return new Vector3(Mathf.Sin(angle) * r, Mathf.Cos(angle) * r, 0f);
        }


        Material BuildWedgeMaterial(Color c)
        {
            var sh = Shader.Find("Sprites/Default");
            var mat = new Material(sh);
            mat.color = c;
            return mat;
        }

        void SetMatColor(MeshRenderer mr, Color c)
        {
            if (!mr) return;
            // instance so coloring this enemy’s wedges doesn’t affect others
            if (!mr.material || mr.sharedMaterial == mr.material) mr.material = new Material(mr.material);
            mr.material.color = c;
        }

        void OnDestroy()
        {
            // restore agent if needed
            if (agent) agent.isStopped = false;

            if (indicator) Destroy(indicator.gameObject);
        }
    }

    // --- Simple slowing puddle ---
    public class HoneyPuddleRuntime : MonoBehaviour
    {
        public float radius = 3f;
        public float slow = 0.35f;
        public float duration = 4f;

        SphereCollider col;
        void Awake()
        {
            col = gameObject.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = radius;

            // simple visual (flat disc)
            var lr = gameObject.AddComponent<LineRenderer>();
            lr.useWorldSpace = false; lr.loop = true; lr.positionCount = 48; lr.widthMultiplier = 0.05f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            Vector3[] pts = new Vector3[lr.positionCount];
            for (int i = 0; i < pts.Length; i++)
            {
                float t = i / (float)pts.Length * Mathf.PI * 2f;
                pts[i] = new Vector3(Mathf.Cos(t) * radius, 0f, Mathf.Sin(t) * radius);
            }
            lr.SetPositions(pts);

            Destroy(gameObject, duration);
        }

        void OnTriggerEnter(Collider other)
        {
            var agent = other.GetComponent<NavMeshAgent>();
            if (!agent) return;
            StartCoroutine(ApplySlow(agent));
        }

        IEnumerator ApplySlow(NavMeshAgent agent)
        {
            if (!agent) yield break;
            float baseSpeed = agent.speed;
            agent.speed = baseSpeed * (1f - Mathf.Clamp01(slow));
            yield return new WaitForSeconds(duration);
            if (agent) agent.speed = baseSpeed;
        }
    }
}
