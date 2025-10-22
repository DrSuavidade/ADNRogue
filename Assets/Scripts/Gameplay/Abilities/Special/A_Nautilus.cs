using UnityEngine;
using System.Collections;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Characters.Enemies;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Nautilus - Shell & Surge")]
public class A_NautilusShellSurge : EssenceAbility
{
    [Header("Surge")]
    public float surgeInterval = 5f;
    public float surgeRadius = 5.5f;
    public float surgeKnockback = 10f;

    [Header("Shell")]
    public float shellCooldown = 30f;

    public override void OnPrimaryEquipped(GameObject owner, WeaponStats activeStats)
    {
        var host = owner.GetComponent<NautilusRuntime>();
        if (!host) host = owner.AddComponent<NautilusRuntime>();
        host.Configure(this, owner);
    }

    public override void OnPrimaryUnequipped(GameObject owner)
    {
        var host = owner.GetComponent<NautilusRuntime>();
        if (host) Destroy(host);
    }

    // Runtime component living on the player/gun while primary
    public class NautilusRuntime : MonoBehaviour
    {
        A_NautilusShellSurge def;
        Transform owner;
        Coroutine surgeLoop;

        // Shell state
        bool shellReady = true;

        // PlayerHealth optional hook
        Component playerHealth;
        float lastHealth = -1f;

        public void Configure(A_NautilusShellSurge d, GameObject own)
        {
            def = d; owner = own.transform;
            if (surgeLoop != null) StopCoroutine(surgeLoop);
            surgeLoop = StartCoroutine(SurgeLoop());

            // Try to find PlayerHealth (optional)
            playerHealth = own.GetComponent("PlayerHealth");
            lastHealth = GetHealth();
        }

        void Update()
        {
            if (!shellReady) { lastHealth = GetHealth(); return; }

            float h = GetHealth();
            if (h >= 0f && lastHealth >= 0f && h < lastHealth)
            {
                // Damage detected -> negate and trigger cooldown
                float delta = lastHealth - h;
                Heal(delta);
                shellReady = false;
                StartCoroutine(ShellCooldown());
            }
            lastHealth = h;
        }

        IEnumerator SurgeLoop()
        {
            var wait = new WaitForSeconds(Mathf.Max(0.1f, def.surgeInterval));
            while (true)
            {
                Vector3 p = owner.position;
                var cols = Physics.OverlapSphere(p, def.surgeRadius, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < cols.Length; i++)
                {
                    var e = cols[i].GetComponent<Enemy>();
                    if (e == null) continue;
                    Vector3 dir = (e.transform.position - p); dir.y = 0f;
                    if (dir.sqrMagnitude > 0.001f)
                        e.ApplyKnockback(dir.normalized, def.surgeKnockback);
                }
                yield return wait;
            }
        }

        IEnumerator ShellCooldown()
        {
            yield return new WaitForSeconds(def.shellCooldown);
            shellReady = true;
        }

        // --- Minimal reflection helpers to work with your PlayerHealth without changes ---
        float GetHealth()
        {
            if (playerHealth == null) return -1f;

            var t = playerHealth.GetType();
            var f = t.GetField("currentHealth") ?? t.GetField("health") ?? t.GetField("hp");
            if (f != null) return (float)f.GetValue(playerHealth);

            var p = t.GetProperty("CurrentHealth") ?? t.GetProperty("Health");
            if (p != null) return (float)p.GetValue(playerHealth);

            return -1f;
        }

        void Heal(float amt)
        {
            if (playerHealth == null) return;

            var t = playerHealth.GetType();
            var m = t.GetMethod("Heal", new[] { typeof(float) });
            if (m != null) { m.Invoke(playerHealth, new object[] { amt }); return; }

            // fallback: set the field back
            var f = t.GetField("currentHealth") ?? t.GetField("health") ?? t.GetField("hp");
            if (f != null)
            {
                float newVal = Mathf.Max((float)f.GetValue(playerHealth), 0f) + amt;
                f.SetValue(playerHealth, newVal);
            }
        }
    }
}
