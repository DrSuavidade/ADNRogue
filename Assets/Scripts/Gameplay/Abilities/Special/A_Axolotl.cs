// Assets/Scripts/Abilities/A_Axolotl.cs
using UnityEngine;
using System.Collections.Generic;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Core.Stats;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Axolotl - Mitotic Split")]
public class A_AxolotlMitoticSplit : EssenceAbility
{
    [Header("Layout")]
    public float cloneRadius = 1.2f;   // orbit radius
    public float cloneScale  = 1f;     // visual scale of clones

    public override void OnPrimaryEquipped(GameObject owner, WeaponStats baseline)
    {
        var rt = owner.GetComponent<SplitRuntime>();
        if (!rt) rt = owner.AddComponent<SplitRuntime>();
        rt.Configure(this, owner);
    }

    public override void OnPrimaryUnequipped(GameObject owner)
    {
        var rt = owner.GetComponent<SplitRuntime>();
        if (rt) Object.Destroy(rt);
    }

    // -------- Runtime on the real player --------
    class SplitRuntime : MonoBehaviour
    {
        A_AxolotlMitoticSplit def;
        PlayerController player;
        RunStats run;

        Animator ownerAnim;
        Transform ownerFirePoint;

        struct Clone
        {
            public GameObject go;
            public Transform  muzzle;
            public Animator   anim;
            public Vector3    localOffset; // ring offset relative to player
        }

        readonly List<Clone> clones = new List<Clone>();

        void OnDestroy()
        {
            if (player) player.OnFired -= OnOwnerFired;
            ClearClones();
        }

        public void Configure(A_AxolotlMitoticSplit d, GameObject owner)
        {
            def    = d;
            player = owner.GetComponent<PlayerController>();
            run    = owner.GetComponent<RunStats>();
            if (!player || !run) { Debug.LogWarning("Axolotl needs PlayerController + RunStats on owner."); return; }

            ownerAnim     = player.GetComponentInChildren<Animator>(true);
            ownerFirePoint = player.firePoint;

            player.OnFired -= OnOwnerFired;
            player.OnFired += OnOwnerFired;

            RebuildClones(DesiredCloneCount());
        }

        void Update()
        {
            if (!def || !player || !run) return;

            int want = DesiredCloneCount();
            if (want != clones.Count) RebuildClones(want);

            UpdateCloneTransformsAndAnim();
        }

        int DesiredCloneCount()
        {
            if (run == null || run.MaxHP <= 0f) return 0;
            float f = run.CurrentHP / run.MaxHP;
            // >50% -> 0; 20–50% -> 1; ≤20% -> 3 (so total bodies: 1, 2, 4)
            return (f <= 0.2f) ? 3 : (f <= 0.5f ? 1 : 0);
        }

        void RebuildClones(int desired)
        {
            // remove extras
            for (int i = clones.Count - 1; i >= desired; i--) DestroyClone(i);

            // add missing
            while (clones.Count < desired) CreateClone(clones.Count, desired);
        }

        void DestroyClone(int index)
        {
            if (index < 0 || index >= clones.Count) return;
            var c = clones[index];
            if (c.go) Destroy(c.go);
            clones.RemoveAt(index);
        }

