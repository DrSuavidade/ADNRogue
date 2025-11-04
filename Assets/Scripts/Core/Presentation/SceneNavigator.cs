using UnityEngine;
using UnityEngine.SceneManagement;

namespace Geneforge.Core.UI
{
    public class SceneNavigator : MonoBehaviour
    {
        public void GoToScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("SceneNavigator: Nome da cena está vazio!");
                return;
            }

            // Tenta carregar a cena
            try
            {
                SceneManager.LoadScene(sceneName);
            }
            catch
            {
                Debug.LogError($"SceneNavigator: Não foi possível carregar a cena '{sceneName}'. Verifica se está na Build Settings.");
            }
        }
    }
}
