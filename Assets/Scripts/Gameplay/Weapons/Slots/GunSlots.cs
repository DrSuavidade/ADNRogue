using UnityEngine;
using System;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Progression;

namespace Geneforge.Gameplay.Weapons.Slots
{
    public enum SlotKind { Primary, Secondary }

    [Serializable]
    public class GunSlot
    {
        [SerializeField] SlotKind kind = SlotKind.Secondary;
        [SerializeField] AnimalEssence essence;
        public SlotKind Kind => kind;
        public AnimalEssence Essence => essence;
        public bool IsEmpty => essence == null;
        public GunSlot() { }
        public GunSlot(SlotKind k) { kind = k; }
        public void Set(AnimalEssence e) => essence = e;
        public void Clear() => essence = null;
    }

    /// <summary>
    /// Lives on the gun (e.g., same GameObject as PlayerController or a Weapon root).
    /// Manages 1 primary slot + 3 secondary slots. Later, we’ll make this apply
    /// abilities/modifiers to bullets and weapon stats.
    /// </summary>
    public class GunSlots : MonoBehaviour
    {
        [Header("Slots")]
        [SerializeField] GunSlot primary = new GunSlot(SlotKind.Primary);

        [SerializeField]
        GunSlot[] secondaries = new GunSlot[]
        {
        new GunSlot(SlotKind.Secondary),
        new GunSlot(SlotKind.Secondary),
        new GunSlot(SlotKind.Secondary),
        };

        [Header("Progression")]
        [SerializeField] EssenceProgression progression;
        public GunSlot Primary => primary;
        public GunSlot[] Secondaries => secondaries;
        public event Action<AnimalEssence> OnPrimaryChanged;
        public event Action OnSecondariesChanged;
        WeaponStats _cachedActive;
        [SerializeField] private GameObject abilityOwnerOverride;

        private EssenceAbility _wiredAbility;
        private GameObject AbilityOwner =>
            abilityOwnerOverride != null
                ? abilityOwnerOverride
                : (GetComponentInParent<Geneforge.Gameplay.Characters.Player.PlayerController>()?.gameObject ?? gameObject);


        [SerializeField] private WeaponStats baseStatsAsset;

        public WeaponStats ActiveStats => _cachedActive;

        void Awake()
        {
#if UNITY_EDITOR
            if (progression == null)
                progression = FindAnyObjectByType<EssenceProgression>();
#endif

            if (_cachedActive == null && baseStatsAsset != null)
                _cachedActive = BuildActiveStats(baseStatsAsset);

            WirePrimary(primary?.Essence?.specialAbility);
        }


        // --- Assign/Clear ---
        public bool TrySetPrimary(AnimalEssence e)
        {
            if (primary.Essence == e) return false;
            var nextAbility = e ? e.specialAbility : null;
            primary.Set(e);
            RebuildActive();
            WirePrimary(nextAbility);
            OnPrimaryChanged?.Invoke(e);
            return true;
        }

        public bool ClearPrimary()
        {
            if (primary.IsEmpty) return false;
            primary.Clear();
            RebuildActive();
            WirePrimary(null);
            OnPrimaryChanged?.Invoke(null);
            return true;
        }

        public bool TrySetSecondary(int index, AnimalEssence e)
        {
            if (!IsValidIndex(index)) return false;
            if (secondaries[index].Essence == e) return false;
            secondaries[index].Set(e);
            RebuildActive();
            OnSecondariesChanged?.Invoke();
            return true;
        }

        public bool ClearSecondary(int index)
        {
            if (!IsValidIndex(index) || secondaries[index].IsEmpty) return false;
            secondaries[index].Clear();
            RebuildActive();
            OnSecondariesChanged?.Invoke();
            return true;
        }

        public void ClearAll()
        {
            primary.Clear();
            for (int i = 0; i < secondaries.Length; i++) secondaries[i].Clear();
            RebuildActive();
            OnPrimaryChanged?.Invoke(null);
            OnSecondariesChanged?.Invoke();
        }

