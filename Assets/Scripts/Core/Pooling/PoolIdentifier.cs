using UnityEngine;

namespace Geneforge.Core.Pooling
{
    public class PoolIdentifier : MonoBehaviour
    {
        public GameObject SourcePrefab { get; private set; }

        public void SetSourcePrefab(GameObject prefab)
        {
            SourcePrefab = prefab;
        }
    }
}
