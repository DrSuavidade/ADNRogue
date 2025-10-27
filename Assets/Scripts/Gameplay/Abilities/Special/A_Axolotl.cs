//need deep changes

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
    public float cloneRadius = 1.2f;
    public float followLerp  = 15f;
    public float cloneScale  = 1f;

    public override void OnPrimaryEquipped(GameObject owner, WeaponStats baseline)
    {
        var rt = owner.GetComponent<SplitRuntime>();
        if (!rt) rt = owner.AddComponent<SplitRuntime>();
        rt.Configure(this, owner);
    }

    public override void OnPrimaryUnequipped(GameObject owner)
    {
        var rt = owner.GetComponent<SplitRuntime>();
        if (rt) Destroy(rt);
    }

    // -------- Runtime on the real player --------
    class SplitRuntime : MonoBehaviour
    {
        A_AxolotlMitoticSplit def;
        PlayerController player;
        RunStats run;

        struct Clone
        {
            public GameObject go;
            public Transform  muzzle;
        }

        List<Clone> clones = new List<Clone>();
        Transform ownerMuzzle; // owner firePoint local pose snapshot

        void OnDestroy()
        {
            if (player) player.OnFired -= OnOwnerFired;
            ClearClones();
        }

        public void Configure(A_AxolotlMitoticSplit d, GameObject owner)
        {
            def = d;
            player = owner.GetComponent<PlayerController>();
            run    = owner.GetComponent<RunStats>();
            if (!player || !run) { Debug.LogWarning("Axolotl needs PlayerController + RunStats on owner."); return; }

            ownerMuzzle = player.firePoint; // public in your controller
            player.OnFired -= OnOwnerFired;
            player.OnFired += OnOwnerFired;

            RebuildClones(DesiredCloneCount());
        }

        void Update()
        {
            if (!def || !player || !run) return;

            // Re-evaluate thresholds
            int want = DesiredCloneCount();
            if (want != clones.Count) RebuildClones(want);

            // Move/rotate clones around player
            UpdateCloneTransforms();
        }

        int DesiredCloneCount()
        {
            if (run.maxHP <= 0f) return 0;
            float f = run.currentHP / run.maxHP;
            // >50% -> 0; 20–50% -> 1; ≤20% -> 3 (so total bodies: 1, 2, 4)
            return (f <= 0.2f) ? 3 : (f <= 0.5f ? 1 : 0);
        }

        void RebuildClones(int desired)
        {
            // remove extras
            for (int i = clones.Count - 1; i >= desired; i--) DestroyClone(i);

            // add missing
            while (clones.Count < desired) CreateClone();
        }

        void DestroyClone(int index)
        {
            if (index < 0 || index >= clones.Count) return;
            var c = clones[index];
            if (c.go) Destroy(c.go);
            clones.RemoveAt(index);
        }

        void CreateClone()
        {
            // Duplicate the player object so visuals match exactly
            var src = player.gameObject;
            var cloneGO = Instantiate(src);
            cloneGO.name = "AxolotlClone";
            cloneGO.transform.localScale = Vector3.one * def.cloneScale;

            // Strip gameplay: colliders, rigidbodies; disable all scripts
            foreach (var col in cloneGO.GetComponentsInChildren<Collider>(true)) Destroy(col);
            foreach (var rb in cloneGO.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
            foreach (var mb in cloneGO.GetComponentsInChildren<MonoBehaviour>(true))
            {
                // disable every script on the clone (visual-only)
                if (mb) mb.enabled = false;
            }

            // Add our marker and a muzzle child with same local pose as owner firePoint
            var marker = cloneGO.AddComponent<CloneMarker>();
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(cloneGO.transform, false);
            if (ownerMuzzle)
            {
                muzzle.localPosition = ownerMuzzle.localPosition;
                muzzle.localRotation = ownerMuzzle.localRotation;
            }
            marker.muzzle = muzzle;

            clones.Add(new Clone { go = cloneGO, muzzle = muzzle });
        }

        void ClearClones()
        {
            for (int i = 0; i < clones.Count; i++)
                if (clones[i].go) Destroy(clones[i].go);
            clones.Clear();
        }

        void UpdateCloneTransforms()
        {
            if (clones.Count == 0) return;

            int n = clones.Count;
            float baseAngle = (n == 1) ? 180f : 0f;

            for (int i = 0; i < n; i++)
            {
                float ang = baseAngle + (360f / Mathf.Max(1, n)) * i;
                float rad = ang * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
                Vector3 target = player.transform.position + dir * def.cloneRadius;

                var c = clones[i];
                if (!c.go) continue;

                c.go.transform.position = Vector3.Lerp(c.go.transform.position, target, Time.deltaTime * def.followLerp);
                c.go.transform.rotation = player.transform.rotation;
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

        // tiny marker to hold the muzzle
        class CloneMarker : MonoBehaviour { public Transform muzzle; }
    }
}
