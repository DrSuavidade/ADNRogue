using UnityEngine;
using System;
using System.Linq;
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

        void Awake()
        {
            if (progression == null) progression = FindAnyObjectByType<EssenceProgression>();
        }

        // --- Assign/Clear ---
        public bool TrySetPrimary(AnimalEssence e)
        {
            if (primary.Essence == e) return false;
            primary.Set(e);
            OnPrimaryChanged?.Invoke(e);
            return true;
        }


        public bool ClearPrimary()
        {
            if (primary.IsEmpty) return false;
            primary.Clear();
            OnPrimaryChanged?.Invoke(null);
            return true;
        }


        public bool TrySetSecondary(int index, AnimalEssence e)
        {
            if (!IsValidIndex(index)) return false;
            if (secondaries[index].Essence == e) return false;
            secondaries[index].Set(e);
            OnSecondariesChanged?.Invoke();
            return true;
        }


        public bool ClearSecondary(int index)
        {
            if (!IsValidIndex(index) || secondaries[index].IsEmpty) return false;
            secondaries[index].Clear();
            OnSecondariesChanged?.Invoke();
            return true;
        }


        public void ClearAll()
        {
            primary.Clear();
            for (int i = 0; i < secondaries.Length; i++) secondaries[i].Clear();
            OnPrimaryChanged?.Invoke(null);
            OnSecondariesChanged?.Invoke();
        }

        // --- Utilities ---
        public bool SwapSecondary(int a, int b)
        {
            if (!IsValidIndex(a) || !IsValidIndex(b) || a == b) return false;
            var tmp = secondaries[a].Essence;
            secondaries[a].Set(secondaries[b].Essence);
            secondaries[b].Set(tmp);
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

            b.ApplyRuntimeStats(_cachedActive);

            var ability = primary?.Essence?.specialAbility;
            if (ability != null)
            {
                AbilityUpgrade[] ups = null;
                if (progression != null && primary?.Essence != null)
                    ups = progression.GetActiveAbilityUpgrades(primary.Essence).ToArray();
                b.BindAbility(ability, _cachedActive, ups);
            }
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

            _cachedActive = ws;
            return ws;
        }
    }
}