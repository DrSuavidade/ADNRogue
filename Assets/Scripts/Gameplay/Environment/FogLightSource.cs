using UnityEngine;

namespace Geneforge.Gameplay.Environment
{
    public class FogLightSource : MonoBehaviour
    {
        [Header("Fog Clearing Settings")]
        [Tooltip("O tamanho total do buraco no nevoeiro.")]
        public float Radius = 15f;
        
        [Tooltip("Quanto do centro deve estar 100% limpo (0 a 1).")]
        [Range(0f, 1f)]
        public float InnerStrength = 0.5f;

        private void Start()
        {
            // Tenta registar novamente no Start caso o Manager tenha demorado a carregar
            Register();
        }

        private void OnEnable() => Register();
        private void OnDisable() => Unregister();

        private void Register()
        {
            if (FogManager.Instance != null) FogManager.Instance.RegisterSource(this);
        }

        private void Unregister()
        {
            if (FogManager.Instance != null) FogManager.Instance.UnregisterSource(this);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.9f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, Radius);
            Gizmos.color = new Color(1f, 0.9f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, Radius * InnerStrength);
        }
    }
}
