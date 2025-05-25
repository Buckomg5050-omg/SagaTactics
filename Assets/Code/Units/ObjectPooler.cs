// File: ObjectPooler.cs
using UnityEngine;
using System.Collections.Generic;

public class ObjectPooler : MonoBehaviour
{
    [System.Serializable]
    public class Pool
    {
        public string tag;
        public GameObject prefab;
        public int size;
        [Tooltip("Allow pool to expand if it runs out of objects?")]
        public bool allowExpand = true; // Added a flag to control expansion
    }

    public static ObjectPooler Instance { get; private set; }

    public List<Pool> pools;
    public Dictionary<string, Queue<GameObject>> poolDictionary;
    private Dictionary<string, Pool> poolConfigMap; // For easy lookup of pool config for expansion

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate ObjectPooler instance found. Destroying this one.", this);
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        poolDictionary = new Dictionary<string, Queue<GameObject>>();
        poolConfigMap = new Dictionary<string, Pool>();

        foreach (Pool pool in pools)
        {
            if (pool.prefab == null)
            {
                Debug.LogError($"ObjectPooler: Pool with tag '{pool.tag}' has a null prefab! Skipping this pool.", this);
                continue;
            }
            if (string.IsNullOrEmpty(pool.tag))
            {
                Debug.LogError($"ObjectPooler: Pool with prefab '{pool.prefab.name}' has an empty or null tag! Skipping this pool.", this);
                continue;
            }
            if (poolDictionary.ContainsKey(pool.tag))
            {
                Debug.LogWarning($"ObjectPooler: Pool with tag '{pool.tag}' already exists. Skipping duplicate.", this);
                continue;
            }


            Queue<GameObject> objectPool = new Queue<GameObject>();
            for (int i = 0; i < pool.size; i++)
            {
                GameObject obj = Instantiate(pool.prefab);
                if (obj == null) {
                    Debug.LogError($"ObjectPooler: Failed to instantiate prefab for pool '{pool.tag}' during initial population.", this);
                    continue;
                }
                obj.SetActive(false);
                obj.transform.SetParent(this.transform); 
                objectPool.Enqueue(obj);
            }
            poolDictionary.Add(pool.tag, objectPool);
            poolConfigMap.Add(pool.tag, pool); // Store config for expansion
            Debug.Log($"ObjectPooler: Pool '{pool.tag}' initialized with {objectPool.Count} objects.", this);
        }
    }

    public GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation)
    {
        if (string.IsNullOrEmpty(tag)) {
            Debug.LogError("ObjectPooler: SpawnFromPool called with null or empty tag.", this);
            return null;
        }

        if (!poolDictionary.ContainsKey(tag))
        {
            Debug.LogWarning($"ObjectPooler: Pool with tag '{tag}' doesn't exist.", this);
            return null;
        }

        if (poolDictionary[tag].Count == 0) // Pool is empty
        {
            if (poolConfigMap.TryGetValue(tag, out Pool poolToExpand) && poolToExpand.allowExpand)
            {
                Debug.LogWarning($"ObjectPooler: Pool '{tag}' is empty, expanding by 1 (allowed).", this);
                if (poolToExpand.prefab == null) {
                    Debug.LogError($"ObjectPooler: Cannot expand pool '{tag}', original prefab is null in config.", this);
                    return null;
                }
                GameObject objToAdd = Instantiate(poolToExpand.prefab);
                 if (objToAdd == null) {
                    Debug.LogError($"ObjectPooler: Failed to instantiate prefab for expanding pool '{tag}'.", this);
                    return null;
                }
                objToAdd.SetActive(false);
                objToAdd.transform.SetParent(this.transform);
                // No need to Enqueue here, we'll Dequeue it immediately after it's effectively added.
                // Instead, we just use this newly created one.
                // For a more robust pool expansion, you might add a few, then Dequeue.
                // For this simple case, using it directly is fine.
                 objectToSpawn = objToAdd; // Use the newly created object
            }
            else if (poolToExpand != null && !poolToExpand.allowExpand)
            {
                Debug.LogWarning($"ObjectPooler: Pool '{tag}' is empty and expansion is not allowed. Cannot spawn object.", this);
                return null;
            }
            else { // Should not happen if poolConfigMap is synced with poolDictionary
                 Debug.LogError($"ObjectPooler: Pool '{tag}' is empty and could not find original pool config to expand, or expansion not allowed.", this);
                return null;
            }
        } else {
            objectToSpawn = poolDictionary[tag].Dequeue();
        }


        if (objectToSpawn == null) { // Should be caught by earlier checks but good for safety
            Debug.LogError($"ObjectPooler: Dequeued a null object from pool '{tag}'. This should not happen.", this);
            return null;
        }

        objectToSpawn.SetActive(true);
        objectToSpawn.transform.position = position;
        objectToSpawn.transform.rotation = rotation;
        
        // Optional: IPooledObject pooledObj = objectToSpawn.GetComponent<IPooledObject>();
        // if (pooledObj != null) { pooledObj.OnObjectSpawn(); }
        // Debug.Log($"ObjectPooler: Spawned '{objectToSpawn.name}' from pool '{tag}'. Remaining in queue: {poolDictionary[tag].Count}", this);

        return objectToSpawn;
    }
    GameObject objectToSpawn; // Declare here to be accessible in the else block

    public void ReturnToPool(string tag, GameObject objectToReturn)
    {
        if (string.IsNullOrEmpty(tag)) {
            Debug.LogError("ObjectPooler: ReturnToPool called with null or empty tag.", this);
            if(objectToReturn != null) Destroy(objectToReturn); // Destroy if tag is bad
            return;
        }

        if (objectToReturn == null)
        {
            Debug.LogWarning($"ObjectPooler: Attempted to return a NULL object to pool with tag '{tag}'. Skipping.", this);
            return;
        }

        if (!poolDictionary.ContainsKey(tag))
        {
            Debug.LogWarning($"ObjectPooler: Pool with tag '{tag}' doesn't exist for object '{objectToReturn.name}'. Destroying object instead.", this);
            Destroy(objectToReturn);
            return;
        }

        // Check if object is already inactive (might have been returned already or deactivated by other means)
        if (!objectToReturn.activeSelf) {
            // It's possible it was returned and then tried to be returned again if not removed from active list.
            // Or it was deactivated by some other game logic.
            // If it's already in the queue, don't re-add it.
            if (poolDictionary[tag].Contains(objectToReturn)) {
                // Debug.LogWarning($"ObjectPooler: Object '{objectToReturn.name}' is already in pool '{tag}' and inactive. Not re-adding.", this);
                return;
            }
            // If it's inactive but not in the queue, it might be an orphaned object.
            // Still, let's try to queue it.
        }


        objectToReturn.SetActive(false);
        // Ensure it's parented back to the pooler if it was changed during use
        if (objectToReturn.transform.parent != this.transform)
        {
            objectToReturn.transform.SetParent(this.transform);
        }
        poolDictionary[tag].Enqueue(objectToReturn);
        // Debug.Log($"ObjectPooler: Returned '{objectToReturn.name}' (Active: {objectToReturn.activeSelf}) to pool '{tag}'. Pool '{tag}' size now: {poolDictionary[tag].Count}", this);
    }
}