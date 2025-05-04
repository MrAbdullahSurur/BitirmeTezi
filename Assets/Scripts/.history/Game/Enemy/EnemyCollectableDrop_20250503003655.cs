using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class EnemyCollectableDrop : NetworkBehaviour
{
    [SerializeField]
    private float _chanceOfCollectableDrop;

    private CollectableSpawner _collectableSpawner;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            _collectableSpawner = FindObjectOfType<CollectableSpawner>();
            if (_collectableSpawner == null)
            {
                Debug.LogError($"EnemyCollectableDrop on {gameObject.name} could not find CollectableSpawner!");
            }
        }
        enabled = IsServer;
    }

    public void RandomlyDropCollectable()
    {
        if (!IsServer || _collectableSpawner == null) 
        {
            return; 
        }

        float random = Random.Range(0f, 1f);

        if (_chanceOfCollectableDrop >= random)
        {
            Debug.Log($"Server attempting to spawn collectable at {transform.position}");
            _collectableSpawner.SpawnCollectable(transform.position);
        }
    }
}
