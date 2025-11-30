using UnityEngine;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Prehistoric
{
    [RequireComponent(typeof(Enemy))]
    public class PrehistoricTammer : PrehistoricEnemyAbilityBase
    {
        [Header("Summoning")]
        public GameObject raptorPrefab;
        public Transform[] spawnPoints;
        public int maxActiveRaptors = 4;

        int _activeRaptors;

        public void AnimEvent_SummonRaptor()
        {
            if (!raptorPrefab || spawnPoints == null || spawnPoints.Length == 0)
                return;

            if (_activeRaptors >= maxActiveRaptors)
                return;

            var sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
            var obj = Object.Instantiate(raptorPrefab, sp.position, sp.rotation);

            _activeRaptors++;

            var raptorEnemy = obj.GetComponent<EnemyCore>();
            if (raptorEnemy != null)
                raptorEnemy.OnDied += HandleRaptorDied;
        }

        void HandleRaptorDied()
        {
            _activeRaptors = Mathf.Max(0, _activeRaptors - 1);
        }
    }
}