        void CreateClone(int idx, int totalAfterCreate)
        {
            // Duplicate the player so visuals match exactly
            var visualRoot = ownerAnim != null ? ownerAnim.gameObject : player.gameObject;
            var cloneGO = Instantiate(visualRoot);
            cloneGO.name = "AxolotlClone";
            var originalLocal = visualRoot.transform.localScale;
            var mul = def.cloneScale;
            cloneGO.transform.localScale = new Vector3(
                originalLocal.x * mul,
                originalLocal.y * mul,
                originalLocal.z * mul
            );

            // Parent to player so it moves WITH the player 1:1
            cloneGO.transform.SetParent(player.transform, false);

            // Strip gameplay: colliders, rigidbodies; disable ALL scripts EXCEPT Animator
            foreach (var col in cloneGO.GetComponentsInChildren<Collider>(true)) Destroy(col);
            foreach (var rb in cloneGO.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
            foreach (var comp in cloneGO.GetComponentsInChildren<Component>(true))
            {
                // Keep Animators and Transforms running for visuals
                if (comp is Animator) continue;
                if (comp is Transform) continue;
                // Disable MonoBehaviour scripts (if any)
                var mb = comp as MonoBehaviour;
                if (mb != null) mb.enabled = false;
            }

            // Grab the clone animator
            var cloneAnim = cloneGO.GetComponentInChildren<Animator>(true);

            // Add our marker muzzle with the same local pose as owner's firePoint
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(cloneGO.transform, false);
            if (ownerFirePoint)
            {
                muzzle.localPosition = ownerFirePoint.localPosition;
                muzzle.localRotation = ownerFirePoint.localRotation;
            }

            // Precompute ring local offset
            Vector3 localOffset = ComputeRingOffset(idx, totalAfterCreate);

            clones.Add(new Clone { go = cloneGO, muzzle = muzzle, anim = cloneAnim, localOffset = localOffset });
        }

        Vector3 ComputeRingOffset(int i, int n)
        {
            if (n <= 0) return Vector3.zero;
            float baseAngle = (n == 1) ? 180f : 0f;
            float ang = baseAngle + (360f / Mathf.Max(1, n)) * i;
            float rad = ang * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * def.cloneRadius;
        }

        void ClearClones()
        {
            for (int i = 0; i < clones.Count; i++)
                if (clones[i].go) Destroy(clones[i].go);
            clones.Clear();
        }

        void UpdateCloneTransformsAndAnim()
        {
            if (clones.Count == 0) return;

            for (int i = 0; i < clones.Count; i++)
            {
                var c = clones[i];
                if (!c.go) continue;

                // Recompute ring offset based on *current* clone count
                Vector3 offsetNow = ComputeRingOffset(i, clones.Count);

                c.go.transform.localPosition = offsetNow;
                c.go.transform.localRotation = Quaternion.identity;

                // Mirror animator params from owner
                MirrorAnimatorParams(ownerAnim, c.anim);
            }
        }


        // Robust animator parameter mirroring (floats, ints, bools, triggers)
        void MirrorAnimatorParams(Animator src, Animator dst)
        {
            if (!src || !dst) return;

            var ps = src.parameters;
            for (int i = 0; i < ps.Length; i++)
            {
                var p = ps[i];
                switch (p.type)
                {
                    case AnimatorControllerParameterType.Float:
                        dst.SetFloat(p.nameHash, src.GetFloat(p.nameHash));
                        break;
                    case AnimatorControllerParameterType.Int:
                        dst.SetInteger(p.nameHash, src.GetInteger(p.nameHash));
                        break;
                    case AnimatorControllerParameterType.Bool:
                        dst.SetBool(p.nameHash, src.GetBool(p.nameHash));
                        break;
                    case AnimatorControllerParameterType.Trigger:
                        // If the source has a trigger set this frame, re-fire it on the clone
                        // (simple heuristic: if src is in a state with that tag, or use custom bridging)
                        // Here we naively forward rising edges: clear+set every frame the source is set.
                        if (src.GetBool(p.nameHash)) { dst.ResetTrigger(p.nameHash); dst.SetTrigger(p.nameHash); }
                        break;
                }
            }

            // Optionally sync state time to avoid drift:
            var s0 = src.GetCurrentAnimatorStateInfo(0);
            var d0 = dst.GetCurrentAnimatorStateInfo(0);
            if (s0.shortNameHash == d0.shortNameHash)
            {
                // keep normalized time roughly in step
                dst.Play(s0.shortNameHash, 0, s0.normalizedTime % 1f);
            }
        }

        // Mirror fire: when the owner fires, spawn volleys from each clone’s muzzle
        void OnOwnerFired(WeaponStats active)
        {
            if (clones.Count == 0 || !player) return;
            for (int i = 0; i < clones.Count; i++)
            {
                var m = clones[i].muzzle;
                if (m) player.FireOnceFrom(m, active);
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
                case "Split/CloneRadius":
                    cloneRadius = Mathf.Max(0f, ApplyNumeric(cloneRadius, u));
                    break;

                case "Split/CloneScale":
                    cloneScale = Mathf.Max(0.1f, ApplyNumeric(cloneScale, u));
                    break;
            }
        }
    }
}
