using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class EnemyDestroyController : NetworkBehaviour
{
    public void DestroyEnemy(float delay)
    {
        if (!IsServer) return;

        NetworkObject networkObject = GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            StartCoroutine(DespawnAfterDelay(networkObject, delay));
        }
        else
        {
            Debug.LogError($"Enemy {gameObject.name} is missing NetworkObject component! Cannot Despawn.", this);
        }
    }

    private IEnumerator DespawnAfterDelay(NetworkObject netObj, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn();
            Debug.Log($"Server despawned enemy {netObj.name}");
        }
    }
}
