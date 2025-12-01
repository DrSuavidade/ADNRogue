namespace Geneforge.Gameplay.Characters.Enemies
{
    public interface IEnemy
    {
        float MaxHealth { get; }
        float CurrentHealth { get; }
        void TakeDamage(float amount, bool wasCrit = false);
        UnityEngine.Transform transform { get; }
    }
}
