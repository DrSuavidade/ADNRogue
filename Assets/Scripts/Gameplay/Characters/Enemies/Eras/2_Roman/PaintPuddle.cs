using UnityEngine;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Roman
{
    public class PaintPuddle : MonoBehaviour
    {
        public float lifetime = 4f;

        public void Init(Color color)
        {
            // 1. Tenta pintar se for um Sprite
            var sprite = GetComponent<SpriteRenderer>();
            if (sprite != null)
            {
                sprite.color = color;
            }

            // 2. Tenta pintar o material (Quad ou Mesh)
            var renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                // Criamos uma instância única para não pintar todos os objetos da mesma cor
                Material mat = renderer.material;
                mat.color = color;
                
                // Suporte para Shaders URP (_BaseColor)
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", color);

                // Forçar o uso de MaterialPropertyBlock (é melhor para performance)
                var propBlock = new MaterialPropertyBlock();
                propBlock.SetColor("_Color", color);
                propBlock.SetColor("_BaseColor", color);
                renderer.SetPropertyBlock(propBlock);
            }

            // Garantir que a escala é visível (removido LeanTween para evitar erros)
            transform.localScale = Vector3.one;

            // Destrói após o tempo
            Destroy(gameObject, lifetime);
        }
    }
}
