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
                Debug.LogError("SceneNavigator: Scene name is empty.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"SceneNavigator: Scene '{sceneName}' is not in Build Settings or cannot be loaded.");
                return;
            }

            SceneManager.LoadScene(sceneName);
        }
    }
}
