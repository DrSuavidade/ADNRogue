using UnityEngine;

// Attach this to every prefab you want pooled.
// It simply remembers which prefab it came from.
public class PoolIdentifier : MonoBehaviour
{
    [HideInInspector] public GameObject sourcePrefab;
}
