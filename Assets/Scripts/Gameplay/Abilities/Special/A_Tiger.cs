using UnityEngine;
using System.Collections;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Characters.Enemies;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Tiger - Rend")]
public class A_TigerRend : EssenceAbility
{
    [Header("Rend Stacks")]
    [Min(1)] public int stacksToBleed = 5;
    [Tooltip("If no hits for this long, stacks slowly decay to 0.")]
    public float stackExpireSeconds = 6f;

    [Header("Bleed")]
    public float bleedDps = 1.2f;
    public float bleedDuration = 4f;

    [Header("Shred (bonus dmg from your shots)")]
    [Range(0f, 1f)] public float shredPerStack = 0.1f;
    public int maxShredStacks = 5;
    public float shredDuration = 6f;

    [Header("VFX")]
    public Color bleedFlashColor = new Color(0.85f, 0f, 0f, 1f);
    public float bleedFlashDuration = 0.05f;

    [Header("Stack HUD")]
    public float hudHeightOffset = 1.6f;
    public float hudPipSize = 0.12f;
    public float hudPipSpacing = 0.1f;
    public Color hudFilled = new Color(0.95f, 0.25f, 0.25f, 1f);
    public Color hudEmpty  = new Color(0.25f, 0.25f, 0.25f, 0.5f);

    public override void OnHitEnemy(Bullet bullet, Enemy enemy, WeaponStats stats)
    {
        if (!enemy) return;

        // --- Shred (as before) ---
        var sh = enemy.GetComponent<RendShredStatus>();
        if (!sh) sh = enemy.gameObject.AddComponent<RendShredStatus>();
        sh.Apply(shredPerStack, maxShredStacks, shredDuration);

        // Optional immediate bonus based on shred stacks
        float bonus = sh.CurrentBonusMultiplier;
        if (bonus > 0f && stats != null)
            enemy.TakeDamage(stats.damage * bonus, false);

        // --- Rend stacks + HUD ---
        var rs = enemy.GetComponent<RendStacksStatus>();
        if (!rs) rs = enemy.gameObject.AddComponent<RendStacksStatus>();
        rs.Setup(this, enemy);     // pass config + link HUD
        rs.AddStack(1);            // +1 per hit
    }

    // ====================== REND STACKS + HUD ======================
    class RendStacksStatus : MonoBehaviour
    {
        A_TigerRend def;
        Enemy enemy;
        int stacks;
        float lastHitTime;

        TigerStackHUD hud;

        public void Setup(A_TigerRend ability, Enemy e)
        {
            def = ability; enemy = e;
            if (!hud)
            {
                hud = gameObject.GetComponent<TigerStackHUD>();
                if (!hud) hud = gameObject.AddComponent<TigerStackHUD>();
                hud.Init(def);
            }
            hud.SetVisible(false);
        }

        public void AddStack(int amount)
        {
            if (!def || !enemy) return;

            // Decay if expired
            if (def.stackExpireSeconds > 0f && Time.time - lastHitTime > def.stackExpireSeconds)
                stacks = 0;

            lastHitTime = Time.time;

            // Increment and cap (we’ll overflow to trigger bleed below)
            stacks += amount;
            if (stacks < 0) stacks = 0;

            // Update HUD
            hud.UpdateHUD(stacks, def.stacksToBleed, enemy);

            // Trigger bleed on threshold, then reset stacks (distinctive vs Poison)
            if (stacks >= def.stacksToBleed)
            {
                stacks = 0;
                hud.UpdateHUD(stacks, def.stacksToBleed, enemy); // reset HUD immediately

                // Start/refresh bleed
                var bleed = enemy.GetComponent<RendBleedStatus>();
                if (!bleed) bleed = enemy.gameObject.AddComponent<RendBleedStatus>();
                bleed.Begin(def, def.bleedDps, def.bleedDuration);
            }
        }

        void Update()
        {
            if (!def || !enemy) return;

            // Passive decay toward 0 when idle
            if (def.stackExpireSeconds > 0f && stacks > 0 && Time.time - lastHitTime > def.stackExpireSeconds)
            {
                stacks = 0;
                hud.UpdateHUD(stacks, def.stacksToBleed, enemy);
            }

            // Keep HUD anchored above head, facing camera
            hud.TickAnchor(enemy);
        }

        void OnDestroy()
        {
            if (hud) Destroy(hud);
        }
    }

    // Simple world-space 5-pip HUD that faces camera
    class TigerStackHUD : MonoBehaviour
    {
        A_TigerRend def;
        GameObject root;
        GameObject[] pips;
        float cachedHeight = -1f;

        static readonly string ShaderName = "Sprites/Default";

        public void Init(A_TigerRend ability)
        {
            def = ability;
            root = new GameObject("Tiger_StacksHUD");
            root.transform.SetParent(transform, false);

            int max = Mathf.Max(1, def.stacksToBleed);
            pips = new GameObject[max];
            for (int i = 0; i < max; i++)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "pip_" + i;
                quad.transform.SetParent(root.transform, false);
                quad.transform.localScale = Vector3.one * def.hudPipSize;
                var mr = quad.GetComponent<MeshRenderer>();
                mr.sharedMaterial = new Material(Shader.Find(ShaderName));
                SetColor(mr, def.hudEmpty);
                // Remove collider from primitive
                var col = quad.GetComponent<Collider>(); if (col) Destroy(col);
                pips[i] = quad;
            }
        }

        public void SetVisible(bool v)
        {
            if (!root) return;
            root.SetActive(v);
        }

