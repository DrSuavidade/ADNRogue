using UnityEngine;
using System;


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


    public GunSlot Primary => primary;
    public GunSlot[] Secondaries => secondaries;


    public event Action<AnimalEssence> OnPrimaryChanged;
    public event Action OnSecondariesChanged;

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


    // --- Hook points for later ---
    public void ApplyToBullet(Bullet b)
    {
        // Placeholder: primary essence will add on-hit abilities (e.g., chain lightning)
        // and secondaries will tweak damage/size/etc. in a follow-up pass.
    }


    public void ApplyToWeaponStats(WeaponStats stats)
    {
        // Placeholder: e.g., additive multipliers or fire rate tweaks from secondaries
    }
}