using UnityEngine;
using Geneforge.Gameplay.Progression;

namespace Geneforge.Gameplay.Map
{
    public class BossController : MonoBehaviour
    {
        // Call this from your boss HP/death logic.
        public void OnBossDefeated()
        {
            if (RunFlowController.Instance != null)
            {
                RunFlowController.Instance.OnBossDefeated();
            }
        }
    }
}
