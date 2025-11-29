using System;
using System.Collections.Generic;
using UnityEngine;

namespace Geneforge.Core.Pooling
{
    [Serializable]
    public struct PoolDefinition
    {
        public GameObject prefab;
        public int initialSize;
    }

    public class PoolManager : MonoBehaviour
    {
        public static PoolManager Instance { get; private set; }

        [Tooltip("Define each prefab and how many to preload.")]
        [SerializeField] private PoolDefinition[] pools;

        // Runtime lookup: prefab → queue of instances
        readonly Dictionary<GameObject, Queue<GameObject>> poolDict =
            new Dictionary<GameObject, Queue<GameObject>>();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError($"Duplicate PoolManager on {name}, destroying this instance.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Preload each pool
            foreach (var def in pools)
            {
                if (def.prefab == null)
                {
                    Debug.LogWarning("PoolManager: PoolDefinition with null prefab, skipping.");
                    continue;
                }

                var queue = new Queue<GameObject>();
                for (int i = 0; i < def.initialSize; i++)
                {
                    var go = CreateInstance(def.prefab);
                    go.SetActive(false);
                    queue.Enqueue(go);
                }
                poolDict[def.prefab] = queue;
            }
        }

        GameObject CreateInstance(GameObject prefab)
        {
            var go = Instantiate(prefab, transform);
            var id = go.GetComponent<PoolIdentifier>();
            if (id == null)
                id = go.AddComponent<PoolIdentifier>();

            id.SetSourcePrefab(prefab);
            return go;
        }

        /// <summary>
        /// Spawns a pooled instance of that prefab at position/rotation.
        /// </summary>
        public GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent = null)
        {
            if (prefab == null)
            {
                Debug.LogError("PoolManager.Spawn called with null prefab.");
                return null;
            }

            if (!poolDict.TryGetValue(prefab, out var queue))
            {
                // first-use beyond definitions: create a new queue
                queue = new Queue<GameObject>();
                poolDict[prefab] = queue;
            }

            GameObject inst;
            if (queue.Count > 0)
            {
                inst = queue.Dequeue();
            }
            else
            {
                inst = CreateInstance(prefab);
            }

            inst.transform.SetParent(parent, worldPositionStays: false);
            inst.transform.SetPositionAndRotation(pos, rot);
            inst.SetActive(true);
            return inst;
        }

        /// <summary>
        /// Returns an instance back to its pool.
        /// </summary>
        public void Reclaim(GameObject inst)
        {
            if (inst == null) return;

            var identifier = inst.GetComponent<PoolIdentifier>();
            if (identifier == null || identifier.SourcePrefab == null)
            {
                Debug.LogWarning($"PoolManager.Reclaim: {inst.name} has no PoolIdentifier/SourcePrefab, destroying.");
                Destroy(inst);
                return;
            }

            var prefab = identifier.SourcePrefab;

            if (!poolDict.TryGetValue(prefab, out var queue))
            {
                // create a queue on the fly so we don't leak the object
                queue = new Queue<GameObject>();
                poolDict[prefab] = queue;
            }

            inst.SetActive(false);
            inst.transform.SetParent(transform, false);
            queue.Enqueue(inst);
        }
    }
}
