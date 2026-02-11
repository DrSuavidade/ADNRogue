using UnityEngine;

namespace Geneforge.Gameplay.Environment
{
    public class FogPositionUpdater : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private string shaderVariableName = "_PlayerPos";

        private void Update()
        {
            if (playerTransform == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) playerTransform = player.transform;
            }

            if (playerTransform != null)
            {
                Shader.SetGlobalVector(shaderVariableName, playerTransform.position);
            }
        }
    }
}
