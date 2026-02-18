using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

namespace Geneforge.Core.UI
{
    public class SceneTransitionManager : MonoBehaviour
    {
        public static SceneTransitionManager Instance { get; private set; }

        private Canvas fadeCanvas;
        private CanvasGroup fadeGroup;
        private Image fadeImage;

        public static SceneTransitionManager Ensure()
        {
            if (Instance != null) return Instance;
            GameObject go = new GameObject("SceneTransitionManager");
            Instance = go.AddComponent<SceneTransitionManager>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            CreateFadeUI();
        }

        private void CreateFadeUI()
        {
            GameObject canvasGO = new GameObject("FadeCanvas");
            canvasGO.transform.SetParent(transform);
            
            fadeCanvas = canvasGO.AddComponent<Canvas>();
            fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            fadeCanvas.sortingOrder = 999;
            
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            
            fadeGroup = canvasGO.AddComponent<CanvasGroup>();
            fadeGroup.alpha = 0f;
            fadeGroup.interactable = false;
            fadeGroup.blocksRaycasts = false;

            GameObject imageGO = new GameObject("FadeImage");
            imageGO.transform.SetParent(canvasGO.transform);
            
            fadeImage = imageGO.AddComponent<Image>();
            fadeImage.color = Color.black;
            
            RectTransform rt = imageGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
        }

        public IEnumerator FadeOut(float duration)
        {
            float elapsed = 0f;
            fadeGroup.blocksRaycasts = true;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                fadeGroup.alpha = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
            fadeGroup.alpha = 1f;
        }

        public IEnumerator FadeIn(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                fadeGroup.alpha = Mathf.Clamp01(1f - (elapsed / duration));
                yield return null;
            }
            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;
        }

        public void StartSceneTransition(string sceneName, System.Action onComplete = null)
        {
            StartCoroutine(TransitionRoutine(sceneName, onComplete));
        }

        private IEnumerator TransitionRoutine(string sceneName, System.Action onComplete)
        {
            yield return StartCoroutine(FadeOut(0.25f));
            
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            while (!op.isDone)
            {
                yield return null;
            }
            
            onComplete?.Invoke();
            yield return StartCoroutine(FadeIn(0.25f));
        }
    }
}
