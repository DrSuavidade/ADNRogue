using System;
using System.Collections.Generic;
using UnityEngine;

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
    public PoolDefinition[] pools;

    // Runtime lookup: prefab → queue of instances
    Dictionary<GameObject, Queue<GameObject>> poolDict = new Dictionary<GameObject, Queue<GameObject>>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Preload each pool
        foreach (var def in pools)
        {
            var queue = new Queue<GameObject>();
            for (int i = 0; i < def.initialSize; i++)
            {
                var go = Instantiate(def.prefab, transform);
                go.SetActive(false);
                queue.Enqueue(go);
            }
            poolDict[def.prefab] = queue;
        }
    }

    /// <summary>
    /// Spawns a pooled instance of that prefab at position/rotation.
    /// </summary>
    public GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        if (!poolDict.TryGetValue(prefab, out var queue))
        {
            // first‐use beyond definitions: create a new queue
            queue = new Queue<GameObject>();
            poolDict[prefab] = queue;
        }

        GameObject inst;
        if (queue.Count > 0)
        {
            inst = queue.Dequeue();
            inst.transform.SetParent(transform, false);
            inst.transform.SetPositionAndRotation(pos, rot);
            inst.SetActive(true);
        }
        else
        {
            // on‐demand allocation, but prefab already warmed in Awake
            inst = Instantiate(prefab, pos, rot, transform);
        }

        return inst;
    }

    /// <summary>
    /// Returns an instance back to its pool.
    /// </summary>
    public void Reclaim(GameObject inst)
    {
        inst.SetActive(false);
        poolDict[inst.GetComponent<PoolIdentifier>().sourcePrefab].Enqueue(inst);
    }
}
