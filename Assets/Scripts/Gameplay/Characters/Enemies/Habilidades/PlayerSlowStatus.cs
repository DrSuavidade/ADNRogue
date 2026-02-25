using UnityEngine;
using System.Collections;
using Geneforge.Gameplay.Progression;

namespace Geneforge.Gameplay.Characters.Enemies.Habilidades
{
    public class PlayerSlowStatus : MonoBehaviour
    {
        private float _amount;
        private float _expireAt;
        private bool _isActive;

        public void Apply(float slowAmount, float duration)
        {
            _amount = slowAmount; // e.g. -0.5f
            _expireAt = Time.time + duration;

            if (!_isActive)
            {
                StartCoroutine(SlowRoutine());
            }
        }

        private IEnumerator SlowRoutine()
        {
            _isActive = true;
            var run = RunSession.Instance?.Run;
            
            if (run != null)
            {
                run.ModifySpeed(_amount);
                Debug.Log($"<color=cyan><b>[SLOW]</b> Player abrandado em {_amount * 100}%!</color>");
            }

            while (Time.time < _expireAt)
            {
                yield return new WaitForSeconds(0.2f);
            }

            if (run != null)
            {
                run.ModifySpeed(-_amount);
                Debug.Log("<color=green><b>[SLOW]</b> Velocidade do Player restaurada.</color>");
            }

            _isActive = false;
            Destroy(this);
        }
    }
}
