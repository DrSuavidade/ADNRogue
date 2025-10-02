using UnityEngine;

[CreateAssetMenu(menuName = "Geneforge/WeaponStats")]
public class WeaponStats : ScriptableObject
{
    [Header("Combat")]
    [Tooltip("Seconds between shots")]
    public float fireRate = 0.25f;
    [Tooltip("Units per second")]
    public float projectileSpeed = 20f;
    [Tooltip("Hit points dealt on impact")]
    public float damage = 1f;
    [Tooltip("Scale multiplier for the projectile mesh")]
    public float projectileSize = 1f;
    [Tooltip("Impulse force applied to enemies on hit")]
    public float knockbackForce = 5f;

    [Tooltip("Chance to land a critical hit (0 to 1)")]
    [Range(0f, 1f)] public float critChance = 0f;

    [Tooltip("Damage multiplier applied on a critical hit")]
    public float critMultiplier = 2f;

    // Basic upgrade methods
    public void UpgradeFireRate(float delta) => fireRate = Mathf.Max(0.05f, fireRate - delta);
    public void UpgradeProjectileSpeed(float delta) => projectileSpeed += delta;
    public void UpgradeDamage(float delta) => damage += delta;
    public void UpgradeProjectileSize(float delta) => projectileSize = Mathf.Max(0.1f, projectileSize + delta);
    public void UpgradeKnockback(float delta) => knockbackForce = Mathf.Max(0f, knockbackForce + delta);
    public void UpgradeCritChance(float delta)    => critChance    = Mathf.Clamp01(critChance + delta);
    public void UpgradeCritMultiplier(float delta) => critMultiplier = Mathf.Max(1f, critMultiplier + delta);
}