        public void UpdateHUD(int stacks, int maxStacks, Enemy enemy)
        {
            if (!root || pips == null || pips.Length == 0) return;

            // First call: compute anchor height once
            if (cachedHeight < 0f)
                cachedHeight = EstimateHeight(enemy);

            // Layout pips centered
            float totalWidth = (pips.Length - 1) * def.hudPipSpacing;
            for (int i = 0; i < pips.Length; i++)
            {
                float x = -totalWidth * 0.5f + i * def.hudPipSpacing;
                pips[i].transform.localPosition = new Vector3(x, 0f, 0f);
                var mr = pips[i].GetComponent<MeshRenderer>();
                SetColor(mr, i < stacks ? def.hudFilled : def.hudEmpty);
            }

            // Show only if we have stacks > 0
            SetVisible(stacks > 0);

            // Anchor now
            TickAnchor(enemy);
        }

        public void TickAnchor(Enemy enemy)
        {
            if (!root || !enemy) return;
            Vector3 pos = enemy.transform.position + Vector3.up * (cachedHeight > 0f ? cachedHeight : EstimateHeight(enemy));
            pos.y += def.hudHeightOffset; // extra offset
            root.transform.position = pos;

            var cam = Camera.main;
            if (cam) root.transform.rotation = Quaternion.LookRotation(-cam.transform.forward, cam.transform.up);
        }

        float EstimateHeight(Enemy enemy)
        {
            // Try collider bounds
            float h = 1.6f;
            var cc = enemy.GetComponent<CharacterController>();
            if (cc) h = cc.height;
            else
            {
                var col = enemy.GetComponent<Collider>();
                if (col) h = Mathf.Max(1f, col.bounds.size.y);
            }
            return h;
        }

        void SetColor(Renderer r, Color c)
        {
            if (!r) return;
            var m = r.sharedMaterial;
            if (!m) return;
            int baseColor = Shader.PropertyToID("_BaseColor");
            int colorProp = m.HasProperty(baseColor) ? baseColor : Shader.PropertyToID("_Color");
            if (m.HasProperty(colorProp)) m.SetColor(colorProp, c);
        }

        void OnDestroy()
        {
            if (root) Destroy(root);
        }
    }

    // ====================== BLEED STATUS (unchanged + flash) ======================
    class RendBleedStatus : MonoBehaviour
    {
        A_TigerRend def;
        float dps; 
        float endAt; 
        bool ticking;

        public void Begin(A_TigerRend ability, float _dps, float duration)
        {
            def = ability;
            dps = _dps; 
            endAt = Time.time + duration;
            if (!ticking) StartCoroutine(Tick());
        }

        IEnumerator Tick()
        {
            ticking = true;
            const float interval = 0.5f;
            while (Time.time < endAt && this && gameObject)
            {
                var e = GetComponent<Enemy>();
                if (e)
                {
                    // Damage tick
                    e.TakeDamage(dps * interval, false);

                    // Blood flash VFX
                    var flash = e.GetComponent<BleedFlash>();
                    if (!flash) flash = e.gameObject.AddComponent<BleedFlash>();
                    flash.Trigger(def != null ? def.bleedFlashDuration : 0.05f,
                                  def != null ? def.bleedFlashColor    : new Color(0.85f,0f,0f,1f));
                }

                yield return new WaitForSeconds(interval);
            }
            ticking = false;
            Destroy(this);
        }
    }

    class RendShredStatus : MonoBehaviour
    {
        int stacks;
        int max;
        float perStack;
        float duration;
        float expireAt;
        bool active;

        public float CurrentBonusMultiplier => Mathf.Max(0f, stacks * perStack);

        public void Apply(float _perStack, int _max, float _duration)
        {
            perStack = _perStack; max = _max; duration = _duration;

            stacks = Mathf.Min(max, stacks + 1);
            expireAt = Time.time + duration;

            if (!active) StartCoroutine(Life());
        }

        IEnumerator Life()
        {
            active = true;
            while (Time.time < expireAt) yield return null;
            Destroy(this);
        }

        void OnDestroy() { active = false; }
    }

    // --- Tiny helper: flash red on bleed tick, then restore ---
    class BleedFlash : MonoBehaviour
    {
        static readonly int _ColorID     = Shader.PropertyToID("_Color");
        static readonly int _BaseColorID = Shader.PropertyToID("_BaseColor");
        Coroutine co;

        public void Trigger(float duration, Color flashColor)
        {
            if (co != null) StopCoroutine(co);
            co = StartCoroutine(Flash(duration, flashColor));
        }

        IEnumerator Flash(float duration, Color flashColor)
        {
            var rends = GetComponentsInChildren<Renderer>(true);
            if (rends == null || rends.Length == 0) yield break;

            var originals = new System.Collections.Generic.List<(Material mat, Color col, int prop)>();
            foreach (var r in rends)
            {
                var mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i]; if (!m) continue;
                    int prop = m.HasProperty(_BaseColorID) ? _BaseColorID :
                               (m.HasProperty(_ColorID) ? _ColorID : -1);
                    if (prop < 0) continue;
                    originals.Add((m, m.GetColor(prop), prop));
                    m.SetColor(prop, flashColor);
                }
            }

            yield return new WaitForSeconds(Mathf.Max(0.01f, duration));

            foreach (var tuple in originals)
                if (tuple.mat) tuple.mat.SetColor(tuple.prop, tuple.col);

            co = null;
        }

        void OnDisable() { if (co != null) { StopCoroutine(co); co = null; } }
        void OnDestroy() { if (co != null) { StopCoroutine(co); co = null; } }
    }
}