        public void OnAboutToFire()
        {
            if (_cachedActive == null && baseStatsAsset != null)
                _cachedActive = BuildActiveStats(baseStatsAsset);

            _wiredAbility?.OnAboutToFire(_cachedActive);
        }


        private void WirePrimary(EssenceAbility next)
        {
            if (_wiredAbility != null)
            {
                _wiredAbility.OnPrimaryUnequipped(AbilityOwner);

                if (Application.isPlaying)
                    Destroy(_wiredAbility);
                else
                    DestroyImmediate(_wiredAbility);

                _wiredAbility = null;
            }

            if (next == null) return;

            _wiredAbility = Instantiate(next);

            if (progression != null && primary?.Essence != null)
            {
                var ups = progression.GetActiveAbilityUpgrades(primary.Essence).ToArray();
                if (ups.Length > 0)
                    _wiredAbility.ApplyUpgrades(ups);
            }

            _wiredAbility.OnPrimaryEquipped(AbilityOwner, _cachedActive);
        }


        public void OnFireHeldStart()
        {
            _wiredAbility?.OnFireHeldStart();
        }

        public void OnFireHeldStop()
        {
            _wiredAbility?.OnFireHeldStop();
        }


        void OnDisable()
        {
            WirePrimary(null);
        }


        // --- Utilities ---
        public bool SwapSecondary(int a, int b)
        {
            if (!IsValidIndex(a) || !IsValidIndex(b) || a == b) return false;
            var tmp = secondaries[a].Essence;
            secondaries[a].Set(secondaries[b].Essence);
            secondaries[b].Set(tmp);
            RebuildActive();
            OnSecondariesChanged?.Invoke();
            return true;
        }

        public (SlotKind kind, int index) Find(AnimalEssence e)
        {
            if (primary.Essence == e) return (SlotKind.Primary, 0);
            for (int i = 0; i < secondaries.Length; i++)
                if (secondaries[i].Essence == e) return (SlotKind.Secondary, i);
            return (SlotKind.Secondary, -1);
        }

        bool IsValidIndex(int i) => i >= 0 && i < secondaries.Length;

        public void ApplyToBullet(Bullet b)
        {
            if (b == null || _cachedActive == null) return;

            // stats for this shot
            b.ApplyRuntimeStats(_cachedActive);

            // bind the runtime ability instance (if any)
            if (_wiredAbility != null)
            {
                b.BindAbility(_wiredAbility, _cachedActive);
            }
        }


        [SerializeField, HideInInspector]
        private System.Collections.Generic.List<StatModifier> permanentPassives = new System.Collections.Generic.List<StatModifier>();

        public void AddPassive(StatModifier mod)
        {
            permanentPassives.Add(mod);
            RebuildActive();
        }

        public WeaponStats BuildActiveStats(WeaponStats baseStats)
        {
            if (baseStats == null) return null;
            var ws = baseStats.CloneRuntime();

            // Apply secondaries (base mods + unlocked node mods)
            foreach (var s in secondaries)
            {
                var e = s?.Essence;
                e?.ApplyTo(ws); // base essence mods
                if (e != null && progression != null)
                {
                    var mods = progression.GetActiveStatMods(e);
                    foreach (var m in mods) WeaponStatApplier.Apply(ws, m);
                }
            }

            // Apply primary last (base mods + unlocked node mods)
            var p = primary?.Essence;
            p?.ApplyTo(ws);
            if (p != null && progression != null)
            {
                var mods = progression.GetActiveStatMods(p);
                foreach (var m in mods) WeaponStatApplier.Apply(ws, m);
            }

            // Apply permanent passives (from Reward Items)
            if (permanentPassives.Count > 0)
            {
                WeaponStatApplier.ApplyAll(ws, permanentPassives);
            }

            _cachedActive = ws;
            return ws;
        }

        void RebuildActive()
        {
            if (baseStatsAsset != null)
                _cachedActive = BuildActiveStats(baseStatsAsset);

            if (_wiredAbility != null)
                _wiredAbility.OnPrimaryEquipped(AbilityOwner, _cachedActive);
        }
    }
}