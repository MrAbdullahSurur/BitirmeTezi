using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class CollectableSpawner : NetworkBehaviour
{
    [SerializeField]
    private List<GameObject> _collectablePrefabs;

    public void SpawnCollectable(Vector2 position)
    {
        if (!IsServer) return;

        if (_collectablePrefabs == null || _collectablePrefabs.Count == 0)
        {
            Debug.LogError("CollectableSpawner: No collectable prefabs assigned!");
            return;
        }

        int index = Random.Range(0, _collectablePrefabs.Count);
        GameObject selectedCollectablePrefab = _collectablePrefabs[index];

        if (selectedCollectablePrefab == null)
        {
            Debug.LogError($"CollectableSpawner: Prefab at index {index} is null!");
            return;
        }

        GameObject collectableInstance = Instantiate(selectedCollectablePrefab, position, Quaternion.identity);
        
        NetworkObject networkObject = collectableInstance.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            networkObject.Spawn(true);
            Debug.Log($"Server spawned collectable: {collectableInstance.name}");
        }
        else
        {
            Debug.LogError($"CollectableSpawner: Spawned collectable prefab \"{selectedCollectablePrefab.name}\" is missing a NetworkObject component! Destroying instance.");
            Destroy(collectableInstance);
        }
    }
}
