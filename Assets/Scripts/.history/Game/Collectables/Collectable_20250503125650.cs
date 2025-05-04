using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public class Collectable : NetworkBehaviour
{
    private collectablesbehaviour _collectableBehaviour;

    private void Awake()
    {
        _collectableBehaviour = GetComponent<collectablesbehaviour>();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!IsServer) return;

        NetworkObject playerNetworkObject = collision.GetComponent<NetworkObject>();
        if (playerNetworkObject != null && playerNetworkObject.IsPlayerObject)
        {
            Debug.Log($"Collectable {gameObject.name} collected by player {playerNetworkObject.OwnerClientId} on server.");
            if (_collectableBehaviour != null)
            {
                _collectableBehaviour.OnCollected(playerNetworkObject.gameObject);
            }
            else
            {
                Debug.LogWarning($"Collectable {gameObject.name} missing collectable behaviour!", this);
            }
            
            SafelyDespawn();
        }
    }
    
    private void SafelyDespawn()
    {
        if (!IsServer) return;
        
        NetworkObject networkObject = GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.IsSpawned)
        {
            networkObject.Despawn();
        }
        else
        {
            Destroy(gameObject);
        }
    }
}

