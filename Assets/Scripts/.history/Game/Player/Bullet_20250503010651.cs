using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class Bullet : NetworkBehaviour
{
    private Camera _camera;
    private bool _canDestroy = false;

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
            HealthController healthController = collision.GetComponent<HealthController>();
            if (healthController != null) 
            {
                healthController.RequestTakeDamage(10);
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