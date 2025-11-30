using UnityEngine;
using Geneforge.Gameplay.Progression;

namespace Geneforge.Gameplay.Map
{
    public class BossController : MonoBehaviour
    {
        public void OnBossDefeated()
        {
            if (RunFlowController.Instance != null)
            {
                RunFlowController.Instance.OnBossDefeated();
            }
        }
    }
}
