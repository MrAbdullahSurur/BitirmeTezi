using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class Bullet : NetworkBehaviour
{
    private Camera _camera;
    private bool _canDestroy = false;
    private ulong _ownerClientId = 0; // Store the player ID who fired this bullet

    private NetworkObject _networkObject;

    private void Awake()
    {
        _camera = Camera.main;
        _networkObject = GetComponent<NetworkObject>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            _canDestroy = true;
        }
        if (_networkObject == null || !_networkObject.IsSpawned)
        {
            Debug.LogWarning("Bullet destroying itself locally due to invalid spawn state.");
            Destroy(gameObject);
        }
    }
    
    // Set the player ID who fired this bullet
    public void SetOwner(ulong clientId)
    {
        _ownerClientId = clientId;
        Debug.Log($"Bullet owned by player {_ownerClientId}");
    }

    private void Update()
    {
        if (!IsServer || !_canDestroy) return;
        DestroyWhenOffScreen();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!IsServer || !_canDestroy) return;

        if (collision.GetComponent<EnemyMovement>() != null)
        {
            Debug.Log($"Bullet (owner: {_ownerClientId}) hit enemy: {collision.gameObject.name}");
            
            // Register who damaged this enemy for score allocation FIRST
            // This must happen before dealing damage, as the enemy could die from the damage
            EnemyScoreAllocator scoreAllocator = collision.GetComponent<EnemyScoreAllocator>();
            if (scoreAllocator != null)
            {
                scoreAllocator.RegisterPlayerDamage(_ownerClientId);
                Debug.Log($"Registered player {_ownerClientId} as damage source for enemy: {collision.gameObject.name}");
            }
            else
            {
                Debug.LogWarning($"Enemy {collision.gameObject.name} does not have an EnemyScoreAllocator component!");
            }
            
            // AFTER registering the damage source, deal damage to the enemy
            HealthController healthController = collision.GetComponent<HealthController>();
            if (healthController != null) 
            {
                Debug.Log($"Dealing damage to enemy: {collision.gameObject.name} from player: {_ownerClientId}");
                healthController.RequestTakeDamage(10);
            }
            else
            {
                Debug.LogWarning($"Enemy {collision.gameObject.name} does not have a HealthController component!");
            }
            
            DespawnBullet();
        }
    }

    private void DestroyWhenOffScreen()
    {
        if (_camera == null) 
        {
            _camera = Camera.main;
            if (_camera == null) 
            {
                Debug.LogError("Bullet cannot find Main Camera to check screen bounds!");
                return;
            }
        }
        
        Vector2 screenPosition = _camera.WorldToScreenPoint(transform.position);

        if (screenPosition.x < -50 ||
            screenPosition.x > _camera.pixelWidth + 50 ||
            screenPosition.y < -50 ||
            screenPosition.y > _camera.pixelHeight + 50)
        {
            DespawnBullet();
        }
    }

    private void DespawnBullet()
    {
        if (!IsServer || !_canDestroy || _networkObject == null || !_networkObject.IsSpawned)
        {
            return;
        }
        
        _canDestroy = false;
        Debug.Log($"Server despawning bullet {gameObject.name}");
        _networkObject.Despawn();
    }
}