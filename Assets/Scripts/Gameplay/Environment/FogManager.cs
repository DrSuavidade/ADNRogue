using UnityEngine;
using System.Collections.Generic;

namespace Geneforge.Gameplay.Environment
{
    public class FogManager : MonoBehaviour
    {
        public static FogManager Instance;

        [Header("Settings")]
        [SerializeField] private Transform playerTransform;

        private List<FogLightSource> lightSources = new List<FogLightSource>();
        
        // Arrays para enviar ao shader (máximo 8 luzes extras)
        private Vector4[] lightPositions = new Vector4[8];
        private float[] lightRadii = new float[8];

        private void Awake()
        {
            Instance = this;
        }

        public void RegisterSource(FogLightSource source) => lightSources.Add(source);
        public void UnregisterSource(FogLightSource source) => lightSources.Remove(source);

        private void Update()
        {
            if (playerTransform == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) playerTransform = p.transform;
                return;
            }

            // 1. Enviar posição do Player
            Shader.SetGlobalVector("_PlayerPos", playerTransform.position);

            // 2. Coletar e enviar as 8 luzes mais próximas (ou as primeiras 8)
            for (int i = 0; i < 8; i++)
            {
                if (i < lightSources.Count && lightSources[i] != null)
                {
                    Vector3 pos = lightSources[i].transform.position;
                    lightPositions[i] = new Vector4(pos.x, pos.y, pos.z, 1); // W=1 significa ativo
                    lightRadii[i] = lightSources[i].Radius;
                }
                else
                {
                    lightPositions[i] = Vector4.zero; // W=0 desativado
                    lightRadii[i] = 0;
                }
            }

            Shader.SetGlobalVectorArray("_ExtraLightPos", lightPositions);
            Shader.SetGlobalFloatArray("_ExtraLightRadius", lightRadii);
        }
    }
}
