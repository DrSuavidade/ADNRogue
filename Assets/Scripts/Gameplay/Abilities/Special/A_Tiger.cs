using UnityEngine;
using System.Collections;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Characters.Enemies;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Tiger - Rend")]
public class A_TigerRend : EssenceAbility
{
    [Header("Bleed")]
    public float bleedDps = 1.2f;
    public float bleedDuration = 4f;

    [Header("Shred (increases damage from this ability's bullets)")]
    [Range(0f, 1f)] public float shredPerStack = 0.1f;   // +10% per stack
    public int maxStacks = 5;
    public float shredDuration = 6f;                    // refreshes on add

    public override void OnHitEnemy(Bullet bullet, Enemy enemy, WeaponStats stats)
    {
        if (!enemy) return;

        // Apply/refresh bleed
        var bleed = enemy.GetComponent<RendBleedStatus>();
        if (!bleed) bleed = enemy.gameObject.AddComponent<RendBleedStatus>();
        bleed.Begin(bleedDps, bleedDuration);

        // Apply/refresh shred
        var sh = enemy.GetComponent<RendShredStatus>();
        if (!sh) sh = enemy.gameObject.AddComponent<RendShredStatus>();
        sh.Apply(shredPerStack, maxStacks, shredDuration);

        // Bonus hit now based on shred stacks (affects this shot too)
        float bonus = sh.CurrentBonusMultiplier; // e.g., stacks*shredPerStack
        if (bonus > 0f)
            enemy.TakeDamage(stats.damage * bonus, false);
    }

    // --- statuses ---
    class RendBleedStatus : MonoBehaviour
    {
        float dps; float endAt; bool ticking;

        public void Begin(float _dps, float duration)
        {
            dps = _dps; endAt = Time.time + duration;
            if (!ticking) StartCoroutine(Tick());
        }

        IEnumerator Tick()
        {
            ticking = true;
            while (Time.time < endAt && this && gameObject)
            {
                var e = GetComponent<Enemy>();
                if (e) e.TakeDamage(dps * 0.5f, false); // tick every 0.5s
                yield return new WaitForSeconds(0.5f);
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
}
