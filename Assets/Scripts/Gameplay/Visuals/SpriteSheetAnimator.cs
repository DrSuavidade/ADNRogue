using UnityEngine;

namespace Geneforge.Gameplay.Visuals
{
    public class SpriteSheetAnimator : MonoBehaviour
    {
        public enum AnimationMode { Billboard, Floor, Horizontal }
        
        [Header("Settings")]
        public AnimationMode mode = AnimationMode.Billboard;
        
        private SpriteRenderer _sr;
        private Sprite[] _frames;
        private float _fps;
        private float _timer;
        private int _currentIndex;

        public void Initialize(Sprite[] frames, float fps, AnimationMode animationMode)
        {
            _sr = GetComponent<SpriteRenderer>();
            
            // Se não conseguirmos adicionar ao objeto principal (provavelmente conflito com MeshRenderer)
            // Criamos um objeto filho para a animação
            if (_sr == null)
            {
                GameObject child = new GameObject("SpriteAnimation");
                child.transform.SetParent(this.transform, false);
                _sr = child.AddComponent<SpriteRenderer>();
            }
            
            _frames = frames;
            _fps = fps;
            mode = animationMode;
            _timer = 0f;
            _currentIndex = 0;

            if (_frames != null && _frames.Length > 0 && _sr != null)
            {
                _sr.sprite = _frames[0];
            }
        }

        private void Update()
        {
            if (_frames == null || _frames.Length == 0 || _sr == null) return;

            _timer += Time.deltaTime;
            float frameDuration = 1f / _fps;

            if (_timer >= frameDuration)
            {
                _timer -= frameDuration;
                _currentIndex = (_currentIndex + 1) % _frames.Length;
                _sr.sprite = _frames[_currentIndex];
            }
        }

        private void LateUpdate()
        {
            if (mode == AnimationMode.Billboard)
            {
                if (Camera.main != null)
                {
                    transform.LookAt(transform.position + Camera.main.transform.rotation * Vector3.forward,
                                     Camera.main.transform.rotation * Vector3.up);
                }
            }
            else if (mode == AnimationMode.Floor)
            {
                // Deita o sprite no chão do mundo (olhando para cima)
                transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }
            else if (mode == AnimationMode.Horizontal)
            {
                // Deita o sprite mas deixa-o rodar com o objeto (local)
                transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }
    }
}
