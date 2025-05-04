using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class Bullet : NetworkBehaviour
{
    [SerializeField]
    private Color _bulletColor = Color.white; // Merminin varsayılan rengi
    
    private Camera _camera;
    private bool _canDestroy = false;
    private ulong _ownerClientId = 0; // Store the player ID who fired this bullet
    private SpriteRenderer _spriteRenderer;

    private NetworkObject _networkObject;

    private void Awake()
    {
        _camera = Camera.main;
        _networkObject = GetComponent<NetworkObject>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        
        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = _bulletColor;
        }
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
        Debug.Log($"Bullet owned by player {_ownerClientId} (prefab: {gameObject.name})");
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
            // ÖNCE: Hasarı kimin verdiğini kaydet
            EnemyScoreAllocator scoreAllocator = collision.GetComponent<EnemyScoreAllocator>();
            if (scoreAllocator != null)
            {
                scoreAllocator.RegisterPlayerDamage(_ownerClientId);
                Debug.Log($"Bullet from player {_ownerClientId} (prefab: {gameObject.name}) registered damage first.");
            }
            
            // SONRA: Düşmanın canını azaltma isteği gönder
            HealthController healthController = collision.GetComponent<HealthController>();
            if (healthController != null) 
            {
                healthController.RequestTakeDamage(10);
            }
            
            // Mermiyi yok et
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
        Debug.Log($"Server despawning bullet {gameObject.name} from player {_ownerClientId}");
        _networkObject.Despawn();
    }
}